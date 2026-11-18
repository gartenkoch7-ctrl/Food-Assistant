using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Checkout;

/// <summary>A request line that passed the input rules; amount and kind are parsed for booked lines.</summary>
/// <param name="Index">Position in the request (for field errors).</param>
/// <param name="Request">The line as sent.</param>
/// <param name="Quantity">Parsed amount; null for a skipped line.</param>
/// <param name="Kind">Parsed kind of the printed date.</param>
/// <param name="NewProduct">Product to create, with its new id; null otherwise.</param>
internal sealed record CheckoutParsedLine(
    int Index,
    CheckoutLineRequest Request,
    Quantity? Quantity,
    ExpiryKind Kind,
    CheckoutNewProduct? NewProduct);
