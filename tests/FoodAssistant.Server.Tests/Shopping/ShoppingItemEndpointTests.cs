using System.Net;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Shopping;
using FoodAssistant.Server.Tests.Api;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;

namespace FoodAssistant.Server.Tests.Shopping;

public sealed class ShoppingItemEndpointTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task AddFreeText_UsesOtherSection_AndRepeatedIdReturnsTheExistingEntry()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var id = Guid.CreateVersion7();
        var request = new CreateShoppingItemRequest(id, null, "  Candles ", new QuantityDto(2m, UnitCodes.Piece), " for the table ", null);
        var url = $"{api.Base}/lists/{list.Id}/items";

        using var first = await api.Owner.PostAsync(url, request);
        var created = await TestClient.ReadAsync<ShoppingItemDto>(first, HttpStatusCode.Created);
        using var repeat = await api.Owner.PostAsync(url, request with { Text = "Other" });
        var repeated = await TestClient.ReadAsync<ShoppingItemDto>(repeat);

        Assert.Equal((id, "Candles", "for the table", StoreSections.Other), (created.Id, created.Text, created.Note, created.SectionKey));
        Assert.Equal((ShoppingItemOriginDto.Manual, 1L, false), (created.Origin, created.Version, created.Checked));
        Assert.Equal(new QuantityDto(2m, UnitCodes.Piece), created.Quantity);
        Assert.Equal("\"1\"", first.Headers.ETag?.Tag);
        Assert.Equal(created, repeated);
        Assert.Single(await api.ItemsAsync(list.Id));

        // Also a deleted entry answers 200 with its tombstone instead of coming back.
        await api.DeleteOkAsync(api.Owner, id, created.Version);
        using var afterDelete = await api.Owner.PostAsync(url, request);
        Assert.Equal(HttpStatusCode.OK, afterDelete.StatusCode);
        Assert.Empty(await api.ItemsAsync(list.Id));
    }

    [Fact]
    public async Task AddProduct_TakesNameAndSectionFromProductOrCategory()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var milk = await HouseholdDataSeed.ProductAsync(api.App.Services, api.HouseholdId, "Milk", ProductCategories.Dairy);
        var tofu = await HouseholdDataSeed.ProductAsync(api.App.Services, api.HouseholdId, "Tofu", ProductCategories.PlantBased);
        await api.WithDbAsync(async db =>
        {
            db.Attach(tofu);
            tofu.SectionKey = StoreSections.Produce;
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var url = $"{api.Base}/lists/{list.Id}/items";

        using var milkResponse = await api.Owner.PostAsync(url, new CreateShoppingItemRequest(null, milk.Id, "ignored", null, null, StoreSections.Bakery));
        using var tofuResponse = await api.Owner.PostAsync(url, new CreateShoppingItemRequest(null, tofu.Id, null, null, null, null));

        var milkItem = await TestClient.ReadAsync<ShoppingItemDto>(milkResponse, HttpStatusCode.Created);
        var tofuItem = await TestClient.ReadAsync<ShoppingItemDto>(tofuResponse, HttpStatusCode.Created);
        Assert.Equal((milk.Id, "Milk", StoreSections.DairyChilled), (milkItem.ProductId, milkItem.Text, milkItem.SectionKey));
        Assert.Equal(("Tofu", StoreSections.Produce), (tofuItem.Text, tofuItem.SectionKey));
    }

    [Fact]
    public async Task AddItem_UnknownOrArchivedProduct_Returns404()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var archived = await HouseholdDataSeed.ProductAsync(api.App.Services, api.HouseholdId, "Old");
        await api.WithDbAsync(async db =>
        {
            db.Attach(archived);
            archived.ArchivedAt = DateTimeOffset.UtcNow;
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var url = $"{api.Base}/lists/{list.Id}/items";

        using var unknown = await api.Owner.PostAsync(url, new CreateShoppingItemRequest(null, Guid.CreateVersion7(), null, null, null, null));
        using var old = await api.Owner.PostAsync(url, new CreateShoppingItemRequest(null, archived.Id, null, null, null, null));

        await TestClient.AssertProblemAsync(unknown, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
        await TestClient.AssertProblemAsync(old, HttpStatusCode.NotFound, CatalogErrorCodes.ProductNotFound);
    }

    [Fact]
    public async Task AddAndChange_InvalidInput_Returns400WithFieldErrors()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var item = await api.AddTextAsync(list.Id, "Bread");

        using var add = await api.Owner.PostAsync(
            $"{api.Base}/lists/{list.Id}/items",
            new CreateShoppingItemRequest(null, null, " ", new QuantityDto(0m, UnitCodes.Gram), new string('n', 501), "aisle_7"));
        using var change = await api.ChangeAsync(
            api.Owner,
            new ChangeShoppingItemRequest(0, false, new QuantityDto(1m, "bucket"), null, new string('t', 201), ""),
            item.Id);

        var addProblem = await ProblemAssert.IsProblemAsync(add, HttpStatusCode.BadRequest, ShoppingErrorCodes.ValidationFailed);
        var changeProblem = await ProblemAssert.IsProblemAsync(change, HttpStatusCode.BadRequest, ShoppingErrorCodes.ValidationFailed);
        Assert.Equal(["note", "quantity", "sectionKey", "text"], FieldNames(addProblem));
        Assert.Equal(["baseVersion", "quantity", "sectionKey", "text"], FieldNames(changeProblem));
    }

    [Fact]
    public async Task CheckAndUncheck_SetAndClearCheckedAt_AndFilterTheList()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var item = await api.AddTextAsync(list.Id, "Bread");
        await api.AddTextAsync(list.Id, "Butter");

        var check = await api.ChangeOkAsync(api.Owner, item, c => c with { Checked = true });
        var open = await api.ItemsAsync(list.Id, includeChecked: false);
        var counts = await api.DefaultListAsync();
        var uncheck = await api.ChangeOkAsync(api.Owner, check.Item, c => c with { Checked = false });

        Assert.Equal(ShoppingConflictOutcomeDto.NoConflict, check.Outcome);
        Assert.True(check.Item.Checked);
        Assert.NotNull(check.Item.CheckedAt);
        Assert.Equal(2, check.Item.Version);
        Assert.Equal("Butter", Assert.Single(open).Text);
        Assert.Equal((1, 1), (counts.OpenItemCount, counts.CheckedItemCount));
        Assert.False(uncheck.Item.Checked);
        Assert.Null(uncheck.Item.CheckedAt);
        Assert.Equal(3, uncheck.Item.Version);
    }

    [Fact]
    public async Task Restricted_MayAddAndCheckOnly()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var (kid, _) = await api.AddMemberAsync(HouseholdRole.Restricted, "kid");
        using var _ = kid;
        var other = await api.CreateListAsync("Market");

        var item = await api.AddTextAsync(list.Id, "Apples", kid);
        var check = await api.ChangeOkAsync(kid, item, c => c with { Checked = true });
        using var changeNote = await api.ChangeAsync(kid, ShoppingApi.ToChange(check.Item) with { Note = "green" }, item.Id);
        using var changeText = await api.ChangeAsync(kid, ShoppingApi.ToChange(check.Item) with { Text = "Pears" }, item.Id);
        using var delete = await kid.DeleteAsync($"{api.Base}/items/{item.Id}?baseVersion={check.Item.Version}");
        using var move = await kid.PostAsync($"{api.Base}/items/{item.Id}/move", new MoveShoppingItemRequest(other.Id));

        Assert.True(check.Item.Checked);
        await TestClient.AssertProblemAsync(changeNote, HttpStatusCode.Forbidden, ShoppingErrorCodes.RestrictedChange);
        await TestClient.AssertProblemAsync(changeText, HttpStatusCode.Forbidden, ShoppingErrorCodes.RestrictedChange);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
        await TestClient.AssertProblemAsync(move, HttpStatusCode.Forbidden, HouseholdErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Move_ChangesListAndVersion_UnknownTargetIs404()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var other = await api.CreateListAsync("Market");
        var item = await api.AddTextAsync(list.Id, "Bread");
        var moveUrl = $"{api.Base}/items/{item.Id}/move";

        using var move = await api.Owner.PostAsync(moveUrl, new MoveShoppingItemRequest(other.Id));
        using var unknown = await api.Owner.PostAsync(moveUrl, new MoveShoppingItemRequest(Guid.CreateVersion7()));

        var moved = await TestClient.ReadAsync<ShoppingItemDto>(move);
        Assert.Equal((other.Id, 2L), (moved.ListId, moved.Version));
        Assert.Empty(await api.ItemsAsync(list.Id));
        await TestClient.AssertProblemAsync(unknown, HttpStatusCode.NotFound, ShoppingErrorCodes.ListNotFound);
    }

    [Fact]
    public async Task ForeignHousehold_EntriesAndListsAreNotFound()
    {
        await using var api = await ShoppingApi.StartAsync(postgres);
        var list = await api.DefaultListAsync();
        var item = await api.AddTextAsync(list.Id, "Bread");
        var foreign = await Scenario.CreateHouseholdAsync(api.Owner, "Neighbours");
        var (outsider, _) = await Scenario.AddMemberAsync(api.App, api.Owner, foreign, HouseholdRole.Owner, "outsider");
        using var _ = outsider;
        var ownRoot = ShoppingApi.Root(foreign);
        var ownList = Assert.Single(await outsider.GetJsonAsync<List<ShoppingListDto>>($"{ownRoot}/lists"));
        var ownItem = await PostAsync(outsider, $"{ownRoot}/lists/{ownList.Id}/items", "Milk");

        using var direct = await outsider.PutAsync($"{api.Base}/items/{item.Id}", ShoppingApi.ToChange(item));
        using var change = await outsider.PutAsync($"{ownRoot}/items/{item.Id}", ShoppingApi.ToChange(item) with { Checked = true });
        using var delete = await outsider.DeleteAsync($"{ownRoot}/items/{item.Id}?baseVersion=1");
        using var move = await outsider.PostAsync($"{ownRoot}/items/{item.Id}/move", new MoveShoppingItemRequest(ownList.Id));
        using var add = await outsider.PostAsync($"{ownRoot}/lists/{list.Id}/items", new CreateShoppingItemRequest(null, null, "x", null, null, null));
        using var moveIn = await outsider.PostAsync($"{ownRoot}/items/{ownItem.Id}/move", new MoveShoppingItemRequest(list.Id));

        await TestClient.AssertProblemAsync(direct, HttpStatusCode.NotFound, HouseholdErrorCodes.NotFound);
        await TestClient.AssertProblemAsync(change, HttpStatusCode.NotFound, ShoppingErrorCodes.ItemNotFound);
        await TestClient.AssertProblemAsync(delete, HttpStatusCode.NotFound, ShoppingErrorCodes.ItemNotFound);
        await TestClient.AssertProblemAsync(move, HttpStatusCode.NotFound, ShoppingErrorCodes.ItemNotFound);
        await TestClient.AssertProblemAsync(add, HttpStatusCode.NotFound, ShoppingErrorCodes.ListNotFound);
        await TestClient.AssertProblemAsync(moveIn, HttpStatusCode.NotFound, ShoppingErrorCodes.ListNotFound);

        // An id of another household's entry cannot be taken over by sending it as a new entry.
        using var reuse = await outsider.PostAsync(
            $"{ownRoot}/lists/{ownList.Id}/items", new CreateShoppingItemRequest(item.Id, null, "x", null, null, null));
        await TestClient.AssertProblemAsync(reuse, HttpStatusCode.Conflict, ErrorCodes.Conflict);
        Assert.False((await api.ItemsAsync(list.Id)).Single().Checked);
    }

    private static async Task<ShoppingItemDto> PostAsync(TestClient client, string url, string text)
    {
        using var response = await client.PostAsync(url, new CreateShoppingItemRequest(null, null, text, null, null, null));
        return await TestClient.ReadAsync<ShoppingItemDto>(response, HttpStatusCode.Created);
    }

    private static string[] FieldNames(System.Text.Json.JsonElement problem) =>
        [.. problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];
}
