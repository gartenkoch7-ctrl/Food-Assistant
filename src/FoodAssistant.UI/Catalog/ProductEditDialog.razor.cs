using System.Globalization;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace FoodAssistant.UI.Catalog;

/// <summary>
/// Creates or edits a product. Closes with <c>DialogResult.Ok(ProductDto)</c> after saving or
/// archiving (the archived product is returned as it was); callers reload their list on that result.
/// </summary>
public partial class ProductEditDialog
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    private IReadOnlyList<StorageLocationDto> _locations = [];
    private IReadOnlyDictionary<string, string[]> _fieldErrors = NoFieldErrors;
    private readonly List<string> _barcodes = [];
    private ApiError? _error;
    private string _name = string.Empty;
    private string? _brand;
    private string _category = string.Empty;
    private string _unit = UnitCodes.Piece;
    private Guid _locationId = Guid.Empty;
    private string _section = string.Empty;
    private QuantityDto? _minimum;
    private string _newBarcode = string.Empty;
    private bool _barcodeInvalid;
    private bool _reloaded;
    private bool _saving;
    private long _version;

    // Fixed per dialog so a retry after a lost response answers 409 instead of creating a duplicate.
    private readonly Guid _newId = Guid.CreateVersion7();

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>The product to edit; <see langword="null"/> creates a new one.</summary>
    [Parameter]
    public ProductDto? Product { get; set; }

    /// <summary>Start values of a new product (scan, shopping list entry); ignored when <see cref="Product"/> is set.</summary>
    [Parameter]
    public CreateProductRequest? Prefill { get; set; }

    /// <summary>Shown at the top of the dialog, e.g. the Open Food Facts attribution of the scan.</summary>
    [Parameter]
    public RenderFragment? Notice { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (Product is not null)
        {
            Fill(Product);
        }
        else if (Prefill is not null)
        {
            Fill(Prefill);
        }

        if (Session.ActiveHouseholdId is { } household)
        {
            var locations = await Catalog.ListStorageLocationsAsync(household, CancellationToken.None);
            _locations = locations.Value ?? [];
        }
    }

    private static string ETagOf(long version) => $"\"{version}\"";

    private bool HasError(string field) => _fieldErrors.ContainsKey(field);

    private string? FieldText(string field)
    {
        if (!_fieldErrors.TryGetValue(field, out var codes) || codes.Length == 0)
        {
            return null;
        }

        var text = T["FieldError." + codes[0]];
        return text.ResourceNotFound ? T["FieldError.invalid"].Value : text.Value;
    }

    private void Fill(ProductDto product)
    {
        _version = product.Version;
        _name = product.Name;
        _brand = product.Brand;
        _category = product.CategoryKey;
        _unit = product.DefaultUnit;
        _locationId = product.DefaultStorageLocationId ?? Guid.Empty;
        _section = product.SectionKey ?? string.Empty;
        _minimum = product.MinimumStock;
        _barcodes.Clear();
        _barcodes.AddRange(product.Barcodes);
    }

    private void Fill(CreateProductRequest request)
    {
        _name = request.Name;
        _brand = request.Brand;
        _category = request.CategoryKey;
        _unit = request.DefaultUnit;
        _locationId = request.DefaultStorageLocationId ?? Guid.Empty;
        _section = request.SectionKey ?? string.Empty;
        _minimum = request.MinimumStock;
        _barcodes.AddRange(request.Barcodes ?? []);
    }

    private void RemoveBarcode(string code) => _barcodes.Remove(code);

    private void OnBarcodeKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            AddBarcode();
        }
    }

    // The same rule as the server (digits, 8/12/13/14 long); the server still has the last word.
    private void AddBarcode()
    {
        var code = _newBarcode.Trim();
        _barcodeInvalid = !(code.All(char.IsAsciiDigit) && code.Length is 8 or 12 or 13 or 14);
        if (_barcodeInvalid)
        {
            return;
        }

        if (!_barcodes.Contains(code))
        {
            _barcodes.Add(code);
        }

        _newBarcode = string.Empty;
    }

    private async Task SaveAsync()
    {
        if (Session.ActiveHouseholdId is not { } household)
        {
            return;
        }

        _saving = true;
        _error = null;
        _fieldErrors = NoFieldErrors;
        _reloaded = false;
        var location = _locationId == Guid.Empty ? (Guid?)null : _locationId;
        var section = string.IsNullOrEmpty(_section) ? null : _section;
        var brand = string.IsNullOrWhiteSpace(_brand) ? null : _brand.Trim();
        var result = Product is null
            ? await Catalog.CreateProductAsync(
                household,
                new CreateProductRequest(Prefill?.Id ?? _newId, _name, brand, _category, _unit, location, section, _minimum, [.. _barcodes], Prefill?.FoodFactsBarcode),
                CancellationToken.None)
            : await Catalog.UpdateProductAsync(
                household,
                Product.Id,
                new UpdateProductRequest(_name, brand, _category, _unit, location, section, _minimum, [.. _barcodes]),
                ETagOf(_version),
                CancellationToken.None);
        _saving = false;

        if (result.IsSuccess)
        {
            MudDialog.Close(DialogResult.Ok(result.Value!));
            return;
        }

        await HandleFailureAsync(household, result.Error!);
    }

    private async Task ArchiveAsync()
    {
        if (Session.ActiveHouseholdId is not { } household || Product is null)
        {
            return;
        }

        var confirmed = await Dialogs.ShowMessageBoxAsync(
            T["ArchiveTitle"],
            string.Format(CultureInfo.CurrentCulture, T["ArchiveText"].Value, Product.Name),
            yesText: T["ArchiveConfirm"],
            cancelText: Common["Cancel"]);
        if (confirmed != true)
        {
            return;
        }

        _saving = true;
        _error = null;
        var result = await Catalog.ArchiveProductAsync(household, Product.Id, ETagOf(_version), CancellationToken.None);
        _saving = false;
        if (result.IsSuccess)
        {
            MudDialog.Close(DialogResult.Ok(Product));
            return;
        }

        await HandleFailureAsync(household, result.Error!);
    }

    private async Task HandleFailureAsync(Guid household, ApiError error)
    {
        if (error.Status == 412 && Product is not null)
        {
            // Someone else changed the product: show the stored state, keep the user in the dialog.
            var current = await Catalog.GetProductAsync(household, Product.Id, CancellationToken.None);
            if (current.IsSuccess)
            {
                Fill(current.Value!);
                _reloaded = true;
                return;
            }

            error = current.Error!;
        }

        _fieldErrors = error.FieldErrors;
        _error = error;
    }
}
