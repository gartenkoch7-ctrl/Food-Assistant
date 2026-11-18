using System.Globalization;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Inventory;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Tests.Inventory;

/// <summary>Wording of expiry dates after the KB table, German and English, including the 6/7-day boundary.</summary>
public sealed class ExpiryTextTests : UiTestContext
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Theory]
    [InlineData("de", -1, ExpiryStatusDto.Expired, "seit gestern abgelaufen")]
    [InlineData("de", -3, ExpiryStatusDto.Expired, "seit 3 Tagen abgelaufen")]
    [InlineData("de", 0, ExpiryStatusDto.Soon, "heute")]
    [InlineData("de", 1, ExpiryStatusDto.Soon, "morgen")]
    [InlineData("de", 2, ExpiryStatusDto.Soon, "noch 2 Tage")]
    [InlineData("de", 6, ExpiryStatusDto.Ok, "bis 24.11.")]
    [InlineData("de", 7, ExpiryStatusDto.Ok, "25.11.")]
    [InlineData("en", -1, ExpiryStatusDto.Expired, "expired yesterday")]
    [InlineData("en", -3, ExpiryStatusDto.Expired, "expired 3 days ago")]
    [InlineData("en", 0, ExpiryStatusDto.Soon, "today")]
    [InlineData("en", 1, ExpiryStatusDto.Soon, "tomorrow")]
    [InlineData("en", 2, ExpiryStatusDto.Soon, "2 days left")]
    [InlineData("en", 6, ExpiryStatusDto.Ok, "until 11/24")]
    [InlineData("en", 7, ExpiryStatusDto.Ok, "11/25")]
    public void Describe_FollowsTheKbTable(string culture, int days, ExpiryStatusDto status, string expected)
    {
        UseCulture(culture);

        Assert.Equal(expected, ExpiryText.Describe(Batch(days, status), Text, CultureInfo.CurrentCulture));
    }

    [Theory]
    [InlineData("de", "kein Datum")]
    [InlineData("en", "no date")]
    public void Describe_WithoutDate(string culture, string expected)
    {
        UseCulture(culture);

        Assert.Equal(expected, ExpiryText.Describe(Batch(null, ExpiryStatusDto.NoDate), Text, CultureInfo.CurrentCulture));
    }

    [Fact]
    public void Hint_DependsOnTheKind_OnlyWhenExpired()
    {
        Assert.Equal(
            "Verbrauchsdatum überschritten – nicht mehr essen.",
            ExpiryText.Hint(Batch(-1, ExpiryStatusDto.Expired, ExpiryKindDto.UseBy), Text));
        Assert.StartsWith("Mindesthaltbarkeit überschritten – oft noch gut.", ExpiryText.Hint(Batch(-1, ExpiryStatusDto.Expired), Text), StringComparison.Ordinal);
        Assert.Null(ExpiryText.Hint(Batch(0, ExpiryStatusDto.Soon, ExpiryKindDto.UseBy), Text));
    }

    private IStringLocalizer<InventoryText> Text => Services.GetRequiredService<IStringLocalizer<InventoryText>>();

    private static BatchDto Batch(int? days, ExpiryStatusDto status, ExpiryKindDto kind = ExpiryKindDto.BestBefore)
    {
        DateOnly? date = days is { } d ? Today.AddDays(d) : null;
        var dateKind = date is null ? ExpiryKindDto.None : kind;
        return new BatchDto(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), new QuantityDto(1m, "l"), StorageTypeDto.Chilled,
            BatchStateDto.Sealed, date, dateKind, date, dateKind, null, null, null, null, BatchOriginDto.Manual, status, days, 1);
    }
}
