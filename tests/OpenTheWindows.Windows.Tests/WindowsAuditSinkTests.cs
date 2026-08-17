using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Exercises the JSONL side of the audit sink against a real temporary directory
/// (safe on any machine). The Event Log side is best-effort and needs elevation
/// the first time, so it is not asserted here.
/// </summary>
public sealed class WindowsAuditSinkTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("otw-audit-test").FullName;

    [Fact]
    public void Write_appends_a_jsonl_line_for_the_event()
    {
        var sink = new WindowsAuditSink(_directory);
        var at = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

        sink.Write(new AuditEvent(AuditEventId.ApplyStarted, at, "apply started", Guid.NewGuid(), "privacy.test.entry"));

        string file = Path.Combine(_directory, "otw-202608.jsonl");
        Assert.True(File.Exists(file));
        string content = File.ReadAllText(file);
        Assert.Contains("apply started", content, StringComparison.Ordinal);
        Assert.Contains("ApplyStarted", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_appends_multiple_events_as_separate_lines()
    {
        var sink = new WindowsAuditSink(_directory);
        var at = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

        sink.Write(new AuditEvent(AuditEventId.ApplyStarted, at, "one", null, null));
        sink.Write(new AuditEvent(AuditEventId.TweakApplied, at, "two", null, null));

        string[] lines = File.ReadAllLines(Path.Combine(_directory, "otw-202608.jsonl"));
        Assert.Equal(2, lines.Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
