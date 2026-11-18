using Microsoft.AspNetCore.Authorization;

namespace FoodAssistant.Server.Auth;

/// <summary>The caller's account must be the server admin (checked against the database).</summary>
public sealed class ServerAdminRequirement : IAuthorizationRequirement;
