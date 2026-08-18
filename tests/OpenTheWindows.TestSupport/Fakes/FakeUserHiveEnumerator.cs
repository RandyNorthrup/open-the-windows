using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>In-memory user-hive enumerator for tests.</summary>
public sealed class FakeUserHiveEnumerator : IUserHiveEnumerator
{
    private readonly IReadOnlyList<UserProfile> _profiles;

    public FakeUserHiveEnumerator(params UserProfile[] profiles) => _profiles = profiles;

    /// <inheritdoc />
    public IReadOnlyList<UserProfile> Enumerate() => _profiles;
}
