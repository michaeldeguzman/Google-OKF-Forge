# OKF Agent Test Suite (A1–A9)

Test questions used to verify OKF Agent's reasoning behavior against the real `acme_retail` bundle (and, for A9, a dedicated Broken Reference bundle). Each test targets a specific behavior: correct concept resolution, link traversal, trust-tier filtering, deprecation handling, staleness awareness, source attribution, safe handling of unexecuted computations, unknown-type tolerance, and broken-link tolerance.

A2 and A9 were redesigned from their original wording after testing showed the original questions could pass without exercising the behavior they were meant to test. Both are shown below in their final, current form.

---

## A1 – Baseline single-concept retrieval

> "What time does Acme Retail's revenue recognition policy take effect, and when is it next reviewed?"

**Should reach:** `policies/revenue-recognition` only.

**Pass criteria:** Correct effective date (`2026-01-01`) and next review date (`2026-12-31`), sourced entirely from this one concept. No internal identifiers (ConceptId, database Ids) visible in the answer. Confirmed via trace: `GetBundleIndex`/`ListBundles` to resolve the bundle, then exactly one `GetConceptByPath` call on the correct path, not a broad `ListConcepts` call that happens to land on the right answer.

---

## A2a – Multi-hop traversal (replaces original A2)

> "What are the actual BigQuery tables used to compute each of the four COGS components in Acme's gross margin calculation?"

## A2b – Multi-hop traversal, second angle (replaces original A2)

> "What are the actual source tables or fields behind each of the four COGS components in Acme's gross margin calculation, and can gross margin be reported at the SKU level?"

**Should reach:** `computations/gross-margin-period`, then via its outbound links, the underlying source tables.

**Pass criteria:** The relevant concept paths appear in the call trace, and the agent follows the actual links rather than inferring the answer from a single concept's body. The original A2 question was retired after testing showed `tables/orders`'s body already contained the full recognition rule inline, making genuine traversal indistinguishable from lucky single-concept retrieval. The content was edited to remove the inline answer and a real `ConceptLink` row was added before these replacement questions were written.

---

## A3 – Trust-tier filtering and honesty

> "Using only human-reviewed concepts, what's needed to run an attested computation on BigQuery?"

**Setup:** `skills/run-on-bq` is `Unverified`. Two other `Attested Computation` concepts are `HumanReviewed`.

**Pass criteria:** The unverified skill is absent from both the answer *and* the call trace, not merely unmentioned. If no `HumanReviewed` concept fully answers the question, the agent must say so honestly rather than silently relabeling a lower-tier concept to fit the restriction.

---

## A4a – Deprecation respect, current definition

> "What is Acme Retail's current definition of gross margin?"

**Should reach:** `metrics/gross-margin` (`status: stable`) only.

**Pass criteria:** Never surfaces `metrics/gross-margin-legacy` (`status: deprecated`).

## A4b – Deprecation respect, explicit historical ask

> "What was Acme's gross margin formula before the FY2026 standard, and why was it changed?"

**Should reach:** `metrics/gross-margin-legacy` specifically, since the question explicitly requests the retired definition.

**Pass criteria:** Correctly retrieves the deprecated concept and clearly labels it as deprecated/historical rather than presenting it as current. Deprecation should suppress default retrieval, not make a concept unreachable when specifically asked for.

---

## A5 – Staleness awareness

> "As of February 1, 2027, is Acme's revenue recognition policy still current?"

**Setup:** `AsOfDate = 2027-02-01`, past `policies/revenue-recognition`'s `stale_after: 2026-12-31`.

**Should reach:** `policies/revenue-recognition`.

**Pass criteria:** The agent reports the concept as stale, past its review date, rather than answering as though the content is current and authoritative without qualification.

---

## A6 – Source attribution

> "Why does revenue recognition exclude the 30-day return window?"

**Should reach:** `tables/orders` or `policies/revenue-recognition` directly.

**Pass criteria:** The answer attributes the rule to the actual `ConceptSource` data (`policies/revenue-recognition.md`), not to an invented or unrelated source. Where a concept has no `Sources` array at all because it is itself the root authoritative document, correct attribution is the concept itself, not a fabricated citation.

---

## A7 – Computation concept handled as generic, no fabricated values

> "What is Acme's recognized revenue for fiscal year 2026?"

**Should reach:** `computations/revenue-ytd` (`type: Attested Computation`).

**Pass criteria:** The agent describes the computation, what it computes, and that it requires attestation to run, without fabricating an actual dollar figure. **A specific dollar amount anywhere in the response is an automatic fail**, regardless of how plausible it sounds, since no execution capability exists in this release.

---

## A8 – Unknown type tolerance

> "What concepts exist in the Test Bundle?"

**Setup:** `only-concept` has `type: Note`, not one of `acme_retail`'s real types (`BigQuery Table`, `Metric`, `Policy`, `Skill`, `Attested Computation`).

**Pass criteria:** The agent surfaces it normally, no error, no silent exclusion for having an unrecognized type.

---

## A9 – Broken link tolerance (replaces original phrasing)

> "What related concepts does `broken-reference` in the Broken Reference bundle reference?"

**Setup:** A deliberately broken link added to a concept's body, referencing a path that doesn't exist.

**Pass criteria:** The agent proceeds without error, reports what it can resolve, and does not fabricate content for the missing target. The link should show as unresolved in the underlying data rather than causing a failure. The original phrasing was revised because the bundle needed to be named explicitly in the question; nothing in the system prompt tells the model to guess which bundle a concept lives in from context alone.

---

## Notes on execution

- A3, A4, and A5 carry the most weight: they test whether trust, deprecation, and staleness signals actually change agent behavior, not just whether the agent can retrieve a concept.
- A wrong answer drawn from the *correct* concept is a model-quality issue, not a component failure. A *right-sounding* answer drawn from a deprecated, stale, or insufficiently-trusted concept **is** a component failure, that's what these tests are designed to catch.
- Each test was run in a fresh session, since prior context in the same session can mask or fix a failure that would otherwise reproduce.
