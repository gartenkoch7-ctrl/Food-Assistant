namespace FoodAssistant.Client.Http;

/// <summary>
/// Reports that the server no longer accepts the session: a data call was answered with 401, for
/// example because the cookie expired. The session layer listens and reloads, which leads to the
/// login page. Scoped like the session, so one app instance hears only its own calls.
/// </summary>
public sealed class SessionExpiry
{
    public event Action? Expired;

    public void Notify() => Expired?.Invoke();
}
