# Submission video — shot sequence

**Status: planned, NOT shot.** Every timing below is buildable, but the rules require *device*
footage ("footage that shows the Project functioning on the device"), so this cannot be finished
until the Pixel 9 Pro `48121FDAP006X4` is attached and the release build is installed on it. Cut
nothing from the editor capture rig except the two title cards.

Target length **1:35**, against the rules' hard ceiling of "less than two (2) minutes". YouTube or
Vimeo, unlisted is fine. **No third-party trademark and no licensed music** — the entire soundtrack
and every SFX in the build are original synthesis (`feat/game-audio`, provenance in its
`PROVENANCE.md`), so the music clause is satisfied by using the game's own audio and nothing else.

## The three blockers on this video are now closed

`shipaton-submission-package.md` previously said not to cut video from the win frame, that the
arriving cat was occluded, and that the rider was too small. All three are fixed and measured on
the current candidate:

| was | now |
|---|---|
| win title rendered grey, 0.692 max luminance, 0 bright pixels | **0.9195035 / 8318 bright pixels** |
| arriving cat hidden behind its own station badge, 27.7–91.5% covered | **badge_share 0.0000, ≥99.79% visible** on L001/L009/L011 and the L008 second delivery |
| rider ~1/12 of frame width | **9.92% of frame width on L001**, 8.07% on L009 at ConsistScale 1.30 |

## Sequence

Times are cumulative. "Device" = screen recording from the Pixel at 1179×2556, portrait, 60fps.

| # | t | dur | Source | Content | Caption / VO |
|---|---|---|---|---|---|
| 1 | 0:00 | 0:04 | Title card | App icon on the warm paper ground, title animates in | "Cat Metro" |
| 2 | 0:04 | 0:08 | Device | Cold launch → Home. The carved sign, the diorama window showing the live tick-0 board, the three pills | "A tabletop metro, on your desk." |
| 3 | 0:12 | 0:10 | Device | Tap Play → L001 intro ticket → the board. **Hold on the rider** — this is the shot the scale work was for | "One thumb. Route the cats." |
| 4 | 0:22 | 0:14 | Device | L001 played through: tap the junction, the switch throws, the cat rides, **arrival and celebration** | "Tap a junction. Throw the switch." |
| 5 | 0:36 | 0:10 | Device | Win banner "All cats home!", Next chip | — (let the beat land) |
| 6 | 0:46 | 0:16 | Device | L009 or L011: three stations, colour **and symbol** signs, wave preview capsule filling | "Colour *and* symbol — readable either way." |
| 7 | 1:02 | 0:10 | Device | A deliberate failure: two trains bump, "Two trains bumped!", Try again | "Fail fast, retry instantly." |
| 8 | 1:12 | 0:08 | Device | Wardrobe: the Conductor outfit, the RevenueCat-backed purchase sheet **opened, not completed** | "One optional outfit. No ads, no timers." |
| 9 | 1:20 | 0:08 | Device | Daily Line entry, then a crowded later level (L052 shows five deliveries at two stations) | "Sixty levels, plus a Daily Line." |
| 10 | 1:28 | 0:07 | Title card | Icon, "Free on Google Play", the store URL | — |

**Shot 8 is the RevenueCat evidence shot** and the one the judges are most likely to scrub to.
Show the purchase *sheet* rendered by the shipped SDK; do not complete a purchase on camera. A real
completed Play Billing purchase was already recorded on 2026-08-31 and can be re-recorded
separately if the entry wants it.

## Capture procedure on the device

```sh
adb devices -l                     # confirm model:Pixel 9 Pro and serial 48121FDAP006X4 FIRST
adb -s 48121FDAP006X4 shell screenrecord --size 1179x2556 --bit-rate 16000000 \
    --time-limit 180 /sdcard/catmetro-raw.mp4
adb -s 48121FDAP006X4 pull /sdcard/catmetro-raw.mp4
```

Never target `2G0YC5ZF7Z056Q` (Quest 3) or `emulator-5554` (Pico) — they belong to other projects.
`screenrecord` captures no audio; lay the game's own audio under the cut, or record device audio
separately.

## What must be true before this is shot

1. The release build is installed on the Pixel — not the debug APK, if the footage is meant to
   represent the shipping binary.
2. The device pass has run, so a defect is not discovered *after* the edit.
3. The store URL in shot 10 exists, which means the production release has happened.

Until then this file is a plan and the frames in `docs/store/assets/screenshots/` are the only
approved media.
