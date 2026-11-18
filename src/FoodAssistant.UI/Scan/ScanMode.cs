namespace FoodAssistant.UI.Scan;

/// <summary>What a scan does: put a package into stock, or use one up.</summary>
public enum ScanMode
{
    /// <summary>Add stock ("Einbuchen").</summary>
    Add,

    /// <summary>Take stock out, first expired first out ("Verbraucht").</summary>
    Use,
}
