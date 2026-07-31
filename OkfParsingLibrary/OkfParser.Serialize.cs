using System.Text;
using System.Text.Json.Nodes;

namespace OkfParsingLibrary;

public static partial class OkfParser
{
    /// <summary>
    /// SerializeConcept's input contract: a JSON object whose keys mirror
    /// ParsedFrontmatter field-for-field (type, title, description, resource,
    /// tagsCsv, status, staleAfter, generatedBy, generatedAt, verifiedJson,
    /// sourcesJson, extraJson) plus a "body" string. A caller typically builds
    /// this straight from a ParsedFrontmatter it already has plus the Body from
    /// SplitFrontmatter.
    ///
    /// Two ParsedFrontmatter fields are intentionally NOT part of this contract
    /// and are never re-emitted: okfVersion (§12 of the spec restricts it to a
    /// bundle-root index.md, which is not "a concept") and legacyTimestamp (a
    /// read-only v0.1 fallback superseded by generated.at -- re-serializing a
    /// concept never needs to re-emit it).
    ///
    /// Fields absent or empty in the input are omitted from the output frontmatter
    /// entirely (never emitted as `tags: []` or `status: `). Extra (unrecognised)
    /// keys inside extraJson are re-emitted after the canonical fields, in the
    /// order they appear in the extraJson object -- JsonObject preserves parse
    /// order, so round-tripping ExtraJson straight from ParseFrontmatter keeps
    /// the original key order.
    /// </summary>
    public static string SerializeConcept(string conceptJson)
    {
        var input = JsonNode.Parse(conceptJson)!.AsObject();
        string GetStr(string key) => input[key] is JsonValue v ? v.GetValue<string>() : "";

        var sb = new StringBuilder();
        sb.Append("---\n");

        void EmitScalarField(string yamlKey, string value)
        {
            if (value.Length == 0) return;
            sb.Append(yamlKey).Append(": ").Append(YamlSubsetEmitter.EmitScalar(value)).Append('\n');
        }

        EmitScalarField("type", GetStr("type"));
        EmitScalarField("title", GetStr("title"));
        EmitScalarField("description", GetStr("description"));
        EmitScalarField("resource", GetStr("resource"));

        string tagsCsv = GetStr("tagsCsv");
        if (tagsCsv.Length > 0)
        {
            var tags = SplitTagsForEmit(tagsCsv);
            sb.Append("tags: [").Append(string.Join(", ", tags.Select(EmitTagValue))).Append("]\n");
        }

        string generatedBy = GetStr("generatedBy");
        string generatedAt = GetStr("generatedAt");
        if (generatedBy.Length > 0 || generatedAt.Length > 0)
        {
            var genObj = new JsonObject();
            if (generatedBy.Length > 0) genObj["by"] = generatedBy;
            if (generatedAt.Length > 0) genObj["at"] = generatedAt;
            sb.Append("generated: ").Append(YamlSubsetEmitter.EmitFlowMapping(genObj)).Append('\n');
        }

        if (input["verifiedJson"] is JsonValue verifiedJsonVal)
        {
            var verifiedArr = JsonNode.Parse(verifiedJsonVal.GetValue<string>())?.AsArray();
            if (verifiedArr != null && verifiedArr.Count > 0)
                YamlSubsetEmitter.EmitKeyLine(sb, "", 0, "verified", verifiedArr);
        }

        EmitScalarField("status", GetStr("status"));
        EmitScalarField("stale_after", GetStr("staleAfter"));

        if (input["sourcesJson"] is JsonValue sourcesJsonVal)
        {
            var sourcesArr = JsonNode.Parse(sourcesJsonVal.GetValue<string>())?.AsArray();
            if (sourcesArr != null && sourcesArr.Count > 0)
                YamlSubsetEmitter.EmitKeyLine(sb, "", 0, "sources", sourcesArr);
        }

        if (input["extraJson"] is JsonValue extraJsonVal)
        {
            var extraObj = JsonNode.Parse(extraJsonVal.GetValue<string>())?.AsObject();
            if (extraObj != null)
            {
                foreach (var kv in extraObj)
                    YamlSubsetEmitter.EmitKeyLine(sb, "", 0, kv.Key, kv.Value);
            }
        }

        sb.Append("---\n");
        string body = GetStr("body");
        if (body.Length > 0)
        {
            sb.Append('\n');
            sb.Append(body);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Splits TagsCsv back into individual tag values for emission. A plain
    /// `Split(',')` can't distinguish a real delimiter from a comma that is
    /// part of a single tag's own text (e.g. a tag parsed from a quoted
    /// `"hello, world"`) -- by the time it reaches TagsCsv, the quote marks
    /// that protected it are already gone. The one signal still available:
    /// every genuine delimiter comma is immediately followed by a non-space
    /// character (every real tag is trimmed before being joined with a bare
    /// "," -- see ParseFlowSequence/ParseBlockSequence/the tags case in
    /// OkfParser.Frontmatter.cs), so a comma followed by a space can only be
    /// a tag's own embedded comma, never a delimiter.
    /// </summary>
    private static List<string> SplitTagsForEmit(string tagsCsv)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        for (int i = 0; i < tagsCsv.Length; i++)
        {
            char c = tagsCsv[i];
            if (c == ',' && (i + 1 >= tagsCsv.Length || tagsCsv[i + 1] != ' '))
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        tokens.Add(current.ToString());
        return tokens;
    }

    /// <summary>
    /// Quotes a single tag value if left unquoted it would either change
    /// meaning or fail to survive re-parsing inside a flow sequence: a comma
    /// (indistinguishable from the sequence's own delimiter once unquoted),
    /// a leading/trailing space (silently dropped -- SplitTopLevel's Trim()
    /// runs before Unquote and only quoting protects content inside it), a
    /// leading quote character (would be misread as this scalar starting a
    /// quoted run), or an empty value (an unquoted empty slot is skipped
    /// entirely by ParseFlowSequence, silently dropping the tag). Uses the
    /// same double-quote/backslash-escaping convention YamlSubset.cs's
    /// Unquote already reads on the way in.
    /// </summary>
    private static string EmitTagValue(string tag)
    {
        bool needsQuoting = tag.Length == 0
            || tag.Contains(',')
            || char.IsWhiteSpace(tag[0]) || char.IsWhiteSpace(tag[^1])
            || tag[0] == '"' || tag[0] == '\'';
        if (!needsQuoting) return tag;
        return "\"" + tag.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
