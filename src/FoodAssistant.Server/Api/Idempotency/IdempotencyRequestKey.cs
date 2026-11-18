namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>
/// Scope of an idempotency key: the same key from another person or for another path is a
/// different request, so nobody can replay someone else's response.
/// </summary>
/// <param name="Caller">Signed-in person (<c>person:&lt;id&gt;</c>), or <c>anonymous</c>.</param>
/// <param name="Path">Request path, upper-cased (routing ignores case).</param>
/// <param name="Key">Client-generated UUID from the <c>Idempotency-Key</c> header.</param>
public readonly record struct IdempotencyRequestKey(string Caller, string Path, Guid Key);
