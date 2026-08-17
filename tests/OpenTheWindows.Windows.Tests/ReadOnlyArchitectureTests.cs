using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Architectural guard: the OS-touching Windows assembly must be read-only in
/// M2. It inspects the assembly's member references and fails if any mutating
/// registry, service, package, task or file API is referenced anywhere. This is
/// precise (it matches the declaring type and member name, so <c>List.Add</c>
/// and friends are not flagged) and catches an accidental write before it can
/// ship.
/// </summary>
public sealed class ReadOnlyArchitectureTests
{
    [Fact]
    public void Windows_assembly_references_no_write_apis()
    {
        string assemblyPath = typeof(WindowsRegistryReader).Assembly.Location;

        using FileStream stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        MetadataReader metadata = peReader.GetMetadataReader();

        var violations = new List<string>();
        foreach (MemberReferenceHandle handle in metadata.MemberReferences)
        {
            MemberReference reference = metadata.GetMemberReference(handle);
            string member = metadata.GetString(reference.Name);
            string declaringType = DeclaringTypeName(metadata, reference.Parent);
            if (IsMutatingApi(declaringType, member))
            {
                violations.Add(declaringType + "." + member);
            }
        }

        Assert.True(violations.Count == 0, "Write API(s) referenced: " + string.Join(", ", violations.Distinct(StringComparer.Ordinal)));
    }

    private static string DeclaringTypeName(MetadataReader metadata, EntityHandle parent)
    {
        if (parent.Kind == HandleKind.TypeReference)
        {
            return metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)parent).Name);
        }

        return string.Empty;
    }

    private static bool IsMutatingApi(string declaringType, string member) => declaringType switch
    {
        "RegistryKey" => member is "SetValue" or "DeleteValue" or "DeleteValueTree"
            or "DeleteSubKey" or "DeleteSubKeyTree" or "CreateSubKey" or "CreateSubKeyTree",
        "ServiceController" => member is "Start" or "Stop" or "Pause" or "Continue" or "ExecuteCommand",
        "PackageManager" => StartsWithAny(member, "Add", "Remove", "Stage", "Register", "Provision", "Deprovision"),
        "TaskService" or "TaskFolder" or "TaskDefinition" =>
            member.Contains("Register", StringComparison.Ordinal) || member.Contains("Delete", StringComparison.Ordinal),
        "File" or "Directory" => member is "Delete" or "Create" or "CreateDirectory" or "Move" or "Copy"
            or "WriteAllText" or "WriteAllBytes" or "WriteAllLines" or "AppendAllText" or "AppendAllLines",
        _ => false,
    };

    private static bool StartsWithAny(string value, params string[] prefixes)
        => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
}
