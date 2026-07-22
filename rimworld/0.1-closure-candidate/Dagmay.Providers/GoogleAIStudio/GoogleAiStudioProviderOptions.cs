using System;
using System.Text.RegularExpressions;

namespace Dagmay.Providers.GoogleAIStudio
{
    public sealed class GoogleAiStudioProviderOptions
    {
        private static readonly Regex ModelPattern = new Regex(
            "^[A-Za-z0-9._-]{1,128}$",
            RegexOptions.CultureInvariant);

        public GoogleAiStudioProviderOptions(
            string modelId = "gemini-3.1-flash-lite",
            string apiKeyEnvironmentVariable = "DAGMAY_GOOGLE_API_KEY",
            TimeSpan? requestTimeout = null,
            Uri? endpointBase = null,
            double temperature = 0.25,
            int maximumResponseCharacters = 250_000)
        {
            if (string.IsNullOrWhiteSpace(modelId)) throw new ArgumentException("A model ID is required.", nameof(modelId));
            if (!ModelPattern.IsMatch(modelId.Trim())) throw new ArgumentException("The model ID contains unsupported characters.", nameof(modelId));
            if (string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable)) throw new ArgumentException("An environment-variable name is required.", nameof(apiKeyEnvironmentVariable));

            var timeout = requestTimeout ?? TimeSpan.FromSeconds(45);
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
            var endpoint = endpointBase ?? new Uri("https://generativelanguage.googleapis.com/v1beta/", UriKind.Absolute);
            if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("The endpoint must be an absolute HTTPS URI.", nameof(endpointBase));
            if (double.IsNaN(temperature) || double.IsInfinity(temperature) || temperature < 0 || temperature > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(temperature));
            }

            if (maximumResponseCharacters < 1024 || maximumResponseCharacters > 1_000_000)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumResponseCharacters));
            }

            ModelId = modelId.Trim();
            ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable.Trim();
            RequestTimeout = timeout;
            EndpointBase = endpoint;
            Temperature = temperature;
            MaximumResponseCharacters = maximumResponseCharacters;
        }

        public string ModelId { get; }
        public string ApiKeyEnvironmentVariable { get; }
        public TimeSpan RequestTimeout { get; }
        public Uri EndpointBase { get; }
        public double Temperature { get; }
        public int MaximumResponseCharacters { get; }
    }
}
