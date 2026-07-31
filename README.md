# OkfParsingLibrary

A stateless C# library that parses and serialises [Open Knowledge Format
(OKF)](https://github.com/GoogleCloudPlatform/knowledge-catalog) v0.2 bundles,
plus an adapter for using it as OutSystems Developer Cloud (ODC) External
Logic.

OKF is a bundle format for documenting data/analytics assets (tables,
metrics, joins, computations, ...) as Markdown files with YAML frontmatter.
The format is specified in `okf/SPEC.md` in
[GoogleCloudPlatform/knowledge-catalog](https://github.com/GoogleCloudPlatform/knowledge-catalog).

## What's here

- **`OkfParsingLibrary`** — the core library. No persistence, no HTTP, no
  OutSystems dependency of any kind. Every public function on `OkfParser` is
  pure and never throws: malformed input is reported through a result field
  (`ParseError`, an empty array, etc.), never an exception.
  - `YamlSubset.cs` — a hand-written recursive-descent parser/emitter for the
    small YAML subset OKF frontmatter actually uses (scalars, quoted scalars,
    flow sequences/mappings, block sequences/mappings, folded plain scalars).
  - `OkfParser.*.cs` — one `public static partial class`, split by concern:
    archive (zip) handling, frontmatter split/parse, link extraction,
    serialisation, and SPEC conformance checks.
- **`OkfParsingLibrary.OdcExternalLogic`** — a thin `[OSInterface]` adapter
  over the core library, for ODC Server Actions to call. Depends on
  `OutSystems.ExternalLibraries.SDK`; the core library deliberately doesn't,
  so it stays a plain, independently-testable .NET library.
- **`OkfParsingLibrary.Tests`** — the xUnit suite, including a full round-trip
  test against all four canonical Google OKF bundles (`acme_retail`,
  `crypto_bitcoin`, `ga4`, `stackoverflow` — see [Fixture provenance](#fixture-provenance)
  below).
- **`OdcReferenceLogicTests`** — a standalone reference implementation of two
  small pieces of logic (`ResolveCrossBundleLink`, `NormalizeRelativeConceptPath`)
  that live in ODC's own visual flow builder, not in the shipped library. It
  exists purely to de-risk hand-translating that logic into ODC flows before
  building it there for real — it doesn't reference `OkfParsingLibrary`.

## Requirements

.NET 10 SDK (all four projects target `net10.0`).

## Build & test

```bash
dotnet build                                    # build all four projects (OkfParsingLibrary.slnx)
dotnet test                                     # run the full test suite
dotnet test --filter "FullyQualifiedName~RoundTripTests"   # run one test class
```

## Deploying to ODC

Package `OkfParsingLibrary.OdcExternalLogic` for upload via ODC Portal ->
External Logic -> Upload:

```bash
dotnet publish OkfParsingLibrary.OdcExternalLogic/OkfParsingLibrary.OdcExternalLogic.csproj -c Release -o OkfParsingLibrary.OdcExternalLogic/publish
cd OkfParsingLibrary.OdcExternalLogic/publish && zip -r ../../OkfParsingLibrary-ODC.zip . && cd ../..
```

Zip the *contents* of `publish/`, not the `publish/` folder itself — ODC
requires the DLLs at the zip root.

ODC's documented recommendation (at time of writing) is `net8.0`. If a Portal
upload rejects `net10.0`, retarget `OkfParsingLibrary.OdcExternalLogic` (and
its `OkfParsingLibrary` reference) to `net8.0` and republish — check current
ODC docs for the supported target framework first.

## Design notes

- **Never throws.** Every `OkfParser` function reports failure through a
  result field, never an exception — callers (including ODC flows) never need
  a try/catch around a parsing call.
- **Round-trip contract.** `SerializeConcept`'s input mirrors `ParsedFrontmatter`
  field-for-field (plus a `body` string) and is guaranteed to parse back to an
  *equivalent* `ParsedFrontmatter` — not byte-identical output, but the same
  parsed meaning. See the doc comment at the top of `OkfParser.Serialize.cs`.
- **Unrecognised frontmatter keys** (anything outside the documented OKF
  fields) round-trip losslessly through `ExtraJson`, since `YamlSubset.cs`'s
  block/flow parsing is fully generic rather than hand-coded per key.

## Fixture provenance

`OkfParsingLibrary.Tests/Fixtures/` contains real, unmodified sample OKF
bundles pulled directly from
[GoogleCloudPlatform/knowledge-catalog](https://github.com/GoogleCloudPlatform/knowledge-catalog)'s
`okf/bundles/` directory (`acme_retail`, `crypto_bitcoin`, `ga4`,
`stackoverflow`), used as realistic test data — not hand-written fixtures.

## License

[BSD 2-Clause](LICENSE).
