namespace OdcReferenceLogicTests;

// Tests for the reference implementation of ODC's NormalizeRelativeConceptPath
// server action (see OdcReferenceLogic.cs). Not a test of the shipped
// OkfParsingLibrary. ConceptPath values in rows 1-4 are drawn from the real
// acme_retail fixture bundle used elsewhere in this repo's test suite
// (OkfParsingLibrary.Tests/Fixtures/acme_retail/).
public class NormalizeRelativeConceptPathReferenceTests
{
    [Theory]
    [InlineData("metrics/revenue", "../computations/revenue-ytd.md", "computations/revenue-ytd")]
    [InlineData("metrics/gross-margin-legacy", "./gross-margin.md", "metrics/gross-margin")]
    [InlineData("computations/gross-margin-period", "./revenue-ytd.md", "computations/revenue-ytd")]
    [InlineData("tables/orders", "sibling.md", "tables/sibling")]
    // Double ".." and above-root cases are new ground (never verified by hand or
    // by execution before this harness) - these two rows document the ACTUAL
    // output of the spec'd algorithm, not a pre-assumed "correct" answer.
    [InlineData("a/b/c", "../../x.md", "x")]
    [InlineData("metrics/revenue", "../../../x.md", "x")]
    public void NormalizesAsExpected(string sourceConceptPath, string rawLinkPath, string expected)
    {
        var result = OdcReferenceLogic.NormalizeRelativeConceptPathReference(sourceConceptPath, rawLinkPath);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AbsolutePathPassedInByMistake_DegradesSafelyRatherThanThrowing()
    {
        // This input should never occur in real use - ResolveIntraBundleLink is
        // supposed to catch a leading "/" and route to the bundle-absolute path
        // before this function is ever called. This test only confirms the
        // function doesn't throw and doesn't silently produce something that
        // looks like a valid, misleadingly-plausible concept path.
        var result = OdcReferenceLogic.NormalizeRelativeConceptPathReference(
            "policies/revenue-recognition",
            "/tables/orders.md"
        );

        // Actual output is "policies//tables/orders" (double slash) - the leading
        // "/" in rawLinkPath produces an empty split segment that gets pushed onto
        // the stack literally (it is neither "." nor ".."), rejoining as "//".
        // Still not a match for any real ConceptPath (verified against every path
        // in the acme_retail fixtures), so a downstream lookup still fails safely
        // rather than resolving to the wrong concept - but it is a structurally
        // malformed path, not clean nonsense. Documented here, not silently fixed.
        Assert.Equal("policies//tables/orders", result);
    }
}
