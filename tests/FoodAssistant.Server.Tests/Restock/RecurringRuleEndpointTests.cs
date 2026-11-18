using System.Net;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Restock;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Server.Tests.Api;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Shopping;
using FoodAssistant.Server.Tests.Support;

namespace FoodAssistant.Server.Tests.Restock;

public sealed class RecurringRuleEndpointTests(PostgresContainerFixture postgres)
{
    private static string Recurring(ShoppingApi api) => $"{api.Base}/recurring";

    private static RecurringRuleRequest Weekly(Guid listId, string text = "Milk") =>
        new(null, listId, null, text, new QuantityDto(2m, "l"), RecurrenceFrequencyDto.Weekly, DayOfWeek.Monday, null);

    [Fact]
    public async Task Rule_CreateChangeDelete_WithETags_AndFirstDueDateCountsToday()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var url = Recurring(api);

        using var create = await api.Owner.PostAsync(url, Weekly(list.Id, "  Milk "));
        var created = await TestClient.ReadAsync<RecurringRuleDto>(create, HttpStatusCode.Created);
        var ruleUrl = $"{url}/{created.Id}";
        var change = Weekly(list.Id, "Oat milk") with { Frequency = RecurrenceFrequencyDto.Daily, DayOfWeek = null };
        using var missing = await ShoppingApi.SendAsync(api.Owner, HttpMethod.Put, ruleUrl, change, etag: null);
        using var stale = await ShoppingApi.SendAsync(api.Owner, HttpMethod.Put, ruleUrl, change, ShoppingApi.ETag(created.Version + 1));
        using var put = await ShoppingApi.SendAsync(api.Owner, HttpMethod.Put, ruleUrl, change, ShoppingApi.ETag(created.Version));

        Assert.Equal(("Milk", RecurrenceFrequencyDto.Weekly, DayOfWeek.Monday), (created.Text, created.Frequency, created.DayOfWeek));
        Assert.Equal(DayOfWeek.Monday, created.NextDueOn.DayOfWeek);
        Assert.InRange(created.NextDueOn, Today(api), Today(api).AddDays(6));
        Assert.Equal(ShoppingApi.ETag(created.Version), create.Headers.ETag?.Tag);
        await TestClient.AssertProblemAsync(missing, HttpStatusCode.PreconditionRequired, ErrorCodes.PreconditionRequired);
        await TestClient.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
        var changed = await TestClient.ReadAsync<RecurringRuleDto>(put);
        Assert.Equal(("Oat milk", RecurrenceFrequencyDto.Daily, (DayOfWeek?)null, Today(api)), (changed.Text, changed.Frequency, changed.DayOfWeek, changed.NextDueOn));
        Assert.Equal(changed, Assert.Single(await api.Owner.GetJsonAsync<List<RecurringRuleDto>>(url)));

        using var delete = await ShoppingApi.SendAsync<object>(api.Owner, HttpMethod.Delete, ruleUrl, null, ShoppingApi.ETag(changed.Version));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await api.Owner.GetJsonAsync<List<RecurringRuleDto>>(url));
    }

    [Fact]
    public async Task Change_OfTextQuantityOrList_KeepsTheDueDate_AndARepeatedIdIsAConflict()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var other = await api.CreateListAsync("Party");
        var id = Guid.CreateVersion7();
        using var create = await api.Owner.PostAsync(Recurring(api), Weekly(list.Id) with { Id = id });
        var created = await TestClient.ReadAsync<RecurringRuleDto>(create, HttpStatusCode.Created);

        using var change = await ShoppingApi.SendAsync(
            api.Owner, HttpMethod.Put, $"{Recurring(api)}/{id}",
            Weekly(other.Id, "Oat milk") with { Quantity = null }, ShoppingApi.ETag(created.Version));
        using var again = await api.Owner.PostAsync(Recurring(api), Weekly(list.Id) with { Id = id });
        using var staleDelete = await ShoppingApi.SendAsync<object>(
            api.Owner, HttpMethod.Delete, $"{Recurring(api)}/{id}", null, ShoppingApi.ETag(created.Version));

        var changed = await TestClient.ReadAsync<RecurringRuleDto>(change);
        Assert.Equal((created.NextDueOn, other.Id, "Oat milk"), (changed.NextDueOn, changed.ListId, changed.Text));
        await TestClient.AssertProblemAsync(again, HttpStatusCode.Conflict, ErrorCodes.Conflict);
        await TestClient.AssertProblemAsync(staleDelete, HttpStatusCode.PreconditionFailed, ErrorCodes.PreconditionFailed);
    }

    [Fact]
    public async Task Rule_WithProduct_TakesItsName()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var product = await HouseholdDataSeed.ProductAsync(api.App.Services, api.HouseholdId, "Butter");

        using var create = await api.Owner.PostAsync(
            Recurring(api),
            new RecurringRuleRequest(null, list.Id, product.Id, "ignored", null, RecurrenceFrequencyDto.Monthly, null, 31));

        var rule = await TestClient.ReadAsync<RecurringRuleDto>(create, HttpStatusCode.Created);
        Assert.Equal(("Butter", product.Id, 31), (rule.Text, rule.ProductId, rule.DayOfMonth));
    }

    [Fact]
    public async Task InvalidInput_Returns400WithFieldErrors()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();

        using var noWeekday = await api.Owner.PostAsync(Recurring(api), Weekly(list.Id) with { DayOfWeek = null });
        using var badDay = await api.Owner.PostAsync(
            Recurring(api), Weekly(list.Id) with { Frequency = RecurrenceFrequencyDto.Monthly, DayOfMonth = 32 });
        using var noText = await api.Owner.PostAsync(Recurring(api), Weekly(list.Id, " "));
        using var badQuantity = await api.Owner.PostAsync(Recurring(api), Weekly(list.Id) with { Quantity = new QuantityDto(-1m, "l") });

        var weekday = await ProblemAssert.IsProblemAsync(noWeekday, HttpStatusCode.BadRequest, RestockErrorCodes.ValidationFailed);
        var day = await ProblemAssert.IsProblemAsync(badDay, HttpStatusCode.BadRequest, RestockErrorCodes.ValidationFailed);
        var text = await ProblemAssert.IsProblemAsync(noText, HttpStatusCode.BadRequest, RestockErrorCodes.ValidationFailed);
        var quantity = await ProblemAssert.IsProblemAsync(badQuantity, HttpStatusCode.BadRequest, RestockErrorCodes.ValidationFailed);
        Assert.Equal("required", weekday.GetProperty("errors").GetProperty("dayOfWeek")[0].GetString());
        Assert.Equal("required", day.GetProperty("errors").GetProperty("dayOfMonth")[0].GetString());
        Assert.Equal("required", text.GetProperty("errors").GetProperty("text")[0].GetString());
        Assert.Equal("invalid", quantity.GetProperty("errors").GetProperty("quantity")[0].GetString());
    }

    [Fact]
    public async Task Restricted_Reads_ButCannotWrite()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        using var create = await api.Owner.PostAsync(Recurring(api), Weekly(list.Id));
        var rule = await TestClient.ReadAsync<RecurringRuleDto>(create, HttpStatusCode.Created);
        var (kid, _) = await api.AddMemberAsync(HouseholdRole.Restricted, "kid");
        using var _ = kid;

        Assert.Single(await kid.GetJsonAsync<List<RecurringRuleDto>>(Recurring(api)));
        using var post = await kid.PostAsync(Recurring(api), Weekly(list.Id));
        using var put = await ShoppingApi.SendAsync(kid, HttpMethod.Put, $"{Recurring(api)}/{rule.Id}", Weekly(list.Id), "*");
        using var delete = await ShoppingApi.SendAsync<object>(kid, HttpMethod.Delete, $"{Recurring(api)}/{rule.Id}", null, "*");

        await TestClient.AssertProblemAsync(post, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(put, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task ForeignHousehold_RulesListsAndProductsAreNotFound()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        using var create = await api.Owner.PostAsync(Recurring(api), Weekly(list.Id));
        var rule = await TestClient.ReadAsync<RecurringRuleDto>(create, HttpStatusCode.Created);
        var product = await HouseholdDataSeed.ProductAsync(api.App.Services, api.HouseholdId, "Butter");
        var foreign = await Scenario.CreateHouseholdAsync(api.Owner, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(api.App, api.Owner, foreign, HouseholdRole.Owner, "outsider");
        using var _ = outsider;
        var own = $"{ShoppingApi.Root(foreign)}/recurring";
        var ownList = Assert.Single(
            await outsider.GetJsonAsync<List<ShoppingListDto>>($"{ShoppingApi.Root(foreign)}/lists"), l => l.IsDefault);

        using var direct = await outsider.GetAsync(Recurring(api));
        using var put = await ShoppingApi.SendAsync(outsider, HttpMethod.Put, $"{own}/{rule.Id}", Weekly(ownList.Id), "*");
        using var delete = await ShoppingApi.SendAsync<object>(outsider, HttpMethod.Delete, $"{own}/{rule.Id}", null, "*");
        using var foreignList = await outsider.PostAsync(own, Weekly(list.Id));
        using var foreignProduct = await outsider.PostAsync(
            own, Weekly(ownList.Id) with { ProductId = product.Id });

        await TestClient.AssertProblemAsync(direct, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(put, HttpStatusCode.NotFound, RestockErrorCodes.RuleNotFound);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.NotFound, RestockErrorCodes.RuleNotFound);
        await TestClient.AssertProblemAsync(foreignList, HttpStatusCode.NotFound, ShoppingErrorCodes.ListNotFound);
        await TestClient.AssertProblemAsync(foreignProduct, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        Assert.Empty(await outsider.GetJsonAsync<List<RecurringRuleDto>>(own));
    }

    [Fact]
    public async Task DeletingTheList_RemovesItsRules()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var extra = await api.CreateListAsync("Party");
        using var create = await api.Owner.PostAsync(Recurring(api), Weekly(extra.Id));
        await TestClient.ReadAsync<RecurringRuleDto>(create, HttpStatusCode.Created);

        using var delete = await ShoppingApi.SendAsync<object>(
            api.Owner, HttpMethod.Delete, $"{api.Base}/lists/{extra.Id}", null, "*");

        Assert.True(delete.IsSuccessStatusCode);
        Assert.Empty(await api.Owner.GetJsonAsync<List<RecurringRuleDto>>(Recurring(api)));
    }

    // The household of the test setup lives in Europe/Berlin (Scenario.SetupRequest).
    private static DateOnly Today(ShoppingApi api)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.App.Time.GetUtcNow(), zone).DateTime);
    }
}
