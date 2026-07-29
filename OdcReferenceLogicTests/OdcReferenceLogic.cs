namespace OdcReferenceLogicTests;

// This file simulates logic that lives in OutSystems ODC's visual flow builder
// (server actions ResolveCrossBundleLink / NormalizeRelativeConceptPath), NOT
// logic in the shipped OkfParsingLibrary. It exists purely to de-risk hand-
// translating this logic into ODC flows before building it there for real.
// It is not part of, and does not reference, the shipped library.
public static class OdcReferenceLogic
{
    public static (int? TargetBundleId, int? TargetConceptId, bool IsResolved) ResolveCrossBundleLinkReference(
        string targetBundleName,
        string targetConceptPath,
        List<(int Id, string Name)> fakeBundles,
        List<(int Id, int BundleId, string ConceptPath)> fakeConcepts)
    {
        int? bundleId = null;
        foreach (var bundle in fakeBundles)
        {
            if (bundle.Name == targetBundleName)
            {
                bundleId = bundle.Id;
                break;
            }
        }

        if (bundleId is null)
        {
            return (null, null, false);
        }

        int? conceptId = null;
        foreach (var concept in fakeConcepts)
        {
            if (concept.BundleId == bundleId.Value && concept.ConceptPath == targetConceptPath)
            {
                conceptId = concept.Id;
                break;
            }
        }

        if (conceptId is null)
        {
            return (bundleId, null, false);
        }

        return (bundleId, conceptId, true);
    }

    public static string NormalizeRelativeConceptPathReference(string sourceConceptPath, string rawLinkPath)
    {
        var lastSlash = sourceConceptPath.LastIndexOf('/');
        var sourceDir = lastSlash >= 0 ? sourceConceptPath[..lastSlash] : "";
        var joined = sourceDir.Length > 0 ? $"{sourceDir}/{rawLinkPath}" : rawLinkPath;

        var stack = new List<string>();
        foreach (var segment in joined.Split('/'))
        {
            if (segment == ".")
            {
                continue;
            }
            else if (segment == "..")
            {
                if (stack.Count > 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                }
            }
            else
            {
                stack.Add(segment);
            }
        }

        var result = string.Join("/", stack);
        if (result.EndsWith(".md"))
        {
            result = result[..^3];
        }

        return result;
    }
}
