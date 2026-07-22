using System;
using Dagmay.Core.Abstractions;
using Dagmay.Providers.Configuration;
using Dagmay.Providers.Fake;
using Dagmay.Providers.GoogleAIStudio;

namespace Dagmay.RimWorld.Reflection
{
    internal sealed class RimWorldReflectionProviderSelection : IDisposable
    {
        private readonly IDisposable? _ownedProvider;

        private RimWorldReflectionProviderSelection(
            string mode,
            string providerId,
            string modelId,
            string diagnostic,
            IModelProvider? provider)
        {
            Mode = mode;
            ProviderId = providerId;
            ModelId = modelId;
            Diagnostic = diagnostic;
            Provider = provider;
            _ownedProvider = provider as IDisposable;
        }

        public string Mode { get; }
        public string ProviderId { get; }
        public string ModelId { get; }
        public string Diagnostic { get; }
        public IModelProvider? Provider { get; }
        public bool CanDispatch => Provider is not null;

        public static RimWorldReflectionProviderSelection FromEnvironment()
        {
            var configured = UserEnvironmentConfiguration.Read("DAGMAY_REFLECTION_MODE");
            var mode = string.IsNullOrWhiteSpace(configured)
                ? "offline"
                : configured!.Trim().ToLowerInvariant();

            if (mode == "offline")
            {
                return new RimWorldReflectionProviderSelection(
                    "offline",
                    "none",
                    string.Empty,
                    "Reflection transport is offline; durable work will remain queued.",
                    null);
            }

            if (mode == "fake")
            {
                var fake = new DeterministicFakeProvider();
                return new RimWorldReflectionProviderSelection(
                    "fake",
                    fake.ProviderId,
                    "deterministic-v1",
                    "Deterministic local test reflection is enabled; no network request will be made.",
                    fake);
            }

            if (mode != "google")
            {
                return new RimWorldReflectionProviderSelection(
                    "offline",
                    "none",
                    string.Empty,
                    "Unrecognized DAGMAY_REFLECTION_MODE value; reflection transport was kept offline.",
                    null);
            }

            var configuredModel = UserEnvironmentConfiguration.Read("DAGMAY_GOOGLE_MODEL");
            var model = string.IsNullOrWhiteSpace(configuredModel)
                ? "gemini-3.1-flash-lite"
                : configuredModel!.Trim();
            GoogleAiStudioProviderOptions options;
            try
            {
                options = new GoogleAiStudioProviderOptions(model);
            }
            catch (ArgumentException)
            {
                return new RimWorldReflectionProviderSelection(
                    "google-unconfigured",
                    "google.ai-studio",
                    string.Empty,
                    "The configured Google model ID is invalid; reflection calls are disabled.",
                    null);
            }

            var apiKey = UserEnvironmentConfiguration.Read(options.ApiKeyEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new RimWorldReflectionProviderSelection(
                    "google-unconfigured",
                    "google.ai-studio",
                    options.ModelId,
                    "Google reflection was selected, but the API-key environment variable is absent; work remains queued.",
                    null);
            }

            var provider = new GoogleAiStudioProvider(options);
            return new RimWorldReflectionProviderSelection(
                "google",
                provider.ProviderId,
                options.ModelId,
                "Google AI Studio reflection is enabled with an environment-provided credential.",
                provider);
        }

        public void Dispose()
        {
            _ownedProvider?.Dispose();
        }
    }
}
