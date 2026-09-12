# Devpost entry — draft

**Status: DRAFT. Not submittable.** Three fields cannot be filled until inputs land: the store
URL (needs the production release), the video link (needs the Pixel), and the judge promo code
(needs Console). Everything else below is written and count-checked.

Judges may judge from text, images and video alone, so this text has to stand on its own.

---

## Project name

```
Cat Metro
```

## Elevator pitch (Devpost limit 200 characters — this is 118)

```
A one-thumb tabletop train puzzle. Route cat commuters to the right station. No forced ads, no energy timers, no loot boxes.
```

## Built with

```
unity, c-sharp, il2cpp, android, revenuecat, google-play-billing, blender, urp
```

## Story

### Inspiration

Puzzle games on phones mostly ask you to wait or to watch. We wanted the opposite: a small wooden
train set on a desk that you can pick up with one thumb, solve in ninety seconds, and put down —
with nothing between you and the next level. The cats came first, honestly. The routing puzzle
grew around wanting somewhere for them to sit.

### What it does

Tap a junction to throw its switch and route each cat commuter to the station matching its colour
**and** symbol — the symbols are there so the game reads without relying on colour vision. Sixty
hand-authored campaign levels introduce one mechanic at a time; a Daily Line unlocks after seven
campaign wins. Every level is solver-proved before it ships, so no level in the campaign is
impossible and none needs a hint purchase.

### How we built it

Unity 6 with URP, IL2CPP, ARM64. The board is not an authored scene — it is built at runtime from
the level DTO, which is what lets a solver validate all sixty levels in CI and lets the capture rig
replay any level to an exact tick for deterministic screenshots. The simulation is a separate
plain-C# library with its own test suite, so the game logic is testable without Unity at all.
Monetisation is a single optional cosmetic through RevenueCat over Play Billing. The cats and props
are licensed 3D art corrected only in the presentation layer; the carriage is our own Blender model
with a reproduction script checked in.

### Challenges we ran into

The honest one: **a whole clearance test suite can be green while the thing is visibly wrong.**
Every geometry check in the project projects onto the board plane, so when we scaled the trains up
to make the cats readable, the rider's feet went six centimetres through the carriage floor and not
one test noticed. The seat sink was measured from the carriage origin, but the carriage grows
upward from the rails — the rails do not move when the toy gets bigger. Fixing it meant separating
three kinds of quantity: the vehicle's own dimensions, which scale; its contact with the fixed
board, which does not; and composites where only part scales. The guard we added measures the real
imported mesh and the real baked skin, so it holds at any scale.

The same lesson twice: an arriving cat was hidden behind its own station sign for months —
measured at 3% of its own pixels visible — with everything passing. Now a test renders four passes
and attributes the loss.

### Accomplishments that we're proud of

Sixty solver-proved levels. A capture rig that replays to an exact tick, so every store screenshot
is reproducible rather than a lucky frame. And a monetisation stance we did not blink on: no ads,
no timers, no loot boxes, campaign free.

### What we learned

Assertions that call the code under test agree with it by construction. We caught ourselves doing
exactly that — five positional assertions that all passed with the clearance constant set to zero,
which was the original bug. Now the magnitude, the direction law and the tie are each written out
by hand in one table, and mutation-checking is a step rather than a virtue.

### What's next for Cat Metro

More Daily Line variants, a colourblind-first pass on the station symbols, and a proper level
editor if people want one.

---

## Fields blocked on an input

| Field | Blocked on | Note |
|---|---|---|
| Store URL | production release | Rules require the first public release inside the submission window |
| Video link | Pixel + release build | Sequence planned in `docs/store/video-sequence.md` |
| Judge promo code | Console | Generate for `cm_outfit_conductor`; the app has no free trial, so the rules require a code |

## Fields ready now

| Field | Source |
|---|---|
| 1024×1024 icon | `docs/store/assets/icon/cat-metro-icon-devpost-1024.png` |
| 1179×2556 frameless screenshot | `docs/store/assets/screenshots/01-board-L009-1179x2556.png` (hero) |
| Additional screenshots | `06`, `03`, `02`, `04` in the same directory |

## Do not claim in this entry

- Anything about push notifications or advertising. Neither SDK is in the binary.
- A hint economy, energy, or rewarded video. None ship.
- A level count other than the one the exact AAB reports. `scripts/build-aab.sh` substitutes it.
