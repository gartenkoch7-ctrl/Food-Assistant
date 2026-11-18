using FoodAssistant.Application.Common;

namespace FoodAssistant.Application.Tests.Common;

public sealed class OperationResultTests
{
    [Fact]
    public void Ok_CarriesValue_AndSucceeds()
    {
        var result = OperationResult<string>.Ok("milk");

        Assert.Equal(OperationStatus.Ok, result.Status);
        Assert.Equal("milk", result.Value);
        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Created_And_Deleted_Succeed()
    {
        Assert.True(OperationResult<string>.Created("x").Succeeded);
        Assert.Equal(OperationStatus.Created, OperationResult<string>.Created("x").Status);

        var deleted = OperationResult<string>.Deleted();
        Assert.True(deleted.Succeeded);
        Assert.Equal(OperationStatus.Deleted, deleted.Status);
        Assert.Null(deleted.Value);
    }

    [Fact]
    public void Failures_CarryTheirCode_AndDoNotSucceed()
    {
        var failures = new[]
        {
            (OperationResult<string>.NotFound("t.not_found"), OperationStatus.NotFound),
            (OperationResult<string>.Conflict("t.conflict"), OperationStatus.Conflict),
            (OperationResult<string>.RuleViolated("t.rule"), OperationStatus.RuleViolated),
            (OperationResult<string>.Forbidden("t.forbidden"), OperationStatus.Forbidden),
        };

        foreach (var (result, status) in failures)
        {
            Assert.Equal(status, result.Status);
            Assert.False(result.Succeeded);
            Assert.StartsWith("t.", result.ErrorCode, StringComparison.Ordinal);
            Assert.Null(result.Value);
        }
    }

    [Fact]
    public void Invalid_CarriesFieldErrors()
    {
        var errors = new Dictionary<string, string[]> { ["name"] = ["required"] };

        var result = OperationResult<string>.Invalid(errors, "t.validation_failed");

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.Same(errors, result.Errors);
        Assert.Equal("t.validation_failed", result.ErrorCode);
    }

    [Fact]
    public void VersionMismatch_CarriesCurrentVersionAndState()
    {
        var result = OperationResult<string>.VersionMismatch(7, "stored");

        Assert.Equal(OperationStatus.VersionMismatch, result.Status);
        Assert.Equal(7, result.CurrentVersion);
        Assert.Equal("stored", result.Value);
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Failures_RequireACode(string code)
    {
        Assert.Throws<ArgumentException>(() => OperationResult<string>.NotFound(code));
        Assert.Throws<ArgumentException>(() => OperationResult<string>.Invalid(new Dictionary<string, string[]>(), code));
    }
}
