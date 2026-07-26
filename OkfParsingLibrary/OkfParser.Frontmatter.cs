using System.Text.Json.Nodes;

namespace OkfParsingLibrary;

public static partial class OkfParser
{
    /// <summary>
    /// Splits a concept file into its frontmatter block and body. Never throws:
    /// missing frontmatter and an unterminated frontmatter block are both
    /// reported through the result fields, not exceptions.
    /// </summary>
    public static FrontmatterSplitResult SplitFrontmatter(string fileContent)
    {
        int idx = 0;
        while (idx < fileContent.Length)
        {
            int eol = fileContent.IndexOf('\n', idx);
            string lineText = eol == -1 ? fileContent.Substring(idx) : fileContent.Substring(idx, eol - idx);
            if (lineText.Trim().Length == 0)
            {
                if (eol == -1) { idx = fileContent.Length; break; }
                idx = eol + 1;
                continue;
            }
            break;
        }

        if (idx >= fileContent.Length)
            return new FrontmatterSplitResult { HasFrontmatter = false, FrontmatterRaw = "", Body = fileContent, ParseError = "" };

        int firstLineEol = fileContent.IndexOf('\n', idx);
        string firstLine = (firstLineEol == -1 ? fileContent.Substring(idx) : fileContent.Substring(idx, firstLineEol - idx)).TrimEnd('\r');
        if (firstLine != "---")
            return new FrontmatterSplitResult { HasFrontmatter = false, FrontmatterRaw = "", Body = fileContent, ParseError = "" };

        int openLineEnd = firstLineEol == -1 ? fileContent.Length : firstLineEol + 1;

        int pos = openLineEnd;
        int closingStart = -1;
        int closingLineEnd = -1;
        while (pos <= fileContent.Length)
        {
            int eol = fileContent.IndexOf('\n', pos);
            string line = (eol == -1 ? fileContent.Substring(pos) : fileContent.Substring(pos, eol - pos)).TrimEnd('\r');
            if (line == "---")
            {
                closingStart = pos;
                closingLineEnd = eol == -1 ? fileContent.Length : eol + 1;
                break;
            }
            if (eol == -1) break;
            pos = eol + 1;
        }

        if (closingStart == -1)
            return new FrontmatterSplitResult
            {
                HasFrontmatter = false,
                FrontmatterRaw = "",
                Body = fileContent,
                ParseError = "opening '---' found but no closing '---' delimiter"
            };

        string frontmatterRaw = fileContent.Substring(openLineEnd, closingStart - openLineEnd);
        string rawBodyAfter = fileContent.Substring(closingLineEnd);
        string body = rawBodyAfter.StartsWith("\r\n") ? rawBodyAfter.Substring(2)
            : rawBodyAfter.StartsWith('\n') ? rawBodyAfter.Substring(1)
            : rawBodyAfter;

        return new FrontmatterSplitResult { HasFrontmatter = true, FrontmatterRaw = frontmatterRaw, Body = body, ParseError = "" };
    }

    /// <summary>
    /// Parses an OKF v0.2 frontmatter block using the constrained YAML subset
    /// documented in YamlSubset.cs. Never throws: any construct outside the
    /// subset is reported via ParsedFrontmatter.ParseError, with every key
    /// parsed before the failure point still routed into the result.
    /// </summary>
    public static ParsedFrontmatter ParseFrontmatter(string frontmatterRaw)
    {
        var result = new ParsedFrontmatter
        {
            Type = "", Title = "", Description = "", Resource = "", TagsCsv = "", Status = "",
            StaleAfter = "", GeneratedBy = "", GeneratedAt = "", LegacyTimestamp = "", OkfVersion = "",
            VerifiedJson = "[]", SourcesJson = "[]", ExtraJson = "{}", ParseError = ""
        };

        var lines = YamlSubsetParser.SplitLines(frontmatterRaw);
        var topLevel = new JsonObject();
        try
        {
            YamlSubsetParser.ParseTopLevelInto(lines, topLevel);
        }
        catch (YamlSubsetException ex)
        {
            result.ParseError = ex.Message;
        }

        var extra = new JsonObject();
        foreach (var kv in topLevel)
        {
            string key = kv.Key;
            JsonNode? value = kv.Value;
            switch (key)
            {
                case "type": result.Type = ScalarOf(value); break;
                case "title": result.Title = ScalarOf(value); break;
                case "description": result.Description = ScalarOf(value); break;
                case "resource": result.Resource = ScalarOf(value); break;
                case "status": result.Status = ScalarOf(value); break;
                case "stale_after": result.StaleAfter = ScalarOf(value); break;
                case "timestamp": result.LegacyTimestamp = ScalarOf(value); break;
                case "okf_version": result.OkfVersion = ScalarOf(value); break;
                case "tags":
                    if (value is JsonArray tagsArr)
                        result.TagsCsv = string.Join(",", tagsArr.Select(n => (n as JsonValue)?.GetValue<string>() ?? ""));
                    break;
                case "generated":
                    if (value is JsonObject genObj)
                    {
                        result.GeneratedBy = genObj["by"] is JsonValue byVal ? byVal.GetValue<string>() : "";
                        result.GeneratedAt = genObj["at"] is JsonValue atVal ? atVal.GetValue<string>() : "";
                    }
                    break;
                case "verified":
                    JsonArray verifiedArr = value switch
                    {
                        JsonArray a => a,
                        JsonObject o => new JsonArray(o.DeepClone()),
                        _ => new JsonArray()
                    };
                    result.VerifiedJson = verifiedArr.ToJsonString();
                    break;
                case "sources":
                    if (value is JsonArray sourcesArr)
                        result.SourcesJson = sourcesArr.ToJsonString();
                    break;
                default:
                    extra[key] = value?.DeepClone();
                    break;
            }
        }

        result.ExtraJson = extra.ToJsonString();
        return result;
    }

    private static string ScalarOf(JsonNode? node) => node is JsonValue v ? v.GetValue<string>() : "";
}
