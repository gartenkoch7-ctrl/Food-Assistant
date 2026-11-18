using System.Text.Json;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>Contract enums mirror the domain enums member by member, so mapping by name never fails.</summary>
public sealed class ContractEnumTests
{
    public static TheoryData<Type, Type> Mirrors => new()
    {
        { typeof(StorageTypeDto), typeof(StorageType) },
        { typeof(BatchStateDto), typeof(BatchState) },
        { typeof(ExpiryKindDto), typeof(ExpiryKind) },
        { typeof(ConsumptionKindDto), typeof(ConsumptionKind) },
        { typeof(ConsumptionReasonDto), typeof(ConsumptionReason) },
        { typeof(BatchOriginDto), typeof(BatchOrigin) },
        { typeof(ShoppingItemOriginDto), typeof(ShoppingItemOrigin) },
    };

    [Theory]
    [MemberData(nameof(Mirrors))]
    public void ContractEnum_HasTheDomainNamesAndValues(Type contract, Type domain)
    {
        Assert.Equal(Enum.GetNames(domain), Enum.GetNames(contract));
        Assert.Equal(UnderlyingValues(domain), UnderlyingValues(contract));
    }

    [Theory]
    [MemberData(nameof(Mirrors))]
    public void ContractEnum_SerializesAsLowerSnakeCaseString(Type contract, Type domain)
    {
        _ = domain;
        foreach (var value in Enum.GetValues(contract))
        {
            var json = JsonSerializer.Serialize(value, contract);

            Assert.Matches("^\"[a-z_]+\"$", json);
            Assert.Equal(value, JsonSerializer.Deserialize(json, contract));
        }
    }

    [Fact]
    public void ContractEnum_MultiWordMember_UsesSnakeCase() =>
        Assert.Equal("\"minimum_stock\"", JsonSerializer.Serialize(ShoppingItemOriginDto.MinimumStock));

    // A new int[] so the comparison sees plain numbers, not the enum types.
    private static int[] UnderlyingValues(Type enumType) =>
        [.. Enum.GetValues(enumType).Cast<object>().Select(value => (int)value)];
}
