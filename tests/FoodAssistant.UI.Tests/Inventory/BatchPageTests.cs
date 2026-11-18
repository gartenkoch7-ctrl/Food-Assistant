using Bunit;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Inventory;

namespace FoodAssistant.UI.Tests.Inventory;

public sealed class BatchPageTests : InventoryTestContext
{
    [Fact]
    public void KnownBatch_ShowsProductAndActions()
    {
        var soup = Summary("Suppe");
        var batch = Batch(soup, 2, ExpiryStatusDto.Soon, location: Freezer);
        Stock(soup, batch);

        var cut = Render<BatchPage>(p => p.Add(x => x.BatchId, batch.Id));

        Assert.Equal("Suppe", cut.Find("h1").TextContent);
        Assert.Contains("Gefrierschrank", cut.Find(".fa-batch-card").TextContent, StringComparison.Ordinal);
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Aufgebraucht");
    }

    [Fact]
    public void UsedUpBatch_ShowsThatItIsGone_NotAnError()
    {
        var cut = Render<BatchPage>(p => p.Add(x => x.BatchId, Guid.CreateVersion7()));

        Assert.Contains("Diese Charge gibt es nicht mehr", cut.Find(".fa-empty-heading").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[role='alert']"));
    }
}
