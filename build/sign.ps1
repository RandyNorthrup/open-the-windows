<#
.SYNOPSIS
    Authenticode-signs release files with Azure Artifact Signing (formerly Trusted
    Signing), then verifies every signature.

.DESCRIPTION
        ./build/sign.ps1 -Path otw.exe, OpenTheWindows.exe   # sign, then verify each file
        pwsh build/sign.ps1 -CheckAccess                     # check prerequisites and sign-in only

    Signs with signtool.exe from the Windows SDK and the Artifact Signing dlib
    (Microsoft.ArtifactSigning.Client, pinned by version and by the SHA-512 that
    nuget.org records for it). Each file gets a SHA-256 file digest and an RFC 3161
    timestamp (SHA-256) from the Microsoft timestamp authority, then must pass
    `signtool verify /pa /tw`, carry a timestamp, and name the expected publisher.

    Authentication is the Azure CLI only (every other DefaultAzureCredential source
    is excluded): `az login` on a workstation, `azure/login` with GitHub OIDC in the
    release workflow. The signed-in identity needs the "Artifact Signing Certificate
    Profile Signer" role on the certificate profile.

    The script refuses a file that already carries a signature, a reparse point, or
    anything that is not a regular file, so it never re-signs someone else's binary.

    build/package.ps1 -Sign calls this script: -CheckAccess first, so a missing
    prerequisite or sign-in fails before the long publish, then the binaries before
    they are zipped or packed into an MSI, then the MSIs.

    Needs: Windows SDK (signtool.exe, x64), a .NET 8 or later x64 runtime (the dlib
    rolls forward), and the Azure CLI. The dlib is downloaded once into
    artifacts/tools/ (gitignored).
#>
[CmdletBinding(DefaultParameterSetName = 'Sign')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Sign')]
    [string[]] $Path,

    [Parameter(Mandatory, ParameterSetName = 'CheckAccess')]
    [switch] $CheckAccess,

    [string] $Endpoint = 'https://wus.codesigning.azure.net/',
    [string] $Account = 'scnetsolutions',
    [string] $CertificateProfile = 'SAKUtility',
    [string] $ExpectedPublisher = 'Randy Northrup',
    [string] $Description = 'Open the Windows',
    [string] $DescriptionUrl = 'https://github.com/RandyNorthrup/open-the-windows'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

# -- Pinned configuration -------------------------------------------------------
$digestAlgorithm = 'SHA256'
$timestampUrl = 'http://timestamp.acs.microsoft.com'
$codeSigningResource = 'https://codesigning.azure.net'

# signtool loads Azure.CodeSigning.Dlib.dll from this package (the successor of the
# unlisted Microsoft.Trusted.Signing.Client). Pinned by version and by the SHA-512
# nuget.org records for it; each version extracts to its own folder.
$dlibPackage = 'Microsoft.ArtifactSigning.Client'
$dlibVersion = '1.0.146'
$dlibSha512 = '/G0hG/VDIS6psW23b5t1V/ORIgn0PVAOtGj9w+hkPmAuI4pempfwlW50E2GtHscnz9+5Qz3hbC4mcb7ej2Yn1A=='
$dlibPublisher = 'Microsoft Corporation'
$dlibUrl = "https://www.nuget.org/api/v2/package/$dlibPackage/$dlibVersion"
$signingArchitecture = 'x64'
$dlibRoot = Join-Path $repoRoot "artifacts/tools/$dlibPackage/$dlibVersion"
$dlibBin = Join-Path $dlibRoot "bin/$signingArchitecture"
$dlibDll = Join-Path $dlibBin 'Azure.CodeSigning.Dlib.dll'

# The Azure CLI is the only credential the dlib may use: no managed identity probe,
# no environment secret, no interactive browser window.
$excludedCredentials = @(
    'EnvironmentCredential',
    'WorkloadIdentityCredential',
    'ManagedIdentityCredential',
    'SharedTokenCacheCredential',
    'VisualStudioCredential',
    'VisualStudioCodeCredential',
    'AzurePowerShellCredential',
    'AzureDeveloperCliCredential',
    'InteractiveBrowserCredential'
)

function Get-CommonName {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)
    return $Certificate.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
}

function Resolve-SignTool {
    # signtool comes from an installed Windows SDK only, never from PATH, where a
    # planted signtool.exe would run with signing rights.
    $sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $candidates = @(Get-ChildItem -LiteralPath $sdkBin -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -as [version] } |
            Sort-Object { [version] $_.Name } -Descending |
            ForEach-Object { Join-Path $_.FullName "$signingArchitecture/signtool.exe" } |
            Where-Object { Test-Path -LiteralPath $_ })
    if ($candidates.Count -eq 0) {
        throw "signtool.exe ($signingArchitecture) not found under $sdkBin. Install the Windows SDK."
    }
    return $candidates[0]
}

function Install-SigningClient {
    if (Test-Path -LiteralPath $dlibDll) {
        return
    }
    Write-Host "Installing $dlibPackage $dlibVersion" -ForegroundColor DarkGray
    # Extract beside the final folder and move it into place in one step, so an
    # interrupted install never leaves a half-extracted dlib that looks complete.
    $partial = "$dlibRoot.partial"
    if (Test-Path -LiteralPath $partial) {
        Remove-Item -LiteralPath $partial -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $partial | Out-Null
    $archive = Join-Path $partial "$dlibPackage.$dlibVersion.zip"
    Invoke-WebRequest -Uri $dlibUrl -OutFile $archive -UseBasicParsing
    $actual = [Convert]::ToBase64String([Convert]::FromHexString((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash))
    if ($actual -cne $dlibSha512) {
        Remove-Item -LiteralPath $partial -Recurse -Force
        throw "$dlibPackage $dlibVersion SHA-512 mismatch: expected $dlibSha512, got $actual."
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $partial -Force
    Remove-Item -LiteralPath $archive
    if (Test-Path -LiteralPath $dlibRoot) {
        Remove-Item -LiteralPath $dlibRoot -Recurse -Force
    }
    Move-Item -LiteralPath $partial -Destination $dlibRoot
    if (-not (Test-Path -LiteralPath $dlibDll)) {
        throw "Azure.CodeSigning.Dlib.dll is missing from $dlibPackage $dlibVersion."
    }
}

function Confirm-SigningClient {
    # signtool loads the dlib and everything beside it with signing rights: each DLL
    # must hold a valid Authenticode signature from Microsoft.
    foreach ($dll in Get-ChildItem -LiteralPath $dlibBin -Filter '*.dll' -File) {
        $signature = Get-AuthenticodeSignature -LiteralPath $dll.FullName
        if ($signature.Status -ne 'Valid') {
            throw "Artifact Signing client file $($dll.FullName) has signature status $($signature.Status)."
        }
        $subject = $signature.SignerCertificate.Subject
        if ($subject -notmatch "O=$([regex]::Escape($dlibPublisher))(,|$)") {
            throw "Artifact Signing client file $($dll.FullName) is signed by '$subject', not $dlibPublisher."
        }
    }
}

function Confirm-AzureSignIn {
    if (-not (Get-Command az -CommandType Application -ErrorAction SilentlyContinue)) {
        throw 'Azure CLI (az) not found. Install it (https://aka.ms/installazurecliwindows) and run az login.'
    }
    # Requesting the code-signing token proves the sign-in now, before a long build,
    # and leaves it in the Azure CLI token cache for the signtool calls that follow.
    # Only the expiry is read; the token itself is never printed.
    $expiresOn = & az account get-access-token --resource $codeSigningResource --query expiresOn --output tsv
    if ($LASTEXITCODE -ne 0 -or -not $expiresOn) {
        throw "The Azure CLI could not get a token for $codeSigningResource. Run az login (or azure/login in CI)."
    }
    Write-Host "Azure CLI signed in; code-signing token valid until $expiresOn." -ForegroundColor DarkGray
}

function Resolve-SigningTarget {
    param([string] $Candidate)
    $item = Get-Item -LiteralPath $Candidate -Force
    if ($item.PSIsContainer) {
        throw "Refusing to sign '$Candidate': it is a directory."
    }
    if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Refusing to sign '$Candidate': it is a reparse point (symbolic link or junction)."
    }
    $existing = Get-AuthenticodeSignature -LiteralPath $item.FullName
    if ($existing.Status -ne 'NotSigned') {
        throw "Refusing to sign '$($item.FullName)': it already carries a signature or cannot carry one ($($existing.Status))."
    }
    return $item.FullName
}

function Confirm-Signature {
    param(
        [string] $SignTool,
        [string] $File,
        [string] $Publisher
    )
    # /pa: the default Authenticode verification policy. /tw: a missing timestamp is
    # a warning, which signtool reports as a non-zero exit.
    & $SignTool verify /pa /tw /v $File
    if ($LASTEXITCODE -ne 0) {
        throw "signtool verify failed for $File (exit $LASTEXITCODE)."
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $File
    if ($signature.Status -ne 'Valid') {
        throw "$File has signature status $($signature.Status): $($signature.StatusMessage)"
    }
    $signer = Get-CommonName -Certificate $signature.SignerCertificate
    if ($signer -cne $Publisher) {
        throw "$File is signed by '$signer', not '$Publisher'."
    }
    if (-not $signature.TimeStamperCertificate) {
        throw "$File carries no timestamp."
    }
    Write-Host "Signed $File" -ForegroundColor Green
    Write-Host "  publisher: $($signature.SignerCertificate.Subject)"
    Write-Host "  timestamp: $($signature.TimeStamperCertificate.Subject)"
}

$signTool = Resolve-SignTool
Write-Host "signtool: $signTool" -ForegroundColor DarkGray
Install-SigningClient
Confirm-SigningClient
Confirm-AzureSignIn

if ($CheckAccess) {
    Write-Host 'Signing prerequisites and Azure sign-in are in place.' -ForegroundColor Green
    return
}

$files = @($Path | ForEach-Object { Resolve-SigningTarget -Candidate $_ })

# The dlib requires UTF-8 without a byte-order mark. The file names the account and
# profile only; it holds no credential.
$metadata = [ordered]@{}
$metadata['Endpoint'] = $Endpoint
$metadata['CodeSigningAccountName'] = $Account
$metadata['CertificateProfileName'] = $CertificateProfile
$metadata['ExcludeCredentials'] = $excludedCredentials
$metadataPath = Join-Path $dlibRoot 'metadata.json'
[System.IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json), [System.Text.UTF8Encoding]::new($false))

$signArguments = @(
    'sign', '/v',
    '/fd', $digestAlgorithm,
    '/tr', $timestampUrl,
    '/td', $digestAlgorithm,
    '/d', $Description,
    '/du', $DescriptionUrl,
    '/dlib', $dlibDll,
    '/dmdf', $metadataPath
)
Write-Host "==> sign $($files.Count) file(s) with $Account/$CertificateProfile" -ForegroundColor Cyan
& $signTool @signArguments @files
if ($LASTEXITCODE -ne 0) {
    throw "signtool sign failed (exit $LASTEXITCODE)."
}

Write-Host '==> verify signatures' -ForegroundColor Cyan
foreach ($file in $files) {
    Confirm-Signature -SignTool $signTool -File $file -Publisher $ExpectedPublisher
}
