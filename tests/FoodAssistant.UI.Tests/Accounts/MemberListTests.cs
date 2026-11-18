using Bunit;
using FoodAssistant.Client.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.UI.Accounts;
using FoodAssistant.UI.Common;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.UI.Tests.Accounts;

public sealed class MemberListTests : UiTestContext
{
    private static readonly Guid Anna = Guid.Parse("018f0000-0000-7000-8000-0000000000aa");
    private static readonly Guid Ben = Guid.Parse("018f0000-0000-7000-8000-0000000000bb");

    public MemberListTests()
        : base(role: HouseholdRole.Owner)
    {
        Households = new FakeHouseholdClient();
        Services.AddSingleton<IHouseholdClient>(Households);
    }

    private FakeHouseholdClient Households { get; }

    private string Announced => Services.GetRequiredService<ILiveAnnouncer>().Message.Trim();

    private IRenderedComponent<MemberList> RenderList(bool isOwner = true) =>
        Render<MemberList>(p => p
            .Add(c => c.HouseholdId, FakeAccountClient.TestHouseholdId)
            .Add(c => c.IsOwner, isOwner));

    [Fact]
    public void ListsNamesAndRoles_AndMarksTheSignedInPerson()
    {
        var cut = RenderList();

        var rows = cut.FindAll("li.fa-member");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Anna (du)", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Besitzer", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Ben", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("Mitglied", rows[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void NonOwner_SeesNoActions()
    {
        var cut = RenderList(isOwner: false);

        Assert.Empty(cut.FindAll("select"));
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Owner_ChangesARole_OnlyAfterConfirmingWithTheButton()
    {
        var cut = RenderList();
        Assert.Empty(cut.FindAll("button[aria-label='Neue Rolle für Ben übernehmen']"));

        cut.Find("select[aria-label='Rolle von Ben']").Change("restricted");
        Assert.DoesNotContain(Households.Calls, c => c.StartsWith("members.role", StringComparison.Ordinal));
        cut.Find("button[aria-label='Neue Rolle für Ben übernehmen']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Rolle von Ben geändert.", Announced));
        Assert.Contains($"members.role:{Ben}:Restricted", Households.Calls);
        Assert.Contains("Eingeschränkt", cut.FindAll("li.fa-member")[1].TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("button[aria-label='Neue Rolle für Ben übernehmen']"));
    }

    [Fact]
    public void Remove_AsksFirst_AndCanBeCancelled()
    {
        var cut = RenderList();

        cut.Find("button[aria-label='Ben entfernen']").Click();
        Assert.Contains("Ben wirklich aus dem Haushalt entfernen?", cut.Markup, StringComparison.Ordinal);
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Abbrechen").Click();

        Assert.DoesNotContain("wirklich aus dem Haushalt", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(Households.Calls, c => c.StartsWith("members.remove", StringComparison.Ordinal));
    }

    [Fact]
    public void Remove_AfterConfirming_RemovesTheMember()
    {
        var cut = RenderList();

        cut.Find("button[aria-label='Ben entfernen']").Click();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Ja, entfernen").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("li.fa-member")));
        Assert.Contains($"members.remove:{Ben}", Households.Calls);
        Assert.Equal("Ben wurde entfernt.", Announced);
    }

    [Fact]
    public void LastOwner_IsExplained_AndTheListStays()
    {
        Households.Fail["members.role"] = FakeAccountClient.Error(409, HouseholdErrorCodes.LastOwner);
        var cut = RenderList();

        cut.Find("select[aria-label='Rolle von Anna']").Change("member");
        cut.Find("button[aria-label='Neue Rolle für Anna übernehmen']").Click();

        cut.WaitForAssertion(() => Assert.Contains("mindestens einen Besitzer", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Contains("Besitzer", cut.FindAll("li.fa-member")[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MemberGoneMeanwhile_ShowsTheErrorAndTheCurrentList()
    {
        Households.Fail["members.remove"] = FakeAccountClient.Error(404, HouseholdErrorCodes.MemberNotFound);
        var cut = RenderList();
        Households.Members.RemoveAll(m => m.UserId == Ben);

        cut.Find("button[aria-label='Ben entfernen']").Click();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Ja, entfernen").Click();

        cut.WaitForAssertion(() => Assert.Contains("gibt es nicht mehr", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Single(cut.FindAll("li.fa-member"));
    }

    [Fact]
    public void ChangingTheOwnRole_ReloadsTheSession()
    {
        Households.Members.Add(new HouseholdMemberResponse(Guid.CreateVersion7(), "Cleo", HouseholdRole.Owner, DateTimeOffset.UnixEpoch));
        var before = Accounts.Calls.Count(c => c == "me");
        var cut = RenderList();

        cut.Find("select[aria-label='Rolle von Anna']").Change("member");
        cut.Find("button[aria-label='Neue Rolle für Anna übernehmen']").Click();

        cut.WaitForAssertion(() => Assert.Equal(before + 1, Accounts.Calls.Count(c => c == "me")));
        Assert.Contains($"members.role:{Anna}:Member", Households.Calls);
    }

    [Fact]
    public void English_UsesEnglishTexts()
    {
        UseCulture("en");

        var cut = RenderList();

        Assert.Equal("Members", cut.Find("h2").TextContent);
        Assert.NotNull(cut.Find("select[aria-label='Role of Ben']"));
    }
}
