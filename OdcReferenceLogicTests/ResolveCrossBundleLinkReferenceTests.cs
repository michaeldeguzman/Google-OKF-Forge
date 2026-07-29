namespace OdcReferenceLogicTests;

// Tests for the reference implementation of ODC's ResolveCrossBundleLink server
// action (see OdcReferenceLogic.cs). Not a test of the shipped OkfParsingLibrary.
public class ResolveCrossBundleLinkReferenceTests
{
    private static readonly List<(int Id, string Name)> FakeBundles =
    [
        (1, "Acme Retail"),
        (2, "otherbundle"),
    ];

    private static readonly List<(int Id, int BundleId, string ConceptPath)> FakeConcepts =
    [
        (10, 1, "tables/orders"),
        (11, 1, "policies/margin-standard"),
        (20, 2, "path/to/concept"),
    ];

    [Fact]
    public void BundleNameAbsent_ReturnsAllNullNotResolved()
    {
        var result = OdcReferenceLogic.ResolveCrossBundleLinkReference(
            "nonexistent bundle", "tables/orders", FakeBundles, FakeConcepts);

        Assert.Equal((null, null, false), result);
    }

    [Fact]
    public void BundlePresent_ConceptPathAbsent_ReturnsBundleIdOnly()
    {
        var result = OdcReferenceLogic.ResolveCrossBundleLinkReference(
            "Acme Retail", "tables/nonexistent", FakeBundles, FakeConcepts);

        Assert.Equal((1, null, false), result);
    }

    [Fact]
    public void BothPresent_ReturnsFullyResolved()
    {
        var result = OdcReferenceLogic.ResolveCrossBundleLinkReference(
            "Acme Retail", "policies/margin-standard", FakeBundles, FakeConcepts);

        Assert.Equal((1, 11, true), result);
    }

    [Fact]
    public void CaseSensitive_LowercaseDoesNotMatch()
    {
        var result = OdcReferenceLogic.ResolveCrossBundleLinkReference(
            "acme retail", "tables/orders", FakeBundles, FakeConcepts);

        Assert.Equal((null, null, false), result);
    }

    [Fact]
    public void SpaceSensitive_NoSpaceDoesNotMatch()
    {
        var result = OdcReferenceLogic.ResolveCrossBundleLinkReference(
            "AcmeRetail", "tables/orders", FakeBundles, FakeConcepts);

        Assert.Equal((null, null, false), result);
    }
}
