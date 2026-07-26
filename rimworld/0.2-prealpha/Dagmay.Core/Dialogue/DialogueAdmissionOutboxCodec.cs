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
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Core.Dialogue
{
    public sealed class DialogueAdmissionOutboxCodec
    {
        public const int FormatVersion = 1;
        public const int MaximumArchiveBytes = 64 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public byte[] Encode(DialogueAdmissionOutboxSnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            var payload = new XDocument(
                new XElement("dialogueAdmissionOutbox",
                    new XAttribute("schema", FormatVersion),
                    new XAttribute("storeId", snapshot.Binding.StoreId.ToString("N")),
                    new XAttribute("generation", snapshot.Binding.CheckpointGeneration.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("savedAtUtc", FormatDate(snapshot.SavedAtUtc)),
                    EncodeText("saveId", snapshot.Binding.SaveId),
                    EncodeText("worldId", snapshot.Binding.WorldId),
                    new XElement("entries", snapshot.Entries.Select(EncodeEntry))));
            var payloadBytes = Utf8.GetBytes(payload.ToString(SaveOptions.DisableFormatting));
            var envelope = new XDocument(
                new XElement("mosaicDialogueAdmissionOutbox",
                    new XAttribute("format", FormatVersion),
                    new XElement("checksum", ComputeChecksum(payloadBytes)),
                    new XElement("payload", Convert.ToBase64String(payloadBytes))));
            var encoded = Utf8.GetBytes(envelope.ToString(SaveOptions.DisableFormatting));
            if (encoded.Length > MaximumArchiveBytes)
                throw new InvalidOperationException("Dialogue admission outbox exceeds the maximum supported size.");
            return encoded;
        }

        public DialogueAdmissionOutboxSnapshot Decode(byte[] encoded)
        {
            if (encoded is null) throw new ArgumentNullException(nameof(encoded));
            if (encoded.Length == 0 || encoded.Length > MaximumArchiveBytes)
                throw new InvalidDataException("Dialogue admission outbox size is invalid.");

            var envelope = LoadXml(encoded);
            var root = RequireRoot(envelope, "mosaicDialogueAdmissionOutbox");
            RequireVersion(root, "format");
            var checksum = RequireText(root.Element("checksum"), "checksum", 128);
            var payloadText = RequireText(root.Element("payload"), "payload", MaximumArchiveBytes);
            byte[] payloadBytes;
            try
            {
                payloadBytes = Convert.FromBase64String(payloadText);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Dialogue outbox payload is not valid base64.", exception);
            }
            if (!FixedTimeEquals(checksum, ComputeChecksum(payloadBytes)))
                throw new InvalidDataException("Dialogue outbox checksum does not match its payload.");

            var payload = LoadXml(payloadBytes);
            var store = RequireRoot(payload, "dialogueAdmissionOutbox");
            RequireVersion(store, "schema");
            var binding = new DialogueAdmissionBinding(
                ParseGuid(RequireAttribute(store, "storeId"), "storeId"),
                DecodeText(store, "saveId", 256),
                DecodeText(store, "worldId", 256),
                ParseLong(RequireAttribute(store, "generation"), "generation"));
            var savedAt = ParseDate(RequireAttribute(store, "savedAtUtc"));
            var entries = store.Element("entries")?.Elements("entry").Select(DecodeEntry).ToList()
                ?? throw new InvalidDataException("Dialogue outbox entries are missing.");
            if (entries.Count > DialogueAdmissionOutboxSnapshot.MaximumEntries)
                throw new InvalidDataException("Dialogue outbox exceeds its entry limit.");
            return new DialogueAdmissionOutboxSnapshot(binding, savedAt, entries);
        }

        public string ComputePlanHash(DialogueEventAdmissionPlan plan)
        {
            if (plan is null) throw new ArgumentNullException(nameof(plan));
            EnsureFactualOnlyPlan(plan);
            return ComputeEventHash(plan.FactualEvent);
        }

        public string ComputeEventHash(EnvironmentEvent value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            return ComputeChecksum(Utf8.GetBytes(
                EncodeEvent(value).ToString(SaveOptions.DisableFormatting)));
        }

        private XElement EncodeEntry(DialogueAdmissionOutboxEntry entry)
        {
            var actualHash = ComputePlanHash(entry.Plan);
            if (!FixedTimeEquals(actualHash, entry.CanonicalPayloadHash))
                throw new InvalidDataException("Dialogue outbox entry payload hash does not match its plan.");
            return new XElement("entry",
                new XAttribute("id", entry.EntryId.ToString("N")),
                new XAttribute("eventId", entry.EventId.ToString()),
                new XAttribute("expectedGeneration", entry.ExpectedCheckpointGeneration.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("state", entry.State.ToString()),
                entry.CompletedAtGeneration.HasValue
                    ? new XAttribute("completedGeneration", entry.CompletedAtGeneration.Value.ToString(CultureInfo.InvariantCulture))
                    : null,
                new XAttribute("payloadHash", entry.CanonicalPayloadHash),
                EncodeEvent(entry.Plan.FactualEvent));
        }

        private DialogueAdmissionOutboxEntry DecodeEntry(XElement element)
        {
            var eventValue = DecodeEvent(
                element.Element("event") ??
                throw new InvalidDataException("Dialogue outbox entry event is missing."));
            var plan = new DialogueEventAdmissionPlan(
                eventValue,
                new ExperienceJournalRecord(eventValue, null, null));
            var entry = new DialogueAdmissionOutboxEntry(
                ParseGuid(RequireAttribute(element, "id"), "id"),
                plan,
                ParseLong(RequireAttribute(element, "expectedGeneration"), "expectedGeneration"),
                RequireAttribute(element, "payloadHash"),
                ParseEnum<DialogueOutboxEntryState>(RequireAttribute(element, "state"), "state"),
                OptionalLong(element, "completedGeneration"));
            if (entry.EventId != EventId.Parse(RequireAttribute(element, "eventId")))
                throw new InvalidDataException("Dialogue outbox EventId does not match its payload.");
            if (!FixedTimeEquals(entry.CanonicalPayloadHash, ComputePlanHash(plan)))
                throw new InvalidDataException("Dialogue outbox canonical payload hash is invalid.");
            return entry;
        }

        private static void EnsureFactualOnlyPlan(DialogueEventAdmissionPlan plan)
        {
            if (plan.JournalRecord.Perception is not null || plan.JournalRecord.Memory is not null)
                throw new InvalidDataException("Dialogue admission outbox may contain factual evidence only.");
            if (plan.JournalRecord.FactualEvent.Id != plan.FactualEvent.Id)
                throw new InvalidDataException("Dialogue admission journal payload does not match its factual event.");
        }

        private static XElement EncodeEvent(EnvironmentEvent value)
        {
            return new XElement("event",
                new XAttribute("id", value.Id.ToString()),
                new XAttribute("deduplicationKey", value.DeduplicationKey),
                new XAttribute("kind", value.Kind),
                new XAttribute("environment", value.Environment),
                new XAttribute("occurredAtUtc", FormatDate(value.OccurredAtUtc)),
                new XAttribute("observedAtUtc", FormatDate(value.ObservedAtUtc)),
                value.GameTick.HasValue
                    ? new XAttribute("gameTick", value.GameTick.Value.ToString(CultureInfo.InvariantCulture))
                    : null,
                new XAttribute("source", value.Source),
                new XElement("facts", value.FactualPayload
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new XElement(
                        "fact",
                        new XAttribute("key", pair.Key),
                        new XAttribute("value", pair.Value)))),
                new XElement("subjects", value.Subjects
                    .OrderBy(subject => subject.ToString(), StringComparer.Ordinal)
                    .Select(subject => new XElement("subject", new XAttribute("id", subject.ToString())))));
        }

        private static EnvironmentEvent DecodeEvent(XElement element)
        {
            var facts = new Dictionary<string, string>(StringComparer.Ordinal);
            var factElements = element.Element("facts")?.Elements("fact")
                ?? throw new InvalidDataException("Dialogue event facts are missing.");
            foreach (var fact in factElements)
            {
                var key = RequireAttribute(fact, "key");
                if (facts.ContainsKey(key))
                    throw new InvalidDataException("Dialogue event contains duplicate fact keys.");
                facts.Add(key, RequireAttribute(fact, "value", allowEmpty: true));
            }
            var subjects = element.Element("subjects")?.Elements("subject")
                .Select(value => IndividualId.Parse(RequireAttribute(value, "id"))).ToList()
                ?? throw new InvalidDataException("Dialogue event subjects are missing.");
            if (subjects.GroupBy(value => value).Any(group => group.Count() != 1))
                throw new InvalidDataException("Dialogue event contains duplicate subjects.");

            return new EnvironmentEvent(
                EventId.Parse(RequireAttribute(element, "id")),
                RequireAttribute(element, "deduplicationKey"),
                RequireAttribute(element, "kind"),
                RequireAttribute(element, "environment"),
                ParseDate(RequireAttribute(element, "occurredAtUtc")),
                ParseDate(RequireAttribute(element, "observedAtUtc")),
                OptionalLong(element, "gameTick"),
                RequireAttribute(element, "source"),
                facts,
                subjects);
        }

        private static XElement EncodeText(string name, string value) =>
            new XElement(
                name,
                new XAttribute("encoding", "base64-utf8"),
                Convert.ToBase64String(Utf8.GetBytes(value)));

        private static string DecodeText(XElement parent, string name, int maximum)
        {
            var element = parent.Element(name) ??
                throw new InvalidDataException($"Dialogue outbox element '{name}' is missing.");
            if (!string.Equals((string?)element.Attribute("encoding"), "base64-utf8", StringComparison.Ordinal))
                throw new InvalidDataException($"Dialogue outbox element '{name}' has an unsupported encoding.");
            try
            {
                var value = Utf8.GetString(Convert.FromBase64String(element.Value));
                if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
                    throw new InvalidDataException($"Dialogue outbox element '{name}' is invalid.");
                return value;
            }
            catch (Exception exception) when (
                exception is FormatException || exception is DecoderFallbackException)
            {
                throw new InvalidDataException($"Dialogue outbox element '{name}' is invalid.", exception);
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
                throw new InvalidDataException("Dialogue admission outbox XML is invalid.", exception);
            }
        }

        private static XElement RequireRoot(XDocument document, string name)
        {
            if (document.Root is null || document.Root.Name != name)
                throw new InvalidDataException($"Expected dialogue outbox root '{name}'.");
            return document.Root;
        }

        private static void RequireVersion(XElement element, string attribute)
        {
            if (ParseLong(RequireAttribute(element, attribute), attribute) != FormatVersion)
                throw new InvalidDataException($"Unsupported dialogue outbox {attribute}.");
        }

        private static string RequireAttribute(
            XElement element,
            string name,
            bool allowEmpty = false)
        {
            var value = (string?)element.Attribute(name);
            if (value is null || (!allowEmpty && string.IsNullOrWhiteSpace(value)))
                throw new InvalidDataException($"Required attribute '{name}' is missing.");
            if (value.Length > 8192)
                throw new InvalidDataException($"Attribute '{name}' is too long.");
            return value;
        }

        private static string RequireText(XElement? element, string name, int maximum)
        {
            if (element is null || string.IsNullOrWhiteSpace(element.Value))
                throw new InvalidDataException($"Required element '{name}' is missing.");
            if (element.Value.Length > maximum)
                throw new InvalidDataException($"Element '{name}' is too long.");
            return element.Value.Trim();
        }

        private static Guid ParseGuid(string value, string name)
        {
            if (!Guid.TryParse(value, out var result) || result == Guid.Empty)
                throw new InvalidDataException($"Value '{name}' is invalid.");
            return result;
        }

        private static long ParseLong(string value, string name)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < 0)
                throw new InvalidDataException($"Value '{name}' is invalid.");
            return result;
        }

        private static long? OptionalLong(XElement element, string name)
        {
            var value = (string?)element.Attribute(name);
            return string.IsNullOrWhiteSpace(value) ? (long?)null : ParseLong(value!, name);
        }

        private static DateTimeOffset ParseDate(string value)
        {
            if (!DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var result))
                throw new InvalidDataException("Dialogue outbox timestamp is invalid.");
            return result;
        }

        private static T ParseEnum<T>(string value, string name) where T : struct
        {
            if (!Enum.TryParse<T>(value, false, out var result) ||
                !Enum.IsDefined(typeof(T), result))
                throw new InvalidDataException($"Value '{name}' is invalid.");
            return result;
        }

        private static string FormatDate(DateTimeOffset value) =>
            value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        private static string ComputeChecksum(byte[] value)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(value);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var item in hash)
                    builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
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
