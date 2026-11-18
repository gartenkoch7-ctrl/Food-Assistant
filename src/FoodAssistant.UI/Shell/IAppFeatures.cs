namespace FoodAssistant.UI.Shell;

/// <summary>
/// Which optional areas of the app exist. Hidden areas do not appear in the navigation, so nobody
/// lands on an empty promise. The server or a settings page can supply a different implementation later.
/// </summary>
public interface IAppFeatures
{
    /// <summary>Recipes and the cooking assistant. Off until recipes or AI are available.</summary>
    bool CookingAvailable { get; }
}
