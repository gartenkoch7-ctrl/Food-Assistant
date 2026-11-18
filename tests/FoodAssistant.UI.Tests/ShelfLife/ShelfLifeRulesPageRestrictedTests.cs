using Bunit;
using FoodAssistant.Client.ShelfLife;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.UI.ShelfLife;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.ShelfLife;

public sealed class ShelfLifeRulesPageRestrictedTests : UiTestContext
{
    public ShelfLifeRulesPageRestrictedTests()
        : base(role: HouseholdRole.Restricted)
    {
        var client = new FakeShelfLifeClient();
        client.Rules.Add(FakeShelfLifeClient.Row("dairy", StorageTypeDto.Chilled, own: new ShelfLifeDaysDto(1, null, null, null)));
        Services.AddSingleton<IShelfLifeClient>(client);
    }

    [Fact]
    public void Restricted_SeesValuesButNoEditButtons()
    {
        var cut = Render<ShelfLifeRulesPage>();
        cut.Find("button.fa-shelf-life-toggle").Click();

        Assert.Contains("1 Tag", cut.Find(".fa-shelf-life-row").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".fa-shelf-life-actions"));
    }
}
