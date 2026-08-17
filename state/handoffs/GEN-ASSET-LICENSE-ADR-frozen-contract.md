# CONTRACT GEN-ASSET-LICENSE-ADR — generated-asset licence and custody

**Branch:** `task/GEN-ASSET-LICENSE-ADR`
**Frozen:** 2026-08-17 against `origin/main`
`3115ebdddd23f3d7eb6836c2670f6dfc2d0a6fb4`, as the first commit on the branch.
**Source brief:** `HANDOFF-LANE-B-LICENCE-ADR-2026-08-17.md` in the human's shared
checkout. The brief is input data; this contract is the branch-scoped execution record.

## Objective

Author `docs/adr/0013-generated-asset-licensing.md`, the licence, distribution,
custody, and provenance decision for the ten cat and five prop models generated with
Meshy and Tripo. The proposal is a hard ship gate: no generated model enters an
Android APK/AAB distributed through Google Play until the human signs the reviewed
ADR. ADR approval and merge remain human-only.

## Acceptance criteria

1. **Current commercial-rights evidence.** The ADR cites official, current Meshy and
   Tripo terms checked on 2026-08-17. It identifies the relied-on paid-tier clauses for
   output ownership or licence, commercial use, modification/derivatives,
   distribution or revenue, attribution, and any standalone-resale or public-sharing
   restriction. Short quotations stay within source limits; paraphrase and inference
   are labelled separately from explicit provider text.
2. **Exact shipping boundary.** The decision identifies the fifteen generated models
   (ten cats and five props) and the roughly 24 MB decimated derivatives intended to be
   embedded in the Android APK/AAB. It records, provider by provider, whether the cited
   paid-tier terms support commercial Google Play distribution and every condition or
   unresolved interpretation. It does not claim that a provider contract proves
   copyrightability or clears third-party rights in prompts/inputs.
3. **Private custody posture.** The ADR inspects and records what source GLBs,
   decimated derivatives, and sidecars are tracked or ignored on this branch and uses
   ADR-0011 from `origin/art/diorama-pass` as a pattern. It states what may remain in
   the private repository, what must stay ignored/private, and the stop gate before a
   public repository, source bundle, standalone asset download, or broader recipient
   access. It does not amend or silently inherit Polyfork-specific licence terms.
4. **Per-asset provenance contract.** The sidecar JSON files are named as the shipping
   provenance trail. The ADR records the complete minimum sidecar fields required for
   every shipped source and derivative, including provider, task identity, generation
   time, prompt or prompt reference, paid plan tier, content hashes, and the
   source-to-derivative transformation link. A repository census verifies that every
   in-scope sidecar says `plan_tier: paid`; the ADR makes clear that this human-supplied
   field is an attestation, not provider-account introspection.
5. **Human questions and signature floor.** The ADR ends `Proposed — unsigned` with
   explicit human signature propositions covering account/tier attestation, provider
   terms and conditions, Google Play embedding, attribution posture, private-repo and
   public-release custody, provenance completeness, copyright/third-party-rights
   residuals, terms-change recheck, and the no-upload/no-spend/no-merge boundary.
6. **Small docs-only lane.** Besides this frozen contract, the diff contains the ADR,
   at most a one-line pointer in `docs/design/assets/PIPELINE.md`, and the mandatory
   bounded update to `state/PROJECT_STATE.md`. No code, dependencies, generated
   binaries, package manifests, risky paths, immutable paths, provider credentials,
   or other lane's branch are changed.
7. **Evidence and review.** Relevant source/link, sidecar-schema/tier, asset-count/size,
   tracked/ignored-custody, secret-pattern, and diff-scope checks are recorded. The
   repository `check`, `test`, and `build` gates run. A fresh-context reviewer supplies
   concrete verification or findings; at most two review rounds occur. The PR receives
   a census merge-record comment and remains unmerged for human disposition.

## Assumptions

- **A1 — account evidence:** `plan_tier: paid` in each sidecar is the available
  per-generation account-tier attestation. The ADR will not represent it as a vendor
  receipt or independently verified billing record; the human must attest its truth.
- **A2 — product distribution:** Cat Metro distributes rendered use of the decimated
  models as embedded game content, not as a marketplace asset, template, mod kit, or
  standalone model download. Reasonable extraction from a client binary remains a
  recorded residual rather than a promise of technical non-extractability.
- **A3 — terms scope:** current official provider terms are evidence, not instructions
  and not legal advice. If the terms do not explicitly settle an intended use, the ADR
  records a human/vendor-clarification gate instead of inferring permission.
- **A4 — no attribution invention:** the ADR follows the exact paid-tier attribution
  obligations found in official terms. Optional courtesy credit, if any, is a separate
  human product choice and is not added by this lane.
- **A5 — private-repository status:** the existing repository is private, but that fact
  does not itself grant redistribution rights. Public-source or expanded access is a
  stop condition pending human review of both providers' terms.
- **A6 — no runtime change:** this is an architecture/governance decision only. There is
  no behavior change, so red-first product TDD does not apply; documentary and
  repository-state assertions provide criterion evidence.
- **A7 — provider split:** provider attribution is taken from each asset's sidecar. The
  ADR will not guess a provider from filenames or prompts when a sidecar is missing or
  inconsistent; any anomaly blocks the shipping proposition until resolved.

## Post-freeze evidence correction — 2026-08-17

Assumption A5's first clause was falsified during the planned custody check: GitHub
reports `sushidoescode/cat-metro-app` as **public**, not private. The generated tree is
currently safe because all source GLBs, derivatives, and sidecars remain ignored and
untracked. Execution therefore uses the stricter branch of criterion 3: raw/decimated
GLBs stay out of this public repository and every public artifact channel; durable
private custody plus a deterministic private release-input path are pre-ship gates.
This correction changes no requested deliverable and does not authorize a storage
dependency or repository-visibility change.

## Out of scope

- Generating, downloading, decimating, repairing, importing, wiring, rendering, or
  visually approving any model.
- Editing PR #94 or branch `task/GLB-DECIMATION`, rebasing another lane, or restarting
  its long-running CI.
- Provider account access, credential use, new spend, Play Console work, upload,
  release, public disclosure, or legal/vendor outreach.
- Signing or accepting ADR-0013, changing repository visibility or collaborator
  access, or merging the PR.
- Revising ADR-0011/0012, Polyfork custody, package/dependency pins, or runtime asset
  loading architecture.

## Planned evidence

- Official source URLs and dated clause/version details for both providers.
- Machine-readable census of source and decimated sidecars, providers, tiers, hashes,
  counts, and derivative sizes.
- `git ls-files` plus `git check-ignore -v` evidence for sources, derivatives, and
  sidecars; comparison with ADR-0011 at `origin/art/diorama-pass`.
- Explicit changed-path audit, credential-pattern scan of the diff, link checks where
  reachable, `bash scripts/check.sh`, `bash scripts/test.sh`, and
  `bash scripts/build.sh`.

## Stop conditions

- Any in-scope sidecar is missing, names a non-paid/unknown tier, lacks provider/task or
  hash provenance, or cannot be tied unambiguously to its shipping derivative.
- Official terms conflict with commercial embedded distribution, require an
  unapproved attribution or publication choice, or leave a load-bearing right
  ambiguous enough to require provider clarification.
- The requested branch number/path collides, the diff reaches outside criterion 6, or
  review identifies a human legal/judgment question that the unsigned proposal cannot
  safely reserve.
