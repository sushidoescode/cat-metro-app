# Shipaton 2026 submission checklist

Use this checklist against one exact public release candidate. It records engineering evidence
separately from human submission actions and does not claim that an unchecked item already exists.

**Eligibility window:** 2026-08-01 through **2026-09-30 at 11:45pm PDT**.

Eligibility requires Cat Metro's first public release to be live on an accepted store during the
Shipaton window. App Store, Google Play, and Samsung Galaxy Store are accepted. TestFlight,
internal/closed testing, “in review,” “Waiting for Review,” and “Pending Developer Release” are not
public-live. Ship iOS first; perform the Android/Pixel release later because the Play account path
can add closed-test and production-access lead time.

All accounts, configuration, credentials, signing, builds, archives, uploads, store releases,
promo-code generation, organizer contact, public video hosting, and Devpost submission actions are
human-only. Never put a credential, judge code, human-supplied test-device identifier, dashboard
token, or private receipt in Git; the sole device identifier written below is the repository-
authorized Android target serial. Never read `.env`.

## Engineering evidence gate

- [ ] Freeze one exact candidate receipt: Git commit and tree status, app version/build, artifact
      path and SHA-256, device/OS, SDK and resolved native dependency versions, timestamp/time zone,
      and masked configuration identifiers.
- [ ] Run the repository, Unity, candidate-artifact, device, and privacy checks required by the
      applicable release runbook. Keep automated, native-artifact, device, dashboard, and store
      evidence labelled separately.
- [ ] Prove the shipped candidate uses the RevenueCat SDK to power a real purchase or RevenueCat
      Ads tracking path. Merely bundling the SDK does not satisfy the gate.
- [ ] For the Catvertising path, complete
      `docs/runbooks/rewarded-ads-device-proof.md` on a configured physical iOS device first and on
      the authorized Pixel 9 Pro later. Preserve separate controlled-proof-build and frozen-release-
      candidate receipts; proof-build evidence never proves the public binary. Unchecked native/
      configuration prerequisites remain outstanding.
- [ ] Confirm the shipped privacy policy, App Store App Privacy answers, Play Data Safety answers,
      ads declaration, IAP declaration, content rating, CMP/TCF behavior, ATT behavior, and binary
      all describe the same exact candidate.
- [ ] Confirm the public candidate remains rewarded-video-only: no interstitial, banner,
      rewarded-interstitial, or app-open ad path and no paid randomness.
- [ ] Confirm the human has made and privately recorded the commercial-distribution decision for
      every paid Meshy and Tripo asset embedded in the candidate. Repository cleanliness and local
      development permission do not settle public-store licensing.

## Public-live eligibility

- [ ] Human selects the accepted-store release path and records the store name in the private
      receipt.
- [ ] Human completes signing, archive/AAB validation, store metadata, privacy disclosures, review,
      upload, and release for the exact candidate.
- [ ] The first public version becomes downloadable during the eligibility window and before
      **2026-09-30 11:45pm PDT**.
- [ ] Open the public product page while logged out in an intended storefront/region and preserve
      the public URL, timestamp, time zone, version/build, and a screenshot. A review or test URL is
      not acceptable.
- [ ] Install or open the store-delivered version and repeat the qualifying RevenueCat path and
      ordinary gameplay smoke check; do not substitute a sideloaded proof build.
- [ ] Keep iOS as the first release sequence. If Android is also submitted, the human first runs
      `adb devices -l` before every Android device command and requires serial `48121FDAP006X4` plus
      its matching `model:` to identify the Cat Metro Pixel 9 Pro. Exclude the Quest and Pico, and
      preserve a separate Play-delivered release-candidate receipt.

## RevenueCat and Catvertising truth gate

- [ ] RevenueCat SDK usage is visible in the shipped path and supported by configured-device plus
      dashboard evidence, not source text alone.
- [ ] A human asks the Shipaton organizer in writing whether **LevelPlay serving + RevenueCat
      `AdTracker` analytics** qualifies for Catvertising and preserves the response privately.
- [ ] Treat organizer confirmation as a human administrative follow-up, not an engineering blocker.
      Do not pre-assert the organizer's ruling in submission copy while confirmation is pending.
- [ ] Submission copy states that LevelPlay serves/mediates the rewarded video and emits the local
      reward callback; Cat Metro's shared entitlement/save path grants the named lease; RevenueCat
      `AdTracker` records supported lifecycle and impression-revenue analytics.
- [ ] Submission copy never says RevenueCat serves the video, grants the lease, verifies the reward,
      or makes the lease server-verified.
- [ ] Dashboard copy and screenshots claim only the events actually sent by this bridge:
      `Loaded`, `Displayed`, `Opened`/clicked, `LoadFailed`, and `Revenue`. Do not claim RevenueCat
      receives `Rewarded`, `Closed`, or `DisplayFailed`.
- [ ] Revenue copy says the callback revenue is mapped to `USD` and integer micros. It does not call
      `USD` a raw LevelPlay currency field, and it claims raw micros/precision in RevenueCat only if
      the current dashboard visibly exposes them.
- [ ] If test inventory reports zero revenue, say so. A separate nonzero live ILR sample is optional,
      not an engineering completion requirement.

## Truthful shipped rewarded surface

Confirm each statement against the exact public candidate before using it:

- [ ] The only rewarded surfaces are four opt-in try-ons beside locked named Wardrobe items:
      - `wardrobe_try_conductor`
      - `wardrobe_try_engineer`
      - `wardrobe_try_scarf`
      - `wardrobe_try_goggles`
- [ ] All four placements reuse one rewarded ad unit for the platform.
- [ ] A LevelPlay local reward callback grants through the same purchase/entitlement ledger and save
      path used by the rest of the Wardrobe; Presentation does not create a second unlock state.
- [ ] Each named item is borrowed for 24 hours with its original exact expiry surviving restart.
- [ ] Goggles additionally enforce one opportunity per session.
- [ ] No-fill or missing configuration hides the optional ad action and leaves gameplay,
      Buy/Restore, and Back usable.
- [ ] No ad interrupts gameplay; there are no interstitials.
- [ ] No failure rewind exists in this candidate. Do not describe, film, or imply rewarded rewind,
      retry, extra moves, or a level-boundary ad.

## Public video, maximum two minutes

- [ ] Human hosts the final video publicly on YouTube or Vimeo and verifies it while logged out.
- [ ] Duration is **2:00 or less** in the public player, not only in the local editor timeline.
- [ ] Every gameplay and monetization beat comes from the exact target-device candidate; no concept
      art, Unity Editor, proof-only helper, another branch, or another build substitutes for shipped
      behavior.
- [ ] The cut includes actual puzzle play and this unambiguous Catvertising sequence: locked named
      Wardrobe need -> explicit opt-in offer -> completed rewarded video -> visibly borrowed item.
- [ ] The narration/captions accurately distinguish LevelPlay serving/local reward callbacks from
      RevenueCat `AdTracker` analytics and do not claim organizer approval while pending.
- [ ] The cut shows no credentials, private notifications, account details, device identifiers,
      dashboard tokens, judge codes, or unmasked configuration IDs.
- [ ] Music, narration, footage, fonts, logos, and every other included asset are owned, licensed,
      or permitted for public submission. Do not use unlicensed media.
- [ ] The video does not state a campaign level count unless the exact public candidate receipt
      proves the count through ordinary progression and its release validation.
- [ ] Preserve the public video URL, final duration, candidate commit/build, device, capture date,
      and hosted-page screenshot in the private submission receipt.

## Icon and screenshot

- [ ] Supply a **1024×1024** submission icon exported from the exact approved visual identity.
- [ ] Supply at least one **1179×2556** screenshot as an opaque sRGB image with no alpha and no
      device frame, bezel, status bar, editor chrome, caption band, or concept-art substitution.
- [ ] The screenshot is a real exact-candidate frame at the required resolution or a proportional
      target-device capture cropped without stretching; it is not upscaled from a smaller image.
- [ ] Inspect the screenshot at full size and at a 20% thumbnail. Confirm the actual shipped UI,
      materials, cats, pins, stations, safe area, and art assets are present and readable.
- [ ] Preserve icon/screenshot paths, dimensions, color/alpha inspection, SHA-256 hashes, candidate
      commit/build, level or screen, device, and capture date in the private receipt.

## Description, categories, and access

- [ ] Human registers for Devpost and selects only categories the exact candidate and evidence can
      support.
- [ ] Main description and category blurbs are rewritten from shipped proof. Remove outdated
      statements from `docs/release/submission-plan.md` instead of copying them blindly.
- [ ] Catvertising copy names the four Wardrobe need-state placements and explains why opt-in named
      borrowing fits the player need without interrupting a puzzle.
- [ ] HAMM/RevenueCat copy reports only real purchase, conversion, retention, or revenue numbers,
      with date range and denominator where applicable; omit unavailable metrics rather than
      estimating them.
- [ ] Do not state a level count unless the exact candidate proves it. The tracked content artifact
      contains 19 JSON files, `L001.json` through `L019.json`, while older research/design documents
      report 17. The source discrepancy and tracked filenames do not prove ordinary reachability;
      no count enters submission copy until the public-candidate receipt proves the exact reachable
      count through ordinary progression.
- [ ] Human prepares judge access instructions and a working free-trial or promo-code path that
      unlocks every premium feature required for judging.
- [ ] Judge credentials, trial details, promo codes, and redemption receipts remain human-held and
      never enter Git, public video frames, or public screenshots.
- [ ] Human tests judge instructions from a clean account/device state and preserves one unused
      working access code or equivalent route for submission.

## Audience, store, and rights audit

- [ ] Store listing and screenshots remain puzzle-first and general-audience; target only 13+
      brackets on Play.
- [ ] Never select Apple's Kids Category unless the human deliberately changes the product and
      accepts the resulting obligations; a cute cat aesthetic is not authorization to select it.
- [ ] IARC/App Store age-rating answers are truthful and match rewarded ads, deterministic named
      purchases, and the absence of paid randomness.
- [ ] Privacy policy and disclosures include the exact shipped LevelPlay networks/adapters,
      RevenueCat, CMP/consent behavior, identifiers, analytics, and purchase behavior.
- [ ] ATT acceptance is never required for gameplay, purchase, or a completed rewarded grant.
- [ ] Every store and submission claim—no forced ads, no paid randomness, named 24-hour Wardrobe
      leases, and no rewind—matches the exact binary.
- [ ] Human has made the final paid-asset commercial license decision before any public upload and
      confirms the video/screenshot rights separately from binary rights.

## Human-only submission actions

- [ ] Configure human-owned LevelPlay, selected-network, RevenueCat, Apple, Google, CMP, and Devpost
      accounts and keep their values private.
- [ ] Create the real ignored configs without committing them.
- [ ] Select signing identities/keys, build, archive, validate, install, and capture the exact
      candidates.
- [ ] Complete App Store Connect and/or Play Console records, disclosures, product configuration,
      review notes, uploads, review responses, release controls, and public rollout.
- [ ] Generate and validate judge promo/trial access.
- [ ] Contact the Shipaton organizer and preserve written Catvertising confirmation.
- [ ] Edit and publicly host the final video; upload the icon and screenshot.
- [ ] Complete every Devpost field, category choice, store URL, video URL, judge-access field, and
      final submission action before the deadline.

## Final logged-out audit and receipt

- [ ] A second human pass checks every required field, link, image, category, description, privacy
      statement, and judge-access instruction against the exact public candidate.
- [ ] The accepted-store URL resolves while logged out and shows the submitted version publicly
      downloadable; no TestFlight/internal/review state is mistaken for live.
- [ ] The YouTube/Vimeo URL resolves while logged out and reports a duration no greater than two
      minutes.
- [ ] The icon is 1024×1024. At least one screenshot is opaque sRGB 1179×2556 and frameless.
- [ ] Public-live timestamp precedes 2026-09-30 11:45pm PDT and falls inside the eligibility window.
- [ ] RevenueCat qualification evidence and, if entered, the Catvertising configured-device bundle
      correspond to the store-delivered candidate.
- [ ] Human submits Devpost and saves the final submission confirmation/receipt, final field export,
      public URLs, eligibility timestamp, and asset hashes in private storage.

Related authorities: `docs/release/submission-plan.md`, `docs/release/ios-release-runbook.md`,
`docs/release/play-release-runbook.md`, `docs/release/release-checklist.md`, and
`docs/runbooks/revenuecat-setup.md`.
