using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dagmay.Core.Contracts;
using Dagmay.Core.Reflection;

namespace Dagmay.Core.Dialogue
{
    /// <summary>
    /// Strict provider-boundary codec for one dialogue proposal. Runtime-owned
    /// identity and time are supplied separately and never delegated to a model.
    /// </summary>
    public static class UtteranceProposalJson
    {
        public const int MaximumInputCharacters = 16_000;
        public const int MaximumInputBytes = 32_000;
        private const int MaximumTextCharacters = 4_000;

        private static readonly string[] Members =
        {
            "request_id",
            "conversation_id",
            "speaker_id",
            "recipient_id",
            "text",
            "evidence_ids"
        };

        public static UtteranceProposal Parse(
            byte[] utf8Json,
            DialogueRequest request,
            UtteranceId utteranceId,
            long generatedAtTick)
        {
            if (utf8Json is null) throw new ArgumentNullException(nameof(utf8Json));
            if (utf8Json.Length > MaximumInputBytes)
                throw new InvalidDataException("Utterance response is too large.");

            string json;
            try
            {
                json = new UTF8Encoding(false, true).GetString(utf8Json);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Utterance response is not valid UTF-8.", exception);
            }

            return Parse(json, request, utteranceId, generatedAtTick);
        }

        public static UtteranceProposal Parse(
            string json,
            DialogueRequest request,
            UtteranceId utteranceId,
            long generatedAtTick)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (utteranceId.Value == Guid.Empty)
                throw new ArgumentException("Utterance ID cannot be empty.", nameof(utteranceId));
            if (generatedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(generatedAtTick));
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Utterance response is empty.");
            if (json.Length > MaximumInputCharacters)
                throw new InvalidDataException("Utterance response is too large.");

            var root = RequireObject(new ReflectionProposalJson.StrictJsonParser(json).Parse());
            RequireExactMembers(root);

            var requestId = ParseGuid<DialogueRequestId>(
                RequireString(root, "request_id"),
                value => new DialogueRequestId(value),
                "request ID");
            var conversationId = ParseGuid<ConversationId>(
                RequireString(root, "conversation_id"),
                value => new ConversationId(value),
                "conversation ID");
            var speakerId = ParseGuid<IndividualId>(
                RequireString(root, "speaker_id"),
                value => new IndividualId(value),
                "speaker ID");
            var recipientId = ParseOptionalIndividualId(RequireString(root, "recipient_id"));

            if (requestId != request.Id)
                throw new InvalidDataException("Utterance request ID does not match the dispatched request.");
            if (conversationId != request.ConversationId)
                throw new InvalidDataException("Utterance conversation ID does not match the dispatched request.");
            if (speakerId != request.ExpectedSpeakerId)
                throw new InvalidDataException("Utterance speaker ID does not match the expected speaker.");
            if (recipientId != request.RecipientId)
                throw new InvalidDataException("Utterance recipient ID does not match the dispatched request.");

            var text = RequireString(root, "text");
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumTextCharacters)
                throw new InvalidDataException("Utterance text length is invalid.");
            if (text.Any(char.IsControl))
                throw new InvalidDataException("Utterance text contains control characters.");

            var evidenceValues = RequireArray(root, "evidence_ids");
            if (evidenceValues.Count == 0 || evidenceValues.Count > 32)
                throw new InvalidDataException("Utterance evidence count is invalid.");

            var allowedEvidence = new HashSet<EventId>(request.SourceEventIds);
            var evidenceIds = new List<EventId>(evidenceValues.Count);
            var uniqueEvidence = new HashSet<EventId>();
            foreach (var value in evidenceValues)
            {
                var eventId = ParseGuid<EventId>(
                    RequireStringValue(value, "evidence ID"),
                    id => new EventId(id),
                    "evidence ID");
                if (!uniqueEvidence.Add(eventId))
                    throw new InvalidDataException("Utterance evidence IDs must be unique.");
                if (!allowedEvidence.Contains(eventId))
                    throw new InvalidDataException("Utterance evidence is outside the dispatched request.");
                evidenceIds.Add(eventId);
            }

            return new UtteranceProposal(
                utteranceId,
                requestId,
                conversationId,
                speakerId,
                recipientId,
                text,
                evidenceIds,
                generatedAtTick);
        }

        public static string Serialize(UtteranceProposal proposal)
        {
            if (proposal is null) throw new ArgumentNullException(nameof(proposal));
            if (!string.IsNullOrWhiteSpace(proposal.ActionDirective))
                throw new InvalidDataException("Action directives cannot be encoded as utterance proposals.");

            var builder = new StringBuilder(512);
            builder.Append('{');
            AppendMember(builder, "request_id", proposal.RequestId.ToString(), false);
            AppendMember(builder, "conversation_id", proposal.ConversationId.ToString(), true);
            AppendMember(builder, "speaker_id", proposal.SpeakerId.ToString(), true);
            AppendMember(builder, "recipient_id", proposal.RecipientId?.ToString() ?? string.Empty, true);
            AppendMember(builder, "text", proposal.Text, true);
            builder.Append(",\"evidence_ids\":[");
            var orderedEvidence = proposal.EvidenceIds
                .OrderBy(eventId => eventId.ToString(), StringComparer.Ordinal)
                .ToArray();
            for (var index = 0; index < orderedEvidence.Length; index++)
            {
                if (index > 0) builder.Append(',');
                AppendString(builder, orderedEvidence[index].ToString());
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private static IDictionary<string, object> RequireObject(object value)
        {
            if (!(value is IDictionary<string, object> result))
                throw new InvalidDataException("Utterance response root must be an object.");
            return result;
        }

        private static void RequireExactMembers(IDictionary<string, object> root)
        {
            var allowed = new HashSet<string>(Members, StringComparer.Ordinal);
            if (root.Count != allowed.Count)
                throw new InvalidDataException("Utterance response has missing or additional members.");
            foreach (var member in root.Keys)
            {
                if (!allowed.Contains(member))
                    throw new InvalidDataException($"Utterance response contains unsupported member '{member}'.");
            }
        }

        private static string RequireString(IDictionary<string, object> root, string name)
        {
            if (!root.TryGetValue(name, out var value))
                throw new InvalidDataException($"Required utterance member '{name}' is missing.");
            return RequireStringValue(value, name);
        }

        private static string RequireStringValue(object value, string name)
        {
            if (!(value is string result))
                throw new InvalidDataException($"Utterance member '{name}' must be a string.");
            return result;
        }

        private static IList<object> RequireArray(IDictionary<string, object> root, string name)
        {
            if (!root.TryGetValue(name, out var value) || !(value is IList<object> result))
                throw new InvalidDataException($"Utterance member '{name}' must be an array.");
            return result;
        }

        private static T ParseGuid<T>(string text, Func<Guid, T> factory, string name)
        {
            if (!Guid.TryParseExact(text, "N", out var value) || value == Guid.Empty)
                throw new InvalidDataException($"Utterance {name} is invalid.");
            return factory(value);
        }

        private static IndividualId? ParseOptionalIndividualId(string text)
        {
            return text.Length == 0
                ? (IndividualId?)null
                : ParseGuid(text, value => new IndividualId(value), "recipient ID");
        }

        private static void AppendMember(
            StringBuilder builder,
            string name,
            string value,
            bool prependComma)
        {
            if (prependComma) builder.Append(',');
            AppendString(builder, name);
            builder.Append(':');
            AppendString(builder, value);
        }

        private static void AppendString(StringBuilder builder, string value)
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
                            builder.Append(((int)character).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
