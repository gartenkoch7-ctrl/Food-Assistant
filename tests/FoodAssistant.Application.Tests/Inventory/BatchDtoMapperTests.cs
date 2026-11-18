using FoodAssistant.Application.Households;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Tests.Inventory;

/// <summary>Batch and product DTOs: status for the household's today, totals, worst status.</summary>
public sealed class BatchDtoMapperTests
{
    private static readonly HouseholdToday Today = new(new DateOnly(2026, 11, 18), ExpiryLeadDays: 2, "Europe/Berlin");
    private static readonly ProductSummaryDto Milk = new(Guid.CreateVersion7(), "Milk", null, "dairy", "l");

    [Theory]
    [InlineData(-1, ExpiryStatusDto.Expired)]
    [InlineData(0, ExpiryStatusDto.Soon)]
    [InlineData(2, ExpiryStatusDto.Soon)]
    [InlineData(3, ExpiryStatusDto.Ok)]
    public void ToDto_StatusFollowsLeadDays(int days, ExpiryStatusDto expected)
    {
        var dto = BatchDtoMapper.ToDto(Context(Batch(1m, Unit.Litre, days)), Today);

        Assert.Equal((expected, days), (dto.Status, dto.DaysUntilExpiry!.Value));
    }

    [Fact]
    public void ToDto_CopiesStateOriginAndVersion()
    {
        var batch = Batch(2m, Unit.Piece, null);
        var context = Context(batch) with { Origin = BatchOrigin.Purchase, Version = 42 };

        var dto = BatchDtoMapper.ToDto(context, Today);

        Assert.Equal((ExpiryStatusDto.NoDate, (int?)null, BatchOriginDto.Purchase, 42L), (dto.Status, dto.DaysUntilExpiry, dto.Origin, dto.Version));
        Assert.Equal((new QuantityDto(2m, "piece"), StorageTypeDto.Chilled, BatchStateDto.Sealed), (dto.Quantity, dto.StorageType, dto.State));
    }

    [Fact]
    public void ToProductDto_SumsPerConvertibleGroup_InDefaultUnit_WorstStatusAndNextExpiry()
    {
        BatchContext[] batches =
        [
            Context(Batch(500m, Unit.Millilitre, 5)),
            Context(Batch(1m, Unit.Litre, null)),
            Context(Batch(2m, Unit.Package, 1)),
            Context(Batch(1m, Unit.Package, -3)),
        ];

        var dto = BatchDtoMapper.ToProductDto(Milk, batches, Today);

        Assert.Equal([new QuantityDto(3m, "package"), new QuantityDto(1.5m, "l")], dto.Totals);
        Assert.Equal((ExpiryStatusDto.Expired, Today.Today.AddDays(-3)), (dto.WorstStatus, dto.NextExpiry));
        Assert.Equal([-3, 1, 5], dto.Batches.Where(b => b.DaysUntilExpiry is not null).Select(b => b.DaysUntilExpiry!.Value));
        Assert.Null(dto.Batches[^1].ExpiresOn);
    }

    [Fact]
    public void ToProductDto_NoBatches_HasNoDateAndNoTotals()
    {
        var dto = BatchDtoMapper.ToProductDto(Milk, [], Today);

        Assert.Equal((ExpiryStatusDto.NoDate, (DateOnly?)null), (dto.WorstStatus, dto.NextExpiry));
        Assert.Empty(dto.Totals);
        Assert.Empty(dto.Batches);
    }

    [Fact]
    public void ToProductDto_UnknownDefaultUnit_UsesFirstBatchUnit()
    {
        var dto = BatchDtoMapper.ToProductDto(Milk with { DefaultUnit = "bucket" }, [Context(Batch(1m, Unit.Kilogram, 4)), Context(Batch(250m, Unit.Gram, 3))], Today);

        Assert.Equal([new QuantityDto(1250m, "g")], dto.Totals);
    }

    [Fact]
    public void ToProductDto_ApproximateUnits_SumInExactUnitRoundedToThreeDecimals()
    {
        var spice = Milk with { DefaultUnit = "tbsp" };

        var dto = BatchDtoMapper.ToProductDto(spice, [Context(Batch(1m, Unit.Tablespoon, 1)), Context(Batch(100m, Unit.Millilitre, 2))], Today);
        var thirds = BatchDtoMapper.ToProductDto(Milk, [Context(Batch(1m, Unit.Litre, 1)), Context(Batch(1m, Unit.Teaspoon, 2))], Today);

        Assert.Equal([new QuantityDto(115m, "ml")], dto.Totals);
        Assert.Equal([new QuantityDto(1.005m, "l")], thirds.Totals);
    }

    private static StockBatch Batch(decimal amount, Unit unit, int? days) =>
        new(Guid.CreateVersion7(), new Quantity(amount, unit), StorageType.Chilled,
            days is { } d ? Today.Today.AddDays(d) : null, days is null ? ExpiryKind.None : ExpiryKind.BestBefore);

    private static BatchContext Context(StockBatch batch) =>
        new(batch, Milk.Id, Guid.CreateVersion7(), BatchOrigin.Manual, Today.Today, DateTimeOffset.UnixEpoch, 1);
}
