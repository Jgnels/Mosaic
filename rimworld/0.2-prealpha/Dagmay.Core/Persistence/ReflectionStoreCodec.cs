using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Reflection;
using Dagmay.Core.Scheduling;

namespace Dagmay.Core.Persistence
{
    public sealed class ReflectionStoreCodec
    {
        public const int FormatVersion = 1;
        public const int MaximumArchiveBytes = 64 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public byte[] Encode(ReflectionStoreSnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            var payload = new XDocument(
                new XElement("reflectionStore",
                    new XAttribute("schema", FormatVersion),
                    new XAttribute("storeId", snapshot.StoreId.ToString("N")),
                    new XAttribute("generation", snapshot.Generation.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("savedAtUtc", FormatDate(snapshot.SavedAtUtc)),
                    new XElement("pendingTasks", snapshot.PendingTasks.Select(EncodeTask)),
                    new XElement("auditRecords", snapshot.AuditRecords.Select(EncodeAudit))));
            var payloadBytes = Utf8.GetBytes(payload.ToString(SaveOptions.DisableFormatting));
            var envelope = new XDocument(
                new XElement("dagmayReflectionStore",
                    new XAttribute("format", FormatVersion),
                    new XElement("checksum", ComputeChecksum(payloadBytes)),
                    new XElement("payload", Convert.ToBase64String(payloadBytes))));
            var bytes = Utf8.GetBytes(envelope.ToString(SaveOptions.DisableFormatting));
            if (bytes.Length > MaximumArchiveBytes) throw new InvalidOperationException("Reflection store exceeds the maximum supported size.");
            return bytes;
        }

        public ReflectionStoreSnapshot Decode(byte[] encoded)
        {
            if (encoded is null) throw new ArgumentNullException(nameof(encoded));
            if (encoded.Length == 0 || encoded.Length > MaximumArchiveBytes) throw new InvalidDataException("Reflection store size is invalid.");

            var envelope = LoadXml(encoded);
            var root = RequireRoot(envelope, "dagmayReflectionStore");
            RequireVersion(root, "format");
            var checksum = RequireText(root.Element("checksum"), "checksum", 128);
            var encodedPayload = RequireText(root.Element("payload"), "payload", MaximumArchiveBytes);
            byte[] payloadBytes;
            try
            {
                payloadBytes = Convert.FromBase64String(encodedPayload);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Reflection store payload is not valid base64.", exception);
            }

            if (!FixedTimeEquals(checksum, ComputeChecksum(payloadBytes)))
            {
                throw new InvalidDataException("Reflection store checksum does not match its payload.");
            }

            var payload = LoadXml(payloadBytes);
            var store = RequireRoot(payload, "reflectionStore");
            RequireVersion(store, "schema");
            var storeId = ParseGuid(RequireAttribute(store, "storeId"), "storeId");
            var generation = ParseLong(RequireAttribute(store, "generation"), "generation");
            var savedAt = ParseDate(RequireAttribute(store, "savedAtUtc"));
            var taskElements = store.Element("pendingTasks")?.Elements("task").ToList()
                ?? throw new InvalidDataException("Pending reflection tasks are missing.");
            var auditElements = store.Element("auditRecords")?.Elements("record").ToList()
                ?? throw new InvalidDataException("Reflection audit records are missing.");
            if (taskElements.Count > 500 || auditElements.Count > 500)
            {
                throw new InvalidDataException("Reflection store item count exceeds the supported limit.");
            }

            return new ReflectionStoreSnapshot(
                storeId,
                generation,
                savedAt,
                taskElements.Select(DecodeTask),
                auditElements.Select(DecodeAudit));
        }

        private static XElement EncodeTask(PendingReflectionTask value)
        {
            var task = value.Task;
            return new XElement("task",
                new XAttribute("id", task.Id.ToString()),
                new XAttribute("individualId", task.IndividualId.ToString()),
                new XAttribute("kind", task.TaskKind.ToString()),
                new XAttribute("priority", task.Priority.ToString()),
                new XAttribute("createdAtUtc", FormatDate(task.CreatedAtUtc)),
                new XAttribute("coalescingKey", task.CoalescingKey),
                new XAttribute("estimatedTokens", task.EstimatedTokens.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("attemptCount", value.AttemptCount.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("nextAttemptAtUtc", FormatDate(value.NextAttemptAtUtc)),
                new XAttribute("lastFailureCode", value.LastFailureCode),
                new XElement("sourceEvents", task.SourceEventIds.Select(id =>
                    new XElement("event", new XAttribute("id", id.ToString())))));
        }

        private static PendingReflectionTask DecodeTask(XElement element)
        {
            var task = new ReflectionTask(
                ReflectionTaskId.Parse(RequireAttribute(element, "id")),
                IndividualId.Parse(RequireAttribute(element, "individualId")),
                ParseEnum<ModelTaskKind>(RequireAttribute(element, "kind"), "kind"),
                ParseEnum<ReflectionPriority>(RequireAttribute(element, "priority"), "priority"),
                ParseDate(RequireAttribute(element, "createdAtUtc")),
                RequireAttribute(element, "coalescingKey"),
                element.Element("sourceEvents")?.Elements("event")
                    .Select(value => EventId.Parse(RequireAttribute(value, "id")))
                    ?? throw new InvalidDataException("Reflection task evidence is missing."),
                ParseInt(RequireAttribute(element, "estimatedTokens"), "estimatedTokens", 1));
            return new PendingReflectionTask(
                task,
                ParseInt(RequireAttribute(element, "attemptCount"), "attemptCount", 0),
                ParseDate(RequireAttribute(element, "nextAttemptAtUtc")),
                OptionalAttribute(element, "lastFailureCode"));
        }

        private static XElement EncodeAudit(ReflectionAuditRecord value)
        {
            return new XElement("record",
                new XAttribute("id", value.Id.ToString()),
                new XAttribute("schema", value.SchemaVersion),
                new XAttribute("taskId", value.TaskId.ToString()),
                new XAttribute("requestId", value.RequestId.ToString()),
                new XAttribute("individualId", value.IndividualId.ToString()),
                new XAttribute("lineageId", value.LineageId.ToString()),
                new XAttribute("baseStateVersion", value.BaseStateVersion.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("taskKind", value.TaskKind.ToString()),
                new XAttribute("promptVersion", value.PromptVersion),
                new XAttribute("occurredAtUtc", FormatDate(value.OccurredAtUtc)),
                new XAttribute("status", value.Status.ToString()),
                new XAttribute("attemptNumber", value.AttemptNumber.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("provider", value.Provider),
                new XAttribute("model", value.Model),
                new XAttribute("providerOperationId", value.ProviderOperationId),
                new XAttribute("resultStatus", value.ResultStatus.ToString()),
                new XAttribute("retryable", value.Retryable),
                new XAttribute("estimatedTokens", value.EstimatedTokens.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("promptTokens", value.PromptTokens.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("outputTokens", value.OutputTokens.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("totalTokens", value.TotalTokens.ToString(CultureInfo.InvariantCulture)),
                EncodeAuditText("errorCode", value.ErrorCode),
                EncodeAuditText("errorMessage", value.ErrorMessage),
                EncodeAuditText("requestContext", value.RequestContext),
                EncodeAuditText("structuredPayload", value.StructuredPayload),
                EncodeAuditText("diagnostic", value.Diagnostic));
        }

        private static ReflectionAuditRecord DecodeAudit(XElement element)
        {
            var schema = ParseLong(RequireAttribute(element, "schema"), "schema");
            if (schema != SchemaVersions.ReflectionRecord) throw new InvalidDataException("Reflection record schema is unsupported.");
            return new ReflectionAuditRecord(
                ReflectionRecordId.Parse(RequireAttribute(element, "id")),
                ReflectionTaskId.Parse(RequireAttribute(element, "taskId")),
                RequestId.Parse(RequireAttribute(element, "requestId")),
                IndividualId.Parse(RequireAttribute(element, "individualId")),
                LineageId.Parse(RequireAttribute(element, "lineageId")),
                ParseLong(RequireAttribute(element, "baseStateVersion"), "baseStateVersion"),
                ParseEnum<ModelTaskKind>(RequireAttribute(element, "taskKind"), "taskKind"),
                RequireAttribute(element, "promptVersion"),
                ParseDate(RequireAttribute(element, "occurredAtUtc")),
                ParseEnum<ReflectionAuditStatus>(RequireAttribute(element, "status"), "status"),
                ParseInt(RequireAttribute(element, "attemptNumber"), "attemptNumber", 1),
                OptionalAttribute(element, "provider"),
                OptionalAttribute(element, "model"),
                OptionalAttribute(element, "providerOperationId"),
                ParseEnum<ModelResultStatus>(RequireAttribute(element, "resultStatus"), "resultStatus"),
                DecodeAuditText(element, "errorCode", 128),
                DecodeAuditText(element, "errorMessage", 2000),
                ParseBool(RequireAttribute(element, "retryable"), "retryable"),
                ParseInt(RequireAttribute(element, "estimatedTokens"), "estimatedTokens", 0),
                ParseInt(RequireAttribute(element, "promptTokens"), "promptTokens", 0),
                ParseInt(RequireAttribute(element, "outputTokens"), "outputTokens", 0),
                ParseInt(RequireAttribute(element, "totalTokens"), "totalTokens", 0),
                DecodeAuditText(element, "requestContext", 100_000),
                DecodeAuditText(element, "structuredPayload", 100_000),
                DecodeAuditText(element, "diagnostic", 2000));
        }

        private static XElement EncodeAuditText(string name, string value)
        {
            return new XElement(
                name,
                new XAttribute("encoding", "base64-utf8"),
                Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static string DecodeAuditText(XElement element, string name, int maximum)
        {
            var child = element.Element(name);
            if (child is null) return string.Empty;
            if (!string.Equals((string?)child.Attribute("encoding"), "base64-utf8", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Reflection audit element '{name}' has an unsupported encoding.");
            }

            if (child.Value.Length > maximum * 4L + 16L)
            {
                throw new InvalidDataException($"Reflection audit element '{name}' is too large.");
            }

            try
            {
                var value = Utf8.GetString(Convert.FromBase64String(child.Value));
                if (value.Length > maximum)
                {
                    throw new InvalidDataException($"Reflection audit element '{name}' is too large.");
                }

                return value;
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException($"Reflection audit element '{name}' is not valid base64.", exception);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException($"Reflection audit element '{name}' is not valid UTF-8.", exception);
            }
        }

        private static XDocument LoadXml(byte[] bytes)
        {
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumArchiveBytes
                }))
                {
                    return XDocument.Load(reader, LoadOptions.None);
                }
            }
            catch (Exception exception) when (
                exception is XmlException
                || exception is InvalidOperationException
                || exception is DecoderFallbackException)
            {
                throw new InvalidDataException("Reflection store XML is invalid.", exception);
            }
        }

        private static XElement RequireRoot(XDocument document, string name)
        {
            if (document.Root is null || document.Root.Name != name) throw new InvalidDataException($"Expected reflection root '{name}'.");
            return document.Root;
        }

        private static void RequireVersion(XElement element, string attribute)
        {
            if (ParseLong(RequireAttribute(element, attribute), attribute) != FormatVersion)
            {
                throw new InvalidDataException($"Unsupported reflection store {attribute}.");
            }
        }

        private static string RequireAttribute(XElement element, string name)
        {
            var value = (string?)element.Attribute(name);
            if (value is null || string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"Required attribute '{name}' is missing.");
            if (value.Length > 4096) throw new InvalidDataException($"Attribute '{name}' is too long.");
            return value;
        }

        private static string OptionalAttribute(XElement element, string name)
        {
            var value = (string?)element.Attribute(name);
            if (value is null || string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Length > 4096) throw new InvalidDataException($"Attribute '{name}' is too long.");
            return value;
        }

        private static string RequireText(XElement? element, string name, int maximum)
        {
            if (element is null || string.IsNullOrWhiteSpace(element.Value)) throw new InvalidDataException($"Required element '{name}' is missing.");
            if (element.Value.Length > maximum) throw new InvalidDataException($"Element '{name}' is too long.");
            return element.Value.Trim();
        }

        private static string OptionalElementText(XElement element, string name, int maximum)
        {
            var child = element.Element(name);
            if (child is null || string.IsNullOrEmpty(child.Value)) return string.Empty;
            if (child.Value.Length > maximum) throw new InvalidDataException($"Element '{name}' is too long.");
            return child.Value;
        }

        private static Guid ParseGuid(string value, string name)
        {
            if (!Guid.TryParse(value, out var result) || result == Guid.Empty) throw new InvalidDataException($"Value '{name}' is invalid.");
            return result;
        }

        private static long ParseLong(string value, string name)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < 0)
            {
                throw new InvalidDataException($"Value '{name}' is invalid.");
            }

            return result;
        }

        private static int ParseInt(string value, string name, int minimum)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < minimum)
            {
                throw new InvalidDataException($"Value '{name}' is invalid.");
            }

            return result;
        }

        private static bool ParseBool(string value, string name)
        {
            if (!bool.TryParse(value, out var result)) throw new InvalidDataException($"Value '{name}' is invalid.");
            return result;
        }

        private static DateTimeOffset ParseDate(string value)
        {
            if (!DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            {
                throw new InvalidDataException("Reflection timestamp is invalid.");
            }

            return result;
        }

        private static T ParseEnum<T>(string value, string name) where T : struct
        {
            if (!Enum.TryParse<T>(value, false, out var result) || !Enum.IsDefined(typeof(T), result))
            {
                throw new InvalidDataException($"Value '{name}' is invalid.");
            }

            return result;
        }

        private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        private static string ComputeChecksum(byte[] value)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(value);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var item in hash) builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }
}
