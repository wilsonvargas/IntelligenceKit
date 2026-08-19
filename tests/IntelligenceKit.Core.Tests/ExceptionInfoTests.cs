using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Core.Tests;

public class ExceptionInfoTests
{
    [Fact]
    public void FromException_CapturesTypeAndMessage()
    {
        var ex = new InvalidOperationException("boom");

        var info = ExceptionInfo.FromException(ex);

        Assert.Equal("System.InvalidOperationException", info.Type);
        Assert.Equal("boom", info.Message);
    }

    [Fact]
    public void FromException_WithNoInner_HasNullInnerException()
    {
        var info = ExceptionInfo.FromException(new Exception("solo"));

        Assert.Null(info.InnerException);
    }

    [Fact]
    public void FromException_PreservesInnerExceptionChain()
    {
        var root = new ArgumentNullException("param", "root cause");
        var middle = new InvalidOperationException("middle", root);
        var outer = new ApplicationException("outer", middle);

        var info = ExceptionInfo.FromException(outer);

        Assert.Equal("System.ApplicationException", info.Type);
        Assert.Equal("outer", info.Message);

        Assert.NotNull(info.InnerException);
        Assert.Equal("System.InvalidOperationException", info.InnerException!.Type);
        Assert.Equal("middle", info.InnerException.Message);

        Assert.NotNull(info.InnerException.InnerException);
        Assert.Equal("System.ArgumentNullException", info.InnerException.InnerException!.Type);

        // The chain terminates.
        Assert.Null(info.InnerException.InnerException.InnerException);
    }

    [Fact]
    public void FromException_NeverNullsStackTraceOrSource()
    {
        // A freshly-constructed (unthrown) exception has null StackTrace/Source;
        // the factory must normalize those to empty strings, not propagate null.
        var info = ExceptionInfo.FromException(new Exception("never thrown"));

        Assert.NotNull(info.StackTrace);
        Assert.NotNull(info.Source);
        Assert.Equal(string.Empty, info.StackTrace);
    }

    [Fact]
    public void FromException_CapturesStackTrace_WhenThrown()
    {
        try
        {
            throw new InvalidOperationException("thrown");
        }
        catch (Exception ex)
        {
            var info = ExceptionInfo.FromException(ex);
            Assert.False(string.IsNullOrEmpty(info.StackTrace));
        }
    }
}
