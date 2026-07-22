using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Reflection;
using Dagmay.Providers.Configuration;

namespace Dagmay.Providers.GoogleAIStudio
{
    public sealed class GoogleAiStudioProvider : IModelProvider, IDisposable
    {
        private readonly GoogleAiStudioProviderOptions _options;
        private readonly HttpClient _client;
        private bool _disposed;

        public GoogleAiStudioProvider(
            GoogleAiStudioProviderOptions options,
            HttpMessageHandler? handler = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _client = handler is null ? new HttpClient() : new HttpClient(handler, true);
        }

        public string ProviderId => "google.ai-studio";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            supportsStructuredOutput: true,
            supportsEmbeddings: false,
            maximumInputCharacters: 100_000,
            maximumOutputTokens: 8_192);

        public async Task<ModelResult> GenerateStructuredAsync(
            ModelRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            if (cancellationToken.IsCancellationRequested)
            {
                return Failure(request, ModelResultStatus.Canceled, "CANCELED", "The request was canceled before transport.", TimeSpan.Zero);
            }

            var resolvedApiKey = UserEnvironmentConfiguration.Read(_options.ApiKeyEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(resolvedApiKey))
            {
                return Failure(
                    request,
                    ModelResultStatus.Unavailable,
                    "API_KEY_MISSING",
                    $"Set the {_options.ApiKeyEnvironmentVariable} environment variable to enable Google reflection.",
                    TimeSpan.Zero);
            }

            var apiKey = resolvedApiKey!;

            var remaining = request.DeadlineUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return Failure(request, ModelResultStatus.TimedOut, "DEADLINE_EXPIRED", "The request deadline passed before transport.", TimeSpan.Zero, true);
            }

            var timeout = remaining < _options.RequestTimeout ? remaining : _options.RequestTimeout;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var message = BuildRequest(request, apiKey))
            {
                linked.CancelAfter(timeout);
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    using (var response = await _client.SendAsync(
                        message,
                        HttpCompletionOption.ResponseHeadersRead,
                        linked.Token).ConfigureAwait(false))
                    {
                        var retryAfter = ReadRetryAfter(response, DateTimeOffset.UtcNow);
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorBody = string.Empty;
                            try
                            {
                                errorBody = await ReadBoundedContentAsync(response.Content, linked.Token).ConfigureAwait(false);
                            }
                            catch (InvalidDataException)
                            {
                                errorBody = string.Empty;
                            }
                            catch (IOException)
                            {
                                errorBody = string.Empty;
                            }

                            return NormalizeHttpFailure(
                                request,
                                response.StatusCode,
                                stopwatch.Elapsed,
                                retryAfter,
                                ParseErrorDetail(errorBody));
                        }

                        if (response.Content.Headers.ContentLength.HasValue
                            && response.Content.Headers.ContentLength.Value > _options.MaximumResponseCharacters * 4L)
                        {
                            return Failure(
                                request,
                                ModelResultStatus.InvalidResponse,
                                "RESPONSE_TOO_LARGE",
                                "The provider response exceeded Dagmay's configured size bound.",
                                stopwatch.Elapsed);
                        }

                        var json = await ReadBoundedContentAsync(response.Content, linked.Token).ConfigureAwait(false);

                        return ParseSuccess(request, json, stopwatch.Elapsed);
                    }
                }
                catch (OperationCanceledException)
                {
                    return cancellationToken.IsCancellationRequested
                        ? Failure(request, ModelResultStatus.Canceled, "CANCELED", "The request was canceled.", stopwatch.Elapsed)
                        : Failure(request, ModelResultStatus.TimedOut, "TIMEOUT", "The provider request exceeded its deadline.", stopwatch.Elapsed, true);
                }
                catch (HttpRequestException)
                {
                    return Failure(
                        request,
                        ModelResultStatus.Unavailable,
                        "NETWORK_UNAVAILABLE",
                        "The Google endpoint could not be reached; the durable task remains eligible for retry.",
                        stopwatch.Elapsed,
                        true);
                }
                catch (InvalidDataException)
                {
                    return Failure(
                        request,
                        ModelResultStatus.InvalidResponse,
                        "INVALID_RESPONSE_BODY",
                        "The provider response body exceeded its bound or was not valid UTF-8.",
                        stopwatch.Elapsed);
                }
                catch (IOException)
                {
                    return Failure(
                        request,
                        ModelResultStatus.Unavailable,
                        "RESPONSE_READ_FAILED",
                        "The provider response stream ended unexpectedly; the durable task remains eligible for retry.",
                        stopwatch.Elapsed,
                        true);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client.Dispose();
        }

        private HttpRequestMessage BuildRequest(ModelRequest request, string apiKey)
        {
            var model = Uri.EscapeDataString(_options.ModelId);
            var endpoint = new Uri(_options.EndpointBase, "models/" + model + ":generateContent");
            var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
            message.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey.Trim());
            message.Content = new StringContent(BuildRequestJson(request), Encoding.UTF8, "application/json");
            return message;
        }

        private string BuildRequestJson(ModelRequest request)
        {
            if (!request.ResponseJsonSchema.StartsWith("{", StringComparison.Ordinal)
                || !request.ResponseJsonSchema.EndsWith("}", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The response schema must be a JSON object.");
            }

            var builder = new StringBuilder(request.Context.Length + request.ResponseJsonSchema.Length + 1024);
            builder.Append("{\"systemInstruction\":{\"parts\":[{\"text\":");
            AppendJsonString(builder, request.SystemInstruction);
            builder.Append("}]},\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":");
            AppendJsonString(builder, request.Context);
            builder.Append("}]}],\"generationConfig\":{\"responseMimeType\":\"application/json\",\"responseJsonSchema\":");
            builder.Append(request.ResponseJsonSchema);
            builder.Append(",\"candidateCount\":1,\"maxOutputTokens\":");
            builder.Append(request.MaximumOutputTokens.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"temperature\":");
            builder.Append(_options.Temperature.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("}}");
            return builder.ToString();
        }

        private ModelResult ParseSuccess(ModelRequest request, string json, TimeSpan latency)
        {
            GoogleGenerateContentResponse? response;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(GoogleGenerateContentResponse));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), false))
                {
                    response = serializer.ReadObject(stream) as GoogleGenerateContentResponse;
                }
            }
            catch (Exception exception) when (
                exception is SerializationException
                || exception is InvalidDataContractException
                || exception is FormatException)
            {
                return Failure(request, ModelResultStatus.InvalidResponse, "INVALID_RESPONSE_JSON", "The Google response envelope was not valid JSON.", latency);
            }

            var candidate = response?.Candidates?.FirstOrDefault();
            if (candidate is null)
            {
                var block = response?.PromptFeedback?.BlockReason;
                var code = string.IsNullOrWhiteSpace(block) ? "NO_CANDIDATE" : "PROMPT_BLOCKED";
                var message = string.IsNullOrWhiteSpace(block)
                    ? "Google returned no response candidate."
                    : "Google blocked the request under provider safety policy: " + Bounded(block, 128);
                return Failure(request, ModelResultStatus.InvalidResponse, code, message, latency);
            }

            var finishReason = candidate.FinishReason ?? string.Empty;
            if (!string.Equals(finishReason, "STOP", StringComparison.Ordinal))
            {
                return Failure(
                    request,
                    ModelResultStatus.InvalidResponse,
                    "INCOMPLETE_" + Bounded(finishReason, 64).ToUpperInvariant(),
                    "Google did not return a normally completed structured response.",
                    latency,
                    false,
                    null,
                    response?.ResponseId ?? string.Empty);
            }

            var payloadParts = candidate.Content?.Parts?
                .Where(part => !part.Thought && !string.IsNullOrWhiteSpace(part.Text))
                .Select(part => part.Text!)
                .ToList();
            var payload = payloadParts is null || payloadParts.Count == 0
                ? string.Empty
                : string.Concat(payloadParts);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return Failure(request, ModelResultStatus.InvalidResponse, "EMPTY_PAYLOAD", "Google returned no structured text payload.", latency);
            }

            if (payload.Length > 100_000)
            {
                return Failure(
                    request,
                    ModelResultStatus.InvalidResponse,
                    "STRUCTURED_PAYLOAD_TOO_LARGE",
                    "Google returned a structured payload larger than Dagmay's Core contract permits.",
                    latency);
            }

            var usage = response?.UsageMetadata;
            return ModelResult.Success(
                request.Id,
                ProviderId,
                string.IsNullOrWhiteSpace(response?.ModelVersion) ? _options.ModelId : Bounded(response!.ModelVersion!, 128),
                payload!,
                "Structured provider response received; Core validation is still required.",
                latency,
                Bounded(response?.ResponseId, 256),
                finishReason,
                NonNegative(usage?.PromptTokenCount),
                NonNegative(usage?.CandidatesTokenCount),
                NonNegative(usage?.TotalTokenCount));
        }

        private async Task<string> ReadBoundedContentAsync(HttpContent content, CancellationToken cancellationToken)
        {
            var maximumBytes = checked(_options.MaximumResponseCharacters * 4);
            using (var source = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var destination = new MemoryStream())
            {
                var buffer = new byte[8192];
                while (true)
                {
                    var read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    if (destination.Length + read > maximumBytes)
                    {
                        throw new InvalidDataException("Provider response exceeded the byte bound.");
                    }

                    destination.Write(buffer, 0, read);
                }

                string value;
                try
                {
                    value = new UTF8Encoding(false, true).GetString(destination.ToArray());
                }
                catch (DecoderFallbackException exception)
                {
                    throw new InvalidDataException("Provider response was not valid UTF-8.", exception);
                }

                if (value.Length > _options.MaximumResponseCharacters)
                {
                    throw new InvalidDataException("Provider response exceeded the character bound.");
                }

                return value;
            }
        }

        private ModelResult NormalizeHttpFailure(
            ModelRequest request,
            HttpStatusCode statusCode,
            TimeSpan latency,
            TimeSpan? retryAfter,
            GoogleErrorDetail? detail)
        {
            var numeric = (int)statusCode;
            var providerMessage = string.IsNullOrWhiteSpace(detail?.Message)
                ? string.Empty
                : Bounded(detail!.Message, 1000);
            var providerStatus = NormalizeErrorStatus(detail?.Status);
            if (numeric == 429)
            {
                return Failure(
                    request,
                    ModelResultStatus.RateLimited,
                    "HTTP_429" + providerStatus,
                    providerMessage.Length == 0 ? "Google rate-limited the request." : providerMessage,
                    latency,
                    true,
                    retryAfter);
            }

            if (numeric == 408 || numeric == 504)
            {
                return Failure(
                    request,
                    ModelResultStatus.TimedOut,
                    "HTTP_" + numeric + providerStatus,
                    providerMessage.Length == 0 ? "Google did not complete the request before its deadline." : providerMessage,
                    latency,
                    true,
                    retryAfter);
            }

            if (numeric >= 500 && numeric <= 599)
            {
                return Failure(
                    request,
                    ModelResultStatus.ProviderError,
                    "HTTP_" + numeric + providerStatus,
                    providerMessage.Length == 0 ? "Google reported a transient server error." : providerMessage,
                    latency,
                    true,
                    retryAfter);
            }

            if (numeric == 401 || numeric == 403)
            {
                return Failure(request, ModelResultStatus.ProviderError, "AUTHORIZATION_FAILED", "Google rejected the configured credential or project permission.", latency);
            }

            return Failure(
                request,
                ModelResultStatus.ProviderError,
                "HTTP_" + numeric + providerStatus,
                providerMessage.Length == 0
                    ? "Google rejected the request; it was not retried automatically."
                    : providerMessage,
                latency);
        }

        private static GoogleErrorDetail? ParseErrorDetail(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(GoogleErrorEnvelope));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), false))
                {
                    return (serializer.ReadObject(stream) as GoogleErrorEnvelope)?.Error;
                }
            }
            catch (Exception exception) when (
                exception is SerializationException
                || exception is InvalidDataContractException
                || exception is FormatException)
            {
                return null;
            }
        }

        private static string NormalizeErrorStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var builder = new StringBuilder();
            foreach (var character in value!.Trim().ToUpperInvariant())
            {
                if ((character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9')
                    || character == '_')
                {
                    builder.Append(character);
                }
                else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
                {
                    builder.Append('_');
                }

                if (builder.Length >= 64) break;
            }

            return builder.Length == 0 ? string.Empty : "_" + builder;
        }

        private ModelResult Failure(
            ModelRequest request,
            ModelResultStatus status,
            string code,
            string message,
            TimeSpan latency,
            bool retryable = false,
            TimeSpan? retryAfter = null,
            string providerOperationId = "")
        {
            return ModelResult.Failure(
                request.Id,
                status,
                ProviderId,
                _options.ModelId,
                code,
                message,
                latency,
                retryable,
                retryAfter,
                providerOperationId);
        }

        private static TimeSpan? ReadRetryAfter(HttpResponseMessage response, DateTimeOffset nowUtc)
        {
            var retry = response.Headers.RetryAfter;
            if (retry is null) return null;
            if (retry.Delta.HasValue && retry.Delta.Value > TimeSpan.Zero) return retry.Delta.Value;
            if (retry.Date.HasValue && retry.Date.Value > nowUtc) return retry.Date.Value - nowUtc;
            return null;
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else builder.Append(character);
                        break;
                }
            }

            builder.Append('"');
        }

        private static int NonNegative(int? value) => value.HasValue && value.Value > 0 ? value.Value : 0;

        private static string Bounded(string? value, int maximum)
        {
            if (value is null || string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            return trimmed.Length <= maximum ? trimmed : trimmed.Substring(0, maximum);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GoogleAiStudioProvider));
        }
    }

    [DataContract]
    internal sealed class GoogleGenerateContentResponse
    {
        [DataMember(Name = "candidates")]
        public List<GoogleCandidate>? Candidates { get; set; }

        [DataMember(Name = "promptFeedback")]
        public GooglePromptFeedback? PromptFeedback { get; set; }

        [DataMember(Name = "usageMetadata")]
        public GoogleUsageMetadata? UsageMetadata { get; set; }

        [DataMember(Name = "modelVersion")]
        public string? ModelVersion { get; set; }

        [DataMember(Name = "responseId")]
        public string? ResponseId { get; set; }
    }

    [DataContract]
    internal sealed class GoogleCandidate
    {
        [DataMember(Name = "content")]
        public GoogleContent? Content { get; set; }

        [DataMember(Name = "finishReason")]
        public string? FinishReason { get; set; }
    }

    [DataContract]
    internal sealed class GoogleContent
    {
        [DataMember(Name = "parts")]
        public List<GooglePart>? Parts { get; set; }
    }

    [DataContract]
    internal sealed class GooglePart
    {
        [DataMember(Name = "text")]
        public string? Text { get; set; }

        [DataMember(Name = "thought")]
        public bool Thought { get; set; }
    }

    [DataContract]
    internal sealed class GooglePromptFeedback
    {
        [DataMember(Name = "blockReason")]
        public string? BlockReason { get; set; }
    }

    [DataContract]
    internal sealed class GoogleUsageMetadata
    {
        [DataMember(Name = "promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [DataMember(Name = "candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }

        [DataMember(Name = "totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }

    [DataContract]
    internal sealed class GoogleErrorEnvelope
    {
        [DataMember(Name = "error")]
        public GoogleErrorDetail? Error { get; set; }
    }

    [DataContract]
    internal sealed class GoogleErrorDetail
    {
        [DataMember(Name = "code")]
        public int Code { get; set; }

        [DataMember(Name = "message")]
        public string? Message { get; set; }

        [DataMember(Name = "status")]
        public string? Status { get; set; }
    }
}
