using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;

namespace Dagmay.Core.Persistence
{
    public sealed class ExperienceJournalRecord
    {
        public ExperienceJournalRecord(
            EnvironmentEvent factualEvent,
            PerceivedEvent? perception,
            SubjectiveMemory? memory)
        {
            FactualEvent = factualEvent ?? throw new ArgumentNullException(nameof(factualEvent));
            if (memory is not null && perception is null)
            {
                throw new ArgumentException("A subjective memory requires its source perception.", nameof(memory));
            }

            if (perception is not null && perception.SourceEventId != factualEvent.Id)
            {
                throw new ArgumentException("Perception must reference the factual event in the same journal record.", nameof(perception));
            }

            if (memory is not null && !Contains(memory.SourcePerceptionIds, perception!.Id))
            {
                throw new ArgumentException("Memory must reference the perception in the same journal record.", nameof(memory));
            }

            Perception = perception;
            Memory = memory;
        }

        public EnvironmentEvent FactualEvent { get; }
        public PerceivedEvent? Perception { get; }
        public SubjectiveMemory? Memory { get; }

        private static bool Contains(IReadOnlyList<PerceptionId> values, PerceptionId expected)
        {
            foreach (var value in values)
            {
                if (value == expected) return true;
            }

            return false;
        }
    }

    public enum ExperienceJournalLoadStatus
    {
        Loaded,
        NotFound,
        Invalid
    }

    public sealed class ExperienceJournalLoadResult
    {
        public ExperienceJournalLoadResult(
            ExperienceJournalLoadStatus status,
            IEnumerable<ExperienceJournalRecord> records,
            string lastHash,
            string diagnostic,
            IEnumerable<string>? entryHashes = null)
        {
            Status = status;
            Records = new ReadOnlyCollection<ExperienceJournalRecord>(
                new List<ExperienceJournalRecord>(records ?? throw new ArgumentNullException(nameof(records))));
            LastHash = lastHash ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
            EntryHashes = new ReadOnlyCollection<string>(
                new List<string>(entryHashes ?? Enumerable.Empty<string>()));
        }

        public ExperienceJournalLoadStatus Status { get; }
        public IReadOnlyList<ExperienceJournalRecord> Records { get; }
        public string LastHash { get; }
        public string Diagnostic { get; }
        public IReadOnlyList<string> EntryHashes { get; }
    }

    public sealed class ExperienceJournalAppendResult
    {
        public ExperienceJournalAppendResult(long position, string entryHash)
        {
            Position = position;
            EntryHash = entryHash;
        }

        public long Position { get; }
        public string EntryHash { get; }
    }

    public enum ExperienceJournalRollbackStatus
    {
        AlreadyExact,
        RestoredExactPrefix
    }

    public sealed class ExperienceJournalRollbackResult
    {
        public ExperienceJournalRollbackResult(ExperienceJournalRollbackStatus status, string preservedFuturePath, string diagnostic)
        {
            Status = status;
            PreservedFuturePath = preservedFuturePath ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public ExperienceJournalRollbackStatus Status { get; }
        public string PreservedFuturePath { get; }
        public string Diagnostic { get; }
    }
    public sealed class DurableExperienceJournal
    {
        public const int MaximumRecordCount = 100000;
        public const int MaximumLineCharacters = 128 * 1024;
        public const long MaximumJournalBytes = 128L * 1024L * 1024L;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public ExperienceJournalAppendResult Append(
            string path,
            ExperienceJournalRecord record,
            long expectedPosition,
            string expectedLastHash)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Journal path is required.", nameof(path));
            if (record is null) throw new ArgumentNullException(nameof(record));
            if (expectedPosition < 0 || expectedPosition >= MaximumRecordCount) throw new ArgumentOutOfRangeException(nameof(expectedPosition));
            if (expectedLastHash is null) throw new ArgumentNullException(nameof(expectedLastHash));
            if (expectedPosition == 0 && expectedLastHash.Length != 0)
            {
                throw new InvalidOperationException("An empty journal cannot have a previous hash.");
            }

            var payload = Convert.ToBase64String(EncodeRecord(record));
            var position = expectedPosition + 1;
            var hashInput = HashInput(position, expectedLastHash, payload);
            var entryHash = ComputeChecksum(Utf8.GetBytes(hashInput));
            var line = position.ToString(CultureInfo.InvariantCulture)
                + "\t" + expectedLastHash
                + "\t" + payload
                + "\t" + entryHash;
            if (line.Length > MaximumLineCharacters)
            {
                throw new InvalidOperationException("Experience journal record exceeds the maximum supported size.");
            }

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Experience journal directory is invalid.");
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, Utf8))
            {
                writer.WriteLine(line);
                writer.Flush();
                stream.Flush();
            }

            return new ExperienceJournalAppendResult(position, entryHash);
        }

        public ExperienceJournalLoadResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Journal path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return new ExperienceJournalLoadResult(
                    ExperienceJournalLoadStatus.NotFound,
                    Array.Empty<ExperienceJournalRecord>(),
                    string.Empty,
                    "No experience journal exists yet.");
            }

            try
            {
                var file = new FileInfo(fullPath);
                if (file.Length > MaximumJournalBytes) throw new InvalidDataException("Experience journal exceeds the maximum supported size.");

                var records = new List<ExperienceJournalRecord>();
                var entryHashes = new List<string>();
                var lastHash = string.Empty;
                var expectedPosition = 1L;
                foreach (var line in File.ReadLines(fullPath, Utf8))
                {
                    if (records.Count >= MaximumRecordCount) throw new InvalidDataException("Experience journal exceeds the record limit.");
                    if (string.IsNullOrWhiteSpace(line) || line.Length > MaximumLineCharacters)
                    {
                        throw new InvalidDataException("Experience journal contains an empty or oversized record.");
                    }

                    var parts = line.Split('\t');
                    if (parts.Length != 4) throw new InvalidDataException("Experience journal record shape is invalid.");
                    if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var position)
                        || position != expectedPosition)
                    {
                        throw new InvalidDataException("Experience journal sequence is not continuous.");
                    }

                    if (!FixedTimeEquals(parts[1], lastHash))
                    {
                        throw new InvalidDataException("Experience journal previous-hash link is invalid.");
                    }

                    var actualHash = ComputeChecksum(Utf8.GetBytes(HashInput(position, parts[1], parts[2])));
                    if (!FixedTimeEquals(parts[3], actualHash))
                    {
                        throw new InvalidDataException("Experience journal entry checksum is invalid.");
                    }

                    byte[] payload;
                    try
                    {
                        payload = Convert.FromBase64String(parts[2]);
                    }
                    catch (FormatException exception)
                    {
                        throw new InvalidDataException("Experience journal payload is not valid base64.", exception);
                    }

                    records.Add(DecodeRecord(payload));
                    lastHash = parts[3];
                    entryHashes.Add(lastHash);
                    expectedPosition++;
                }

                return new ExperienceJournalLoadResult(
                    ExperienceJournalLoadStatus.Loaded,
                    records,
                    lastHash,
                    "Experience journal loaded and its complete hash chain was verified.",
                    entryHashes);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is InvalidDataException
                || exception is XmlException
                || exception is ArgumentException)
            {
                return new ExperienceJournalLoadResult(
                    ExperienceJournalLoadStatus.Invalid,
                    Array.Empty<ExperienceJournalRecord>(),
                    string.Empty,
                    "Experience journal was rejected: " + exception.Message);
            }
        }

        public ExperienceJournalRollbackResult RestoreVerifiedPrefix(
            string path,
            long expectedPosition,
            string expectedLastHash)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Journal path is required.", nameof(path));
            if (expectedPosition < 0) throw new ArgumentOutOfRangeException(nameof(expectedPosition));
            if (expectedLastHash is null) throw new ArgumentNullException(nameof(expectedLastHash));
            var loaded = Load(path);
            if (loaded.Status != ExperienceJournalLoadStatus.Loaded)
                throw new InvalidDataException("Only a completely verified experience journal can be restored.");
            if (loaded.Records.Count < expectedPosition)
                throw new InvalidDataException("Experience journal is behind the requested save checkpoint.");
            var actualPrefixHash = expectedPosition == 0
                ? string.Empty
                : loaded.EntryHashes[checked((int)expectedPosition - 1)];
            if (!FixedTimeEquals(actualPrefixHash, expectedLastHash))
                throw new InvalidDataException("Experience journal prefix does not match the requested save checkpoint hash.");
            if (loaded.Records.Count == expectedPosition)
            {
                if (!FixedTimeEquals(loaded.LastHash, expectedLastHash))
                    throw new InvalidDataException("Experience journal head does not match the requested save checkpoint hash.");
                return new ExperienceJournalRollbackResult(
                    ExperienceJournalRollbackStatus.AlreadyExact,
                    string.Empty,
                    "Experience journal already equals the RimWorld save checkpoint.");
            }

            var fullPath = Path.GetFullPath(path);
            var originalBytes = File.ReadAllBytes(fullPath);
            var artifactPath = GetRollbackArtifactPath(
                fullPath,
                expectedPosition,
                expectedLastHash,
                loaded.Records.Count,
                loaded.LastHash);
            ImmutableCheckpointFile.Preserve(artifactPath, originalBytes);

            var prefixLength = PrefixByteLength(originalBytes, expectedPosition);
            var temporaryPath = fullPath + ".rollback.tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(originalBytes, 0, prefixLength);
                    stream.Flush();
                }
                File.Replace(temporaryPath, fullPath, null, true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }

            var verified = Load(fullPath);
            if (verified.Status != ExperienceJournalLoadStatus.Loaded
                || verified.Records.Count != expectedPosition
                || !FixedTimeEquals(verified.LastHash, expectedLastHash))
                throw new InvalidDataException("Restored experience journal failed exact checkpoint verification.");
            return new ExperienceJournalRollbackResult(
                ExperienceJournalRollbackStatus.RestoredExactPrefix,
                artifactPath,
                "Experience journal was restored to the exact RimWorld save prefix; the verified future head was preserved immutably.");
        }

        public string GetRollbackArtifactPath(
            string path,
            long expectedPosition,
            string expectedLastHash,
            long futurePosition,
            string futureLastHash)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Experience recovery directory is invalid.");
            var journalName = Path.GetFileNameWithoutExtension(fullPath);
            var artifactKey = ComputeChecksum(Utf8.GetBytes(
                expectedPosition.ToString(CultureInfo.InvariantCulture) + "\n" + expectedLastHash + "\n"
                + futurePosition.ToString(CultureInfo.InvariantCulture) + "\n" + futureLastHash));
            return Path.Combine(directory, "Recovery", journalName,
                "p" + expectedPosition.ToString(CultureInfo.InvariantCulture)
                + "-h" + futurePosition.ToString(CultureInfo.InvariantCulture)
                + "-" + artifactKey + ".journal");
        }

        private static int PrefixByteLength(byte[] bytes, long position)
        {
            if (position == 0) return 0;
            var lines = 0L;
            for (var index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] != (byte)'\n') continue;
                lines++;
                if (lines == position) return index + 1;
            }
            throw new InvalidDataException("Verified journal bytes do not contain the requested prefix boundary.");
        }
        private static byte[] EncodeRecord(ExperienceJournalRecord record)
        {
            var document = new XDocument(
                new XElement("experience",
                    new XAttribute("schema", SchemaVersions.ExperienceJournal),
                    EncodeEvent(record.FactualEvent),
                    record.Perception is null ? null : EncodePerception(record.Perception),
                    record.Memory is null ? null : EncodeMemory(record.Memory)));
            return Utf8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
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
                value.GameTick.HasValue ? new XAttribute("gameTick", value.GameTick.Value.ToString(CultureInfo.InvariantCulture)) : null,
                new XAttribute("source", value.Source),
                new XElement("facts", value.FactualPayload.Select(pair =>
                    new XElement("fact", new XAttribute("key", pair.Key), new XAttribute("value", pair.Value)))),
                new XElement("subjects", value.Subjects.Select(subject =>
                    new XElement("subject", new XAttribute("id", subject.ToString())))));
        }

        private static XElement EncodePerception(PerceivedEvent value)
        {
            return new XElement("perception",
                new XAttribute("id", value.Id.ToString()),
                new XAttribute("sourceEventId", value.SourceEventId.ToString()),
                new XAttribute("perceiverId", value.PerceiverId.ToString()),
                new XAttribute("channel", value.Channel.ToString()),
                new XAttribute("confidence", FormatDouble(value.Confidence)),
                new XAttribute("stateVersion", value.StateVersionAtPerception.ToString(CultureInfo.InvariantCulture)),
                new XElement("details", value.AvailableDetails.Select(pair =>
                    new XElement("detail", new XAttribute("key", pair.Key), new XAttribute("value", pair.Value)))),
                new XElement("omitted", value.IntentionallyOmittedFields.Select(field =>
                    new XElement("field", new XAttribute("name", field)))));
        }

        private static XElement EncodeMemory(SubjectiveMemory value)
        {
            return new XElement("memory",
                new XAttribute("id", value.Id.ToString()),
                new XAttribute("ownerId", value.OwnerId.ToString()),
                new XAttribute("occurredAtUtc", FormatDate(value.OccurredAtUtc)),
                new XAttribute("encodedAtUtc", FormatDate(value.EncodedAtUtc)),
                new XAttribute("importance", FormatDouble(value.Importance)),
                new XAttribute("emotionalWeight", FormatDouble(value.EmotionalWeight)),
                new XAttribute("confidence", FormatDouble(value.Confidence)),
                new XAttribute("accessibility", FormatDouble(value.Accessibility)),
                new XAttribute("tier", value.Tier.ToString()),
                new XAttribute("privacy", value.Privacy.ToString()),
                new XElement("diary", value.ConciseDiaryEntry),
                new XElement("appraisal", value.Appraisal),
                new XElement("affect",
                    new XAttribute("valence", FormatDouble(value.AffectAtEncoding.Valence)),
                    new XAttribute("arousal", FormatDouble(value.AffectAtEncoding.Arousal)),
                    new XAttribute("threat", FormatDouble(value.AffectAtEncoding.Threat)),
                    new XAttribute("agency", FormatDouble(value.AffectAtEncoding.Agency)),
                    new XAttribute("attachment", FormatDouble(value.AffectAtEncoding.Attachment)),
                    new XAttribute("certainty", FormatDouble(value.AffectAtEncoding.Certainty)),
                    new XAttribute("socialStanding", FormatDouble(value.AffectAtEncoding.SocialStanding))),
                new XElement("sourcePerceptions", value.SourcePerceptionIds.Select(source =>
                    new XElement("source", new XAttribute("id", source.ToString())))),
                new XElement("people", value.PeopleInvolved.Select(person =>
                    new XElement("person", new XAttribute("id", person.ToString())))));
        }

        private static ExperienceJournalRecord DecodeRecord(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumLineCharacters
            }))
            {
                var document = XDocument.Load(reader, LoadOptions.None);
                var root = document.Root;
                if (root is null || root.Name != "experience") throw new InvalidDataException("Experience payload root is invalid.");
                if (ParseLong(Required(root, "schema"), "schema") != SchemaVersions.ExperienceJournal)
                {
                    throw new InvalidDataException("Experience payload schema is unsupported.");
                }

                var eventElement = root.Element("event") ?? throw new InvalidDataException("Experience event is missing.");
                var factualEvent = DecodeEvent(eventElement);
                var perceptionElement = root.Element("perception");
                var memoryElement = root.Element("memory");
                var perception = perceptionElement is null ? null : DecodePerception(perceptionElement);
                var memory = memoryElement is null ? null : DecodeMemory(memoryElement);
                return new ExperienceJournalRecord(factualEvent, perception, memory);
            }
        }

        private static EnvironmentEvent DecodeEvent(XElement element)
        {
            long? gameTick = null;
            var gameTickText = Optional(element, "gameTick");
            if (gameTickText is not null) gameTick = ParseLong(gameTickText, "gameTick");
            var facts = element.Element("facts")?.Elements("fact").ToDictionary(
                fact => Required(fact, "key"),
                fact => Required(fact, "value"),
                StringComparer.Ordinal) ?? new Dictionary<string, string>();
            var subjects = element.Element("subjects")?.Elements("subject")
                .Select(subject => IndividualId.Parse(Required(subject, "id")))
                ?? Enumerable.Empty<IndividualId>();
            return new EnvironmentEvent(
                EventId.Parse(Required(element, "id")),
                Required(element, "deduplicationKey"),
                Required(element, "kind"),
                Required(element, "environment"),
                ParseDate(Required(element, "occurredAtUtc")),
                ParseDate(Required(element, "observedAtUtc")),
                gameTick,
                Required(element, "source"),
                facts,
                subjects);
        }

        private static PerceivedEvent DecodePerception(XElement element)
        {
            var details = element.Element("details")?.Elements("detail").ToDictionary(
                detail => Required(detail, "key"),
                detail => Required(detail, "value"),
                StringComparer.Ordinal) ?? new Dictionary<string, string>();
            var omitted = element.Element("omitted")?.Elements("field")
                .Select(field => Required(field, "name")) ?? Enumerable.Empty<string>();
            return new PerceivedEvent(
                PerceptionId.Parse(Required(element, "id")),
                EventId.Parse(Required(element, "sourceEventId")),
                IndividualId.Parse(Required(element, "perceiverId")),
                ParseEnum<PerceptionChannel>(Required(element, "channel"), "channel"),
                ParseDouble(Required(element, "confidence"), "confidence"),
                details,
                omitted,
                ParseLong(Required(element, "stateVersion"), "stateVersion"));
        }

        private static SubjectiveMemory DecodeMemory(XElement element)
        {
            var affect = element.Element("affect") ?? throw new InvalidDataException("Memory affect is missing.");
            var sourcePerceptions = element.Element("sourcePerceptions")?.Elements("source")
                .Select(source => PerceptionId.Parse(Required(source, "id")))
                ?? Enumerable.Empty<PerceptionId>();
            var people = element.Element("people")?.Elements("person")
                .Select(person => IndividualId.Parse(Required(person, "id")))
                ?? Enumerable.Empty<IndividualId>();
            return new SubjectiveMemory(
                MemoryId.Parse(Required(element, "id")),
                IndividualId.Parse(Required(element, "ownerId")),
                sourcePerceptions,
                ParseDate(Required(element, "occurredAtUtc")),
                ParseDate(Required(element, "encodedAtUtc")),
                RequiredElementText(element, "diary"),
                RequiredElementText(element, "appraisal"),
                new AffectVector(
                    ParseDouble(Required(affect, "valence"), "valence"),
                    ParseDouble(Required(affect, "arousal"), "arousal"),
                    ParseDouble(Required(affect, "threat"), "threat"),
                    ParseDouble(Required(affect, "agency"), "agency"),
                    ParseDouble(Required(affect, "attachment"), "attachment"),
                    ParseDouble(Required(affect, "certainty"), "certainty"),
                    ParseDouble(Required(affect, "socialStanding"), "socialStanding")),
                ParseDouble(Required(element, "importance"), "importance"),
                ParseDouble(Required(element, "emotionalWeight"), "emotionalWeight"),
                ParseDouble(Required(element, "confidence"), "confidence"),
                ParseDouble(Required(element, "accessibility"), "accessibility"),
                ParseEnum<MemoryTier>(Required(element, "tier"), "tier"),
                ParseEnum<PrivacyClassification>(Required(element, "privacy"), "privacy"),
                people);
        }

        private static string HashInput(long position, string previousHash, string payload)
        {
            return position.ToString(CultureInfo.InvariantCulture) + "\n" + previousHash + "\n" + payload;
        }

        private static string ComputeChecksum(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
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

        private static string Required(XElement element, string name)
        {
            var value = (string?)element.Attribute(name);
            if (value is null || string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"Required attribute '{name}' is missing.");
            if (value.Length > 4096) throw new InvalidDataException($"Attribute '{name}' is too long.");
            return value;
        }

        private static string? Optional(XElement element, string name)
        {
            var value = (string?)element.Attribute(name);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string RequiredElementText(XElement element, string name)
        {
            var child = element.Element(name);
            if (child is null || string.IsNullOrWhiteSpace(child.Value)) throw new InvalidDataException($"Required element '{name}' is missing.");
            if (child.Value.Length > 4096) throw new InvalidDataException($"Element '{name}' is too long.");
            return child.Value;
        }

        private static long ParseLong(string value, string name)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < 0)
            {
                throw new InvalidDataException($"Value '{name}' is invalid.");
            }

            return result;
        }

        private static double ParseDouble(string value, string name)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                || double.IsNaN(result)
                || double.IsInfinity(result))
            {
                throw new InvalidDataException($"Value '{name}' is invalid.");
            }

            return result;
        }

        private static DateTimeOffset ParseDate(string value)
        {
            if (!DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            {
                throw new InvalidDataException("Experience timestamp is invalid.");
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
        private static string FormatDouble(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
