namespace FoodAssistant.Server.Checkout;

/// <summary>Extension point for track F (docs/plan/m01-mvp.md). Maps nothing yet.</summary>
internal static class CheckoutEndpoints
{
    public static RouteGroupBuilder MapCheckoutEndpoints(this RouteGroupBuilder household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household;
    }
}
