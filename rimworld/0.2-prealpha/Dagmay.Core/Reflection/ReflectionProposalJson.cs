using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Reflection
{
    public static class ReflectionProposalJson
    {
        public const string ProviderCompatibleSchema =
            "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"schemaVersion\",\"requestId\",\"individualId\",\"baseStateVersion\",\"evidenceEventIds\",\"confidence\",\"interpretation\",\"autobiographicalReflection\",\"decisionSummary\",\"targetAffect\"],\"properties\":{"
            + "\"schemaVersion\":{\"type\":\"integer\",\"enum\":[1]},"
            + "\"requestId\":{\"type\":\"string\",\"description\":\"Copy the supplied 32-character request ID exactly.\"},"
            + "\"individualId\":{\"type\":\"string\",\"description\":\"Copy the supplied 32-character individual ID exactly.\"},"
            + "\"baseStateVersion\":{\"type\":\"integer\",\"minimum\":0},"
            + "\"evidenceEventIds\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":100,\"items\":{\"type\":\"string\"}},"
            + "\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1},"
            + "\"interpretation\":{\"type\":\"string\",\"description\":\"A concise subjective interpretation, with uncertainty where appropriate.\"},"
            + "\"autobiographicalReflection\":{\"type\":\"string\",\"description\":\"A concise first-person reflection grounded only in supplied evidence.\"},"
            + "\"decisionSummary\":{\"type\":\"string\",\"description\":\"One or two sentences naming causal factors and uncertainty, never hidden reasoning.\"},"
            + "\"targetAffect\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"valence\",\"arousal\",\"threat\",\"agency\",\"attachment\",\"certainty\",\"socialStanding\"],\"properties\":{"
            + "\"valence\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1},"
            + "\"arousal\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1},"
            + "\"threat\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1},"
            + "\"agency\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1},"
            + "\"attachment\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1},"
            + "\"certainty\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1},"
            + "\"socialStanding\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1}}}}}";

        private static readonly string[] RootMembers =
        {
            "schemaVersion",
            "requestId",
            "individualId",
            "baseStateVersion",
            "evidenceEventIds",
            "confidence",
            "interpretation",
            "autobiographicalReflection",
            "decisionSummary",
            "targetAffect"
        };

        private static readonly string[] AffectMembers =
        {
            "valence",
            "arousal",
            "threat",
            "agency",
            "attachment",
            "certainty",
            "socialStanding"
        };

        public static string CreateProviderCompatibleSchema(
            AffectVector currentAffect,
            double maximumDimensionChange = 0.25)
        {
            if (currentAffect is null) throw new ArgumentNullException(nameof(currentAffect));
            if (double.IsNaN(maximumDimensionChange)
                || double.IsInfinity(maximumDimensionChange)
                || maximumDimensionChange <= 0
                || maximumDimensionChange > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDimensionChange));
            }

            var builder = new StringBuilder(ProviderCompatibleSchema.Length + 256);
            builder.Append("{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"schemaVersion\",\"requestId\",\"individualId\",\"baseStateVersion\",\"evidenceEventIds\",\"confidence\",\"interpretation\",\"autobiographicalReflection\",\"decisionSummary\",\"targetAffect\"],\"properties\":{");
            builder.Append("\"schemaVersion\":{\"type\":\"integer\",\"enum\":[1]},");
            builder.Append("\"requestId\":{\"type\":\"string\",\"description\":\"Copy the supplied 32-character request ID exactly.\"},");
            builder.Append("\"individualId\":{\"type\":\"string\",\"description\":\"Copy the supplied 32-character individual ID exactly.\"},");
            builder.Append("\"baseStateVersion\":{\"type\":\"integer\",\"minimum\":0},");
            builder.Append("\"evidenceEventIds\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":100,\"items\":{\"type\":\"string\"}},");
            builder.Append("\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1},");
            builder.Append("\"interpretation\":{\"type\":\"string\",\"description\":\"A concise subjective interpretation, with uncertainty where appropriate.\"},");
            builder.Append("\"autobiographicalReflection\":{\"type\":\"string\",\"description\":\"A concise first-person reflection grounded only in supplied evidence.\"},");
            builder.Append("\"decisionSummary\":{\"type\":\"string\",\"description\":\"One or two sentences naming causal factors and uncertainty, never hidden reasoning.\"},");
            builder.Append("\"targetAffect\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"valence\",\"arousal\",\"threat\",\"agency\",\"attachment\",\"certainty\",\"socialStanding\"],\"properties\":{");
            AppendBoundedAffectSchema(builder, "valence", currentAffect.Valence, maximumDimensionChange, false);
            AppendBoundedAffectSchema(builder, "arousal", currentAffect.Arousal, maximumDimensionChange, true);
            AppendBoundedAffectSchema(builder, "threat", currentAffect.Threat, maximumDimensionChange, true);
            AppendBoundedAffectSchema(builder, "agency", currentAffect.Agency, maximumDimensionChange, true);
            AppendBoundedAffectSchema(builder, "attachment", currentAffect.Attachment, maximumDimensionChange, true);
            AppendBoundedAffectSchema(builder, "certainty", currentAffect.Certainty, maximumDimensionChange, true);
            AppendBoundedAffectSchema(builder, "socialStanding", currentAffect.SocialStanding, maximumDimensionChange, true);
            builder.Append("}}}}");
            return builder.ToString();
        }

        public static ReflectionProposal Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Reflection response is empty.");
            if (json.Length > 100_000) throw new InvalidDataException("Reflection response is too large.");

            var root = RequireObject(new StrictJsonParser(json).Parse(), "root");
            RequireExactMembers(root, RootMembers, "reflection proposal");
            var schemaVersion = RequireLong(root, "schemaVersion");
            if (schemaVersion != SchemaVersions.ReflectionProposal)
            {
                throw new InvalidDataException("Reflection proposal schema version is unsupported.");
            }

            var requestId = ParseRequestId(RequireString(root, "requestId"));
            var individualId = ParseIndividualId(RequireString(root, "individualId"));
            var baseStateVersion = RequireLong(root, "baseStateVersion");
            if (baseStateVersion < 0) throw new InvalidDataException("Base state version is invalid.");

            var evidenceValues = RequireArray(root, "evidenceEventIds");
            if (evidenceValues.Count == 0 || evidenceValues.Count > 100)
            {
                throw new InvalidDataException("Evidence event count is invalid.");
            }

            var evidence = new List<EventId>(evidenceValues.Count);
            var uniqueEvidence = new HashSet<EventId>();
            foreach (var value in evidenceValues)
            {
                var id = ParseEventId(RequireStringValue(value, "evidence event ID"));
                if (!uniqueEvidence.Add(id)) throw new InvalidDataException("Evidence event IDs must be unique.");
                evidence.Add(id);
            }

            var affectObject = RequireObject(RequireMember(root, "targetAffect"), "targetAffect");
            RequireExactMembers(affectObject, AffectMembers, "target affect");
            var affect = new AffectVector(
                RequireDouble(affectObject, "valence"),
                RequireDouble(affectObject, "arousal"),
                RequireDouble(affectObject, "threat"),
                RequireDouble(affectObject, "agency"),
                RequireDouble(affectObject, "attachment"),
                RequireDouble(affectObject, "certainty"),
                RequireDouble(affectObject, "socialStanding"));

            return new ReflectionProposal(
                requestId,
                individualId,
                baseStateVersion,
                evidence,
                RequireDouble(root, "confidence"),
                RequireBoundedString(root, "interpretation", 2000),
                RequireBoundedString(root, "autobiographicalReflection", 2000),
                RequireBoundedString(root, "decisionSummary", 2000),
                affect);
        }

        public static string Serialize(ReflectionProposal value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            var builder = new StringBuilder(2048);
            builder.Append('{');
            AppendName(builder, "schemaVersion");
            builder.Append(value.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendName(builder, "requestId");
            AppendString(builder, value.RequestId.ToString());
            builder.Append(',');
            AppendName(builder, "individualId");
            AppendString(builder, value.IndividualId.ToString());
            builder.Append(',');
            AppendName(builder, "baseStateVersion");
            builder.Append(value.BaseStateVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendName(builder, "evidenceEventIds");
            builder.Append('[');
            for (var index = 0; index < value.EvidenceEventIds.Count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendString(builder, value.EvidenceEventIds[index].ToString());
            }

            builder.Append(']');
            builder.Append(',');
            AppendName(builder, "confidence");
            AppendDouble(builder, value.Confidence);
            builder.Append(',');
            AppendName(builder, "interpretation");
            AppendString(builder, value.Interpretation);
            builder.Append(',');
            AppendName(builder, "autobiographicalReflection");
            AppendString(builder, value.AutobiographicalReflection);
            builder.Append(',');
            AppendName(builder, "decisionSummary");
            AppendString(builder, value.DecisionSummary);
            builder.Append(',');
            AppendName(builder, "targetAffect");
            builder.Append('{');
            AppendAffect(builder, "valence", value.TargetAffect.Valence, false);
            AppendAffect(builder, "arousal", value.TargetAffect.Arousal, true);
            AppendAffect(builder, "threat", value.TargetAffect.Threat, true);
            AppendAffect(builder, "agency", value.TargetAffect.Agency, true);
            AppendAffect(builder, "attachment", value.TargetAffect.Attachment, true);
            AppendAffect(builder, "certainty", value.TargetAffect.Certainty, true);
            AppendAffect(builder, "socialStanding", value.TargetAffect.SocialStanding, true);
            builder.Append("}}");
            return builder.ToString();
        }

        private static void AppendAffect(StringBuilder builder, string name, double value, bool comma)
        {
            if (comma) builder.Append(',');
            AppendName(builder, name);
            AppendDouble(builder, value);
        }

        private static void AppendBoundedAffectSchema(
            StringBuilder builder,
            string name,
            double current,
            double maximumChange,
            bool comma)
        {
            if (comma) builder.Append(',');
            AppendName(builder, name);
            builder.Append("{\"type\":\"number\",\"minimum\":");
            AppendDouble(builder, Math.Max(-1, current - maximumChange));
            builder.Append(",\"maximum\":");
            AppendDouble(builder, Math.Min(1, current + maximumChange));
            builder.Append('}');
        }

        private static void AppendName(StringBuilder builder, string name)
        {
            AppendString(builder, name);
            builder.Append(':');
        }

        private static void AppendDouble(StringBuilder builder, double value)
        {
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
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
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
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

        private static object RequireMember(IDictionary<string, object> value, string name)
        {
            if (!value.TryGetValue(name, out var member)) throw new InvalidDataException($"Required JSON member '{name}' is missing.");
            return member;
        }

        private static string RequireString(IDictionary<string, object> value, string name)
        {
            return RequireStringValue(RequireMember(value, name), name);
        }

        private static string RequireBoundedString(IDictionary<string, object> value, string name, int maximum)
        {
            var result = RequireString(value, name);
            if (string.IsNullOrWhiteSpace(result) || result.Length > maximum)
            {
                throw new InvalidDataException($"JSON member '{name}' has invalid text length.");
            }

            return result.Trim();
        }

        private static string RequireStringValue(object value, string name)
        {
            if (!(value is string result)) throw new InvalidDataException($"JSON member '{name}' must be a string.");
            return result;
        }

        private static long RequireLong(IDictionary<string, object> value, string name)
        {
            var member = RequireMember(value, name);
            if (!(member is long result)) throw new InvalidDataException($"JSON member '{name}' must be an integer.");
            return result;
        }

        private static double RequireDouble(IDictionary<string, object> value, string name)
        {
            var member = RequireMember(value, name);
            if (member is double floating) return floating;
            if (member is long integer) return integer;
            throw new InvalidDataException($"JSON member '{name}' must be a number.");
        }

        private static IDictionary<string, object> RequireObject(object value, string name)
        {
            if (!(value is IDictionary<string, object> result))
            {
                throw new InvalidDataException($"JSON member '{name}' must be an object.");
            }

            return result;
        }

        private static IList<object> RequireArray(IDictionary<string, object> value, string name)
        {
            var member = RequireMember(value, name);
            if (!(member is IList<object> result)) throw new InvalidDataException($"JSON member '{name}' must be an array.");
            return result;
        }

        private static void RequireExactMembers(
            IDictionary<string, object> value,
            IEnumerable<string> expected,
            string name)
        {
            var allowed = new HashSet<string>(expected, StringComparer.Ordinal);
            if (value.Count != allowed.Count) throw new InvalidDataException($"The {name} has missing or additional members.");
            foreach (var member in value.Keys)
            {
                if (!allowed.Contains(member)) throw new InvalidDataException($"The {name} contains unsupported member '{member}'.");
            }
        }

        private static RequestId ParseRequestId(string value)
        {
            if (!Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty) throw new InvalidDataException("Request ID is invalid.");
            return new RequestId(id);
        }

        private static IndividualId ParseIndividualId(string value)
        {
            if (!Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty) throw new InvalidDataException("Individual ID is invalid.");
            return new IndividualId(id);
        }

        private static EventId ParseEventId(string value)
        {
            if (!Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty) throw new InvalidDataException("Evidence event ID is invalid.");
            return new EventId(id);
        }

        internal sealed class StrictJsonParser
        {
            private const int MaximumDepth = 32;
            private readonly string _source;
            private int _index;

            public StrictJsonParser(string source)
            {
                _source = source;
            }

            public object Parse()
            {
                SkipWhitespace();
                var value = ParseValue(0);
                SkipWhitespace();
                if (_index != _source.Length) throw Error("Unexpected data follows the JSON value.");
                return value;
            }

            private object ParseValue(int depth)
            {
                if (depth > MaximumDepth) throw Error("JSON nesting exceeds the supported depth.");
                if (_index >= _source.Length) throw Error("JSON ended unexpectedly.");
                switch (_source[_index])
                {
                    case '{': return ParseObject(depth + 1);
                    case '[': return ParseArray(depth + 1);
                    case '"': return ParseString();
                    case 't': ParseLiteral("true"); return true;
                    case 'f': ParseLiteral("false"); return false;
                    case 'n': ParseLiteral("null"); return NullValue.Instance;
                    default:
                        if (_source[_index] == '-' || IsDigit(_source[_index])) return ParseNumber();
                        throw Error("Unexpected JSON token.");
                }
            }

            private IDictionary<string, object> ParseObject(int depth)
            {
                Expect('{');
                SkipWhitespace();
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                if (TryConsume('}')) return result;
                while (true)
                {
                    if (_index >= _source.Length || _source[_index] != '"') throw Error("JSON object member name is missing.");
                    var name = ParseString();
                    if (result.ContainsKey(name)) throw Error($"Duplicate JSON member '{name}'.");
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    result.Add(name, ParseValue(depth));
                    SkipWhitespace();
                    if (TryConsume('}')) return result;
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private IList<object> ParseArray(int depth)
            {
                Expect('[');
                SkipWhitespace();
                var result = new List<object>();
                if (TryConsume(']')) return result;
                while (true)
                {
                    result.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (TryConsume(']')) return result;
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private string ParseString()
            {
                Expect('"');
                var builder = new StringBuilder();
                while (_index < _source.Length)
                {
                    var character = _source[_index++];
                    if (character == '"') return builder.ToString();
                    if (character < 0x20) throw Error("Control characters are not allowed in JSON strings.");
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (_index >= _source.Length) throw Error("JSON escape ended unexpectedly.");
                    var escaped = _source[_index++];
                    switch (escaped)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ParseUnicodeEscape()); break;
                        default: throw Error("JSON string escape is invalid.");
                    }
                }

                throw Error("JSON string is unterminated.");
            }

            private char ParseUnicodeEscape()
            {
                if (_index + 4 > _source.Length) throw Error("Unicode escape is incomplete.");
                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var digit = HexValue(_source[_index++]);
                    if (digit < 0) throw Error("Unicode escape is invalid.");
                    value = (value * 16) + digit;
                }

                return (char)value;
            }

            private object ParseNumber()
            {
                var start = _index;
                TryConsume('-');
                if (_index >= _source.Length) throw Error("JSON number ended unexpectedly.");
                if (TryConsume('0'))
                {
                    if (_index < _source.Length && IsDigit(_source[_index])) throw Error("JSON number has a leading zero.");
                }
                else
                {
                    RequireDigits();
                }

                var floating = false;
                if (TryConsume('.'))
                {
                    floating = true;
                    RequireDigits();
                }

                if (_index < _source.Length && (_source[_index] == 'e' || _source[_index] == 'E'))
                {
                    floating = true;
                    _index++;
                    if (_index < _source.Length && (_source[_index] == '+' || _source[_index] == '-')) _index++;
                    RequireDigits();
                }

                var text = _source.Substring(start, _index - start);
                if (!floating && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
                {
                    return integer;
                }

                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    || double.IsNaN(number)
                    || double.IsInfinity(number))
                {
                    throw Error("JSON number is invalid.");
                }

                return number;
            }

            private void RequireDigits()
            {
                var start = _index;
                while (_index < _source.Length && IsDigit(_source[_index])) _index++;
                if (_index == start) throw Error("JSON number requires a digit.");
            }

            private void ParseLiteral(string literal)
            {
                if (_index + literal.Length > _source.Length
                    || !string.Equals(_source.Substring(_index, literal.Length), literal, StringComparison.Ordinal))
                {
                    throw Error("JSON literal is invalid.");
                }

                _index += literal.Length;
            }

            private void SkipWhitespace()
            {
                while (_index < _source.Length)
                {
                    var character = _source[_index];
                    if (character != ' ' && character != '\t' && character != '\r' && character != '\n') break;
                    _index++;
                }
            }

            private void Expect(char expected)
            {
                if (!TryConsume(expected)) throw Error($"Expected '{expected}'.");
            }

            private bool TryConsume(char expected)
            {
                if (_index >= _source.Length || _source[_index] != expected) return false;
                _index++;
                return true;
            }

            private InvalidDataException Error(string message)
            {
                return new InvalidDataException($"{message} Position {_index.ToString(CultureInfo.InvariantCulture)}.");
            }

            private static bool IsDigit(char value) => value >= '0' && value <= '9';

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9') return value - '0';
                if (value >= 'a' && value <= 'f') return value - 'a' + 10;
                if (value >= 'A' && value <= 'F') return value - 'A' + 10;
                return -1;
            }

            private sealed class NullValue
            {
                public static readonly NullValue Instance = new NullValue();
                private NullValue() { }
            }
        }
    }
}
