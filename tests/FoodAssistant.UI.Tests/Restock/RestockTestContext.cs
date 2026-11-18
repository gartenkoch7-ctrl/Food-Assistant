using AngleSharp.Dom;
using Bunit;
using FoodAssistant.Client.Restock;
using FoodAssistant.Client.Shopping;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Restock;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Restock;

/// <summary>UI test context with fake restock and shopping clients plus the MudBlazor providers dialogs need.</summary>
public abstract class RestockTestContext : UiTestContext
{
    protected RestockTestContext(string culture = "de", HouseholdRole role = HouseholdRole.Member)
        : base(culture, signedIn: true, role)
    {
        Restock = new FakeRestockClient();
        Shopping = new FakeShoppingClient();
        Services.AddSingleton<IRestockClient>(Restock);
        Services.AddSingleton<IShoppingClient>(Shopping);
        Popovers = Render<MudPopoverProvider>();
        Dialogs = Render<MudDialogProvider>();
    }

    protected FakeRestockClient Restock { get; }

    protected FakeShoppingClient Shopping { get; }

    protected IRenderedComponent<MudPopoverProvider> Popovers { get; }

    protected IRenderedComponent<MudDialogProvider> Dialogs { get; }

    protected ShoppingListDto AddList(string name = "", bool isDefault = true)
    {
        var list = new ShoppingListDto(Guid.CreateVersion7(), name, null, isDefault, Shopping.Lists.Count, 0, 0, 1);
        Shopping.Lists.Add(list);
        return list;
    }

    protected RecurringRuleDto AddRule(
        Guid listId,
        string text = "Milch",
        RecurrenceFrequencyDto frequency = RecurrenceFrequencyDto.Weekly,
        DayOfWeek? day = DayOfWeek.Monday,
        int? dayOfMonth = null,
        Guid? productId = null,
        long version = 4)
    {
        var rule = new RecurringRuleDto(
            Guid.CreateVersion7(), listId, productId, text, new QuantityDto(2m, "l"), frequency, day, dayOfMonth, new DateOnly(2026, 11, 24), version);
        Restock.Rules.Add(rule);
        return rule;
    }

    protected async Task<IDialogReference> ShowAsync<TDialog>(Action<DialogParameters<TDialog>>? configure = null)
        where TDialog : ComponentBase
    {
        var parameters = new DialogParameters<TDialog>();
        configure?.Invoke(parameters);
        var service = Services.GetRequiredService<IDialogService>();
        return await Dialogs.InvokeAsync(() => service.ShowAsync<TDialog>("dialog", parameters));
    }

    protected static IElement ButtonByText(IRenderedComponent<MudDialogProvider> provider, string text) =>
        provider.FindAll("button").First(b => b.TextContent.Trim() == text);
}
