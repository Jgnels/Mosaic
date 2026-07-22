using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Lifecycle;

namespace Dagmay.Core.Persistence
{
    public sealed class IdentityArchiveCodec
    {
        public const int FormatVersion = 1;
        public const int MaximumArchiveBytes = 16 * 1024 * 1024;
        public const int MaximumIdentityCount = 1000;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public byte[] Encode(IdentityArchiveSnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Records.Count > MaximumIdentityCount)
            {
                throw new InvalidOperationException("Identity archive exceeds the supported identity count.");
            }

            var payload = new XDocument(
                new XElement("identityArchive",
                    new XAttribute("schema", FormatVersion),
                    new XAttribute("storeId", snapshot.StoreId.ToString("N")),
                    new XAttribute("generation", snapshot.Generation.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("savedAtUtc", snapshot.SavedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                    snapshot.Records.Select(EncodeRecord)));

            var payloadBytes = Utf8.GetBytes(payload.ToString(SaveOptions.DisableFormatting));
            var checksum = ComputeChecksum(payloadBytes);
            var envelope = new XDocument(
                new XElement("dagmayIdentityStore",
                    new XAttribute("format", FormatVersion),
                    new XElement("checksum", checksum),
                    new XElement("payload", Convert.ToBase64String(payloadBytes))));
            var result = Utf8.GetBytes(envelope.ToString(SaveOptions.DisableFormatting));
            if (result.Length > MaximumArchiveBytes)
            {
                throw new InvalidOperationException("Encoded identity archive exceeds the maximum supported size.");
            }

            return result;
        }

        public IdentityArchiveSnapshot Decode(byte[] encoded)
        {
            if (encoded is null) throw new ArgumentNullException(nameof(encoded));
            if (encoded.Length == 0 || encoded.Length > MaximumArchiveBytes)
            {
                throw new InvalidDataException("Identity archive size is invalid.");
            }

            var envelope = LoadXml(encoded);
            var root = RequireElement(envelope, "dagmayIdentityStore");
            RequireVersion(root, "format");
            var expectedChecksum = RequireText(root.Element("checksum"), "checksum", 128);
            var encodedPayload = RequireText(root.Element("payload"), "payload", MaximumArchiveBytes);

            byte[] payloadBytes;
            try
            {
                payloadBytes = Convert.FromBase64String(encodedPayload);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Identity archive payload is not valid base64.", exception);
            }

            var actualChecksum = ComputeChecksum(payloadBytes);
            if (!FixedTimeEquals(expectedChecksum, actualChecksum))
            {
                throw new InvalidDataException("Identity archive checksum does not match its payload.");
            }

            var payload = LoadXml(payloadBytes);
            var archive = RequireElement(payload, "identityArchive");
            RequireVersion(archive, "schema");
            var storeId = ParseGuid(RequireAttribute(archive, "storeId"));
            var generation = ParseLong(RequireAttribute(archive, "generation"), "generation");
            var savedAt = ParseDate(RequireAttribute(archive, "savedAtUtc"));
            var recordElements = archive.Elements("identity").ToList();
            if (recordElements.Count > MaximumIdentityCount)
            {
                throw new InvalidDataException("Identity archive exceeds the supported identity count.");
            }

            return new IdentityArchiveSnapshot(storeId, generation, savedAt, recordElements.Select(DecodeRecord));
        }

        private static XElement EncodeRecord(PersistedIdentityRecord record)
        {
            var state = record.State;
            return new XElement("identity",
                new XAttribute("externalId", record.ExternalEntityId),
                new XAttribute("id", state.Id.ToString()),
                new XAttribute("lineageId", state.LineageId.ToString()),
                new XAttribute("version", state.Version.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("displayName", state.DisplayName),
                new XAttribute("lifecycle", state.Lifecycle.ToString()),
                new XElement("seed",
                    new XAttribute("environment", state.Seed.Environment),
                    new XAttribute("adapterVersion", state.Seed.AdapterVersion),
                    state.Seed.Facts.Select(fact =>
                        new XElement("fact",
                            new XAttribute("category", fact.Category.ToString()),
                            new XAttribute("key", fact.Key),
                            new XAttribute("value", fact.Value),
                            new XAttribute("source", fact.Source),
                            new XAttribute("confidence", FormatDouble(fact.Confidence))))),
                new XElement("continuity",
                    new XAttribute("groundedSeed", FormatDouble(state.Continuity.GroundedSeed)),
                    new XAttribute("autobiographicalHistory", FormatDouble(state.Continuity.AutobiographicalHistory)),
                    new XAttribute("relationships", FormatDouble(state.Continuity.Relationships)),
                    new XAttribute("valuesAndCommitments", FormatDouble(state.Continuity.ValuesAndCommitments)),
                    new XAttribute("expressionAndHabits", FormatDouble(state.Continuity.ExpressionAndHabits))),
                new XElement("affect",
                    new XAttribute("valence", FormatDouble(state.Affect.Valence)),
                    new XAttribute("arousal", FormatDouble(state.Affect.Arousal)),
                    new XAttribute("threat", FormatDouble(state.Affect.Threat)),
                    new XAttribute("agency", FormatDouble(state.Affect.Agency)),
                    new XAttribute("attachment", FormatDouble(state.Affect.Attachment)),
                    new XAttribute("certainty", FormatDouble(state.Affect.Certainty)),
                    new XAttribute("socialStanding", FormatDouble(state.Affect.SocialStanding))));
        }

        private static PersistedIdentityRecord DecodeRecord(XElement element)
        {
            var externalId = RequireAttribute(element, "externalId");
            var id = IndividualId.Parse(RequireAttribute(element, "id"));
            var lineageId = LineageId.Parse(RequireAttribute(element, "lineageId"));
            var version = ParseLong(RequireAttribute(element, "version"), "version");
            var name = RequireAttribute(element, "displayName");
            var lifecycle = ParseEnum<LifecycleState>(RequireAttribute(element, "lifecycle"), "lifecycle");

            var seedElement = element.Element("seed") ?? throw new InvalidDataException("Identity seed is missing.");
            var facts = seedElement.Elements("fact").Select(fact => new SeedFact(
                ParseEnum<SeedFactCategory>(RequireAttribute(fact, "category"), "category"),
                RequireAttribute(fact, "key"),
                RequireAttribute(fact, "value"),
                RequireAttribute(fact, "source"),
                ParseDouble(RequireAttribute(fact, "confidence"), "confidence")));
            var seed = new IdentitySeed(
                RequireAttribute(seedElement, "environment"),
                RequireAttribute(seedElement, "adapterVersion"),
                facts);

            var continuityElement = element.Element("continuity")
                ?? throw new InvalidDataException("Continuity profile is missing.");
            var continuity = new ContinuityProfile(
                ParseDouble(RequireAttribute(continuityElement, "groundedSeed"), "groundedSeed"),
                ParseDouble(RequireAttribute(continuityElement, "autobiographicalHistory"), "autobiographicalHistory"),
                ParseDouble(RequireAttribute(continuityElement, "relationships"), "relationships"),
                ParseDouble(RequireAttribute(continuityElement, "valuesAndCommitments"), "valuesAndCommitments"),
                ParseDouble(RequireAttribute(continuityElement, "expressionAndHabits"), "expressionAndHabits"));

            var affectElement = element.Element("affect") ?? throw new InvalidDataException("Affect vector is missing.");
            var affect = new AffectVector(
                ParseDouble(RequireAttribute(affectElement, "valence"), "valence"),
                ParseDouble(RequireAttribute(affectElement, "arousal"), "arousal"),
                ParseDouble(RequireAttribute(affectElement, "threat"), "threat"),
                ParseDouble(RequireAttribute(affectElement, "agency"), "agency"),
                ParseDouble(RequireAttribute(affectElement, "attachment"), "attachment"),
                ParseDouble(RequireAttribute(affectElement, "certainty"), "certainty"),
                ParseDouble(RequireAttribute(affectElement, "socialStanding"), "socialStanding"));

            var state = IndividualState.Restore(
                id, lineageId, version, name, lifecycle, seed, continuity, affect);
            return new PersistedIdentityRecord(externalId, state);
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
                throw new InvalidDataException("Identity archive XML is invalid.", exception);
            }
        }

        private static XElement RequireElement(XDocument document, string name)
        {
            if (document.Root is null || document.Root.Name != name)
            {
                throw new InvalidDataException($"Expected identity archive root '{name}'.");
            }

            return document.Root;
        }

        private static void RequireVersion(XElement element, string attribute)
        {
            var version = ParseLong(RequireAttribute(element, attribute), attribute);
            if (version != FormatVersion)
            {
                throw new InvalidDataException($"Unsupported identity archive {attribute} '{version}'.");
            }
        }

        private static string RequireAttribute(XElement element, string name)
        {
            var value = (string?)element.Attribute(name);
            if (value is null || string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"Required attribute '{name}' is missing.");
            if (value.Length > 4096) throw new InvalidDataException($"Attribute '{name}' is too long.");
            return value;
        }

        private static string RequireText(XElement? element, string name, int maximumLength)
        {
            if (element is null || string.IsNullOrWhiteSpace(element.Value))
            {
                throw new InvalidDataException($"Required element '{name}' is missing.");
            }

            if (element.Value.Length > maximumLength) throw new InvalidDataException($"Element '{name}' is too long.");
            return element.Value.Trim();
        }

        private static Guid ParseGuid(string value)
        {
            if (!Guid.TryParse(value, out var result) || result == Guid.Empty)
            {
                throw new InvalidDataException("Store ID is invalid.");
            }

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
            if (!DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var result))
            {
                throw new InvalidDataException("Archive timestamp is invalid.");
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

        private static string FormatDouble(double value) => value.ToString("R", CultureInfo.InvariantCulture);

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
    }
}
