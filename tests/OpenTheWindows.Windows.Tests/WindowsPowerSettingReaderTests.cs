using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads real power settings for the active scheme through <c>powrprof.dll</c>
/// (read-only, no elevation needed). Uses the display-timeout setting, defined
/// by every built-in power scheme.
/// </summary>
public sealed class WindowsPowerSettingReaderTests
{
    // SUB_VIDEO / VIDEOIDLE: "turn off display after", present on every scheme.
    private static readonly Guid SubVideo = new("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid VideoIdle = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");

    [Fact]
    public void Reads_a_known_setting()
    {
        var reader = new WindowsPowerSettingReader();

        (uint Ac, uint Dc)? value = reader.Read(SubVideo, VideoIdle);

        Assert.NotNull(value);
    }

    [Fact]
    public void Unknown_setting_is_null()
    {
        var reader = new WindowsPowerSettingReader();

        (uint Ac, uint Dc)? value = reader.Read(
            new Guid("11111111-1111-1111-1111-111111111111"),
            new Guid("22222222-2222-2222-2222-222222222222"));

        Assert.Null(value);
    }
}
