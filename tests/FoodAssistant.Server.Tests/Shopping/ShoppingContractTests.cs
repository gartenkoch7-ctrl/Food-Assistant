using System.Text.Json;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Server.Tests.Shopping;

/// <summary>The outcome enum of the API mirrors the domain enum, like the enums in <c>ContractEnumTests</c>.</summary>
public sealed class ShoppingContractTests
{
    [Fact]
    public void ConflictOutcome_MirrorsTheDomainEnum()
    {
        Assert.Equal(Enum.GetNames<ShoppingConflictOutcome>(), Enum.GetNames<ShoppingConflictOutcomeDto>());
        Assert.Equal(
            Enum.GetValues<ShoppingConflictOutcome>().Select(v => (int)v),
            Enum.GetValues<ShoppingConflictOutcomeDto>().Select(v => (int)v));
    }

    [Fact]
    public void ConflictOutcome_SerializesAsSnakeCase()
    {
        foreach (var value in Enum.GetValues<ShoppingConflictOutcomeDto>())
        {
            var json = JsonSerializer.Serialize(value);

            Assert.Matches("^\"[a-z_]+\"$", json);
            Assert.Equal(value, JsonSerializer.Deserialize<ShoppingConflictOutcomeDto>(json));
        }

        Assert.Equal("\"change_wins_over_delete\"", JsonSerializer.Serialize(ShoppingConflictOutcomeDto.ChangeWinsOverDelete));
    }
}
