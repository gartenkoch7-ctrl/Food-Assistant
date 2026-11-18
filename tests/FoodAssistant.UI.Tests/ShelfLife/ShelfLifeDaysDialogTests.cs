using Bunit;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.UI.ShelfLife;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.ShelfLife;

public sealed class ShelfLifeDaysDialogTests : UiTestContext
{
    private readonly List<ShelfLifeDaysDto> _saved = [];

    private Func<ShelfLifeDaysDto, Task<ApiResult<ShelfLifeRuleRowDto>>> SaveAnswering(ApiResult<ShelfLifeRuleRowDto> answer) =>
        days =>
        {
            _saved.Add(days);
            return Task.FromResult(answer);
        };

    private (IRenderedComponent<MudDialogProvider> Provider, IDialogReference Dialog) Open(
        ShelfLifeDaysDto? own, Func<ShelfLifeDaysDto, Task<ApiResult<ShelfLifeRuleRowDto>>> save)
    {
        var provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<ShelfLifeDaysDialog>
        {
            { d => d.Title, "Milchprodukte, Gekühlt" },
            { d => d.Hint, "Hinweis" },
            { d => d.Own, own },
            { d => d.Inherited, new ShelfLifeDaysDto(3, null, null, null) },
            { d => d.Save, save },
        };
        var service = Services.GetRequiredService<IDialogService>();
        var dialog = provider.InvokeAsync(() => service.ShowAsync<ShelfLifeDaysDialog>("Milchprodukte, Gekühlt", parameters)).Result;
        provider.WaitForAssertion(() => Assert.Equal(4, provider.FindAll("input").Count));
        return (provider, dialog);
    }

    [Fact]
    public void Fields_HaveLabelsUnitAndInheritedValue_AndStartWithOwnValues()
    {
        var (provider, _) = Open(new ShelfLifeDaysDto(null, 60, null, null), SaveAnswering(ApiResult<ShelfLifeRuleRowDto>.Success(Row())));

        var labels = provider.FindAll("label").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(["Nach dem Öffnen", "Im Gefrierfach", "Nach dem Auftauen", "Als Rest"], labels);
        Assert.Contains("Ohne eigenen Wert: 3 Tage", provider.Markup, StringComparison.Ordinal);
        Assert.Contains("Tage", provider.Markup, StringComparison.Ordinal);
        Assert.Equal("60", provider.FindAll("input")[1].GetAttribute("value"));
        Assert.Contains("Milchprodukte, Gekühlt", provider.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_SendsTheValues_AndClosesWithTheRow()
    {
        var row = Row();
        var (provider, dialog) = Open(null, SaveAnswering(ApiResult<ShelfLifeRuleRowDto>.Success(row)));

        provider.FindAll("input")[3].Input("2");
        provider.FindAll("button").Single(b => b.TextContent.Trim() == "Speichern").Click();

        var result = await dialog.Result;
        Assert.False(result!.Canceled);
        Assert.Same(row, result.Data);
        Assert.Equal(new ShelfLifeDaysDto(null, null, null, 2), Assert.Single(_saved));
    }

    [Fact]
    public void ServerFieldErrors_AreShownAtTheField_AndTheDialogStaysOpen()
    {
        var error = new ApiError(
            400,
            ShelfLifeErrorCodes.ValidationFailed,
            null,
            new Dictionary<string, string[]> { ["daysFrozen"] = ["out_of_range"] },
            null);
        var (provider, _) = Open(null, SaveAnswering(ApiResult<ShelfLifeRuleRowDto>.Failure(error)));

        provider.FindAll("input")[1].Input("12");
        provider.FindAll("button").Single(b => b.TextContent.Trim() == "Speichern").Click();

        provider.WaitForAssertion(() => Assert.Contains("Bitte prüfe die markierten Werte.", provider.Markup, StringComparison.Ordinal));
        var message = provider.Find(".fa-shelf-life-field-error");
        Assert.Equal("Bitte eine ganze Zahl von 0 bis 3650 eingeben.", message.TextContent);
        var input = provider.FindAll("input")[1];
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Contains(message.Id!, input.GetAttribute("aria-describedby") ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(4, provider.FindAll("input").Count);
    }

    [Fact]
    public void ValueAboveTheLimit_IsReportedAtTheField_NotClampedOrSaved()
    {
        var (provider, _) = Open(null, SaveAnswering(ApiResult<ShelfLifeRuleRowDto>.Success(Row())));

        provider.FindAll("input")[0].Input("5000");
        provider.FindAll("button").Single(b => b.TextContent.Trim() == "Speichern").Click();

        Assert.Equal("5000", provider.FindAll("input")[0].GetAttribute("value"));
        Assert.Equal("Bitte eine ganze Zahl von 0 bis 3650 eingeben.", provider.Find(".fa-shelf-life-field-error").TextContent);
        Assert.Contains("Bitte prüfe die markierten Werte.", provider.Find(".fa-api-error").TextContent, StringComparison.Ordinal);
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task Cancel_SavesNothing()
    {
        var (provider, dialog) = Open(null, SaveAnswering(ApiResult<ShelfLifeRuleRowDto>.Success(Row())));

        provider.FindAll("button").Single(b => b.TextContent.Trim() == "Abbrechen").Click();

        Assert.True((await dialog.Result)!.Canceled);
        Assert.Empty(_saved);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(3650, true)]
    [InlineData(-1, false)]
    [InlineData(3651, false)]
    public void Limits_MatchTheServer(int? days, bool valid) => Assert.Equal(valid, ShelfLifeDaysDialogLimits.IsValid(days));

    private static ShelfLifeRuleRowDto Row() => FakeShelfLifeClient.Row("dairy", StorageTypeDto.Chilled);
}
