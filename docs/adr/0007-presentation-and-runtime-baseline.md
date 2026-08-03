# ADR-0007: Presentation and runtime baseline — UGUI, no Addressables, no remote config, flags as the kill switch

- **Status:** Proposed (ratifies `docs/plan/specs/architecture.md:13,14,19,20,21,22,23,26,55-76,97-102`)
- **Date:** 2026-08-02
- **Relates:** ADR-0002 (Presentation interpolates, never simulates), ADR-0003 (assembly boundary), ADR-0006 (flags persist in the save).

## Context

Eight runtime choices in `docs/plan/specs/architecture.md` share one shape: each is a case where the
heavyweight, "proper" option would be defensible on a longer timeline and is not defensible here.
Bundling them into one ADR is deliberate — they are one decision made eight times ("take the mature,
smaller option; the scale does not justify the framework"), and splitting them would produce eight
ADRs with the same rejected-alternative reasoning.

The forcing constraints: one content pack shipped locally with no remote-content failure path during
the event (`docs/plan/specs/architecture.md:18`); a ≤60 MB AAB and a ≤3.5 s cold start on mid-tier
(`docs/plan/specs/architecture.md:92-94`); zero GC allocations per frame in `Playing`
(`docs/prd/PRD.md:103`); an agent fleet whose generated code must stay legible; and no server
anywhere at 1.0 (`docs/prd/risks.md:145`).

## Decision

### UI — UGUI + TextMeshPro, not UI Toolkit
Mature world-space + screen-space mixing, mature accessibility/localization patterns, and the
juice-heavy HUD (purr meter, overload ring, chain chimes, cause-first camera) is exactly where UI
Toolkit's runtime is weakest (`docs/plan/specs/architecture.md:14`). Thumb-zone layout constraints
(CM-R07.4: all interactive UI in the bottom 25% of the safe area, `docs/prd/PRD.md:198`) are
validated against RectTransforms by an editor check.

### Navigation — a screen stack inside Home and Game, not scene loads
Four scenes only: `Boot → Home ⇄ Game`, plus editor/dev-only `Capture`
(`docs/plan/specs/architecture.md:55-65`). Screens push/pop on a stack; the stack is persisted as a
breadcrumb in the save (ADR-0006 `breadcrumbs.screenStack`) so a process death lands the player back
where they were (`docs/plan/specs/architecture.md:74-75`). Scene loads on mobile cost more than they
organize.

### No Addressables at launch
One content pack, local, ~50 levels of JSON plus atlased sprites
(`docs/plan/specs/architecture.md:19`). Levels ship in `StreamingAssets` (ADR-0008); art ships in
Resources-free direct references from the scenes that use it.

### No remote config — except RevenueCat Offerings, which is remote by design
Offerings give paywall/product remote control without standing up a config service
(`docs/plan/specs/architecture.md:20`). **Nothing else is remotely mutable at 1.0.**

### Feature flags are the kill-switch mechanism (the direct consequence of the line above)
Compile-time defaults + runtime values persisted in the save (`flags` in ADR-0006 §2):
`ads_enabled`, `paywall_placements`, `daily_enabled`, `weekly_event`, `share_card`,
`leaderboard` (OFF at launch) (`docs/plan/specs/architecture.md:97-102`). Properties:
- A flag OFF must remove the *surface*, not just the entry point — a dark system shows no chip, fires
  no event, fetches no placement.
- **Flags are local.** Flipping one for the installed base requires an app update. That is the
  accepted cost of having no config service, and it must be stated plainly wherever a "kill switch"
  is claimed (CM-R56.4 honesty rule) — for the installed base it is a *next-update* switch, immediate
  only for the developer/testers.
- `ads_enabled` is the named contingency lever for two live conditions: RC Ads beta not granted by
  D10 (`docs/plan/EXECUTION_PLAN.md:560`) and **AdMob invalid-traffic/account-health warnings**
  (RK-25, `docs/prd/risks.md:95`, which notes the PRD does not yet list account health as a trigger —
  it is listed here).
- NEW-Q45 (consent management: certified CMP/UMP, restricted availability, or ship
  `ads_enabled=false` — `docs/prd/PRD.md:38`) is an **open human decision** that this flag exists to
  make cheap either way. No agent answers it.

### Audio — Unity `AudioSource` pools + one central mixer, layered combo stems
No FMOD/Wwise: licensing plus integration cost unjustified for a pentatonic chime chain and a
handful of stems (`docs/plan/specs/architecture.md:21`). Pools are pre-warmed so `Playing` allocates
nothing.

### Haptics — Android `VibrationEffect` behind a thin JNI helper, 3 tiers, master toggle
Unity's built-in `Handheld.Vibrate` is single-amplitude only
(`docs/plan/specs/architecture.md:22`). The helper lives in `CatMetro.Presentation` behind an
`IHaptics` interface with an Android implementation and a no-op implementation for editor/desktop —
it is Presentation-internal, **not** a `CatMetro.Services` interface, because nothing outside
Presentation ever asks for a buzz.

### Localization — a custom CSV string table, EN only at launch, structure ready
The Unity Localization package is heavy for one language, and a CSV keeps agent-generated content
safe and diffable (`docs/plan/specs/architecture.md:23`). Every player-facing string is a key; a
`[CI]` check fails on a literal string in a UI component. This is what makes CM-R03.2, CM-R08.6 and
CM-R10.1's "zero occurrences of 'Rooftop Line'" grep gates mechanical.

### DI — a composition root with constructor injection into plain C# services; no framework
Zenject/VContainer are unnecessary at this scale and add reflection/codegen that IL2CPP stripping
must then be taught about (`docs/plan/specs/architecture.md:26`). The composition root is
`CatMetro.Bootstrap` (ADR-0003) and it is the *only* place `new` meets an SDK adapter.

### Input — Unity Input System, tap/pointer only, one gesture handler
Single verb (`docs/plan/specs/architecture.md:13`, CM-R07.1). A tap enqueues a
`ToggleSwitchCommand` and the lever animates *immediately*, independent of the tick boundary at which
the command applies — that decoupling is what buys the ≤50 ms perceived latency (CM-R07.2) without
touching determinism.

### Android lifecycle rules (ratified verbatim, `docs/plan/specs/architecture.md:78-83`)
`OnApplicationPause(true)`: flush save + analytics queue synchronously within
`SAVE_PAUSE_BUDGET_MS` (ADR-0006), pause the sim, timestamp for the session gap. A purchase or ad in
flight during pause is **never granted from memory on resume** — reconcile from RC `CustomerInfo` /
ad callback + ledger. Back gesture: pause menu in Game, stack pop in Home, never exits mid-purchase.
Process-death (`adb kill` during save / purchase / ad) is part of the smoke suite.

## Alternatives seriously considered

- **UI Toolkit (runtime).** Real advantages: Unity's stated direction, better authoring workflow,
  cleaner styling, and it would age better past this window. Lost on the specific product: this HUD
  is world-space-anchored juice over a game board, which is UGUI's home turf and UI Toolkit's
  weakest runtime area — and betting the highest-churn layer of a 8-week build on the less-proven
  system has no upside inside the window.
- **Addressables.** Real advantages: on-demand content, patchable level packs without a store update,
  and a natural home for the post-launch bands 31-60. Lost because we ship one local pack: it would
  add build complexity, a catalog to version, and a new class of "content failed to load" boot
  failures — during an event whose plan explicitly says "no remote-content failure path"
  (`docs/plan/specs/architecture.md:18`). Post-window, when content updates outnumber code updates,
  this is the first thing to revisit.
- **A remote config service (Firebase Remote Config / Unity Remote Config).** Real advantage — and it
  is a real one: **instant** kill switches for the installed base, which our flags do not give. Lost
  because Firebase Remote Config is another SDK on the EDM4U resolution (the sharpest integration
  risk, `docs/plan/specs/architecture.md:106`), another declared data flow in the Data Safety form,
  another network dependency on the boot path, and a *remote surface that can change client behaviour*
  — which would re-open the threat-model class the project currently gets to skip
  (`docs/prd/risks.md:147`). The accepted residual is stated plainly above: our kill switches are
  next-update switches for the installed base.
- **Scene-per-screen navigation.** Real advantage: crisp memory boundaries and Unity-idiomatic
  organization. Lost on cold-start and transition cost on mid/low-tier Android, and on the process-death
  requirement — restoring a screen *stack* from a breadcrumb is far simpler than restoring a scene
  graph.
- **FMOD or Wwise.** Real advantages: proper adaptive layering for the combo stems, mixer snapshots,
  profiling. Lost on licence + integration + build-size cost for a soundtrack this small, in a game
  whose design ceiling is literally "sound off by default" (`docs/prd/PRD.md:46`).
- **Unity Localization package.** Real advantages: the right answer the day a second language ships,
  with pluralization, smart strings and addressable-backed tables. Lost for EN-only launch; the CSV
  table is structured so the migration is a table import, not a rewrite.
- **VContainer / Zenject.** Real advantages: lifetime scopes, less wiring boilerplate in the
  composition root, and genuinely nicer test seams. Lost because the wiring is one file, and because
  reflection/codegen containers and IL2CPP stripping interact in ways that cost debugging days at
  exactly the wrong time. Constructor injection into plain classes gives the same test seams with
  zero machinery — and keeps agent-generated code obvious.
- **`Handheld.Vibrate` instead of JNI.** Real advantage: zero platform code, works in editor. Lost:
  single amplitude cannot express three tiers, and the tiers are part of the feel spec.
- **The legacy Input Manager (`Input.GetTouch` / `Input.mousePosition`) instead of the Input System
  package.** Real advantages, and for this game they are genuinely the strongest case on this list:
  the product has exactly **one verb** (tap a junction), so the whole input layer is a few lines of
  `Input.GetTouch(0)` + a raycast; it needs **no package at all**, which means one less
  `manifest.json` pin (ADR-0004), one less thing in the IL2CPP strip path, and no input-action asset
  to keep in sync; it is the smallest possible system by the constitution's sizing rule, and "we take
  the smaller option" is the through-line of every other row in this ADR. **It loses on one concrete
  thing:** `EnhancedTouch` is what `docs/plan/specs/architecture.md:13` names for **multi-safe hit
  tests** — a board where two junction colliders can fall under simultaneous or overlapping touches
  needs unambiguous per-touch tracking, and the legacy API's global `Input.touches` polling is where
  double-fire and dropped-touch bugs live on Android. Getting a wrong or duplicated
  `ToggleSwitchCommand` into the command log is not a cosmetic bug: it is a **determinism-visible**
  defect that changes the replay hash and can invalidate a Daily Line result (ADR-0002 §6). Paying one
  package pin to make the tap→command edge unambiguous is worth it precisely because that edge feeds
  the one system in this project that must be bit-exact. The Input System is also the new-project
  default, so choosing legacy would mean actively switching the project's input backend.

## Consequences

**Easier.** Cold start has no remote dependency, no catalog, no container reflection, and no
localization asset resolution. Build size and boot path stay auditable. An agent reading
`CatMetro.Bootstrap` can see the whole object graph in one file. Every player-facing string is
greppable, which is how the PRD's string-level `[CI]` gates work at all.

**Harder.** A kill switch costs an app update for the installed base — the single sharpest cost in
this ADR, and the one to re-read if a live incident happens. UGUI layout work is more manual than
UI Toolkit's. Manual wiring grows with the service count (it is currently **9** interfaces — 6
ratified + `IContentSource`, `IDiagnostics`, `IStorageRoot`, ADR-0003; if it ever reaches ~25,
revisit DI). Adding a second language later means importing the CSV into a real
localization system.

**Locked in:** the **string-table key namespace** becomes a de-facto contract with the store listing
and the string-level CI gates; the **flag names** appear in the save schema (ADR-0006 §2), so
renaming one is a save migration. Everything else here is genuinely reversible — Addressables,
UI Toolkit, a DI container and a remote config service can all be adopted post-window without
touching the Domain, which is precisely why the Domain boundary (ADR-0003) matters more than any of
these choices.

## Security notes

- **No remote config is a security *feature* here.** With RC Offerings as the only remote surface,
  the client has exactly one third party that can change what a player sees, and that surface is
  commerce-shaped and RC-authenticated. Adding a general remote-config service would create an
  attacker-valuable "change the client's behaviour remotely" endpoint, which is precisely the shape
  of RK-29 (`docs/prd/risks.md:106`).
- **Remote copy is rendered as plain text, never rich text/HTML** (RK-29), and no remote content is
  rendered inside a purchase or entitlement surface. Push/IAM destinations are restricted to the
  deep-link allowlist (RK-27, `docs/prd/risks.md:104`) — the router contract is in
  `docs/architecture/overview.md` and its hardening is a human-commissioned MUST, not an agent's
  choice.
- **Flags must fail closed.** A missing, unparseable or out-of-range flag value reads as the
  compile-time default, and for `ads_enabled` that default is the safe one. A flag read must never
  throw on the boot path.
- **Trust boundary reminder:** haptics JNI is the only hand-written platform interop in the client;
  it takes no external input (tier enum only), so it adds no parsing surface.
- **Consent (NEW-Q45) is unresolved and blocks ad surfaces**, not this ADR: shipping ads without a
  certified CMP/UMP flow or a restricted-availability decision is a policy exposure the human must
  close (`docs/prd/PRD.md:38`). `ads_enabled=false` is the fully-supported answer if it does not close
  in time.
