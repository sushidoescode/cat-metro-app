# ADR-0013: Generated-asset licensing, distribution, and custody

- **Status:** Proposed — unsigned; generated assets are blocked from every Play-bound binary
- **Date:** 2026-08-17
- **Relates:** ADR-0007 (presentation/runtime baseline), ADR-0009 (credential custody), ADR-0011
  (Polyfork pattern, currently on `origin/art/diorama-pass`), ADR-0012 (Blender decimation,
  currently on PR #94), `docs/design/assets/PIPELINE.md`, and
  `state/handoffs/GEN-ASSET-LICENSE-ADR-frozen-contract.md`

## Context

Cat Metro has fifteen generated 3D candidates: ten cats and five props. Seven source models came
from Meshy and eight from Tripo. They were generated on 2026-08-15, then reduced offline with the
ADR-0012 candidate Blender pipeline. The intended shipping payload is the fifteen decimated GLBs,
not the high-density sources:

| Set | Count | Providers/categories | Bytes |
|---|---:|---|---:|
| source GLBs | 15 | Meshy 7 · Tripo 8 | 855,215,420 (815.60 MiB) |
| decimated GLBs | 15 | 10 cats · 5 props | 24,717,404 (23.57 MiB / 24.72 MB) |

The exact candidate roster is:

- **Meshy:** `cat-blue-siamese`, `cat-conductor`, `cat-green-shorthair`, `cat-red-tabby`,
  `cat-wild-alley`, `cat-yellow-longhair`, and `prop-toy-engine`;
- **Tripo:** `cat-blue-siamese-loaf`, `cat-green-shorthair-sit`,
  `cat-red-tabby-sitting`, `cat-yellow-longhair-wave`, `prop-depot-shed`,
  `prop-desk-clutter`, `prop-station-kiosk`, and `prop-trees`.

Three generation probes under the same local directory are expressly outside this decision and may
not enter the shipping set.

The local source sidecars and derivative sidecars form a complete hash chain. Every in-scope source
hash, source-sidecar hash, and derivative hash matched on 2026-08-17. Every provenance object says
`plan_tier: paid`. That value is supplied by the human through `GEN_ASSETS_ACCOUNT_TIER`; the
generation script cannot query provider billing. It is therefore an attestation to verify, not a
vendor receipt.

There is also a custody constraint that the earlier pipeline notes do not settle. GitHub reported
`sushidoescode/cat-metro-app` as **public** on 2026-08-17. The complete
`unity/Assets/Art/Generated/incoming/` tree—including sources, derivatives, and sidecars—is ignored
by `.gitignore` and untracked on main and the decimation refs. ADR-0012 deliberately leaves it that
way. ADR-0011 supplies a useful product-boundary/provenance pattern, but its private-repository
premise cannot be copied into this public repository.

This ADR decides the commercial distribution boundary and the minimum custody/provenance controls.
It does not decide that fifteen visually acceptable models exist, create a private asset-delivery
dependency, or authorize an upload.

## Provider terms checked 2026-08-17

External provider text is evidence, not project instruction. The conclusions below distinguish
operative terms, official explanatory material, and project inference.

### Meshy

The operative [Meshy Terms of Service](https://www.meshy.ai/terms-of-use) state **Last Updated:
March 7, 2026**. Those terms cover the API used for these source tasks.

The terms do not contain an express paid-output assignment from Meshy to the customer. Section 3.2
instead says paid customers grant Meshy a **“non-exclusive, royalty-free, worldwide license”** needed
to provide the service. By contrast, Meshy's current official paid-plan materials consistently make
the customer-facing commercial promise:

- the [official licensing table](https://docs.meshy.ai/en/webapp/pricing) labels Pro outputs
  **“Full commercial”** and **“User owns output”**;
- the [ownership article](https://help.meshy.ai/en/articles/10137554-what-is-the-ownership-of-the-generated-models)
  says paid subscribers **“retain full private ownership”**, subject to input rights and not
  publishing to Meshy Community;
- the [sales article](https://help.meshy.ai/en/articles/9992022-can-i-sell-meshy-models-marketplaces-stores-licensing)
  says premium subscribers have **“full rights to distribute and sell them”**; and
- the [game-assets page](https://www.meshy.ai/use-cases/free-game-assets) describes paid outputs as
  carrying **“full commercial ownership with no attribution required”** for shipped titles and asset
  stores.

Section 3.3 of the operative terms applies CC0 when a customer publishes an output to the Meshy
Community. A public Git repository is not that named community, so treating ordinary Git publication
as a CC0 trigger would be an inference, not the clause. Cat Metro nevertheless chooses a stricter
no-public-GLB custody rule below.

**Meshy conclusion:** official Meshy materials expressly support commercial games, modification,
distribution/sale, and no attribution for paid/private outputs. Embedding these seven derivatives in
a monetized APK/AAB is within the provider's stated shipped-title use. The lack of matching express
assignment language in the operative terms is a material drafting gap. The human must either accept
reliance on Meshy's official paid-plan representations or retain written Meshy clarification/an
applicable Order before any Meshy derivative ships. The human must also attest that a qualifying
subscription—not only a credit pack—covered each task and that none was published to Meshy
Community.

Meshy's terms separately require rights in customer inputs, forbid infringement and use of generated
assets to improve a competing AI model, permit deletion of API output after three days, warn that
outputs may be non-unique or ineligible for IP protection, and disclaim non-infringement. Any
applicable Order or third-party pass-through terms can change the analysis and must be recorded.

### Tripo

The operative [Tripo Terms of User Agreement](https://www.tripo3d.ai/terms) state **Last updated:
July 11, 2025**. Section 5.2.2 says **“Paid Users generally have all rights”** and then expressly
lists use, copying, modification, adaptation, publication, derivative works, distribution,
transfer, authorization/licensing, revenue, public communication, performance, and display. Section
3.2 separately permits **“lawful commercial or non-commercial purposes”** subject to the agreement
and law. Section 10.5 says ownership provisions survive termination.

The [official game-development page](https://www.tripo3d.ai/game-development) says paid subscribers
may **“freely integrate the models into commercial games or projects.”** Current
[pricing](https://www.tripo3d.ai/pricing) labels Pro, Max, and Team as private-model/commercial-use
plans.

**Tripo conclusion:** the operative paid-user clause directly supports decimation, derivative work,
commercial embedded distribution, licensing, and revenue for these eight outputs. It does not use a
clean “hereby assigns all right, title, and interest” formulation; it says paid users generally have
the listed rights. It also does not use the literal word “resale,” but embedded Google Play
distribution is expressly supported by the combination of distribution/revenue rights and the
commercial-game statement. No paid-user attribution requirement was found in the agreement. The
no-attribution conclusion is therefore an interpretation of the operative paid clause, not a quoted
promise from that clause.

Tripo conditions those rights on law, the agreement, input ownership, and non-infringement; prohibits
using outputs for a directly competing model/service; disclaims non-infringement and exclusivity;
and lets supplemental terms override the main agreement for a particular service. The human must
attest that each task qualified as paid use and that no API Order or supplemental term narrows the
rights above.

### Common legal residual

A provider's permission to use an output does not prove that untouched AI-generated geometry is
copyrightable, exclusive, or non-infringing. Cat Metro receives no provider warranty that a model is
unique or clears third-party rights. The prompts, reference inputs, output appearance, names, logos,
and any incorporated provider service assets remain a human review responsibility. This ADR is an
engineering risk record, not legal advice.

## Decision

Subject to the unsigned gates below, Cat Metro will use the fifteen exact hash-pinned decimated GLBs
as embedded art in its commercial Android game. We will rely on Tripo's paid-user rights for the
eight Tripo assets and, only after the human resolves the documented drafting gap, Meshy's official
paid/private commercial representations for the seven Meshy assets. We will provide no Meshy or
Tripo attribution in the app or Play listing because the recorded paid-output terms impose none; we
will retain private, auditable provenance instead.

Approval is limited to these fifteen source-to-derivative hash chains. A regenerated file, new task,
new asset, new provider, free/unknown tier, materially changed prompt/input, or different derivative
hash is outside this ADR.

### 1. Licensed product boundary

Allowed after approval and all release gates:

- embedding the exact decimated GLBs inside Cat Metro APKs/AABs and Play-delivered installation
  artifacts, including a paid or monetized game;
- rendering them at runtime and showing them in screenshots, capture video, store art, trailers,
  internal builds, and the Devpost submission;
- the already-recorded offline decimation, plus project-specific scaling, recoloring, material,
  animation, prefab, and composition work that does not expose an asset as a standalone download;
  and
- retaining the exact source and derivative files in approved private custody after a provider
  subscription ends, subject to the terms/version under which the paid tasks were generated.

Not authorized by this ADR:

- a raw or decimated GLB, extracted mesh, texture archive, prefab-as-asset, template, sample project,
  mod kit, source bundle, marketplace listing, or separately downloadable art pack;
- publishing any source or derivative GLB in this public repository, a public fork, PR attachment,
  CI artifact, release asset, support bundle, or contest download;
- publishing a Meshy source or derivative to Meshy Community, which would invoke the terms' CC0
  clause;
- exposing a provider service/API, account, credential, signed download URL, or generation feature
  to players; using the assets to train a competing model; or shipping Meshy, Tripo, or Blender code;
  and
- treating an APK/AAB as technically non-extractable. Determined extraction is an accepted residual
  of client-side art, not permission for Cat Metro to distribute the model separately.

The public-source ban is Cat Metro's proprietary-custody decision, not a claim that either provider
forbids paid users from publishing raw outputs. The intentionally public boundary is the compiled
game and its rendered media.

### 2. Source, derivative, and repository custody

- The public project repository keeps
  `unity/Assets/Art/Generated/incoming/` ignored. No source, derivative, sidecar, Unity meta, or probe
  may be swept into Git merely to make a local build convenient.
- Exact source GLBs, exact derivatives, and their complete sidecars may live in a separate private,
  access-controlled repository, object store, or owner-controlled archive. Access is least-privilege;
  public links and provider credentials are forbidden.
- The complete source-to-derivative hash chain and paid-plan evidence must have at least one durable
  human-controlled backup that does not depend on Meshy/Tripo retention. Provider reacquisition is
  not the release build path.
- Before promotion or a release build, a separate reviewed contract must choose a deterministic
  private delivery mechanism, pin the fifteen derivative hashes, constrain destinations, and prove
  that clean CI/release workspaces receive exactly those files without exposing them publicly. This
  ADR authorizes no storage vendor, dependency, secret, or fetch implementation.
- A public tracked receipt may contain non-secret filenames, provider/task identifiers, terms
  references, and hashes after review, but it is not a substitute for the complete private sidecars.
  Prompts/task identifiers are not published until the human approves that disclosure.
- Repository visibility, asset-recipient access, public-source, build-input, or artifact-retention
  changes are stop conditions. Re-review this ADR before changing any of them.

Because the current repository is public and no private release-input path exists, signing this ADR
alone does not make the asset build reproducible or shippable. That missing delivery mechanism is a
hard pre-promotion and pre-release gate.

### 3. Provenance record and release-complete sidecar

The local sidecars are the authoritative per-asset provenance trail today. Source sidecars contain
`prompt`, `service`, `task_id`, `timestamp_utc`, `sha256`, `plan_tier`, and `note`. Derivative
sidecars use schema version 1 and embed that provenance under `source.provenance`, then bind it to:

- `source.filename`, `source.sha256`, and `source.sidecar_sha256`;
- `derivative.filename` and `derivative.sha256`;
- `tool.name`, `tool.version`, `tool.build_hash`, `tool.operation`, and `tool.timestamp_utc`; and
- `geometry.category`, target/accepted triangle bounds, and source/output structure metrics.

All fifteen current chains validate and every top-level or nested tier reads `paid`. They are
generation/decimation-complete, but not release-complete. Before a derivative may ship, its sidecar
or an integrity-linked companion receipt must additionally carry:

1. the stable Cat Metro asset ID and explicit `shipping: true` selection (the three probes must read
   false or remain absent from the shipping receipt);
2. the exact paid provider product/plan at generation time and an opaque locator for human-held
   invoice/subscription evidence—never payment data in Git;
3. provider terms URL, displayed last-updated/effective date, and Cat Metro access date;
4. an input-rights attestation and any separate Order, API, third-party, or supplemental-term
   identifier that changes the public terms;
5. for Meshy, an explicit `meshy_community_published: false` attestation and the human's selected
   resolution of the missing express paid-output assignment;
6. the no-attribution decision, private-custody location class, and signed ADR-0013 proposal head;
   and
7. the unchanged source, source-sidecar, and derivative SHA-256 values already present.

Task IDs and provider parameters may retain their current provider-specific shapes, but they may not
be dropped into an unstructured note alone. The private receipt may point to confidential billing
evidence; it must not copy an account name, email, credential, payment identifier, signed URL, or
invoice contents into the repository or build.

Any missing field, hash mismatch, `free`/`unknown` tier, unverifiable plan, public-community state,
or source-to-derivative ambiguity fails closed. Adding free-plan attribution is not a fallback for an
uncertain paid record; re-establish the exact rights or replace/regenerate the asset under verified
terms.

### 4. Terms, attribution, and change control

The terms pin is provider + displayed revision + URL + access date, not “whatever the site says
later.” Recheck the official sources against the release-complete receipt:

- immediately before the first Play-bound build containing an affected asset;
- after a provider terms, pricing/license, account, plan, Order, supplemental-term, API product,
  community/publication, or cancellation change;
- after a new generation task, provider, asset family, input source, recipient, storage location,
  repository visibility, or distribution boundary; and
- before any public source/artifact release or standalone-asset use.

A changed page, removed clause, contradictory provider page, or uncertain account coverage blocks
the affected provider group. The human records an ADR amendment, written vendor clarification, or
asset replacement before distribution. No agent may accept the legal residual or silently substitute
a courtesy-credit decision.

## Alternatives seriously considered

- **Commit the 23.57 MiB derivative set to the public repository.** This would give clean checkouts a
  simple, reproducible build and both providers appear to permit paid-output distribution. It lost
  because it intentionally republishes the reusable raw models, may subject them to repository-level
  outbound expectations, destroys practical exclusivity, and repeats the custody class that the
  Polyfork lane had to remediate. Compiled-game distribution is the product requirement; raw-public
  distribution is not.
- **Keep one owner-local copy and build releases manually.** This preserves secrecy with no new
  service. It lost because a single machine is not durable provenance, clean CI cannot reproduce the
  asset set, and an unrecorded local file can be replaced without review.
- **Reacquire/regenerate assets during every build.** This avoids private binary storage and can
  recreate missing files. It lost because paid providers, expiring URLs, nondeterministic outputs,
  credits, network availability, and owner credentials would enter the trusted release path; the
  original hash-pinned output might be impossible to reproduce.
- **Ship the high-density sources.** This avoids derivative lineage questions. It lost on the
  approved geometry/performance boundary and would add roughly 815.60 MiB before Unity processing.
- **Omit generated assets and retain greybox/Polyfork-only art.** This has the smallest generated
  licensing surface and remains the fail-safe if the human rejects either provider, custody, or
  provenance condition. It lost as the proposal because the explicit task is to license the curated
  generated set, but it remains the required fallback rather than shipping on uncertain rights.

## Consequences

**Easier:** once signed and completed, the exact generated set can be modified, rendered, and
commercially distributed inside Cat Metro without player-facing provider credit. Hash-linked source,
tool, derivative, tier, and terms records make the relied-on asset identifiable.

**Harder:** the public code repository cannot by itself reproduce an asset-bearing build. A separate
private delivery design, plan evidence, license-enriched sidecars, human input-rights review, and an
exact-release terms check are mandatory. Meshy's operative-terms drafting gap remains an explicit
human/vendor decision.

**Lock-in and reversibility:** visual content depends on fifteen provider task outputs, but no runtime
SDK or network service does. Each asset is replaceable at the prefab/art layer. Replacement requires
new visual evidence and provenance but no Domain/save migration.

**Spend/license:** no new generation, storage service, subscription, legal advice, or API spend is
authorized here. Existing paid status is asserted but not yet independently evidenced. Attribution
is proposed as not required, not purchased or waived by Cat Metro.

## Security notes

1. **Public disclosure:** a mistaken add, PR attachment, CI upload, or source bundle could make a
   reusable GLB permanently public. Directory ignore, exact changed-path checks, private delivery,
   artifact inspection, and a no-raw-model release rule are the controls.
2. **Asset substitution:** ignored local files can change outside Git review. Source-sidecar and
   derivative hashes, release receipts, clean-workspace delivery, and build-time exact-hash checks
   must bind the selected bytes.
3. **Credential/billing disclosure:** provider keys, signed URLs, account responses, invoices, and
   payment identifiers never enter a sidecar, Git, CI log, APK/AAB, or agent report. Sidecars carry
   only non-secret attestations and opaque private evidence locators.
4. **Untrusted model data:** provider GLBs remain hostile data. ADR-0012's offline, autoexec-disabled,
   structural and geometry boundary applies before Unity sees a derivative. This ADR authorizes no
   executable provider content or runtime importer.
5. **Rights drift and input risk:** paid-plan text can change and outputs can resemble protected work.
   Dated terms pins, prompt/input review, community-publication state, and fail-closed release checks
   reduce but do not eliminate human legal judgment.
6. **Residual extraction:** the APK/AAB can be reverse engineered. Cat Metro does not expose an export
   feature or distribute models separately, but it cannot promise secrecy after client delivery.

## Approval and release gates

Human approval applies only to the exact reviewed proposal head and does not itself authorize merge,
asset promotion, a release build, or Play upload. Before any generated asset enters a Play-bound
binary:

1. an independent review is complete and the human signs every applicable proposition below against
   the exact reviewed commit;
2. the fifteen source, source-sidecar, and derivative hashes are revalidated; the human may reduce
   the shipping roster through visual curation, but adding/replacing a model requires an amendment;
3. release-complete sidecars/receipts satisfy §3, including exact paid-plan evidence, Meshy community
   status, input-rights attestation, terms pins, and the signed proposal head;
4. a reviewed private asset-custody and deterministic release-delivery path exists, while public Git,
   PR, CI, release, and support outputs contain no raw or decimated GLB;
5. the exact asset-bearing build contains only approved derivative hashes and no source/probe model,
   provider SDK, generation credential, signed URL, Blender component, or runtime generation call;
6. the official terms are rechecked at release and no changed clause, Order, supplemental term, or
   account fact contradicts this proposal; and
7. the human, not an agent, performs the production go/no-go and Play upload under the existing
   release runbook.

### Open human questions and signature

- [ ] I attest that the seven Meshy tasks were created under a qualifying paid subscription—not only
      purchased credits—and the eight Tripo tasks qualified as Paid User output at their recorded
      timestamps; I retain private evidence for the exact plans.
- [ ] I attest that no Meshy shipping task was published to Meshy Community and that every prompt,
      reference, incorporated input, name, logo, and provider service asset is owned or permitted for
      this commercial use.
- [ ] **Meshy drafting gap—select and record one:** I accept reliance on Meshy's official
      paid-ownership/commercial/no-attribution representations despite the current Terms lacking an
      express paid-output assignment; **or** I have retained written Meshy/Order clarification at the
      private evidence locator recorded in each affected receipt.
- [ ] I approve the Tripo §5.2.2 paid-rights reading, including commercial modification and embedded
      Google Play distribution, and attest that no supplemental/API term narrows it.
- [ ] I approve shipping the final human-curated subset of these exact fifteen hash-pinned derivatives
      inside Cat Metro's commercial APK/AAB with no Meshy or Tripo credit, while accepting
      copyrightability, non-exclusivity, non-infringement, and client-extraction residuals.
- [ ] I approve Cat Metro's stricter custody rule: source and derivative GLBs stay out of this public
      repository and every public source/artifact channel; complete assets and sidecars live only in
      durable, access-controlled private custody.
- [ ] I accept that no generated asset is promotion- or release-ready until a separate reviewed,
      deterministic private delivery path and every release-complete sidecar field in §3 exist.
- [ ] I approve the terms-change/release recheck and fail-closed replacement/amendment rules in §4.
- [ ] I understand that this signature does not merge this PR, add a dependency, authorize new spend,
      change repository visibility, approve visual taste, build/upload an AAB, publish to Google Play,
      or release any standalone asset.

- **Signed by:**
- **Signature statement:**
- **Signed at (absolute date/time and zone):**
- **Signed proposal head:**
- **Signature record:**

## Official sources accessed 2026-08-17

Meshy:

- [Terms of Service — last updated March 7, 2026](https://www.meshy.ai/terms-of-use)
- [Official documentation: pricing and licensing table](https://docs.meshy.ai/en/webapp/pricing)
- [Help Center: commercial use and copyright](https://help.meshy.ai/en/articles/9992001-can-i-use-meshy-assets-commercially-license-copyright-explained)
- [Help Center: ownership of generated models](https://help.meshy.ai/en/articles/10137554-what-is-the-ownership-of-the-generated-models)
- [Help Center: marketplace sales and licensing](https://help.meshy.ai/en/articles/9992022-can-i-sell-meshy-models-marketplaces-stores-licensing)
- [Help Center: rights after cancellation](https://help.meshy.ai/en/articles/9992023-if-i-cancel-my-subscription-will-all-my-models-revert-to-a-cc-by-4-0-license)
- [Official game-assets use case](https://www.meshy.ai/use-cases/free-game-assets)

Tripo:

- [Terms of User Agreement — last updated July 11, 2025](https://www.tripo3d.ai/terms)
- [Official game-development page](https://www.tripo3d.ai/game-development)
- [Current pricing and paid-plan commercial-use labels](https://www.tripo3d.ai/pricing)

**Disposition: PROPOSED / UNSIGNED. No generated asset may ship under this proposal until the human
signs the exact reviewed head and every release gate above is satisfied.**
