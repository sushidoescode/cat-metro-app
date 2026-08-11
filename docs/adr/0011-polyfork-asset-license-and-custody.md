# ADR-0011: Polyfork asset license and source custody

- **Status:** Accepted — human signature recorded 2026-08-10 against reviewed proposal
  `feb78a12f49fe0207ac61da2f5a363c5fd213b53`
- **Amendment status:** Proposed 2026-08-11 — the public-repository ruling is binding now;
  Amendment A requires a fresh human signature before Lane 1A may merge
- **Date:** 2026-08-09
- **Relates:** ADR-0007 (presentation/runtime baseline), ADR-0009 (credential custody),
  `state/handoffs/ART-DIORAMA-frozen-contract.md`, and
  `unity/Assets/Art/Polyfork/PROVENANCE.md`

## Context

Lane 1A replaces Cat Metro's greybox with a tabletop railway assembled partly from Polyfork
models. Nine source GLBs were acquired on 2026-08-09 through Polyfork's authenticated download
endpoint under the product owner's Founders entitlement, converted offline with Blender 5.1.2,
and committed as Unity-importable FBX derivatives. The source GLBs were not retained. Cat Metro
does not add a runtime GLB importer, Polyfork SDK, package, or network call.

This creates two human-owned decisions that implementation evidence cannot make:

1. whether Polyfork's license permits the GLB-derived models to ship inside a commercial Google
   Play binary without attribution while the model files remain unavailable as standalone assets;
   and
2. how the source files, committed derivatives, Founders seat, and API credential are held.

The official [Polyfork licensing page](https://polyfork.dev/licensing) states that its one license
covers free and paid assets, permits personal and commercial projects and modification, requires
no attribution, and permits models to ship inside a game. It forbids resale or redistribution of
the original or modified files as standalone assets, including through marketplaces, asset packs,
template libraries, file-sharing sites, or tools that give the files to other people. Downloads
remain licensed permanently after a subscription ends. The Founders Club is a one-time,
Pro-equivalent lifetime entitlement; licenses are per person or per team seat and are not
transferable by resale.

That page identifies its terms as last updated July 2026. It was retrieved on 2026-08-09; the
retrieved HTML response had SHA-256
`2bd2e3a8891248b72fd8eaa2688da4b4166eedbf252dd7bcde4aceddf6209f49`. The URL, stated revision,
and substantive terms above are the license pin. A changed page hash alone is a review tripwire,
not proof that the legal terms changed.

The credential is a separate custody exception. The human placed `POLYFORK_KEY` in the main
checkout's root `.env` and explicitly authorized its use for this lane after Polyfork MCP was not
available. That file is ignored and untracked, but it is agent-readable in the current local
environment and was observed with mode `0644` on 2026-08-09. This differs from the 2026-08-02
owner-only credential-store decision recorded in `state/PROJECT_STATE.md` and
`docs/security/threat-model.md`. The deviation must be accepted and narrowed, not described as if
the original control remained true.

## Decision

We will ship the nine provenance-recorded Polyfork models as modified, GLB-derived meshes embedded
inside Cat Metro's commercial Google Play game binary under the July 2026 Polyfork license. We will
not provide attribution in player-facing or store-facing copy because the license does not require
it; we will retain private, auditable provenance. We will not distribute any source GLB or modified
FBX as a standalone asset.

### 1. Licensed product boundary

Allowed under this ADR:

- embedding the provenance-recorded, modified meshes in Cat Metro APKs/AABs and Play-delivered
  binaries, including commercial releases;
- using those meshes in Cat Metro screenshots, videos, store art, prototypes, internal builds, and
  client-side runtime rendering;
- recoloring, decimating, combining, animating, and otherwise modifying them for Cat Metro; and
- continuing to use the downloaded assets after the Founders entitlement or hosted service ends,
  under the license delivered with the files.

Forbidden under this ADR:

- publishing or selling a source GLB, converted FBX, extracted mesh, prefab whose purpose is to
  expose the mesh, asset pack, template, sample project, mod kit, or separately downloadable art
  archive;
- placing the asset-bearing source tree in a public repository or source release;
- attaching the raw or modified model files as publicly downloadable PR, CI, release, support, or
  contest artifacts; and
- granting repository or artifact-store access to a person who needs the model files but is not
  covered by the product owner's person/team-seat entitlement.

The ordinary fact that a compiled game can be reverse engineered is a residual, not standalone
redistribution by Cat Metro. If Cat Metro deliberately exposes extractable model files outside the
game boundary, this ADR no longer authorizes that distribution.

### 2. Source and derivative custody

- The source GLBs remain transient acquisition inputs and are not committed. Reacquisition uses the
  recorded Polyfork asset IDs and source hashes only when an authorized human enables it.
- The nine modified FBXs may remain committed only while the repository is private and everyone
  with source access who may receive them is covered by the Founders person/team-seat license.
- `unity/Assets/Art/Polyfork/PROVENANCE.md` is the authoritative receipt: source ID, title, URL,
  source SHA-256, derivative filename/SHA-256, triangle count, Blender version, and exact conversion
  command. Import tests recompute derivative hashes and imported triangle counts.
- The Unity import disables animation, blend shapes, cameras, lights, visibility import, embedded
  materials, and collider generation. Imported meshes use Cat Metro's reviewed shader/material
  family. No executable/editor script comes from a model file.
- A public-repository, open-source, source-bundle, template, modding, team-access, acquisition-source,
  or asset-delivery change is a stop condition. A human must either confirm the new recipients are
  licensed and amend this ADR or remove/replace the Polyfork derivatives before that change.

### 3. Founders entitlement and API-key custody

The product owner is the account owner and license custodian. The authenticated account response and
the human's in-session statement identify the plan as Founders; no account response containing
personal or key material is committed.

The human-authored root `.env` placement is accepted as a narrow local-acquisition deviation with
all of these controls:

- `.env` stays gitignored and untracked and must be changed to owner-readable/writeable mode `0600`
  before Lane 1A merges;
- the key is never copied into a worktree, repository file, Unity asset, shell argument, URL, log,
  screenshot, evidence pack, CI secret, APK/AAB, or agent response;
- acquisition sends it only in the authenticated request header to `polyfork.dev` over HTTPS;
- no CI, build, editor play, or shipped runtime path requires the key or Polyfork availability;
- logs and committed evidence contain asset identifiers and content hashes, never credential or
  account-response material; and
- suspected disclosure triggers immediate human rotation/revocation, history and artifact scan,
  and a security disposition before further acquisition.

This is not a general approval for agent-readable asset-generation keys. Meshy and Tripo were not
used for the committed Lane 1A assets, and their separate keys/credits are outside this ADR.

### 4. License-change and entitlement checks

The pinned license is rechecked before the first production Play submission and whenever any of
these occurs: Polyfork changes the stated licensing revision or substantive terms; the repository
or asset artifacts may become public; a new person receives source access; the Founders account or
seat owner changes; a new Polyfork asset family is adopted outside the recorded nine; or Polyfork
raises an asset-specific restriction. A conflict fails closed: do not distribute the affected
binary or files until the human records an amendment, obtains written vendor clarification, or
replaces the assets.

## Alternatives seriously considered

- **Keep all Polyfork derivatives outside git and reacquire on every build.** This minimizes model
  files in repository history and makes seat review more obvious. It lost because it puts a live
  vendor service and credential on the reproducible build path, makes old builds dependent on
  availability, and expands secret exposure into CI. The chosen private-repository boundary is
  smaller and produces deterministic, offline builds.
- **Use a separate private asset store instead of the private repository.** This narrows ordinary
  source checkout access and could enforce a dedicated access list. It lost for this nine-asset
  lane because it adds another credential, dependency, backup path, and fetch step without changing
  the per-person/team-seat obligation. Reconsider if the source team grows.
- **Replace Polyfork with bespoke/code-authored assets or newly generated Meshy/Tripo assets.** This
  could remove Polyfork seat and redistribution constraints. It lost because the selected models
  already match the low-poly direction, are provenance-tested, require no further credit spend,
  and replacement would add new license reviews and visual risk. A replacement remains the escape
  hatch if Polyfork terms or custody become unacceptable.
- **Ship the greybox and remove external models.** This has the cleanest license and source-custody
  surface. It lost because it fails Lane 1A's explicit tabletop-diorama product requirement and the
  human's art-push ruling.

## Consequences

**Easier:** Cat Metro can use, modify, and commercially ship the selected models inside its Play
binary without player-facing attribution or a runtime importer. The source hashes, conversion
receipt, and offline Unity derivatives make builds repeatable even if the hosted catalog changes.

**Harder:** source access now has a license-seat dimension. Private-repository status and recipient
coverage must be checked before access or publication changes. The team must preserve an internal
provenance record even though public attribution is unnecessary.

**Lock-in:** the visual scene depends on nine Polyfork-derived meshes and the license under which
they were downloaded. The license says downloaded assets remain usable permanently, limiting
service lock-in, but a public-source release would require removal, replacement, or separately
confirmed licensing. Replacement is localized to the asset/prefab layer and does not affect Domain
or saved data.

**Spend/license:** no incremental asset or API spend is authorized by this ADR. The human already
holds a paid Founders lifetime entitlement; its purchase amount is not asserted here. No Meshy or
Tripo credits were spent. The operational cost is seat/access review, provenance maintenance, and
possible asset replacement. Attribution is optional, not owed.

## Security notes

1. **Credential disclosure:** an agent-readable `.env`, shell history, process list, log, or diff
   could expose the bearer key. Header-only use, no shell-argument interpolation, owner-only file
   mode, secret scanning, and no CI/runtime dependency reduce the path. Human rotation remains the
   recovery control.
2. **Unlicensed redistribution:** a public fork, public CI artifact, source bundle, or unlicensed
   collaborator could receive modified FBXs. Private-source and licensed-recipient gates are the
   primary control; binary embedding is the intentionally allowed boundary.
3. **Asset substitution:** a vendor response or local file could change under the same filename.
   Source and derivative SHA-256 receipts plus imported-triangle tests fail stale or substituted
   content; new hashes require a reviewed provenance update.
4. **Malicious model content:** source files are untrusted data. Offline Blender conversion,
   restricted Unity importer settings, structural inspection, no bundled material/script/runtime
   importer, and the package-manifest parity gate limit executable and dependency surface.
5. **License drift:** the public page can change after acquisition. The dated revision, retrieved
   response hash, permanent-download clause, and pre-production recheck make the relied-on version
   and trigger explicit. Legal interpretation remains a human risk decision.
6. **Residual extraction:** shipping any client-side mesh permits determined extraction. Polyfork's
   published terms expressly allow models inside a game; Cat Metro does not add an export feature or
   separately distribute the files.

## Approval and implementation gates

Approval authorizes the exact license/custody posture, not merge, release, public source disclosure,
new asset acquisition, or spend. Before Lane 1A merges:

1. the human signs every proposition below against the exact reviewed proposal commit;
2. the root `.env` is verified ignored, untracked, secret-scan clean, and mode `0600` without
   printing its contents;
3. repository visibility is private and the human confirms every source recipient is covered by
   the Founders person/team-seat license;
4. all nine source/derivative receipt rows and asset-integrity/import tests are green;
5. the package manifest remains byte-identical to the pre-lane base and the shipped player has no
   Polyfork runtime request/importer;
6. independent security review finds no credential, public standalone asset, executable model
   content, or unapproved recipient path; and
7. the exact Play-bound release rechecks the official licensing page and records any substantive
   change before distribution.

### Human signature

- [x] I approve the July 2026 Polyfork license pin and its commercial-use, modification,
      no-attribution, permanent-download, game-embedding, and no-standalone-redistribution terms.
- [x] I attest that I own the Founders entitlement and accept responsibility for its per-person or
      per-team-seat coverage.
- [x] I approve shipping the nine provenance-recorded GLB-derived meshes inside Cat Metro's
      commercial Google Play binary, but not as standalone source or modified assets.
- [x] I approve keeping the modified FBXs in the private repository only while every source
      recipient is licensed, and I accept the stop gate before public source or broader access.
- [x] I accept my root `.env` placement as a narrow deviation from the owner-only-store decision,
      subject to mode `0600`, gitignore/untracked status, header-only acquisition use, no CI/runtime
      dependency, no disclosure, and rotation on suspicion.
- [x] I approve transient source GLB custody, committed hash-pinned FBX derivatives, and the recorded
      offline Blender/Unity import controls.
- [x] I understand that this ADR signature is not HC-25 merge authorization and does not authorize a
      Play upload, release, new dependency, new asset family, or further spend.

- **Signed by:** Cat Metro product owner (human, in-session; agent-recorded)
- **Signature statement:** “I SIGN ADR-0011 AT
  feb78a12f49fe0207ac61da2f5a363c5fd213b53 AND APPROVE ALL SEVEN HUMAN-SIGNATURE ITEMS. THIS IS
  NOT HC-25.”
- **Signed at (absolute date/time):** 2026-08-10 00:11:30 PDT (-0700), recording time
- **Signed proposal head:** `feb78a12f49fe0207ac61da2f5a363c5fd213b53`
- **Signature record commit:** `33a8d6cfc62461607a0bce0dadfc926e62ec3724`

## Official source checked 2026-08-09

- [Polyfork licensing — stated last update July 2026](https://polyfork.dev/licensing)

## Amendment A — permanently public repository custody (proposed 2026-08-11)

### Trigger, ruling, and supersession

The human ruling is: **the repository stays PUBLIC.** Before that ruling was recorded, unmerged PR
#65 exposed the nine modified FBXs and their nine Unity `.meta` files through its public branch.
That state violated the original licensed-product boundary and the original §2 condition that
derivatives could remain committed only while the repository was private.

On 2026-08-11 the lane-owned branch was rewritten from the former public head
`e4b787af8bdc8341da807d1b2d243d54ccda345d`. The 18 licensed paths were removed from every
rewritten PR-only commit, the shared main ancestry was preserved, and the branch was force-pushed.
The first contained head was `52763289cb758f607e16dd1b1dfae85932adae32`; subsequent commits add
custody enforcement, verification, and documentation without reintroducing a licensed model
payload.

This amendment supersedes only the original custody choice and its private-repository-dependent
text: the Decision section's “private, auditable provenance” phrase (the receipt is public; the
model pack is private), §2's permission to commit FBXs, the “chosen private-repository boundary” in
the alternatives analysis, the corresponding consequences, security note 2's private-source
control, approval gate 3, and original signature items 4 and 6 insofar as they approve committed
derivatives. The July 2026 license pin, embedded-game distribution decision, rendered-media
allowance, entitlement/key controls, and no-standalone-redistribution boundary remain in force.

The original signature remains an honest historical record of the then-reviewed proposal. It does
not approve this public-repository posture, and its proposal/record commits were themselves
rewritten during containment. Amendment A requires a new exact-proposal signature; no old signature
or merge word carries forward.

### A1. Derivative-free public tree

The public repository and every public source, PR, CI, release, support, contest, cache seed, and
artifact path must contain **none** of the following:

- a Polyfork source GLB, modified FBX, corresponding `.fbx.meta`, extracted mesh, recovery archive,
  Git LFS object/pointer that delivers one, or any equivalent standalone model payload;
- a mesh-bearing prefab, sample, template, mod kit, or tool output intended to expose or deliver a
  standalone model outside the compiled Cat Metro product boundary; or
- an automated link, hook, package restore, or build step that delivers a licensed model to a
  public or unlicensed recipient.

Cat-Metro-authored source, shaders, materials, scenes, and prefabs may remain. The current prefabs
reference ignored local model GUIDs and contain no embedded Unity mesh payload. Rendered frames,
videos, store art, and compiled Cat Metro APKs/AABs remain inside the licensed product boundary;
ordinary reverse engineering of an allowed compiled APK/AAB remains the accepted residual in §1.

The automated custody gate checks history reachable from the candidate and every fetched
remote/tag ref plus the current index. It rejects the nine exact source/derivative/meta hashes at
any path; conservatively rejects all Git LFS pointers and unallowlisted standalone model/archive
suffixes or recognized payload bytes; and
token-checks the current nine authored Polyfork prefab YAML files for embedded Unity mesh data.
The gate rejects inherited Git repository/index/object/configuration redirects before its first Git
query, plus on-disk alternates, grafts, and replacement refs, so it cannot silently inspect a
substitute object boundary.
This deliberately broad source-tree gate currently has no payload allowlist. A future Cat-Metro-
owned standalone model/archive needs a reviewed content-hash allowlist and a new custody
disposition. Independent security review owns renamed, obfuscated, encrypted, externally cached,
and public-artifact equivalents that static repository inspection cannot prove absent.

### A2. Local custody and access

The owner's authorized post-remediation copies of the exact nine FBX/meta pairs are held under the
ignored local path `unity/Assets/Art/Polyfork/Models/` in an owner-controlled worktree. They are
untracked, and no committed workflow copies them into another worktree, clone, CI job, artifact, or
support bundle. The owner must keep the pack, its Unity caches, and any Git object store retaining
stale licensed objects excluded from Time Machine and any other backup/synchronization service.
The automated gate verifies the current macOS Time Machine exclusions; other backup products are
an owner operational obligation, not a proposition the repository can prove. This authorized-
custody statement does not deny the stale hosted or third-party copies recorded in A6. Access is
limited to the product owner or another person covered by the applicable Polyfork person/team-seat
entitlement.

The licensed-local profile is supported only on the ACL-checked macOS owner host. The custody
directory is mode `0700`, each FBX/meta is mode `0600`, and symlink/reparse custody paths are
rejected. Unity may create imported/build derivatives only in owner-private local project
`Library`, `Temp`, `Logs`, and `.utmp` caches and the owner-private global Unity cache root under
`~/Library/Caches/com.unity3d.UnityEditor`; present roots are mode `0700`, may not be symlinks, and
may never be shared, uploaded, cached by CI, or attached as an artifact. The worktree/cache parents
are Time-Machine-excluded so a missing cache created later inherits the exclusion. The canonical
build and custody-aware Unity test driver run under `umask 077`, and project settings disable Unity
Accelerator download/upload rather than inheriting global preferences.

The rewritten objects can remain in this worktree's shared Git common object database. When the
gate finds any of the exact source/derivative/meta hashes in the local object inventory, it requires
that common Git directory to be current-user-owned, mode `0700`, free of extended ACL access, and
Time-Machine-excluded. This contains the local stale-object residual; it does not erase it. A future
non-macOS host needs an owner/ACL-enforcement amendment before it may run the licensed-local
profile.

`PROVENANCE.md` is a public receipt, not an asset-delivery mechanism. It pins the nine source IDs,
source hashes, derivative filenames/hashes, triangle counts, Unity GUIDs, `.meta` hashes, Blender
version, and conversion command. `PolyforkLocalCustody` is the compiled mirror used before Unity
authoring/build output mutation; the shell gate enforces exact receipt equality. The product owner
is responsible for protecting the local FBX/meta pack or explicitly enabling reacquisition. A hash
proves identity; it cannot recover lost bytes.

### A3. Owner-enabled reacquisition and metadata repair

Reacquisition is a deliberate human-run operation outside automated checkout, CI, test, build,
Unity-import, and runtime workflows. Source downloads stay in a freshly created owner-private
temporary directory outside the worktree; only a verified derivative/meta pair is hydrated into
the ignored local-custody path:

1. the owner explicitly enables use of the licensed account and credential;
2. fetch each recorded asset ID through the authenticated endpoint and verify the source SHA-256
   before conversion; mismatch fails closed and the response is deleted;
3. convert only with the pinned Blender 5.1.2 and committed conversion script, then verify the
   derivative SHA-256 and triangle count; mismatch fails closed and the unverified derivative is
   deleted;
4. restore the recorded `.meta` where available and verify its hash/GUID/import restrictions;
   metadata mismatch fails closed; or, if metadata was lost, update authoritative PROVENANCE and
   the compiled `PolyforkLocalCustody` receipt mirror together in one reviewed commit before
   re-authoring; then rerun `Cat Metro/Build Diorama Assets` to regenerate Cat-Metro-authored
   prefab/scene references and all visual evidence; and
5. install success/failure cleanup before acquisition and delete every source GLB, private
   temporary directory, and unverified output after successful or failed conversion.

No normal command may prompt for, discover, or fetch the credential. The original `.env` mode,
header-only request, no-logging, no-copy, and rotation controls remain unchanged.

### A4. Two explicit verification profiles

The permanently public tree has two non-interchangeable verification profiles:

1. **Clean-public profile.** A fresh checkout receives no local pack or owner cache. Required CI
   fetches and scans history reachable from the candidate and every fetched remote/tag ref. The
   custody gate validates exactly nine receipts, checks the history/index boundary described in
   A1, verifies gitignore coverage and current prefab payload tokens, and reports no local pack.
   `scripts/test.sh` routes the immutable CM-C2b verifier through
   `scripts/run-unity-editmode.sh`; on an editor-equipped clean checkout the driver runs its exact
   static prefix and loudly defers the editor half. The credential-free shell/static/domain suite
   may pass. This profile does **not** prove external-object erasure or that Unity can import,
   render, or build the Polyfork composition.
2. **Licensed-local profile.** All nine exact FBX/meta pairs exist on the ACL-checked macOS owner
   host. Set `CM_REQUIRE_POLYFORK_LOCAL=1` on the canonical test/build command; the shell gate
   recomputes derivative/meta hashes and GUIDs, rejects extra or partial inventory, and enforces
   local file/cache privacy. The test driver also selects this profile whenever any local pack
   entry exists. Unity EditMode tests enumerate
   exactly nine models and verify derivative hashes, triangle counts, palette, semantic
   `ModelImporter` restrictions, collider/material outcomes, prefab/scene construction, and visual
   behavior. Diorama authoring calls the compiled exact verifier before asset/scene mutation. The
   build wrapper, test harness, and test driver reject inherited acquisition credentials and
   `CM_UNITY_EDITOR` overrides. The licensed wrappers authenticate the fixed Unity 6000.3.16f1
   bundle's Apple-anchored Developer ID signature, application identifier, team identifier, and
   bundle version before the editor receives the project path. The CLI build validates the
   build-flow token and exact local pack
   before output-directory mutation; an Android build preprocessor consumes that token and
   revalidates the pack before `BuildPipeline` proceeds.

The owner-private, one-use build-flow token prevents accidental GUI/direct build entry and
sequences the reviewed shell path. It is **not authentication** and does not independently prove
that `scripts/build.sh` or the full shell custody gate ran: any process executing as the licensed
owner can forge its own path/nonce pair. The trust boundary is the owner plus the reviewed checkout.
GUI/direct `BuildPipeline` invocation remains unsupported, and release evidence must pair the
shell-gate transcript with the build record from the same final commit.

Direct execution of the frozen `tests/unity/editmode.test.sh` is likewise an unsupported
same-owner bypass of the custody-aware test driver and cannot count as licensed-local evidence.
The immutable file remains callable only so the canonical driver can execute its exact full body
after preflight or project its exact static prefix for the clean-public profile.

Missing local assets never count as passing asset-integrity evidence. Every merge candidate that
changes this art path, and every Play-bound build, must run the licensed-local profile immediately
before full Unity tests, Android build, rendered-frame inspection, and device evidence. Green
credential-free CI alone cannot close those legs.

This means a clean public clone is not visually self-contained: it cannot import or render the
Polyfork-dressed scene until a licensed owner hydrates it. The canonical `scripts/build.sh` default
is therefore verification-only; its explicit licensed-local profile is the deployable Android
path. Accepting that consequence is a human decision in the signature below. Project-owned
replacement art is the current route to a visually complete public clone.

### A5. Alternatives re-decided under the fixed public constraint

| Mechanism | Decision | Reason under a permanently public repository |
|---|---|---|
| Regular Git commits | Forbidden | Makes modified assets publicly downloadable as standalone files. |
| Git LFS | Forbidden | A public LFS object still delivers the file; a private endpoint becomes a private asset store. |
| Fetch during build, test, CI, or package restore | Rejected | Puts vendor availability and a credential on the reproducible/public build path and can deliver to an unlicensed runner. |
| Separate private asset store | Rejected for these nine | Adds a credential, access list, backup/fetch dependency, and delivery path; reconsider only through a signed amendment if team scale justifies it. |
| Ignored local custody + explicit owner reacquisition | Accepted | Smallest delivery boundary; no public payload and no vendor/runtime/CI dependency. |
| Private repository | Rejected by human ruling | Cat Metro remains public. |
| Project-owned/procedural replacement | Retained escape hatch | Current route to a visually complete public clone without licensed local custody; requires art rebuild, provenance, rendered evidence, and taste review. |
| Ship greybox / remove external models | Rejected for Lane 1A | Satisfies public custody but fails the tabletop-diorama requirement and human art ruling; retained as an emergency withdrawal fallback. |

### A6. Containment residual and response

History rewriting removed the paths from the branch's reachable PR-only history; it did not recall
bytes already disclosed. Former object IDs, generated pull-request merge refs, GitHub caches,
clones, forks, browser downloads, or other third-party copies may retain the blobs until garbage
collection or indefinitely outside GitHub. A complete hosted-object scrub may require a GitHub
Support request. Contacting GitHub or Polyfork is a separate human-authorized external action; this
amendment records the need but does not authorize that contact or claim erasure.

Before this amendment, project cache-server mode inherited the user's global Unity preferences and
allowed upload/download. The project now pins Accelerator disabled, but any Accelerator that may
have been configured and reached before that pin is a possible prior recipient; repository evidence
cannot prove whether an upload occurred or recall a remote cache. Any discovered remote cache is a
containment residual requiring an owner/security disposition before merge.

The old licensed blobs also remain in this machine's shared Git common object database, and Unity
has created project/global import caches while the local pack was hydrated. Those owner-local copies
are now protected by the owner/mode/ACL and Time Machine boundary in A2. They are intentionally not
claimed erased. At review time no Time Machine destination or configured Unity Accelerator endpoint
was found; that narrows current local evidence but cannot prove that no historical backup, cache,
clone, or remote upload exists. A newly enabled backup/synchronization service must exclude these
paths before the licensed-local workflow runs.

Old clones must not push or merge the former history. Collaborators must discard it or reconcile
onto the rewritten head without reintroducing any forbidden object. Suspected re-exposure fails
closed and blocks merge, build distribution, and release until a human security/license
disposition.

### Amendment A implementation and merge gates

Before Lane 1A merges:

1. the human signs all nine amendment propositions against the exact proposed commit;
2. GitHub and fresh-clone inspection find none of the 18 paths in #65's reachable head history, and
   the force-push/residual record remains visible in the PR and PROVENANCE trail;
3. the clean-public custody gate and credential-free required checks are green;
4. the owner-controlled worktree holds all nine exact FBX/meta pairs and
   `CM_REQUIRE_POLYFORK_LOCAL=1` passes without printing asset or credential content;
5. full licensed-local EditMode, PlayMode, Android build, rendered-frame, Pixel, logcat, CM-R21, and
   taste-gate evidence close on the final integrated head;
6. an independent RISKY code/security review finds no tracked/delivered model payload, broken
   custody preflight, credential path, public/shared cache, or undispositioned finding; and
7. HC-25 is asked fresh only after all preceding gates close; no earlier merge word applies.

### Amendment A human signature — pending

- [ ] I rule that Cat Metro's repository remains permanently public and reject the original
      private-repository custody choice.
- [ ] I acknowledge the prior public exposure and accept the recorded residual without treating
      history rewriting as recall or proof of erasure.
- [ ] I approve the derivative-free Git/LFS/PR/CI/artifact boundary, while retaining compiled-game
      and rendered-media use under the pinned July 2026 license.
- [ ] I approve owner-controlled, ignored local custody of the exact nine FBX/meta pairs and the
      licensed-recipient access limit, including the ACL-checked macOS owner-mode/cache/common-Git
      boundary, Time Machine exclusions, and the operational duty to exclude other backup services.
- [ ] I approve only explicit owner-enabled reacquisition with source/derivative verification,
      transient GLBs, no automated fetch, and the existing credential controls.
- [ ] I approve the GUID/meta receipt and repair path plus the clean-public and licensed-local
      verification profiles, including the canonical custody-aware Unity test route, fixed
      signed-Unity authentication, and the disclosed fact that direct raw Unity/test entry is
      unsupported and the one-use build-flow token prevents accidents but is not shell-origin
      authentication; missing assets do not count as asset-integrity success.
- [ ] I approve the re-decided alternatives matrix, including rejection of regular Git, public
      LFS, fetch-at-build, the current private-store option, a private repository, and shipping the
      greybox for Lane 1A.
- [ ] I accept that a clean public clone is not visually self-contained and that deployable art
      builds require a licensed-local checkout unless Cat Metro replaces the models.
- [ ] I understand this signature is not HC-25, release/deploy authority, vendor/Support contact,
      spend approval, a new dependency, a new asset family, or a Play upload.

- **Signed by:** pending human signature
- **Signature statement:** pending human signature against the exact proposal commit
- **Signed at (absolute date/time):** pending
- **Signed proposal head:** pending
- **Signature record commit:** pending
