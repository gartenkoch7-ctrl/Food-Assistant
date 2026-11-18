using FoodAssistant.Application.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Tests.Catalog;

public sealed class CatalogValidationTests
{
    private static IReadOnlyDictionary<string, string[]> Validate(
        string? name = "Milk",
        string? brand = null,
        string? category = ProductCategories.Dairy,
        string? unit = UnitCodes.Litre,
        string? section = null,
        QuantityDto? minimum = null,
        IReadOnlyList<string>? barcodes = null,
        string? foodFacts = null) =>
        CatalogValidation.ValidateProduct(name, brand, category, unit, section, minimum, barcodes, foodFacts);

    [Fact]
    public void ValidProduct_HasNoErrors() =>
        Assert.Empty(Validate(
            brand: "Farm",
            section: StoreSections.DairyChilled,
            minimum: new QuantityDto(2m, UnitCodes.Litre),
            barcodes: ["4006381333931", "12345678"]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_Missing_IsRequired(string? name) =>
        Assert.Equal(["required"], Validate(name: name)["name"]);

    [Fact]
    public void Name_IsTrimmedBeforeTheLengthCheck()
    {
        Assert.Empty(Validate(name: "  " + new string('a', 120) + "  "));
        Assert.Equal(["too_long"], Validate(name: new string('a', 121))["name"]);
    }

    [Fact]
    public void Brand_TooLong_IsRejected() =>
        Assert.Equal(["too_long"], Validate(brand: new string('b', 121))["brand"]);

    [Fact]
    public void Category_MustBeOfTheFixedList()
    {
        Assert.Equal(["unknown"], Validate(category: "snacks")["categoryKey"]);
        Assert.Equal(["required"], Validate(category: null)["categoryKey"]);
    }

    [Fact]
    public void Unit_MustBeKnown()
    {
        Assert.Equal(["unknown"], Validate(unit: "gallon")["defaultUnit"]);
        Assert.Equal(["required"], Validate(unit: "")["defaultUnit"]);
    }

    [Fact]
    public void Section_NullOrKnown()
    {
        Assert.Empty(Validate(section: null));
        Assert.Equal(["unknown"], Validate(section: "aisle_9")["sectionKey"]);
    }

    [Theory]
    [InlineData(0, UnitCodes.Gram, "must_be_positive")]
    [InlineData(-1, UnitCodes.Gram, "must_be_positive")]
    [InlineData(1, "gallon", "unknown_unit")]
    public void MinimumStock_NeedsPositiveValueAndKnownUnit(int value, string unit, string code) =>
        Assert.Equal([code], Validate(minimum: new QuantityDto(value, unit))["minimumStock"]);

    [Theory]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("123456789", false)]
    [InlineData("123456789012", true)]
    [InlineData("1234567890123", true)]
    [InlineData("12345678901234", true)]
    [InlineData("123456789012345", false)]
    [InlineData("1234567a", false)]
    [InlineData("1234 5678", false)]
    [InlineData("١٢٣٤٥٦٧٨", false)] // Arabic-Indic digits are not barcode digits.
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsBarcode_DigitsOfLength8_12_13_14(string? code, bool expected) =>
        Assert.Equal(expected, CatalogValidation.IsBarcode(code));

    [Fact]
    public void Barcodes_InvalidCode_IsRejected() =>
        Assert.Equal(["invalid"], Validate(barcodes: ["4006381333931", "abc"])["barcodes"]);

    [Fact]
    public void Barcodes_MoreThanTenDistinct_IsRejected_ButDuplicatesDoNotCount()
    {
        var eleven = Enumerable.Range(0, 11).Select(i => (10_000_000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(["too_many"], Validate(barcodes: eleven)["barcodes"]);
        Assert.Empty(Validate(barcodes: [.. eleven.Take(10), .. eleven.Take(10)]));
    }

    [Fact]
    public void FoodFactsBarcode_MustBeBarcode()
    {
        Assert.Empty(Validate(foodFacts: "4006381333931"));
        Assert.Equal(["invalid"], Validate(foodFacts: "x")["foodFactsBarcode"]);
    }

    [Fact]
    public void Location_NameRules()
    {
        Assert.Empty(CatalogValidation.ValidateLocation("Fridge"));
        Assert.Equal(["required"], CatalogValidation.ValidateLocation(" ")["name"]);
        Assert.Equal(["too_long"], CatalogValidation.ValidateLocation(new string('x', 81))["name"]);
        Assert.Empty(CatalogValidation.ValidateLocation(new string('x', 80)));
    }
}
