using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OkfParsingLibrary;

/// <summary>
/// Thrown internally when the constrained YAML-subset parser hits a construct
/// it cannot handle. Always caught at the top of ParseFrontmatter and converted
/// into ParsedFrontmatter.ParseError -- never allowed to escape as an exception.
/// </summary>
internal sealed class YamlSubsetException : Exception
{
    public YamlSubsetException(string message) : base(message) { }
}

/// <summary>
/// A constrained recursive-descent parser for the small YAML subset OKF v0.2
/// frontmatter actually uses: scalars (bare/quoted), flow sequences "[a, b]",
/// flow mappings "{ a: b, c: d }", block sequences ("- " items), and block
/// mappings (nested "key: value" lines). This is not a general YAML parser --
/// anything outside this grammar is reported via YamlSubsetException rather
/// than guessed at.
///
/// Every scalar leaf is preserved as a JSON string (never number/bool), since
/// the spec does not require type inference on frontmatter values and doing
/// so would risk lossy round-tripping (e.g. decimal formatting).
/// </summary>
internal static class YamlSubsetParser
{
    private static readonly Regex KeyLine = new(@"^([A-Za-z_][A-Za-z0-9_]*):(.*)$", RegexOptions.Compiled);

    internal readonly record struct Line(int Indent, string Text);

    internal static List<Line> SplitLines(string raw)
    {
        var result = new List<Line>();
        var rawLines = raw.Replace("\r\n", "\n").Split('\n');
        foreach (var rawLine in rawLines)
        {
            if (rawLine.Trim().Length == 0) continue; // blank lines carry no structure in this subset
            int indent = 0;
            while (indent < rawLine.Length && rawLine[indent] == ' ') indent++;
            result.Add(new Line(indent, rawLine.Substring(indent)));
        }
        return result;
    }

    private static bool StartsWithDash(string text) => text.Length >= 1 && text[0] == '-' && (text.Length == 1 || text[1] == ' ');

    /// <summary>
    /// Parses a top-level frontmatter block into `target`, adding one key at a
    /// time. On a YamlSubsetException, whatever keys were added before the
    /// failing one remain in `target` -- callers rely on this for partial
    /// results after a malformed-frontmatter error.
    /// </summary>
    internal static void ParseTopLevelInto(List<Line> lines, JsonObject target)
    {
        int pos = 0;
        while (pos < lines.Count)
        {
            var line = lines[pos];
            if (line.Indent != 0)
                throw new YamlSubsetException($"unexpected indentation at top level near '{line.Text}'");
            var m = KeyLine.Match(line.Text);
            if (!m.Success)
                throw new YamlSubsetException($"expected 'key: value' at top level, found '{line.Text}'");
            string key = m.Groups[1].Value;
            string rest = m.Groups[2].Value;
            pos++;
            var value = ParseValueFor(rest, 0, lines, ref pos);
            target[key] = value;
        }
    }

    // Parses the value belonging to a key whose own line is at `parentIndent`.
    // `inlineRest` is whatever followed the ':' on that same line (may be empty).
    private static JsonNode? ParseValueFor(string inlineRest, int parentIndent, List<Line> lines, ref int pos)
    {
        string trimmed = inlineRest.Trim();
        if (trimmed.Length > 0)
        {
            if (trimmed.StartsWith('[')) return ParseFlowSequence(trimmed);
            if (trimmed.StartsWith('{')) return ParseFlowMapping(trimmed);
            return ScalarNode(trimmed);
        }

        if (pos < lines.Count && lines[pos].Indent > parentIndent)
        {
            int childIndent = lines[pos].Indent;
            if (StartsWithDash(lines[pos].Text))
                return ParseBlockSequence(childIndent, lines, ref pos);
            return ParseMappingBlock(childIndent, lines, ref pos);
        }

        return ScalarNode(""); // key with no inline value and no block continuation
    }

    private static JsonObject ParseMappingBlock(int indent, List<Line> lines, ref int pos)
    {
        var obj = new JsonObject();
        while (pos < lines.Count && lines[pos].Indent == indent && !StartsWithDash(lines[pos].Text))
        {
            var line = lines[pos];
            var m = KeyLine.Match(line.Text);
            if (!m.Success)
                throw new YamlSubsetException($"expected 'key: value' at indent {indent}, found '{line.Text}'");
            string key = m.Groups[1].Value;
            string rest = m.Groups[2].Value;
            pos++;
            obj[key] = ParseValueFor(rest, indent, lines, ref pos);
        }
        return obj;
    }

    private static JsonArray ParseBlockSequence(int indent, List<Line> lines, ref int pos)
    {
        var arr = new JsonArray();
        while (pos < lines.Count && lines[pos].Indent == indent && StartsWithDash(lines[pos].Text))
        {
            string text = lines[pos].Text;
            string content = text.Length > 1 ? text.Substring(2).Trim() : "";
            pos++;

            if (content.Length == 0)
            {
                // Item value lives entirely on more-indented following lines.
                arr.Add(ParseValueFor("", indent, lines, ref pos));
                continue;
            }
            if (content.StartsWith('{')) { arr.Add(ParseFlowMapping(content)); continue; }
            if (content.StartsWith('[')) { arr.Add(ParseFlowSequence(content)); continue; }

            var m = KeyLine.Match(content);
            if (m.Success)
            {
                var obj = new JsonObject();
                string firstKey = m.Groups[1].Value;
                string firstRest = m.Groups[2].Value;
                obj[firstKey] = ParseValueFor(firstRest, indent, lines, ref pos);

                // Continuation lines of the same item: more keys at a deeper,
                // consistent indent. Reuse the generic mapping parser so any
                // further nesting inside an item is handled uniformly.
                if (pos < lines.Count && lines[pos].Indent > indent && !StartsWithDash(lines[pos].Text))
                {
                    int contIndent = lines[pos].Indent;
                    var more = ParseMappingBlock(contIndent, lines, ref pos);
                    foreach (var kv in more) obj[kv.Key] = kv.Value?.DeepClone();
                }
                arr.Add(obj);
            }
            else
            {
                arr.Add(ScalarNode(content));
            }
        }
        return arr;
    }

    private static JsonValue ScalarNode(string raw) => JsonValue.Create(Unquote(raw));

    internal static string Unquote(string raw)
    {
        raw = raw.Trim();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            return raw.Substring(1, raw.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
        if (raw.Length >= 2 && raw[0] == '\'' && raw[^1] == '\'')
            return raw.Substring(1, raw.Length - 2).Replace("''", "'");
        return raw;
    }

    // ---- flow constructs (single physical line) ----

    private static JsonArray ParseFlowSequence(string s)
    {
        int close = FindMatchingBracket(s, 0, '[', ']');
        if (close == -1) throw new YamlSubsetException($"unterminated flow sequence: '{s}'");
        string inner = s.Substring(1, close - 1);
        var arr = new JsonArray();
        foreach (var part in SplitTopLevel(inner, ','))
        {
            var trimmedPart = part.Trim();
            if (trimmedPart.Length == 0) continue;
            arr.Add(ScalarNode(trimmedPart));
        }
        return arr;
    }

    private static JsonObject ParseFlowMapping(string s)
    {
        int close = FindMatchingBracket(s, 0, '{', '}');
        if (close == -1) throw new YamlSubsetException($"unterminated flow mapping: '{s}'");
        string inner = s.Substring(1, close - 1);
        var obj = new JsonObject();
        foreach (var part in SplitTopLevel(inner, ','))
        {
            var trimmedPart = part.Trim();
            if (trimmedPart.Length == 0) continue;
            int colon = FindTopLevelColon(trimmedPart);
            if (colon == -1)
                throw new YamlSubsetException($"expected 'key: value' inside flow mapping, found '{trimmedPart}'");
            string key = trimmedPart.Substring(0, colon).Trim();
            string val = trimmedPart.Substring(colon + 1).Trim();
            JsonNode? valueNode = val.StartsWith('[') ? ParseFlowSequence(val)
                                : val.StartsWith('{') ? ParseFlowMapping(val)
                                : ScalarNode(val);
            obj[key] = valueNode;
        }
        return obj;
    }

    private static int FindMatchingBracket(string s, int openIndex, char open, char close)
    {
        int depth = 0;
        bool inSingle = false, inDouble = false;
        for (int i = openIndex; i < s.Length; i++)
        {
            char c = s[i];
            if (inSingle) { if (c == '\'') inSingle = false; continue; }
            if (inDouble) { if (c == '"' && (i == 0 || s[i - 1] != '\\')) inDouble = false; continue; }
            if (c == '\'') { inSingle = true; continue; }
            if (c == '"') { inDouble = true; continue; }
            if (c == open) depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static int FindTopLevelColon(string s)
    {
        bool inSingle = false, inDouble = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inSingle) { if (c == '\'') inSingle = false; continue; }
            if (inDouble) { if (c == '"' && (i == 0 || s[i - 1] != '\\')) inDouble = false; continue; }
            if (c == '\'') { inSingle = true; continue; }
            if (c == '"') { inDouble = true; continue; }
            if (c == ':') return i;
        }
        return -1;
    }

    private static List<string> SplitTopLevel(string s, char separator)
    {
        var parts = new List<string>();
        int depth = 0;
        bool inSingle = false, inDouble = false;
        int start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inSingle) { if (c == '\'') inSingle = false; continue; }
            if (inDouble) { if (c == '"' && (i == 0 || s[i - 1] != '\\')) inDouble = false; continue; }
            if (c == '\'') { inSingle = true; continue; }
            if (c == '"') { inDouble = true; continue; }
            if (c == '[' || c == '{') depth++;
            else if (c == ']' || c == '}') depth--;
            else if (c == separator && depth == 0)
            {
                parts.Add(s.Substring(start, i - start));
                start = i + 1;
            }
        }
        parts.Add(s.Substring(start));
        return parts;
    }
}

/// <summary>
/// The inverse of YamlSubsetParser: renders a JsonNode tree back into the same
/// constrained YAML subset. Used both for re-emitting ExtraJson keys and for
/// building the verified/sources blocks in SerializeConcept.
/// </summary>
internal static class YamlSubsetEmitter
{
    private static readonly Regex NumberLike = new(@"^-?\d+(\.\d+)?$", RegexOptions.Compiled);
    private static readonly HashSet<string> BoolLike = new(StringComparer.OrdinalIgnoreCase) { "true", "false", "null", "yes", "no" };

    internal static string EmitScalar(string value)
    {
        if (NeedsQuoting(value))
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        return value;
    }

    private static bool NeedsQuoting(string value)
    {
        if (value.Length == 0) return true;
        if (value.Contains('\n')) return true;
        if (value.Contains(": ") || value.EndsWith(':')) return true;
        if (value.Contains('#')) return true;
        char first = value[0];
        if ("[]{}-&*!|>%@`'\"".IndexOf(first) >= 0) return true;
        if (char.IsWhiteSpace(first) || char.IsWhiteSpace(value[^1])) return true;
        if (NumberLike.IsMatch(value)) return true;
        if (BoolLike.Contains(value)) return true;
        return false;
    }

    /// <summary>Emits `key: <value>` (recursing into blocks as needed) at the given indent.</summary>
    internal static void EmitKeyLine(StringBuilder sb, string prefix, int indent, string key, JsonNode? val)
    {
        switch (val)
        {
            case null:
                sb.Append(prefix).Append(key).Append(":\n");
                return;
            case JsonValue sv:
                sb.Append(prefix).Append(key).Append(": ").Append(EmitScalar(sv.GetValue<string>())).Append('\n');
                return;
            case JsonArray arr:
                if (arr.Count == 0 || AllScalars(arr))
                {
                    sb.Append(prefix).Append(key).Append(": ").Append(EmitFlowSequence(arr)).Append('\n');
                    return;
                }
                sb.Append(prefix).Append(key).Append(":\n");
                foreach (var item in arr)
                {
                    if (item is JsonObject iobj)
                        EmitMapping(sb, iobj, indent + 4, new string(' ', indent + 2) + "- ");
                    else
                        sb.Append(new string(' ', indent + 2)).Append("- ").Append(EmitScalar(item?.ToString() ?? "")).Append('\n');
                }
                return;
            case JsonObject obj:
                if (AllScalars(obj))
                {
                    sb.Append(prefix).Append(key).Append(": ").Append(EmitFlowMapping(obj)).Append('\n');
                    return;
                }
                sb.Append(prefix).Append(key).Append(":\n");
                EmitMapping(sb, obj, indent + 2);
                return;
        }
    }

    internal static void EmitMapping(StringBuilder sb, JsonObject obj, int indent, string? firstLinePrefix = null)
    {
        bool first = true;
        foreach (var kv in obj)
        {
            string prefix = (first && firstLinePrefix != null) ? firstLinePrefix : new string(' ', indent);
            first = false;
            EmitKeyLine(sb, prefix, indent, kv.Key, kv.Value);
        }
    }

    private static bool AllScalars(JsonArray arr) => arr.All(n => n is JsonValue);
    private static bool AllScalars(JsonObject obj) => obj.All(kv => kv.Value is JsonValue);

    internal static string EmitFlowSequence(JsonArray arr) =>
        "[" + string.Join(", ", arr.Select(n => EmitScalar((n as JsonValue)?.GetValue<string>() ?? n?.ToString() ?? ""))) + "]";

    internal static string EmitFlowMapping(JsonObject obj) =>
        "{ " + string.Join(", ", obj.Select(kv => $"{kv.Key}: {EmitScalar((kv.Value as JsonValue)?.GetValue<string>() ?? kv.Value?.ToString() ?? "")}")) + " }";
}
