using System;
using System.Collections.Generic;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Reflection
{
    public enum ModelTaskKind
    {
        InterpretMeaningfulEvent,
        BackgroundReflection,
        ConsolidateMemories,
        ExpressDiaryEntry
    }

    public enum ModelResultStatus
    {
        Success,
        Unavailable,
        TimedOut,
        Canceled,
        InvalidResponse,
        RateLimited,
        ProviderError,
        NotRun
    }

    public sealed class ProviderCapabilities
    {
        public ProviderCapabilities(
            bool supportsStructuredOutput,
            bool supportsEmbeddings,
            int maximumInputCharacters,
            int maximumOutputTokens)
        {
            if (maximumInputCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(maximumInputCharacters));
            if (maximumOutputTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens));

            SupportsStructuredOutput = supportsStructuredOutput;
            SupportsEmbeddings = supportsEmbeddings;
            MaximumInputCharacters = maximumInputCharacters;
            MaximumOutputTokens = maximumOutputTokens;
        }

        public bool SupportsStructuredOutput { get; }
        public bool SupportsEmbeddings { get; }
        public int MaximumInputCharacters { get; }
        public int MaximumOutputTokens { get; }
    }

    public sealed class ModelRequest
    {
        public ModelRequest(
            RequestId id,
            IndividualId individualId,
            LineageId lineageId,
            long baseStateVersion,
            ModelTaskKind taskKind,
            string promptVersion,
            string systemInstruction,
            string context,
            string responseJsonSchema,
            IEnumerable<EventId> evidenceEventIds,
            AffectVector currentAffect,
            DateTimeOffset deadlineUtc,
            int maximumOutputTokens)
        {
            if (baseStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(baseStateVersion));
            if (maximumOutputTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens));

            Id = id;
            SchemaVersion = SchemaVersions.ModelRequest;
            IndividualId = individualId;
            LineageId = lineageId;
            BaseStateVersion = baseStateVersion;
            TaskKind = taskKind;
            PromptVersion = ContractGuard.Text(promptVersion, nameof(promptVersion), 128);
            SystemInstruction = ContractGuard.Text(systemInstruction, nameof(systemInstruction), 20_000);
            Context = ContractGuard.Text(context, nameof(context), 100_000);
            ResponseJsonSchema = ContractGuard.Text(responseJsonSchema, nameof(responseJsonSchema), 30_000);
            EvidenceEventIds = ContractGuard.List(evidenceEventIds, nameof(evidenceEventIds));
            if (EvidenceEventIds.Count == 0 || EvidenceEventIds.Count > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(evidenceEventIds));
            }

            CurrentAffect = currentAffect ?? throw new ArgumentNullException(nameof(currentAffect));
            DeadlineUtc = deadlineUtc;
            MaximumOutputTokens = maximumOutputTokens;
        }

        public RequestId Id { get; }
        public int SchemaVersion { get; }
        public IndividualId IndividualId { get; }
        public LineageId LineageId { get; }
        public long BaseStateVersion { get; }
        public ModelTaskKind TaskKind { get; }
        public string PromptVersion { get; }
        public string SystemInstruction { get; }
        public string Context { get; }
        public string ResponseJsonSchema { get; }
        public IReadOnlyList<EventId> EvidenceEventIds { get; }
        public AffectVector CurrentAffect { get; }
        public DateTimeOffset DeadlineUtc { get; }
        public int MaximumOutputTokens { get; }
    }

    public sealed class ModelResult
    {
        private ModelResult(
            RequestId requestId,
            ModelResultStatus status,
            string provider,
            string model,
            string structuredPayload,
            string decisionSummary,
            string errorCode,
            string errorMessage,
            TimeSpan latency,
            string providerOperationId,
            string finishReason,
            int promptTokens,
            int outputTokens,
            int totalTokens,
            bool retryable,
            TimeSpan? retryAfter)
        {
            RequestId = requestId;
            SchemaVersion = SchemaVersions.ModelResult;
            Status = status;
            Provider = provider;
            Model = model;
            StructuredPayload = structuredPayload;
            DecisionSummary = decisionSummary;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Latency = latency;
            ProviderOperationId = providerOperationId;
            FinishReason = finishReason;
            PromptTokens = promptTokens;
            OutputTokens = outputTokens;
            TotalTokens = totalTokens;
            Retryable = retryable;
            RetryAfter = retryAfter;
        }

        public RequestId RequestId { get; }
        public int SchemaVersion { get; }
        public ModelResultStatus Status { get; }
        public string Provider { get; }
        public string Model { get; }
        public string StructuredPayload { get; }
        public string DecisionSummary { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
        public TimeSpan Latency { get; }
        public string ProviderOperationId { get; }
        public string FinishReason { get; }
        public int PromptTokens { get; }
        public int OutputTokens { get; }
        public int TotalTokens { get; }
        public bool Retryable { get; }
        public TimeSpan? RetryAfter { get; }

        public static ModelResult Success(
            RequestId requestId,
            string provider,
            string model,
            string structuredPayload,
            string decisionSummary,
            TimeSpan latency,
            string providerOperationId = "",
            string finishReason = "STOP",
            int promptTokens = 0,
            int outputTokens = 0,
            int totalTokens = 0)
        {
            ValidateUsage(promptTokens, outputTokens, totalTokens);
            return new ModelResult(
                requestId,
                ModelResultStatus.Success,
                ContractGuard.Text(provider, nameof(provider), 128),
                ContractGuard.Text(model, nameof(model), 128),
                ContractGuard.Text(structuredPayload, nameof(structuredPayload), 100_000),
                ContractGuard.Text(decisionSummary, nameof(decisionSummary), 2000),
                string.Empty,
                string.Empty,
                latency,
                Optional(providerOperationId, 256),
                Optional(finishReason, 128),
                promptTokens,
                outputTokens,
                totalTokens,
                false,
                null);
        }

        public static ModelResult Failure(
            RequestId requestId,
            ModelResultStatus status,
            string provider,
            string model,
            string errorCode,
            string errorMessage,
            TimeSpan latency,
            bool retryable = false,
            TimeSpan? retryAfter = null,
            string providerOperationId = "")
        {
            if (status == ModelResultStatus.Success) throw new ArgumentException("Use Success for successful results.", nameof(status));
            if (retryAfter.HasValue && retryAfter.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(retryAfter));
            }

            return new ModelResult(
                requestId,
                status,
                ContractGuard.Text(provider, nameof(provider), 128),
                string.IsNullOrWhiteSpace(model) ? string.Empty : model.Trim(),
                string.Empty,
                string.Empty,
                ContractGuard.Text(errorCode, nameof(errorCode), 128),
                ContractGuard.Text(errorMessage, nameof(errorMessage), 2000),
                latency,
                Optional(providerOperationId, 256),
                string.Empty,
                0,
                0,
                0,
                retryable,
                retryAfter);
        }

        private static string Optional(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Length > maximumLength) throw new ArgumentOutOfRangeException(nameof(value));
            return value.Trim();
        }

        private static void ValidateUsage(int promptTokens, int outputTokens, int totalTokens)
        {
            if (promptTokens < 0) throw new ArgumentOutOfRangeException(nameof(promptTokens));
            if (outputTokens < 0) throw new ArgumentOutOfRangeException(nameof(outputTokens));
            if (totalTokens < 0) throw new ArgumentOutOfRangeException(nameof(totalTokens));
            if (totalTokens != 0 && totalTokens < promptTokens + outputTokens)
            {
                throw new ArgumentOutOfRangeException(nameof(totalTokens));
            }
        }
    }
}
