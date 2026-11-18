using FoodAssistant.Client.Checkout;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Checkout;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// Checkout client with a settable proposal. Completions are recorded with their key and answered
/// from <see cref="Answers"/> in order; without a queued answer every booked line becomes a batch.
/// </summary>
public sealed class FakeCheckoutClient : ICheckoutClient
{
    public List<CheckoutEntryDto> Entries { get; } = [];

    public ApiError? ProposalError { get; set; }

    public int ProposalCalls { get; private set; }

    public List<(CompleteCheckoutRequest Request, Guid Key)> Completions { get; } = [];

    public Queue<ApiResult<CheckoutResultDto>> Answers { get; } = new();

    public Task<ApiResult<CheckoutProposalDto>> GetProposalAsync(Guid householdId, Guid listId, CancellationToken cancellationToken)
    {
        ProposalCalls++;
        return Task.FromResult(ProposalError is { } error
            ? ApiResult<CheckoutProposalDto>.Failure(error)
            : ApiResult<CheckoutProposalDto>.Success(new CheckoutProposalDto(listId, [.. Entries])));
    }

    public Task<ApiResult<CheckoutResultDto>> CompleteAsync(
        Guid householdId, Guid listId, CompleteCheckoutRequest request, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Completions.Add((request, idempotencyKey));
        if (Answers.TryDequeue(out var answer))
        {
            return Task.FromResult(answer);
        }

        var booked = request.Lines.Count(l => !l.Skip);
        return Task.FromResult(ApiResult<CheckoutResultDto>.Success(
            new CheckoutResultDto(booked, request.Lines.Count, [.. Enumerable.Range(0, booked).Select(_ => Guid.CreateVersion7())], [])));
    }
}
