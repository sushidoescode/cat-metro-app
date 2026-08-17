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
vendor receipt. In particular, it does not prove that the eight Tripo API tasks consumed purchased
API credits rather than trial or complimentary credits.

There is also a custody constraint that the earlier pipeline notes do not settle. GitHub reported
`sushidoescode/cat-metro-app` as **public** on 2026-08-17. The complete
`unity/Assets/Art/Generated/incoming/` tree—including sources, derivatives, and sidecars—is ignored
by `.gitignore` and untracked on main and the decimation refs. ADR-0012 deliberately leaves it that
way. ADR-0011 supplies a useful product-boundary/provenance pattern, but its private-repository
premise cannot be copied into this public repository.

Ignore status is not a confidentiality control: `git add -f` or an ignore-rule change can bypass it.
Current local custody also fails the proposed least-privilege posture. On 2026-08-17 the project and
`incoming/` directories were mode `0755`, and sampled source/derivative GLBs and sidecars were mode
`0644`. They remain readable to other local accounts on a multi-user host until moved to owner-only
custody. No file move or permission change is authorized by this docs lane.

The prompts are not private today: the tracked `docs/design/assets/CAT-MANIFEST.json` already puts
all fifteen exact prompts in this public repository's history. In addition, each of the eight current
Tripo derivatives embeds its provider task identifier in six internal GLB JSON name fields. Task IDs
are not credentials, but they are provider object locators and would enter the client binary unless
the files are sanitized and re-pinned or the human expressly accepts that disclosure.

This ADR decides the commercial distribution boundary and the minimum custody/provenance controls.
It does not decide that fifteen visually acceptable models exist, create a private asset-delivery
dependency, implement the required public-custody check, or authorize an upload. The frozen contract's
assumption A5 incorrectly says this repository is private. An agent cannot rewrite that frozen
authority; human ratification of the public-repository correction is an approval gate below.

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

Those explanatory pages do not provide a stable effective revision that reproduces the text relied
on here. URL plus access date is insufficient for a load-bearing promise that can change silently.
Before signature, each relied-on page therefore needs a timestamped private capture, content hash,
and short exact clause excerpt, unless an applicable Order or written Meshy clarification replaces
the explanatory-page reliance.

Section 3.3 of the operative terms applies CC0 when a customer publishes an output to the Meshy
Community. A public Git repository is not that named community, so treating ordinary Git publication
as a CC0 trigger would be an inference, not the clause. Cat Metro nevertheless chooses a stricter
no-public-GLB custody rule below.

**Meshy conclusion:** official Meshy materials expressly support commercial games,
distribution/sale, and no attribution for paid/private outputs. Decimation and other modification are
an inference from the claimed ownership/full-rights representations, not an express derivative-work
grant in the cited pages. Embedding these seven derivatives in a monetized APK/AAB is within the
provider's stated shipped-title use. The lack of matching express assignment and modification
language in the operative terms is a material drafting gap. The human must either accept reliance on
the captured official paid-plan representations or retain written Meshy clarification/an applicable
Order before any Meshy derivative ships. The human must also attest that a qualifying
subscription—not only a credit pack—covered each task and that none was published to Meshy
Community.

Meshy's terms separately require rights in customer inputs, forbid infringement and use of generated
assets to improve a competing AI model, and require non-Enterprise API output to be deleted three
days after generation (Enterprise retention or an Order may differ). Section 2.9 also permits Meshy
to use non-Enterprise customer inputs and outputs to train, validate, test, or improve its services
unless an Order says otherwise. Private ownership/non-Community status therefore does not mean
provider training is disabled. The terms warn that outputs may be non-unique or ineligible for IP
protection and disclaim non-infringement. Any applicable Order or third-party pass-through terms can
change the analysis and must be recorded.

### Tripo

The operative [Tripo Terms of User Agreement](https://www.tripo3d.ai/terms) state **Last updated:
July 11, 2025**. Section 5.2.2 says **“Paid Users generally have all rights”** and then expressly
lists use, copying, modification, adaptation, publication, derivative works, distribution,
transfer, authorization/licensing, revenue, public communication, performance, and display. Section
3.2 separately permits **“lawful commercial or non-commercial purposes”** subject to the agreement
and law. Section 10.5 says ownership provisions survive termination.

The [official game-development page](https://www.tripo3d.ai/game-development) says paid subscribers
may **“freely integrate the models into commercial games or projects.”** The sources here were
created through Tripo API, not Tripo Studio. Tripo's [developer page](https://developers.tripo3d.ai/en/)
describes API billing as usage-based credits and the API/Studio products as separate. Studio
[pricing](https://www.tripo3d.ai/pricing) labels Pro, Max, and Team as private-model/commercial-use
plans, but a Studio subscription or its private-model label proves neither paid API usage nor API
task visibility.

**Tripo conclusion:** if an API task qualifies under section 5.2.2 as Paid User output, the operative
clause directly supports decimation, derivative work, commercial embedded distribution, licensing,
and revenue. It does not use a clean “hereby assigns all right, title, and interest” formulation or
define precisely which API-credit sources make a task paid; it says paid users generally have the
listed rights. It also does not use the literal word “resale,” but embedded Google Play distribution
is supported by the combination of distribution/revenue rights and the commercial-game statement.
No paid-user attribution requirement was found in the agreement. The no-attribution conclusion is
therefore an interpretation of the operative paid clause, not a quoted promise from that clause.

Tripo conditions those rights on law, the agreement, sufficient input rights/permissions, and
non-infringement; prohibits using outputs for a directly competing model/service; disclaims
non-infringement and exclusivity; and lets supplemental terms override the main agreement for a
particular service. Paid users grant Tripo a perpetual, irrevocable, worldwide, non-exclusive
service-use/display license. The terms also put access-setting responsibility on the user, warn that
some services may default to their most permissive setting, and allow inputs/outputs previously put
in a public service area to be retained indefinitely after termination.

The human must retain timestamp/task-linked API usage plus recharge/order evidence showing that each
of the eight tasks consumed purchased API credits, not trial or complimentary credits. A Studio
subscription, Studio credit pack, or unrelated purchase is not evidence for an API task. If the
public terms and evidence do not establish API Paid User status, written Tripo/API Order clarification
is required. Each task must also have API visibility evidence and `tripo_public: false`; unknown or
public status fails closed even though public status does not by itself erase section 5.2.2 rights.

### Common legal residual

A provider's permission to use an output does not prove that untouched AI-generated geometry is
copyrightable, exclusive, or non-infringing. Cat Metro receives no provider warranty that a model is
unique or clears third-party rights. The prompts, reference inputs, output appearance, names, logos,
and any incorporated provider service assets remain a human review responsibility. This ADR is an
engineering risk record, not legal advice.

## Decision

Subject to the unsigned gates below, Cat Metro will use the fifteen exact hash-pinned decimated GLBs
as embedded art in its commercial Android game. We will rely on Tripo's paid-user rights for the
eight Tripo assets only after paid API-task status and provider visibility are evidenced, and on
Meshy's official paid/private commercial representations for the seven Meshy assets only after the
human resolves the documented drafting gap. We will provide no Meshy or Tripo attribution in the app
or Play listing because the recorded paid-output terms impose none; we will retain private, auditable
provenance instead.

Approval is limited to these fifteen source-to-derivative hash chains. A regenerated file, new task,
new asset, new provider, free/unknown tier, materially changed prompt/input, or different derivative
hash is outside this ADR.

### 1. Licensed product boundary

Allowed after approval and all release gates:

- embedding the exact decimated GLBs inside Cat Metro APKs/AABs and Play-delivered installation
  artifacts, including a paid or monetized game, only after the Tripo metadata choice below is
  resolved;
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
- publishing any source or derivative GLB as a standalone file or uncompiled source payload in this
  public repository, a public fork, PR attachment, CI artifact, release asset, support bundle, or
  contest download; the explicitly approved compiled APK/AAB boundary is the exception;
- including a private sidecar, release receipt, billing evidence, prompt corpus, or custody metadata
  in a source input staged for Unity or in an APK/AAB;
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
  may be swept into Git merely to make a local build convenient. Ignore status is defense in depth,
  not enforcement.
- Exact source GLBs, exact derivatives, and their complete sidecars may live in a separate private,
  access-controlled repository, object store, or owner-controlled archive. Access is least-privilege;
  public links and provider credentials are forbidden. Local filesystem custody must use owner-only
  directories/files (`0700`/`0600`) or an equivalently restrictive, value-suppressing ACL/encrypted
  store. The current `0755`/`0644` checkout placement is noncompliant and must not be the promoted
  custody location.
- The complete source-to-derivative hash chain and paid-plan evidence must have at least one durable
  human-controlled backup that does not depend on Meshy/Tripo retention. Provider reacquisition is
  not the release build path.
- Before promotion or a release build, a separate reviewed contract must choose a deterministic
  private delivery mechanism, pin the fifteen derivative hashes, constrain destinations, and prove
  that clean CI/release workspaces receive exactly those files without exposing them publicly. This
  ADR authorizes no storage vendor, dependency, secret, or fetch implementation.
- Before this ADR may be accepted or an asset promoted, named follow-up
  `GEN-ASSET-PUBLIC-CUSTODY-GATE` must land a human-reviewed required check that rejects tracked
  generated GLBs/private sidecars even when ignore rules are bypassed and inspects public CI, PR,
  release, and support artifacts for forbidden raw assets/receipts. The release leg must also scan
  the final APK/AAB for source/probe filenames, private receipt schema, prompts, and unapproved task
  IDs. This docs lane proposes that permanent check but cannot implement its risky code/CI scope.
- The exact prompts are already public in `CAT-MANIFEST.json`; that disclosure cannot be described as
  awaiting approval. A public tracked receipt may contain non-secret filenames, terms references,
  and the approval hashes below. New provider/task identifiers require review; private sidecars and
  billing/custody records never enter public Git or build inputs.
- Repository visibility, asset-recipient access, public-source, build-input, or artifact-retention
  changes are stop conditions. Re-review this ADR before changing any of them.

Because the current repository is public, the local files are not owner-only, the machine custody
check does not exist, and no private release-input path exists, signing this ADR alone does not make
the asset build reproducible or shippable. Each is a hard pre-promotion and pre-release gate.

### 3. Provenance record and release-complete sidecar

The local sidecars are the authoritative per-asset provenance trail today. Source sidecars contain
`prompt`, `service`, `task_id`, `timestamp_utc`, `sha256`, `plan_tier`, and `note`. Derivative
sidecars use schema version 1 and embed that provenance under `source.provenance`, then bind it to:

- `source.filename`, `source.sha256`, and `source.sidecar_sha256`;
- `derivative.filename` and `derivative.sha256`;
- `tool.name`, `tool.version`, `tool.build_hash`, `tool.operation`, and `tool.timestamp_utc`; and
- `geometry.category`, target/accepted triangle bounds, and source/output structure metrics.

All fifteen current chains validate and every top-level or nested tier reads `paid`. They are
generation/decimation-complete, but not release-complete. The following public, non-secret approval
manifest binds the exact current bytes to this proposal head. It deliberately omits prompts and task
IDs. A signature approves these values only; any changed value requires an amended, independently
reviewed proposal.

| Asset | Provider | Source GLB SHA-256 | Source sidecar SHA-256 | Derivative GLB SHA-256 | Derivative sidecar SHA-256 |
|---|---|---|---|---|---|
| `cat-blue-siamese` | Meshy | `6943b7284d313ef70e1e1f5056b8324bf39e655b8ee571087d0fdfb5f1c0d80a` | `c8c3a24732da9f00dfb53f8a8011bff17044441cbeccfc351b53dfa942d4a070` | `44ceea493949fa7ea92bf40c7bc05e64c4b78e3ca0bb4c08b41fa7d788ee17b7` | `743de6f299f0c70f39dc92b7a7eda5ed6e86bf203ec78eefd4b01b20f9293f29` |
| `cat-conductor` | Meshy | `f2cc476deccbcb4e72b74bd7d7e0bbd0e4c6bd3103149ce3697b85fa5260a9d1` | `9b66a992a560a590a06f435202b5bb246c3d2006dd6fda3b10ed2e059a28d9c4` | `3b0bdbe1a0af9377bfde62ebf2b633e694881dc81438f2814e717c4c71ab9e7d` | `83b5329451479e54719cd06a83445ab74f0bc58ef4dc4749b5b6e3cc50473e6b` |
| `cat-green-shorthair` | Meshy | `d1a40bfbc4beaba8ac5169260c8d18e4664212f2e9a656549a76dfd3ecf0790b` | `84bbb56e37c97f70c0500f82803a7359d8e7c451b294fc2524222c2fae487eee` | `96910d69ad0bfe424c410e0b9df6e137222d858a28322a5276add6228e9186e5` | `9bcd978598e942d139e573ea9cdd3afab7dd86439f0aa56524accc0d9c3b3333` |
| `cat-red-tabby` | Meshy | `d18c2098353cf5688745ef820b0ff8b58c826baa64d95bbec5c3234a3977ebf1` | `f746932debb844dd0db9728ca4033a325de410f1eb27ec413a695ae9df45f009` | `9d6f3e1b0d82f23500779c570943dc2081c6caad7295da7d3fe19c1c50742b59` | `9ebb3638031225ab8ade57cf794cfbb69b3ee98c3ff82e500aaf0d1f8738f4db` |
| `cat-wild-alley` | Meshy | `b1d85cf314bbc295d7ca28af9c41f9c3bddecd81cfb84603a14648b55d5f462c` | `1b9d1a08924982f38bd24ac7fc138331f5f357efc500802cc1cf3d06567a6467` | `3fa010b59c3b5dccbe0eb54453e8d595736cbafa391a9f08effd9d052738479c` | `92f095b97e5c4f03116ac087c6852ebcaeabff611b67051faf2b5f2a96f7260b` |
| `cat-yellow-longhair` | Meshy | `1530b4a5609aeb62499be627630c735c3fd6fae1368b8f58d21c169e4183a813` | `7916d941b2cf27eb0628e77ccfd95252dc8fe671c11b773ffb89b3c513db39d1` | `36f03503fcbcb918870463222f50d6b17b3c880281ce61f3a15c2cec6963ed3e` | `f11a40229f24436206b06d4eee04246ef72c0f10ff3b7c88034d99757be2a4ec` |
| `prop-toy-engine` | Meshy | `724bfbccc0087992b4f767037444ad95d24a21733d58923e9f7fbbfaa4b6e4c5` | `c02b9ff1352be0c1c3bdbdb63f95aff951311c00ef7e8179d8e9c94257b7569a` | `f622b390cdf48fccfb382895bef2988df191b523b614e01f03dbd162e052eeaf` | `2f1bd6850cbb836d8c569791ffaa6939c5d5f58a42487381c3954f3fb03aec1f` |
| `cat-blue-siamese-loaf` | Tripo | `e3015351ec9bda2aebeafcc0ff23f5aa35512af4234c168d79cac750118070e3` | `ce8ea067634f88ee9fc967ea5a0dbc58df890477d3e1dc1905cc3f77a92dcec4` | `cc1ff113257d48994a94cfdff52554236034e3e6455d402de195461b8c8fc236` | `8209d8dcac1e70f31a3070801eeacd3eb3bad19654cb0135ae2c9d7416be4a59` |
| `cat-green-shorthair-sit` | Tripo | `2db4f94d7c59c36c01f1f1d51780b30a417001b5d91a10caf420e5e5c1d6c5b2` | `beb9e95db53e897809194921156c46da974e8519dca3c627b9787b2ca2b40cf5` | `a5791a945bac21cfe55e7e4cdbcd5cd3233c11997cd0f449972a12768cca93f8` | `360ea5e28ca3e09b51fc45c8360ebe04e5b0a6fd38c532f636252bead68439fb` |
| `cat-red-tabby-sitting` | Tripo | `418eaa31df9b65c975dd85bdac3f04ab73b64827d81248d6248b57c6e2a81b14` | `982049a7b96684a6c9d3723764a51f378a07887439b2a4d3de8d318e7216b575` | `3ea8e01d78cb058223c74f225e89512efc44f74f638c99133d7720675e8655b6` | `f40f32794ef55f2f2e797ea870c63fedd6c2959bd0b0facc7fd50f0f1d21d898` |
| `cat-yellow-longhair-wave` | Tripo | `8d7190fd24f552f874bf1d733f2870c44a24c27d6b50cfe1e32095f625fcc57c` | `e65414b151fa1dd868e9086c0e274ac61743aef8f8f26bc7bcaa6f49f99c8936` | `4e20de09cee1dcfa383bb708608f03b5f8c1aa78ca4a510a3064f435f5f87a27` | `a084bae339440e74e3b22b0f578fe1a62fe80c15474f4ffd62f717ad6cb9cfb1` |
| `prop-depot-shed` | Tripo | `5efb54812e9d03c15c8d7c085a840bfabb09fc3312495c3b85af2ffcf8047aed` | `4816eeb87183c47bd60283af934f43215f129c7dd7183be4455cc04d4192ac83` | `68994c2316e7c0b23252569bfc06cbc1155c29dd41798c8effdbbaba638844b1` | `0e6c7f6a9065e12b0f3da93605914672947a2a662175ed788470c81f5d736ae2` |
| `prop-desk-clutter` | Tripo | `f42232e108bce9b9b12a5db6b8472412673a0dea9293cd9d896d43874bb4e9d5` | `393aca3bc5af36b19fed58e32dcd4e975542d1beafe85c0fe85a774830946ced` | `d0403b93dc3db30ec3f7e0b825ba7b48f4af7b79094c6b262c7bfa2fb268ec4d` | `e75ba87683bd0f468871608a0c079adf89e46eca86fecda248997b204da713b4` |
| `prop-station-kiosk` | Tripo | `5fdeeedf04d3b536fc9d3bf3483fb8837b12559557ff16d2ae0c7521c4260b49` | `0d9706c4263a777200122a86c4426ebd23d06ee0370911d35b6f9b548e6c8d4f` | `25053fb73009bf004aeeebab4a861bb664c91935b59c059f21d2fc8c9b6f52cf` | `416098fc269903c81ffbcf40e6f469821bfbc6c045b3ce2018e76fd2d30e9dc3` |
| `prop-trees` | Tripo | `d94b1586f4eb44a5e61ac5499dfef4ec8e4ddc2125bdf6fc95b6d7948673d863` | `1aa63811903a869927c134161101aa57c8993adc34661c03f9e1aade049e3f0b` | `e34f39de9a0db8f977370d7f0808f44a28b9641a458ada4957f552c62271c0dd` | `96b29000ef1e8f03d0982ffbaa1ec3d5a476cfcff8241a1f35b3a29041495b34` |

Before a derivative may ship, its sidecar or an integrity-linked companion receipt must additionally
carry:

1. the stable Cat Metro asset ID and explicit `shipping: true` selection (the three probes must read
   false or remain absent from the shipping receipt);
2. the exact paid provider product/plan at generation time and an opaque locator for human-held
   invoice/subscription evidence—for Tripo, task/timestamp-linked API usage and purchased-credit
   recharge/order evidence; never payment data in Git;
3. provider terms URL, displayed last-updated/effective date, and Cat Metro access date; for every
   undated Meshy explanatory page, a private timestamped capture/content hash and short clause excerpt;
4. an input-rights attestation and any separate Order, API, third-party, or supplemental-term
   identifier that changes the public terms;
5. for Meshy, an explicit `meshy_community_published: false` attestation, Enterprise/Order or accepted
   service-training status, and the human's selected resolution of missing express assignment and
   modification language;
6. for Tripo, task-linked API paid-use evidence, visibility evidence, and
   `tripo_public: false`, plus acceptance of Tripo's service-use/display license;
7. the no-attribution decision, private-custody location class, and signed ADR-0013 proposal head;
   and
8. the unchanged source, source-sidecar, derivative, and derivative-sidecar SHA-256 values in the
   approval manifest above.

Task IDs and provider parameters may retain their current provider-specific shapes only in private
provenance; they may not be dropped into an unstructured note alone. The private receipt may point to
confidential billing evidence; it must not copy an account name, email, credential, payment
identifier, signed URL, or invoice contents into the repository or build.

Before signature, the human must choose one treatment for the task identifiers embedded in the eight
Tripo derivatives: expressly accept their disclosure inside the shipped client while retaining the
exact manifest hashes above, or require deterministic sanitization, new hashes, an amended manifest,
and another independent review. Silent byte changes are forbidden.

Any missing field, hash mismatch, `free`/`unknown` tier, unverifiable plan/API-credit source,
unknown/public provider state, or source-to-derivative ambiguity fails closed. Adding free-plan
attribution is not a fallback for an uncertain paid record; re-establish the exact rights or
replace/regenerate the asset under verified terms.

### 4. Terms, attribution, and change control

The operative-terms pin is provider + displayed revision + URL + access date, not “whatever the site
says later.” An undated explanatory-page pin additionally requires the privately retained capture,
content hash, and short clause excerpt described above. Recheck those records against the official
sources and release-complete receipt:

- immediately before the first Play-bound build containing an affected asset;
- after a provider terms, pricing/license, account, plan, Order, supplemental-term, API product,
  credit source, visibility/community/publication, training-use, or cancellation change;
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
private delivery design, owner-only custody, machine public-disclosure gate, API-specific plan
evidence, license-enriched sidecars, human input-rights review, and an exact-release terms check are
mandatory. Meshy's operative-terms drafting gap remains an explicit human/vendor decision.

**Lock-in and reversibility:** visual content depends on fifteen provider task outputs, but no runtime
SDK or network service does. Each asset is replaceable at the prefab/art layer. Replacement requires
new visual evidence and provenance but no Domain/save migration.

**Spend/license:** no new generation, storage service, subscription, legal advice, or API spend is
authorized here. Existing paid status is asserted but not yet independently evidenced. Attribution
is proposed as not required, not purchased or waived by Cat Metro.

## Security notes

1. **Public disclosure:** a mistaken add, PR attachment, CI upload, or source bundle could make a
   reusable GLB permanently public. `.gitignore` is bypassable and is not the control. The required
   tracked-file/artifact deny check, private delivery, and no-raw-model release rule must enforce the
   boundary before this ADR is accepted.
2. **Asset substitution:** ignored local files can change outside Git review. Source-sidecar and
   derivative hashes in the proposal manifest, release receipts, clean-workspace delivery, and
   build-time exact-hash checks must bind the selected bytes.
3. **Credential/billing disclosure:** provider keys, signed URLs, account responses, invoices, and
   payment identifiers never enter a sidecar, Git, CI log, APK/AAB, or agent report. Private sidecars
   and receipts are excluded from build inputs; they carry only non-secret attestations and opaque
   private evidence locators.
4. **Untrusted model data:** provider GLBs remain hostile data. ADR-0012's offline, autoexec-disabled,
   structural and geometry boundary applies before Unity sees a derivative. Accepted ADR-0012 at an
   exact reviewed commit plus successful structural/security evidence for every final derivative hash
   is a release gate; identity hashes alone do not establish safety. This ADR authorizes no executable
   provider content or runtime importer.
5. **Rights drift and input risk:** paid-plan text can change and outputs can resemble protected work.
   Reproducible terms evidence, prompt/input review, provider-visibility state, and fail-closed release
   checks reduce but do not eliminate human legal judgment.
6. **Residual extraction:** the APK/AAB can be reverse engineered. Cat Metro does not expose an export
   feature or distribute models separately, but it cannot promise secrecy after client delivery.
7. **Local disclosure and embedded metadata:** current `0755`/`0644` placement is not owner-only, all
   prompts are already public, and eight Tripo GLBs contain task IDs. Owner-only custody plus explicit
   prompt/task-ID disposition and final-artifact scans prevent a false claim of private handling.

## Approval and release gates

Human approval applies only to the exact reviewed proposal head and does not itself authorize merge,
asset promotion, a release build, or Play upload. Before any generated asset enters a Play-bound
binary:

1. an independent review is complete; the human expressly amends frozen-contract assumption A5 from
   private to public-repository reality and signs every proposition below against the exact reviewed
   commit;
2. the fifteen source, source-sidecar, and derivative hashes are revalidated; the human may reduce
   and derivative-sidecar hashes against the approval manifest; the human may reduce the shipping
   roster through visual curation, but adding/replacing/changing a model requires an amendment;
3. release-complete sidecars/receipts satisfy §3, including task-linked paid evidence, provider
   visibility/training state, input-rights attestation, reproducible terms evidence, and the signed
   proposal head;
4. owner-only durable custody, a reviewed deterministic private release-delivery path, and the
   `GEN-ASSET-PUBLIC-CUSTODY-GATE` required check exist; public Git, PR, CI, release, and support
   outputs contain no standalone raw/decimated GLB or private sidecar/receipt outside the explicitly
   approved compiled APK/AAB boundary;
5. ADR-0012 is accepted at an exact reviewed commit and structural/security validation succeeds for
   every final derivative hash before Unity import;
6. the exact asset-bearing build contains only approved derivative hashes and no source/probe model,
   private sidecar/receipt, provider SDK, generation credential, signed URL, Blender component, or
   runtime generation call; final-artifact scans find no source/probe names, receipt schema, prompts,
   or task IDs unless the human has expressly approved the task-ID residual;
7. the official terms are rechecked at release and no changed clause, Order, supplemental term,
   credit/visibility fact, or account fact contradicts this proposal; and
8. the human, not an agent, performs the production go/no-go and Play upload under the existing
   release runbook.

### Open human questions and signature

- [ ] I ratify this human contract amendment: frozen-contract assumption A5 was false; this repository
      was public on 2026-08-17, and the stricter public-source/custody gates in this proposal replace
      that assumption without expanding the lane.
- [ ] I attest that the seven Meshy tasks were created under a qualifying paid subscription—not only
      purchased credits—and retain private evidence for the exact plan and timestamps.
- [ ] I attest that each of the eight Tripo API tasks consumed purchased API credits tied by task and
      timestamp to API usage/recharge/order evidence—not a Studio subscription, unrelated credit pack,
      or trial/complimentary API credits—or I have retained written Tripo clarification establishing
      its Paid User status.
- [ ] I attest that no Meshy shipping task was published to Meshy Community and that every prompt,
      reference, incorporated input, name, logo, and provider service asset is owned or permitted for
      this commercial use; I accept Meshy's non-Enterprise service-training right unless the recorded
      Enterprise/Order terms opt out.
- [ ] **Meshy drafting gap—select and record one:** I accept reliance on Meshy's official
      paid-ownership/commercial/no-attribution representations, including inferred modification
      permission, despite the current Terms lacking express assignment/modification language, and I
      retain timestamped private captures/hashes/excerpts; **or** I have retained written Meshy/Order
      clarification at the private evidence locator recorded in each affected receipt.
- [ ] I approve the Tripo §5.2.2 paid-rights reading, including commercial modification and embedded
      Google Play distribution for the evidenced API tasks; I accept Tripo's perpetual service-use/
      display license and attest that no supplemental/API term narrows the rights.
- [ ] I attest that each Tripo task has evidenced non-public API visibility and
      `tripo_public: false`, accepting the permissive-default/public-retention residual in the Terms.
- [ ] **Tripo task-ID disclosure—select and record one:** I accept that the eight exact approved GLBs
      disclose their provider task IDs inside the client; **or** I require sanitization, new hashes,
      an amended manifest, and another independent review before signature/shipping.
- [ ] I acknowledge that the fifteen exact prompts are already present in this public repository's
      history and approve continuing with that irreversible disclosure; no private sidecar, receipt,
      billing record, or additional provider identifier may enter public Git or the build.
- [ ] I approve shipping the final human-curated subset of these exact fifteen hash-pinned derivatives
      inside Cat Metro's commercial APK/AAB with no Meshy or Tripo credit, while accepting
      copyrightability, non-exclusivity, non-infringement, and client-extraction residuals.
- [ ] I approve Cat Metro's stricter custody rule: source and derivative GLBs stay out of this public
      repository and every public source/artifact channel; complete assets and sidecars live only in
      durable owner-only (`0700`/`0600` or equivalent) private custody.
- [ ] I accept that no generated asset is promotion- or release-ready until a separate reviewed,
      deterministic private delivery path, required public-custody/artifact check, accepted ADR-0012
      safety boundary, and every release-complete sidecar field in §3 exist.
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
- [Official developer page: API product and usage-based pricing](https://developers.tripo3d.ai/en/)
- [Official API account/usage documentation](https://developers.tripo3d.ai/en/docs/account)
- [Official game-development page](https://www.tripo3d.ai/game-development)
- [Studio pricing and paid-plan commercial-use labels (not API billing evidence)](https://www.tripo3d.ai/pricing)

**Disposition: PROPOSED / UNSIGNED. No generated asset may ship under this proposal until the human
signs the exact reviewed head and every release gate above is satisfied.**
