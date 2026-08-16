using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Tests.Model;

public sealed class TweakIdTests
{
    [Theory]
    [InlineData("privacy")]
    [InlineData("privacy.diagnostic-data")]
    [InlineData("privacy.diagnostic-data.allow-telemetry")]
    [InlineData("updates.pause-35d")]
    [InlineData("a1.b2-c3")]
    public void Accepts_well_formed_ids(string value)
    {
        var id = new TweakId(value);

        Assert.Equal(value, id.Value);
        Assert.Equal(value, id.ToString());
        Assert.True(TweakId.IsValid(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Privacy")]            // upper case
    [InlineData("privacy_data")]       // underscore
    [InlineData("privacy..data")]      // empty segment
    [InlineData(".privacy")]           // leading dot
    [InlineData("privacy.")]           // trailing dot
    [InlineData("privacy-")]           // trailing hyphen
    [InlineData("-privacy")]           // leading hyphen
    [InlineData("privacy data")]       // space
    [InlineData("privacy/data")]       // slash
    public void Rejects_malformed_ids(string value)
    {
        Assert.False(TweakId.IsValid(value));
        Assert.ThrowsAny<ArgumentException>(() => new TweakId(value));
    }

    [Fact]
    public void Rejects_null()
    {
        Assert.False(TweakId.IsValid(null));
        Assert.Throws<ArgumentNullException>(() => new TweakId(null!));
    }

    [Fact]
    public void Rejects_ids_longer_than_max_length()
    {
        string tooLong = new('a', TweakId.MaxLength + 1);
        string atLimit = new('a', TweakId.MaxLength);

        Assert.False(TweakId.IsValid(tooLong));
        Assert.Throws<ArgumentException>(() => new TweakId(tooLong));
        Assert.True(TweakId.IsValid(atLimit));
    }

    [Fact]
    public void Equality_is_by_value()
    {
        var a = new TweakId("privacy.x");
        var b = new TweakId("privacy.x");
        var c = new TweakId("privacy.y");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
