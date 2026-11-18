namespace FoodAssistant.UI.Shell;

/// <summary>The M0 feature set: no recipes and no AI yet.</summary>
public sealed class DefaultAppFeatures : IAppFeatures
{
    public bool CookingAvailable => false;
}
