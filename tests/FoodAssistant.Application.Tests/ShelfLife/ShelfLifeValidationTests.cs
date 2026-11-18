using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Contracts.ShelfLife;

namespace FoodAssistant.Application.Tests.ShelfLife;

public sealed class ShelfLifeValidationTests
{
    [Fact]
    public void NullAndBoundaryValues_AreValid()
    {
        Assert.Empty(ShelfLifeValidation.Validate(new ShelfLifeDaysDto(null, null, null, null)));
        Assert.Empty(ShelfLifeValidation.Validate(new ShelfLifeDaysDto(0, ShelfLifeValidation.MaxDays, 1, null)));
    }

    [Fact]
    public void NegativeOrTooLargeValues_AreReportedPerField()
    {
        var errors = ShelfLifeValidation.Validate(new ShelfLifeDaysDto(-1, ShelfLifeValidation.MaxDays + 1, 2, -5));

        Assert.Equal(["daysAfterOpening", "daysAsLeftover", "daysFrozen"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.All(errors.Values, codes => Assert.Equal([ShelfLifeValidation.OutOfRange], codes));
    }
}
