<#
    Strict PSScriptAnalyzer settings for the PowerShell scripts in this repo
    (build/, tools/, installer helpers). Run as a gate with:

        Invoke-ScriptAnalyzer -Path . -Recurse -Settings ./PSScriptAnalyzerSettings.psd1 -EnableExit

    -EnableExit is what makes it a gate: non-zero exit when any rule fires.
    Adapted from the high-quality-projects-skill template; deviations noted inline.
#>
@{
    IncludeDefaultRules = $true
    Severity            = @('Error', 'Warning', 'Information')

    ExcludeRules = @(
        # Fires on any function named Get-/Set-/New- that lacks ShouldProcess
        # even when it has no side effects. Build scripts here are imperative
        # task runners; the destructive VM helpers use explicit confirmation.
        'PSUseShouldProcessForStateChangingFunctions',

        # Coloured Write-Host is correct for an interactive build/quality runner.
        'PSAvoidUsingWriteHost',

        # Fires on every native executable call (dotnet, npm, gitleaks, semgrep):
        # native tools have no named parameters. Kept off; PowerShell cmdlets in
        # these scripts still use named parameters by convention.
        'PSAvoidUsingPositionalParameters'
    )

    Rules = @{
        PSPlaceOpenBrace = @{
            Enable             = $true
            OnSameLine         = $true
            NewLineAfter       = $true
            IgnoreOneLineBlock = $true
        }
        PSPlaceCloseBrace = @{
            Enable             = $true
            NewLineAfter       = $true
            IgnoreOneLineBlock = $true
            NoEmptyLineBefore  = $false
        }
        PSUseConsistentIndentation = @{
            Enable              = $true
            Kind                = 'space'
            IndentationSize     = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
        }
        PSUseConsistentWhitespace = @{
            Enable          = $true
            CheckInnerBrace = $true
            CheckOpenBrace  = $true
            CheckOpenParen  = $true
            CheckOperator   = $true
            CheckPipe       = $true
            CheckSeparator  = $true
        }
        PSAlignAssignmentStatement = @{
            Enable         = $true
            CheckHashtable = $true
        }
        PSAvoidUsingCmdletAliases = @{ Enable = $true }
        PSUseCorrectCasing        = @{ Enable = $true }
        # Scripts target PowerShell 7 on Windows only (they drive Windows tooling).
        # PSSA 1.25 ships cmdlet profiles only up to core-6.1.0; a non-existent
        # profile name (e.g. core-7.4.0-windows) makes Invoke-ScriptAnalyzer throw
        # a NullReferenceException instead of reporting, so use a shipped one.
        PSUseCompatibleCmdlets    = @{ Compatibility = @('core-6.1.0-windows') }
    }
}
