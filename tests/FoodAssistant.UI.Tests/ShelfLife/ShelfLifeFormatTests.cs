using FoodAssistant.Contracts.ShelfLife;
using FoodAssistant.UI.ShelfLife;
using FoodAssistant.UI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Tests.ShelfLife;

public sealed class ShelfLifeFormatTests : UiTestContext
{
    private static readonly IReadOnlyList<ShelfLifeSourceDto> Sources =
    [
        new("bfr", "BfR", "https://www.bfr.bund.de/", new DateOnly(2026, 11, 18)),
        new("bzfe", "BZfE", "https://www.bzfe.de/", new DateOnly(2026, 11, 18)),
    ];

    private IStringLocalizer<ShelfLifeText> Text => Services.GetRequiredService<IStringLocalizer<ShelfLifeText>>();

    [Fact]
    public void SourceTitles_AreDistinctInValueOrder_AndSkipUnknownKeys()
    {
        var titles = ShelfLifeFormat.SourceTitles(new ShelfLifeSourceKeysDto("bzfe", null, "bfr", "bzfe"), Sources);
        var withUnknown = ShelfLifeFormat.SourceTitles(new ShelfLifeSourceKeysDto("vz", null, null, "bfr"), Sources);

        Assert.Equal(["BZfE", "BfR"], titles);
        Assert.Equal(["BfR"], withUnknown);
        Assert.Empty(ShelfLifeFormat.SourceTitles(new ShelfLifeSourceKeysDto(null, null, null, null), Sources));
        Assert.Empty(ShelfLifeFormat.SourceTitles(new ShelfLifeSourceKeysDto("bzfe", null, null, null), []));
    }

    [Fact]
    public void SourceLine_UsesSingularOrPluralAndJoinsTitles()
    {
        Assert.Null(ShelfLifeFormat.SourceLine(Text, []));
        Assert.Equal("Quelle: BZfE", ShelfLifeFormat.SourceLine(Text, ["BZfE"]));
        Assert.Equal("Quellen: BZfE; BfR", ShelfLifeFormat.SourceLine(Text, ["BZfE", "BfR"]));
    }

    [Fact]
    public void SourceLine_FollowsTheEnglishCulture()
    {
        UseCulture("en");

        Assert.Equal("Sources: BZfE; BfR", ShelfLifeFormat.SourceLine(Text, ["BZfE", "BfR"]));
    }
}
