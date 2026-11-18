using System.Net;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Restock;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Restock;

namespace FoodAssistant.Client.Tests.Restock;

public sealed class RestockClientTests
{
    private static readonly Guid Household = Guid.Parse("0197a0c0-0000-7000-8000-000000000001");
    private static readonly Guid Rule = Guid.Parse("0197a0c0-0000-7000-8000-000000000002");
    private static readonly Guid List = Guid.Parse("0197a0c0-0000-7000-8000-000000000003");
    private static string Base => $"https://food.test/api/v1/households/{Household:D}/shopping/recurring";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string RuleJson =
        """{"id":"0197a0c0-0000-7000-8000-000000000002","listId":"0197a0c0-0000-7000-8000-000000000003","productId":null,"text":"Milk","quantity":{"value":2,"unit":"l"},"frequency":"weekly","dayOfWeek":1,"dayOfMonth":null,"nextDueOn":"2026-11-23","version":4}""";

    private static readonly RecurringRuleRequest Request =
        new(null, List, null, "Milk", null, RecurrenceFrequencyDto.Weekly, DayOfWeek.Monday, null);

    private static (RestockClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new RestockClient(api), handler);
    }

    [Fact]
    public async Task ListRules_GetsTheRules()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, $"[{RuleJson}]");

        var result = await client.ListRulesAsync(Household, Ct);

        Assert.Equal(Base, handler.Requests[0].Uri.AbsoluteUri);
        var rule = Assert.Single(result.Value!);
        Assert.Equal((RecurrenceFrequencyDto.Weekly, DayOfWeek.Monday, new DateOnly(2026, 11, 23)), (rule.Frequency, rule.DayOfWeek, rule.NextDueOn));
    }

    [Fact]
    public async Task CreateRule_PostsWithoutIfMatch_FrequencyAsSnakeCase()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Created, RuleJson);

        await client.CreateRuleAsync(Household, Request, Ct);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.False(handler.Requests[0].Headers.ContainsKey("If-Match"));
        Assert.Contains("\"frequency\":\"weekly\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateRule_SendsPutWithIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, RuleJson);

        await client.UpdateRuleAsync(Household, Rule, Request, "\"4\"", Ct);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal($"{Base}/{Rule:D}", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("\"4\"", handler.Requests[0].Headers["If-Match"]);
    }

    [Fact]
    public async Task DeleteRule_SendsDeleteWithIfMatch()
    {
        var (client, handler) = Create();
        handler.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await client.DeleteRuleAsync(Household, Rule, "\"4\"", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("\"4\"", handler.Requests[0].Headers["If-Match"]);
    }
}
