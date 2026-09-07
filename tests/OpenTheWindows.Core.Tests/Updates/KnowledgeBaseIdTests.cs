using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.Core.Tests.Updates;

public sealed class KnowledgeBaseIdTests
{
    [Theory]
    [InlineData("KB5063878", "5063878")]
    [InlineData("kb5063878", "5063878")]
    [InlineData("5063878", "5063878")]
    [InlineData("  KB1  ", "1")]
    public void Normalises_kb_prefixes_and_digits(string input, string expected)
        => Assert.Equal(expected, KnowledgeBaseId.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("KB")]
    [InlineData("KBabc")]
    [InlineData("506 3878")]
    public void Rejects_non_kb_values(string? input)
        => Assert.Null(KnowledgeBaseId.Normalize(input));

    [Fact]
    public void Formats_with_kb_prefix()
        => Assert.Equal("KB5063878", KnowledgeBaseId.Format("5063878"));
}
