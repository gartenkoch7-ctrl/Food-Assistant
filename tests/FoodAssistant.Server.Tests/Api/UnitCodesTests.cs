using FoodAssistant.Contracts.Common;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>The API unit codes stay in sync with the domain units.</summary>
public sealed class UnitCodesTests
{
    [Fact]
    public void All_EqualsTheDomainUnitCodes() =>
        Assert.Equal(Unit.All.Select(u => u.Code), UnitCodes.All);
}
