using FoodAssistant.Server.Api.Concurrency;
using Microsoft.AspNetCore.Http;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>Reading the expected version from If-Match (no server needed).</summary>
public sealed class IfMatchTests
{
    [Theory]
    [InlineData("\"42\"", 42L)]
    [InlineData("W/\"42\"", 42L)]
    [InlineData("\"0\"", 0L)]
    public void SingleNumericETag_IsTheExpectedVersion(string header, long expected)
    {
        Assert.True(IfMatch.TryRead(With(header), required: true, out var version, out var problem));

        Assert.Equal(expected, version);
        Assert.Null(problem);
    }

    [Fact]
    public void Star_MeansAnyVersion()
    {
        Assert.True(IfMatch.TryRead(With("*"), required: true, out var version, out var problem));

        Assert.Null(version);
        Assert.Null(problem);
    }

    [Fact]
    public void Missing_WhenRequired_Is428()
    {
        Assert.False(IfMatch.TryRead(With(null), required: true, out var version, out var problem));

        Assert.Null(version);
        Assert.Equal(StatusCodes.Status428PreconditionRequired, StatusOf(problem));
    }

    [Fact]
    public void Missing_WhenOptional_IsNoVersion()
    {
        Assert.True(IfMatch.TryRead(With(null), required: false, out var version, out var problem));

        Assert.Null(version);
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("\"abc\"")]
    [InlineData("\"-1\"")]
    [InlineData("\"1\", \"2\"")]
    [InlineData("\"1\", *")]
    [InlineData("42, \"3\"")]
    [InlineData("\"99999999999999999999\"")]
    public void Malformed_Is400(string header)
    {
        Assert.False(IfMatch.TryRead(With(header), required: true, out var version, out var problem));

        Assert.Null(version);
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(problem));
    }

    private static DefaultHttpContext With(string? ifMatch)
    {
        var context = new DefaultHttpContext();
        if (ifMatch is not null)
        {
            context.Request.Headers.IfMatch = ifMatch;
        }

        return context;
    }

    private static int? StatusOf(IResult? result) => (result as IStatusCodeHttpResult)?.StatusCode;
}
