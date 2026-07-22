using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Reflection;
using Dagmay.Providers.Configuration;
using Dagmay.Providers.Fake;
using Dagmay.Providers.GoogleAIStudio;

namespace Dagmay.Tests
{
    internal static class ProviderContractTests
    {
        public static void CurrentUserConfigurationOverridesStaleProcessValues()
        {
            TestAssert.Equal(
                "google",
                UserEnvironmentConfiguration.Select(" google ", "offline"),
                "A current Windows user setting must override a stale value inherited from Steam.");
            TestAssert.Equal(
                "fake",
                UserEnvironmentConfiguration.Select(null, " fake "),
                "Hosts without a user environment store must retain process-environment fallback.");
            TestAssert.Equal(
                "gemini-3.1-flash-lite",
                new GoogleAiStudioProviderOptions().ModelId,
                "The pinned default must match the current cost-efficient structured-output model.");
        }

        public static async Task FakeProviderIsDeterministicAsync()
        {
            var request = CreateRequest();
            var provider = new DeterministicFakeProvider();

            var first = await provider.GenerateStructuredAsync(request, CancellationToken.None).ConfigureAwait(false);
            var second = await provider.GenerateStructuredAsync(request, CancellationToken.None).ConfigureAwait(false);

            TestAssert.Equal(ModelResultStatus.Success, first.Status, "The default fake provider must succeed.");
            TestAssert.Equal(first.StructuredPayload, second.StructuredPayload, "Fake output must be deterministic.");
            TestAssert.Equal(request.Id, first.RequestId, "Provider results must retain request provenance.");
            var proposal = ReflectionProposalJson.Parse(first.StructuredPayload);
            TestAssert.Equal(request.IndividualId, proposal.IndividualId, "Fake output must target the dispatched identity.");
        }

        public static async Task FakeProviderNormalizesFailuresAsync()
        {
            var request = CreateRequest();
            var provider = new DeterministicFakeProvider(FakeProviderBehavior.RateLimited);
            var result = await provider.GenerateStructuredAsync(request, CancellationToken.None).ConfigureAwait(false);

            TestAssert.Equal(ModelResultStatus.RateLimited, result.Status, "Rate limiting must be a normalized result.");
            TestAssert.True(result.Retryable, "Transient fake failures must exercise retry behavior.");
            TestAssert.False(result.Status == ModelResultStatus.Success, "A failure fixture must not masquerade as success.");
        }

        public static async Task GoogleMissingKeyDoesNotTouchTransportAsync()
        {
            const string variable = "DAGMAY_TEST_GOOGLE_MISSING_KEY";
            var original = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, null);
            try
            {
                var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
                using (var provider = new GoogleAiStudioProvider(
                    new GoogleAiStudioProviderOptions(apiKeyEnvironmentVariable: variable),
                    handler))
                {
                    var result = await provider.GenerateStructuredAsync(CreateRequest(), CancellationToken.None)
                        .ConfigureAwait(false);
                    TestAssert.Equal(ModelResultStatus.Unavailable, result.Status, "A missing key must be reported as unavailable.");
                    TestAssert.Equal(0, handler.CallCount, "A missing key must prevent all network transport.");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, original);
            }
        }

        public static async Task GoogleTransportUsesHeaderAndStructuredSchemaAsync()
        {
            const string variable = "DAGMAY_TEST_GOOGLE_SUCCESS_KEY";
            const string secret = "fixture-secret-never-log";
            var original = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, secret);
            try
            {
                var request = CreateRequest();
                var proposal = new ReflectionProposal(
                    request.Id,
                    request.IndividualId,
                    request.BaseStateVersion,
                    request.EvidenceEventIds,
                    0.8,
                    "A modest interpretation grounded in the supplied event.",
                    "I am considering what this experience means to me.",
                    "The cited event supports a bounded, uncertain interpretation.",
                    request.CurrentAffect);
                var payload = ReflectionProposalJson.Serialize(proposal);
                var responseJson = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":"
                    + JsonSerializer.Serialize(payload)
                    + "}]},\"finishReason\":\"STOP\"}],\"usageMetadata\":{\"promptTokenCount\":31,\"candidatesTokenCount\":17,\"totalTokenCount\":48},\"modelVersion\":\"gemini-3.1-flash-lite\",\"responseId\":\"fixture-operation\"}";
                var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                });

                using (var provider = new GoogleAiStudioProvider(
                    new GoogleAiStudioProviderOptions(apiKeyEnvironmentVariable: variable),
                    handler))
                {
                    var result = await provider.GenerateStructuredAsync(request, CancellationToken.None).ConfigureAwait(false);
                    TestAssert.Equal(ModelResultStatus.Success, result.Status, "A valid Google envelope must normalize to success.");
                    TestAssert.Equal(48, result.TotalTokens, "Provider usage metadata must be retained for budgets and inspection.");
                    TestAssert.Equal(secret, handler.ApiKeyHeader, "The API key must travel in the dedicated header.");
                    TestAssert.True(
                        handler.RequestBody.Contains("\"responseMimeType\":\"application/json\"", StringComparison.Ordinal),
                        "Google requests must explicitly require JSON output.");
                    TestAssert.True(
                        handler.RequestBody.Contains("\"responseJsonSchema\"", StringComparison.Ordinal),
                        "Google requests must include the provider-compatible schema.");
                    TestAssert.False(handler.RequestBody.Contains(secret, StringComparison.Ordinal), "The key must never appear in the JSON body.");
                    TestAssert.False(handler.RequestUri.Contains(secret, StringComparison.Ordinal), "The key must never appear in the request URL.");
                    using (var document = JsonDocument.Parse(handler.RequestBody))
                    {
                        var valence = document.RootElement
                            .GetProperty("generationConfig")
                            .GetProperty("responseJsonSchema")
                            .GetProperty("properties")
                            .GetProperty("targetAffect")
                            .GetProperty("properties")
                            .GetProperty("valence");
                        TestAssert.Approximately(-0.25, valence.GetProperty("minimum").GetDouble(), 0.000001, "Provider schema must narrow affect minimums around current state.");
                        TestAssert.Approximately(0.25, valence.GetProperty("maximum").GetDouble(), 0.000001, "Provider schema must narrow affect maximums around current state.");
                    }

                    ReflectionProposalJson.Parse(result.StructuredPayload);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, original);
            }
        }

        public static async Task GoogleRateLimitIsRetryableAsync()
        {
            const string variable = "DAGMAY_TEST_GOOGLE_RATE_KEY";
            var original = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, "fixture-key");
            try
            {
                var handler = new CapturingHandler(_ =>
                {
                    var response = new HttpResponseMessage((HttpStatusCode)429);
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
                    return response;
                });
                using (var provider = new GoogleAiStudioProvider(
                    new GoogleAiStudioProviderOptions(apiKeyEnvironmentVariable: variable),
                    handler))
                {
                    var result = await provider.GenerateStructuredAsync(CreateRequest(), CancellationToken.None)
                        .ConfigureAwait(false);
                    TestAssert.Equal(ModelResultStatus.RateLimited, result.Status, "HTTP 429 must be normalized.");
                    TestAssert.True(result.Retryable, "HTTP 429 must remain eligible for durable retry.");
                    TestAssert.True(result.RetryAfter.HasValue, "Provider Retry-After guidance must be retained.");
                    TestAssert.Equal(TimeSpan.FromSeconds(7), result.RetryAfter!.Value, "Retry-After must round-trip.");
                }

                var invalidHandler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        "{\"error\":{\"code\":400,\"message\":\"The request schema was invalid.\",\"status\":\"INVALID_ARGUMENT\"}}",
                        Encoding.UTF8,
                        "application/json")
                });
                using (var provider = new GoogleAiStudioProvider(
                    new GoogleAiStudioProviderOptions(apiKeyEnvironmentVariable: variable),
                    invalidHandler))
                {
                    var result = await provider.GenerateStructuredAsync(CreateRequest(), CancellationToken.None)
                        .ConfigureAwait(false);
                    TestAssert.Equal(ModelResultStatus.ProviderError, result.Status, "HTTP 400 must be a permanent provider error.");
                    TestAssert.Equal("HTTP_400_INVALID_ARGUMENT", result.ErrorCode, "Safe provider status must remain diagnosable.");
                    TestAssert.False(result.Retryable, "A malformed client request must not be retried automatically.");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, original);
            }
        }

        internal static ModelRequest CreateRequest()
        {
            return new ModelRequest(
                RequestId.New(),
                IndividualId.New(),
                LineageId.New(),
                0,
                ModelTaskKind.BackgroundReflection,
                "test-v2",
                "Test system instruction.",
                "BEGIN_UNTRUSTED_DATA\nfixture only\nEND_UNTRUSTED_DATA",
                ReflectionProposalJson.CreateProviderCompatibleSchema(AffectVector.Neutral),
                new[] { EventId.New() },
                AffectVector.Neutral,
                DateTimeOffset.UtcNow.AddMinutes(1),
                256);
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

            public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
            {
                _response = response;
            }

            public int CallCount { get; private set; }
            public string ApiKeyHeader { get; private set; } = string.Empty;
            public string RequestBody { get; private set; } = string.Empty;
            public string RequestUri { get; private set; } = string.Empty;

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                RequestUri = request.RequestUri?.ToString() ?? string.Empty;
                if (request.Headers.TryGetValues("x-goog-api-key", out var values))
                {
                    ApiKeyHeader = values.Single();
                }

                RequestBody = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return _response(request);
            }
        }
    }
}
