using OpenTheWindows.Windows.Writers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// The Local GPO save retries a brief <c>Registry.pol</c> sharing violation
/// (<c>0x80070020</c>) that a concurrent policy writer causes, but never masks a
/// real failure. The retry loop is exercised directly with a fake operation so no
/// elevation or real Group Policy object is needed.
/// </summary>
public sealed class LocalGroupPolicyRetryTests
{
    private static InvalidOperationException SharingViolation()
        => new(
            "Local Group Policy operation failed: The process cannot access the file because it is being used by another process.",
            new IOException("Registry.pol is in use.") { HResult = LocalGroupPolicyRetry.SharingViolationHResult });

    [Fact]
    public void Retries_a_sharing_violation_then_succeeds()
    {
        int calls = 0;
        List<TimeSpan> waits = [];

        LocalGroupPolicyRetry.Run(
            () =>
            {
                calls++;
                if (calls < 3)
                {
                    throw SharingViolation();
                }
            },
            waits.Add);

        Assert.Equal(3, calls);
        Assert.Equal(2, waits.Count);
        Assert.All(waits, w => Assert.Equal(LocalGroupPolicyRetry.RetryDelay, w));
    }

    [Fact]
    public void Rethrows_after_exhausting_every_attempt()
    {
        int calls = 0;

        Assert.Throws<InvalidOperationException>(() =>
            LocalGroupPolicyRetry.Run(
                () =>
                {
                    calls++;
                    throw SharingViolation();
                },
                _ => { }));

        Assert.Equal(LocalGroupPolicyRetry.MaxAttempts, calls);
    }

    [Fact]
    public void Does_not_retry_a_non_sharing_failure()
    {
        int calls = 0;

        Assert.Throws<InvalidOperationException>(() =>
            LocalGroupPolicyRetry.Run(
                () =>
                {
                    calls++;
                    throw new InvalidOperationException("The Group Policy COM class is unavailable.");
                },
                _ => { }));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Recognises_a_sharing_violation_directly_and_when_wrapped()
    {
        Assert.True(LocalGroupPolicyRetry.IsSharingViolation(
            new IOException { HResult = LocalGroupPolicyRetry.SharingViolationHResult }));
        Assert.True(LocalGroupPolicyRetry.IsSharingViolation(SharingViolation()));
        Assert.False(LocalGroupPolicyRetry.IsSharingViolation(new InvalidOperationException("unrelated")));
    }
}
