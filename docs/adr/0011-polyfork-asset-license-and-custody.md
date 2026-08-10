# ADR-0011: Polyfork asset license and source custody

- **Status:** Accepted — human signature recorded 2026-08-10 against reviewed proposal
  `feb78a12f49fe0207ac61da2f5a363c5fd213b53`
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
- **Signature record commit:** PENDING

## Official source checked 2026-08-09

- [Polyfork licensing — stated last update July 2026](https://polyfork.dev/licensing)
