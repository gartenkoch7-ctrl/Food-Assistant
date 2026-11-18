using FoodAssistant.Client.Http;

namespace FoodAssistant.UI.Shopping;

internal sealed record MergeResult(MergeStatus Status, ApiError? Error = null);
