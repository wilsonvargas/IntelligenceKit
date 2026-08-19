using IntelligenceKit.Core.Configuration;

namespace IntelligenceKit.Core.Tests;

public class IntelligenceDsnTests
{
    [Fact]
    public void Parse_SplitsAllParts()
    {
        var dsn = IntelligenceDsn.Parse("http://demo-key@10.0.2.2:7099/demo-maui");

        Assert.Equal("http://10.0.2.2:7099", dsn.ServerUrl);
        Assert.Equal("demo-key", dsn.ProjectKey);
        Assert.Equal("demo-maui", dsn.ProjectId);
    }

    [Fact]
    public void Parse_KeepsHttpsScheme()
    {
        var dsn = IntelligenceDsn.Parse("https://key@events.example.com:443/proj");

        Assert.Equal("https://events.example.com", dsn.ServerUrl);
        Assert.Equal("key", dsn.ProjectKey);
        Assert.Equal("proj", dsn.ProjectId);
    }

    [Fact]
    public void Parse_NonDefaultPortIsPreservedInServerUrl()
    {
        var dsn = IntelligenceDsn.Parse("http://k@host:8080/p");

        Assert.Equal("http://host:8080", dsn.ServerUrl);
    }

    [Fact]
    public void Parse_TrimsLeadingAndTrailingSlashesFromProjectId()
    {
        var dsn = IntelligenceDsn.Parse("http://k@host:7099/my-project/");

        Assert.Equal("my-project", dsn.ProjectId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_ThrowsArgumentException_OnEmptyDsn(string? dsn)
    {
        Assert.Throws<ArgumentException>(() => IntelligenceDsn.Parse(dsn!));
    }

    [Theory]
    [InlineData("not-a-uri")]                 // no scheme → not an absolute URI
    [InlineData("has spaces and no scheme")]  // whitespace + no scheme → not an absolute URI
    public void Parse_ThrowsFormatException_OnMalformedDsn(string dsn)
    {
        // NOTE: a bare leading-slash path (e.g. "/just/a/path") is intentionally
        // NOT tested here — .NET on Unix treats it as a valid absolute (file) URI,
        // so it does not throw, unlike on Windows. Keep cases that are invalid
        // absolute URIs on every platform.
        Assert.Throws<FormatException>(() => IntelligenceDsn.Parse(dsn));
    }

    [Fact]
    public void Parse_ThrowsFormatException_WhenProjectIdMissing()
    {
        Assert.Throws<FormatException>(() => IntelligenceDsn.Parse("http://key@host:7099"));
    }

    [Fact]
    public void Parse_ProjectKeyIsEmpty_WhenUserInfoOmitted()
    {
        // A DSN without the "key@" segment is still parseable — the key is a
        // public routing id, not a secret, and may legitimately be absent.
        var dsn = IntelligenceDsn.Parse("http://host:7099/proj");

        Assert.Equal(string.Empty, dsn.ProjectKey);
        Assert.Equal("proj", dsn.ProjectId);
    }
}
