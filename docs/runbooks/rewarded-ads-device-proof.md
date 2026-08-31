# Rewarded ads configured-device proof

This runbook uses two distinct artifacts. First, a **controlled proof build** exposes only the
integration helper/test suite, duplicate-callback injection, and structured redacted diagnostics
needed for invasive checks. Second, a **separately frozen release candidate** is cut only after all
proof switches are off. Each has its own hash and receipt; proof-build evidence must never be
attributed to the release archive, store binary, or public candidate.

This runbook does not configure an account, create a build, install an app, operate a device, or
prove that the current repository already has native ad serving. Every account, dashboard, privacy,
signing, build, install, device, store, and evidence-capture action below is human-run.

LevelPlay serves and mediates the rewarded video and emits the local reward callback. Cat Metro's
shared purchase/entitlement/save path turns that callback into a named 24-hour lease. RevenueCat
`AdTracker` records supported lifecycle and impression-level revenue analytics. RevenueCat neither
serves the video nor verifies or grants this lease. This is not RevenueCat's server-verified reward
product, and the lease must never be described as RevenueCat-verified or server-verified.

No human-supplied value, credential, test-device identifier, dashboard token, private evidence, or
real runtime config belongs in Git. The sole device identifier written below is the repository-
authorized Android target serial. Do not read `.env`. Keep identifiers masked in retained evidence.

## Current proof status and prerequisites

Everything in this section is unchecked until the named artifact supplies the named evidence.
Wrapper pins alone do not prove the generated native dependency set, and a proof-build result does
not prove release-candidate contents.

- [ ] A human has created the ignored
      `unity/Assets/Resources/Monetization/rewarded_ads_config.json` from
      `config/rewarded-ads.example.json` using only these fields: `iosAppKey`, `androidAppKey`,
      `iosRewardedAdUnitId`, and `androidRewardedAdUnitId`.
- [ ] Both artifact receipts use one rewarded ad unit per platform for all four placements, and the
      LevelPlay dashboard contains exactly:
      - `wardrobe_try_conductor`
      - `wardrobe_try_engineer`
      - `wardrobe_try_scarf`
      - `wardrobe_try_goggles`
- [ ] A human has selected the serving networks and retained a private receipt of the native
      LevelPlay core version plus every selected adapter version. The repository currently proves
      only the Unity wrapper pin `9.5.1`; it does not prove native core or adapter resolution.
- [ ] A human has configured the selected networks in LevelPlay and recorded the matching Network
      Manager adapter selections. No selected-network adapter configuration is currently proven.
- [ ] LevelPlay initialization settings are captured separately from each artifact and prove SDK
      auto-init is disabled so Cat Metro remains the single init owner. No tracked LevelPlay
      settings asset or artifact settings receipt currently supplies that proof.
- [ ] Native resolution has completed separately for each artifact and its generated output has been
      inspected. No resolver receipt currently proves the native LevelPlay, adapter, or RevenueCat
      dependency versions.
- [ ] LevelPlay's integration validation and test suite are reachable through an explicit,
      proof-only app route. The repository currently has no app-callable integration helper/test-
      suite route; engineering must supply one before this check can pass.
- [ ] Privacy signals are applied before the single LevelPlay initialization. Current source does
      not prove a configured COPPA/consent/personalization signal path before initialization.
- [ ] A structured, redacted ILR diagnostic records the fields required in step 10. No such
      diagnostic currently exists.
- [ ] Exact `expiresAtUnixSeconds` can be read through a redacted diagnostic or platform-container
      extraction. The current **Borrowed** UI does not expose the exact value, and no diagnostic is
      currently present.
- [ ] A controlled proof-build switch can inject a duplicate reward callback into one known
      attempt. No configured-device injection path currently exists; ordinary test coverage is not
      a substitute.
- [ ] The controlled proof build and separately frozen release candidate have different artifact
      paths, hashes, and receipts. The release candidate is cut only after every proof helper,
      integration test mode, test device override, duplicate injector, verbose diagnostic, and
      debug switch intended to be absent from release is off.

## Human-supplied fields

Record field names and completion status here; record values only in private human storage.

### LevelPlay, mediation, and test setup

- iOS App Key
- Android App Key
- iOS Rewarded Ad Unit ID
- Android Rewarded Ad Unit ID
- Placement Name
- Test Device ID
- Selected Network Name
- Selected Network Account ID
- Selected Network App ID
- Selected Network API Key
- Selected Network API Secret
- Selected Network Placement or Ad Unit ID
- Network Manager Adapter Selection
- LevelPlay Test Suite Mode
- LevelPlay Integration Helper Result

The four `Placement Name` values are the exact repository-owned IDs listed above. All four reuse
the one platform ad unit; do not create four ad units merely because four placements exist.

### RevenueCat and store connection

- RevenueCat Project Name
- RevenueCat Project ID
- RevenueCat App Name
- Store Bundle or Application ID
- Ads Beta Access Status
- Ads Sandbox Environment
- Public Apple SDK Key (`appleApiKey`)
- Public Google SDK Key (`googleApiKey`)
- Test Store Selection (`useTestStore`)
- Apple Store Connection Status
- Google Play Store Connection Status
- App Store Connect In-App Purchase Key Issuer ID
- App Store Connect In-App Purchase Key ID
- App Store Connect In-App Purchase Private Key
- Google Play Service Account Credential

The private store-connection fields are human-held inputs only. Never place their values in the
runbook, runtime config, logs, screenshots, or repository. The RevenueCat runtime example fields
are names only; the real `revenuecat_config.json` and its `.meta` remain ignored.

### Privacy and compliance decisions

- Target Audience Selection
- General-Audience 13+ Decision
- Apple Kids Category Selection
- COPPA Treatment
- Neutral Age Screen Decision
- EEA/UK CMP Provider
- TCF Consent Status
- GDPR Treatment
- US State Privacy Opt-Out Treatment
- Personalized Ads Treatment
- ATT Prompt Timing
- ATT Denied/Not-Requested Behavior
- Privacy Options Re-entry Path
- Privacy Policy URL
- App Store App Privacy Answers
- Google Play Data Safety Answers
- Store Ads Declaration

Preserve the current general-audience 13+ positioning and do not select Apple's Kids Category
unless the human deliberately changes the product strategy. ATT denial or absence must never hide
the opt-in offer after it is otherwise ready, prevent gameplay, or prevent a completed LevelPlay
reward callback from granting the named lease.

### Signing, native, and release choices

- Candidate Platform
- Bundle or Application ID
- Marketing Version
- Build Number or Version Code
- Git Commit
- Candidate Artifact Path
- Candidate SHA-256
- Signing Team or Upload Certificate Reference
- Unity Version
- LevelPlay Unity Wrapper Version
- RevenueCat Unity Wrapper Version
- EDM4U Version
- Resolved LevelPlay Native Core Version
- Resolved RevenueCat Native Dependency Version
- Selected Adapter Name and Version
- iOS Dependency Route (Swift Package Manager or CocoaPods)
- Generated Privacy Manifest Inventory
- Required-Reason API Inventory
- SKAdNetwork ID Inventory
- Android Merged Manifest Path
- Android `AD_ID` Permission Decision
- Selected-Network Manifest/Plist Entry Inventory
- Release Debug/Test Switch Audit

### Evidence receipt metadata

Complete this table twice: once under **Controlled Proof Build Receipt** and once under **Frozen
Release Candidate Receipt**. Never copy an artifact path, hash, native inventory, switch audit, or
device result from one receipt into the other.

| Field | Record in the private evidence receipt |
|---|---|
| App version and build | |
| Git commit and tree status | |
| Artifact path and SHA-256 | |
| Device model and OS version | |
| Unity/LevelPlay/RevenueCat/EDM4U versions | |
| Resolved native core and adapter versions | |
| Timestamp and time zone | |
| Masked app key and ad-unit ID | |
| Exact placement ID | |
| Attempt/correlation reference | |
| Redacted device-log path | |
| Exact-expiry evidence path | |
| RevenueCat dashboard screenshot path | |
| LevelPlay validation/test-suite receipt path | |
| Privacy-state receipt path | |
| Release-switch audit path | |

## Configured physical-device sequence

Run the two-artifact sequence on iOS first. Do not skip ahead to Android because iOS signing, native
dependency, privacy-manifest, SKAdNetwork, ATT, and platform-container behavior are independent
proof surfaces.

Steps 2–13 operate on the controlled proof build. Step 14 cuts the separate release candidate;
steps 15–16 repeat and capture only release-candidate evidence.

1. **Freeze one controlled proof build.** Record every receipt field under **Controlled Proof Build
   Receipt** before the first install. Record its generated native SDK/adapters and exact enabled
   proof switches; do not infer them from Unity wrapper pins.

2. **Cross-check configuration.** Compare the ignored platform config with the LevelPlay and
   RevenueCat dashboards without copying values into evidence. Confirm one platform rewarded unit,
   the four exact placement IDs, their named entitlements, 24-hour leases, and Goggles' additional
   one-per-session cap.

3. **Resolve and validate native integrations.** Inspect the resolved native LevelPlay core,
   RevenueCat dependency, and every chosen network adapter. Use the explicit proof-only route to run
   LevelPlay's integration validation and test suite for every selected adapter. Until that route
   exists and its receipt is captured, mark this step outstanding; a managed test suite cannot pass
   it.

4. **Establish privacy before initialization.** Select the applicable COPPA, consent, US opt-out,
   and personalization states before the one LevelPlay init. Exercise ATT not-requested/denied and
   prove gameplay and the opt-in reward path remain usable. Current source plumbing does not prove
   this ordering, so retain a redacted timestamped diagnostic from the controlled proof build.

5. **Prove one iOS initialization and real readiness.** On a configured physical iOS device, cold
   launch the controlled proof build, confirm a single successful LevelPlay initialization, and
   confirm the shared rewarded unit becomes ready through selected test inventory. A local mock,
   Editor run, or dashboard configuration does not satisfy this step.

6. **Correlate one exact attempt.** Choose one of the four exact Wardrobe placements. Under one
   correlation reference require `Loaded`, `Displayed`, optional `Opened`/clicked, exactly one
   `Rewarded`, and one `Closed`. `Rewarded` and `Closed` may arrive in either order. Only `Rewarded`
   grants the selected named item through the shared entitlement ledger; `Closed` never grants.
   Confirm the Wardrobe visibly changes to **Borrowed** and any duplicate reward remains latched,
   with no second grant or expiry extension. `Opened` is required only when the ad was clicked.

7. **Force no-fill.** Through the approved LevelPlay test path, force load failure/no-fill and prove
   no item is granted, no blocking retry loop appears, and Wardrobe Buy/Restore/Back plus ordinary
   gameplay remain usable.

8. **Prove the original exact expiry survives restart.** After one successful lease, record the
   exact `expiresAtUnixSeconds`, kill the process, relaunch the same proof build, and read the value
   again from a redacted diagnostic or platform-container extraction. The two values must be
   identical. The **Borrowed** label alone is insufficient. Keep this check outstanding until an
   exact-value path is supplied.

9. **Prove duplicate callback handling only with controlled injection.** In a proof build, inject a
   second reward callback into the already rewarded attempt and prove there is no second grant and
   no expiry extension. Do not attempt to manufacture vendor callbacks in a release build. Until a
   controlled injection route exists, record this device check as outstanding and cite automated
   duplicate-latch coverage separately.

10. **Capture a structured, redacted ILR record.** For one correlated impression record raw
    LevelPlay revenue, raw LevelPlay precision label, mapped revenue micros, mapped currency `USD`,
    auction/impression ID, ad ID when supplied, ad-unit ID, placement, and network. Mask every ID.
    LevelPlay does not provide raw currency to this bridge: `USD` is the app's mapped currency, not
    a raw currency callback field. The current repository has no structured diagnostic, so this
    remains outstanding until supplied.

11. **Check only supported RevenueCat events.** In RevenueCat Ads sandbox/dashboard, correlate the
    events and dimensions the current UI actually exposes. This bridge sends `Loaded`, `Displayed`,
    `Opened`/clicked, `LoadFailed`, and `Revenue`. It does **not** send `Rewarded`, `Closed`, or
    `DisplayFailed`. Record mediator, rewarded format, placement, ad unit, network, impression, and
    revenue only when visible. Never claim the dashboard exposes raw micros or precision unless the
    screenshot visibly proves those fields, and never call dashboard ingestion reward verification.

12. **Treat nonzero revenue as optional.** Test inventory may legitimately report zero revenue.
    If appropriate live inventory later produces a nonzero ILR sample, retain it in a separate
    redacted receipt; it is not an engineering completion gate.

13. **Exercise lifecycle and privacy variants.** Repeat background/resume, cold launch, network
    recovery, and ATT denied/not-requested. Confirm no duplicate initialization, no duplicated
    subscriptions, no duplicate grant, and no blocked gameplay or purchase route.

14. **Cut a separate frozen release candidate.** Turn off integration helper/test-suite access,
    test inventory, test-device overrides, duplicate-callback injection, structured verbose
    diagnostics, Development Build, and every other proof-only switch. Create a new artifact and
    complete a new **Frozen Release Candidate Receipt** with its own path and hash. Never relabel the
    controlled proof build as this candidate.

15. **Repeat real native/device checks on the release candidate.** Against the new release-candidate
    hash, independently repeat and record: resolved native core/RevenueCat/adapters and init settings;
    merged manifest/plist, privacy manifest, SKAdNetwork/`AD_ID`, signing, and archive contents;
    privacy signals before one init; ATT denied/not-requested; cold-launch readiness; one ordinary
    correlated load/display/reward/close with reward/close in either order; one local named lease;
    background/resume/cold relaunch; and the supported RevenueCat events the release configuration
    actually ingests. Repeat exact-expiry proof by platform-container extraction if available;
    otherwise leave release-candidate exact expiry unverified rather than borrowing the proof-build
    diagnostic. Helper/test-suite, forced no-fill, structured diagnostic, and duplicate-injection
    receipts remain labelled **proof-build policy evidence only** and never prove final archive or
    store-binary contents. The release-switch audit must prove those controls are absent/off.

16. **Capture the submission beat from the release candidate.** Record locked item need -> explicit
    opt-in -> completed rewarded video -> visibly borrowed named item from the exact release-
    candidate receipt. Do not splice in proof-build, Editor, concept-art, or another-build behavior.

17. **Repeat later on Android/Pixel.** Only after the iOS evidence bundle is complete, repeat this
    two-artifact sequence for Android. Before **every** human-run Android device command, run
    `adb devices -l` and require serial `48121FDAP006X4` plus its matching `model:` to identify the
    Cat Metro Pixel 9 Pro. Explicitly exclude the Quest and Pico devices owned by other projects.
    The agent never runs ADB, installs, builds, or captures. Do not reuse iOS or proof-build native,
    privacy, restart, dashboard, archive, or store evidence for the Android release candidate.

## Platform candidate checks

### iOS first

- [ ] Xcode is version 26 or newer and the generated project uses minimum iOS 15 or newer.
- [ ] Bundle ID, marketing version, build number, team, provisioning, and release signing match the
      frozen receipt.
- [ ] The generated project contains the resolved LevelPlay native core, every selected adapter,
      and the actual RevenueCat native dependency with recorded versions.
- [ ] The chosen Swift Package Manager or CocoaPods route is singular and complete; no stale second
      route contributes duplicate native dependencies.
- [ ] Privacy manifests, required-reason declarations, and SDK signatures are merged and audited
      against the final archive.
- [ ] Selected-network plist entries and the complete generated SKAdNetwork ID inventory are present.
- [ ] ATT usage description and prompt behavior match the final binary; ATT denial/not-requested
      never gates a completed reward.
- [ ] Export-compliance determination is recorded from the final archive.
- [ ] Xcode Organizer validation passes and every proof/test/debug switch is off in the archive.

### Android later

- [ ] Before every human-run Android device command, `adb devices -l` reports serial
      `48121FDAP006X4` and its matching `model:` identifies the Cat Metro Pixel 9 Pro. Quest and Pico
      targets are explicitly excluded.
- [ ] Package ID, version name, version code, candidate hash, and signing reference match the frozen
      receipt.
- [ ] Minimum SDK is 25 or newer, target SDK is 36 or newer, and the candidate is ARM64/IL2CPP.
- [ ] EDM4U output records the resolved native LevelPlay core, every selected adapter, and the
      actual RevenueCat common/native dependency.
- [ ] The final merged manifest is inspected for `AD_ID`, consent/privacy components, exported
      components, and every selected-network entry; the `AD_ID` decision matches store disclosure.
- [ ] Gradle dependencies, repositories, ProGuard/R8 rules, and consumer rules are inspected from
      the exact candidate rather than inferred from source packages.
- [ ] Upload signing and Play App Signing references match the candidate receipt.
- [ ] Development, test-suite, test-device, duplicate-injection, and verbose diagnostic switches
      are off in the release AAB.

## Evidence boundary

Automated .NET, EditMode, and PlayMode tests can prove managed mapping, callback-order and
deduplication policy, atomic save ordering, migration/import/expiry behavior, local-date/session
caps, the shared entitlement path, and rendered no-fill UI. They cannot prove native artifacts.

Only an artifact's own native contents plus its configured physical-device/dashboard evidence can
prove native resolution and linking, selected adapters, real LevelPlay initialization/fill/show and
callback threading, the ILR payload, RevenueCat ingestion/rendering, platform-container persistence,
OS privacy UI, or background lifecycle for that artifact. Only the frozen release candidate's own
archive audit proves shipped archive contents.

Keep the proof domains separate:

- RevenueCat dashboard evidence can prove supported analytics ingestion; it cannot prove the local
  reward, durable save, cap advancement, or exact expiry.
- Redacted local logs/container evidence can prove local callbacks and persistence; it cannot prove
  RevenueCat received or rendered an event.
- Unit/EditMode/PlayMode tests can prove managed policy; they cannot prove native core/adapters,
  real inventory, an OS prompt, a signed archive, or a store-delivered binary.
- Wrapper versions cannot prove native dependency versions. Record generated native versions from
  each candidate.
- Controlled proof-build helper/injection/diagnostic evidence proves only those controlled checks.
  It cannot prove release-candidate native contents, switch state, device behavior, archive, store
  binary, or submission footage; the frozen release candidate needs its own receipt and repeats.

Related human release authorities: `docs/release/ios-release-runbook.md`,
`docs/release/play-release-runbook.md`, `docs/release/release-checklist.md`, and
`docs/runbooks/revenuecat-setup.md`.
