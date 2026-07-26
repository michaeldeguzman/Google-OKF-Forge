using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OkfParsingLibrary;

public static partial class OkfParser
{
    private const char BomChar = '\uFEFF';

    /// <summary>
    /// Extracts every .md entry from a zip archive. Never throws: a corrupt or
    /// empty zip simply yields an empty array. If every retained entry shares
    /// one common top-level directory segment (the shape of a GitHub archive
    /// download), that segment is stripped from every path.
    /// </summary>
    public static OkfFileEntry[] UnzipBundle(byte[] archive)
    {
        try
        {
            using var ms = new MemoryStream(archive);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            var raw = new List<(string Path, byte[] Bytes)>();
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue; // directory entry
                string normalized = entry.FullName.Replace('\\', '/');
                if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;

                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                raw.Add((normalized, buffer.ToArray()));
            }

            if (raw.Count == 0) return Array.Empty<OkfFileEntry>();

            string? commonPrefix = FindCommonTopSegment(raw.Select(r => r.Path));

            var result = new OkfFileEntry[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                string path = raw[i].Path;
                if (commonPrefix != null) path = path.Substring(commonPrefix.Length + 1);

                string content = Encoding.UTF8.GetString(raw[i].Bytes);
                if (content.StartsWith(BomChar)) content = content.Substring(1);

                string lastSegment = path.Contains('/') ? path.Substring(path.LastIndexOf('/') + 1) : path;
                string reservedKind = lastSegment.Equals("index.md", StringComparison.OrdinalIgnoreCase) ? "index"
                    : lastSegment.Equals("log.md", StringComparison.OrdinalIgnoreCase) ? "log"
                    : "";

                result[i] = new OkfFileEntry { Path = path, Content = content, ReservedKind = reservedKind };
            }
            return result;
        }
        catch
        {
            return Array.Empty<OkfFileEntry>();
        }
    }

    private static string? FindCommonTopSegment(IEnumerable<string> paths)
    {
        string? candidate = null;
        foreach (var path in paths)
        {
            int slash = path.IndexOf('/');
            if (slash < 0) return null; // a root-level file breaks any common wrapping folder
            string segment = path.Substring(0, slash);
            if (candidate == null) candidate = segment;
            else if (candidate != segment) return null;
        }
        return candidate;
    }

    /// <summary>
    /// Builds a zip archive from a JSON array of {"path": "...", "content": "..."} objects.
    /// </summary>
    public static byte[] BuildBundleArchive(string filesJson)
    {
        var files = JsonNode.Parse(filesJson)!.AsArray();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var fileNode in files)
            {
                if (fileNode == null) continue;
                string path = fileNode["path"]!.GetValue<string>();
                string content = fileNode["content"]!.GetValue<string>();
                var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }
        return ms.ToArray();
    }
}
