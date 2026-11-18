namespace FoodAssistant.Client.Http;

/// <summary>Error codes the client itself creates (no server answer exists); the UI translates them like server codes.</summary>
public static class ClientErrorCodes
{
    /// <summary>The server could not be reached.</summary>
    public const string Network = "client.network";

    /// <summary>The device is offline and the action needs the server.</summary>
    public const string Offline = "client.offline";
}
