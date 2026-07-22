using System;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Reflection;

namespace Dagmay.Providers.Fake
{
    public enum FakeProviderBehavior
    {
        Success,
        InvalidResponse,
        TimedOut,
        RateLimited,
        ProviderError
    }

    public sealed class DeterministicFakeProvider : IModelProvider
    {
        private readonly FakeProviderBehavior _behavior;
        private readonly string? _payload;

        public DeterministicFakeProvider(
            FakeProviderBehavior behavior = FakeProviderBehavior.Success,
            string? payload = null)
        {
            _behavior = behavior;
            _payload = payload;
        }

        public string ProviderId => "dagmay.fake";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            supportsStructuredOutput: true,
            supportsEmbeddings: false,
            maximumInputCharacters: 100_000,
            maximumOutputTokens: 8_192);

        public Task<ModelResult> GenerateStructuredAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ModelResult.Failure(
                    request.Id,
                    ModelResultStatus.Canceled,
                    ProviderId,
                    "deterministic-v1",
                    "CANCELED",
                    "The deterministic request was canceled.",
                    TimeSpan.Zero));
            }

            switch (_behavior)
            {
                case FakeProviderBehavior.Success:
                    var proposal = new ReflectionProposal(
                        request.Id,
                        request.IndividualId,
                        request.BaseStateVersion,
                        request.EvidenceEventIds,
                        1.0,
                        "The supplied evidence is acknowledged without adding unsupported meaning.",
                        "I am taking stock of what happened while keeping my interpretation modest.",
                        "The deterministic test provider preserves the current affect and cites only dispatched evidence.",
                        request.CurrentAffect);
                    return Task.FromResult(ModelResult.Success(
                        request.Id,
                        ProviderId,
                        "deterministic-v1",
                        _payload ?? ReflectionProposalJson.Serialize(proposal),
                        "Deterministic fixture response.",
                        TimeSpan.Zero));

                case FakeProviderBehavior.InvalidResponse:
                    return Task.FromResult(Failure(request, ModelResultStatus.InvalidResponse, "INVALID_FIXTURE"));

                case FakeProviderBehavior.TimedOut:
                    return Task.FromResult(Failure(request, ModelResultStatus.TimedOut, "TIMEOUT_FIXTURE"));

                case FakeProviderBehavior.RateLimited:
                    return Task.FromResult(Failure(request, ModelResultStatus.RateLimited, "RATE_LIMIT_FIXTURE"));

                default:
                    return Task.FromResult(Failure(request, ModelResultStatus.ProviderError, "PROVIDER_FIXTURE"));
            }
        }

        private ModelResult Failure(ModelRequest request, ModelResultStatus status, string code)
        {
            var retryable = status == ModelResultStatus.RateLimited
                || status == ModelResultStatus.TimedOut
                || status == ModelResultStatus.ProviderError;
            return ModelResult.Failure(
                request.Id,
                status,
                ProviderId,
                "deterministic-v1",
                code,
                "Deterministic failure fixture.",
                TimeSpan.Zero,
                retryable);
        }
    }
}
