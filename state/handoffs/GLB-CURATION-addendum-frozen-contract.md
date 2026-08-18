# Lane C addendum — the wave fragment was NOT stripped (2026-08-17)

Append to `HANDOFF-LANE-C-CURATION-2026-08-17.md`. Written by the orchestrator session after
looking at Lane C's committed renders and measuring the shipping derivatives.

## What happened — the orchestrator's spec was imprecise, Lane C was correct

The original brief said "strip the small detached floating fragment near
`cat-yellow-longhair-wave`'s **feet**." Lane C froze a foot-location predicate from that
wording, removed the min-Y debris under the feet, and **deliberately retained** the other
detached component, recording it plainly in its own evidence README:

> "The separate non-min-Y component visible beside the cat remains intentionally: it fails the
> frozen foot-location and thin-span predicates and was outside the relayed ruling."

That is correct lane behavior — it implemented the ruling as written and disclosed the gap
rather than silently over-reaching. **The spec was wrong, not the execution.** The component is
not near the feet; it floats beside the cat at body height.

## The ruling it should have satisfied

The human's 2026-08-17 ruling accepted the orchestrator's recommendation verbatim, which was:
strip the loaf's plinth **and** strip the wave's detached fragment ("it will render as a
floating blob in-game; I recommend Lane C strips it whichever way you rule"). The fragment
strip is therefore **already ruled** — this addendum is an execution correction, not a new
taste call. Do not re-ask the human for the ruling; do ask if the geometry is ambiguous.

## Measured facts (orchestrator, on the current shipping derivatives)

`unity/Assets/Art/Generated/incoming/decimated/cat-yellow-longhair-wave.glb` —
**2 connected components** (positions welded at 1e-5 before counting, so split-vertex seams
do not fake a part):

```
part 0: 14,524 tris (96.84%)  Y -0.4727..0.5001  X -0.3852.. 0.3853   <- the cat
part 1:    474 tris ( 3.16%)  Y -0.0520..0.0436  X -0.2938..-0.1334   <- the debris
```

The cat's feet sit at Y ≈ −0.47. The debris spans Y −0.052..0.044 — roughly 0.42 units
**above** the feet, at body-centre height and off to one side. That is exactly why a
foot-location predicate excluded it, and it is exactly why it reads as a floating blob.

For contrast, `cat-blue-siamese-loaf.glb` is now **1 connected component, 14,999 tris** — the
plinth strip was clean and complete. That asset needs no further work.

## The corrected criterion

Strip **every connected component that is not the largest one** from
`cat-yellow-longhair-wave`, then re-run the manifest-driven decimation for that single asset.
Success is objective and cheap to assert:

- the curated source and the regenerated derivative each have **exactly 1** connected component;
- the retained component is the 14,524-triangle body (not the 474-triangle part);
- the regenerated derivative still meets the 15k-triangle cat target;
- `cat-blue-siamese-loaf` stays **byte-identical** to its current curated bytes;
- the other 13 assets stay byte-identical (existing hash pins already cover this).

Recommended: for **cats**, replace the "min-Y + thin-span" predicate with "keep the largest
connected component, drop the rest," which is what the ruling actually means and would have
caught this.

### ⛔ DO NOT generalize the largest-component rule beyond cats — it would destroy the props

Orchestrator audit of all 15 shipping derivatives, 2026-08-17 (connected components, positions
welded at 1e-5):

```
all 10 cats            1 component each  — EXCEPT cat-yellow-longhair-wave (2; the defect above)
prop-trees             3 components  49.4% / 28.3% / 22.2%   <- three trees. All wanted.
prop-desk-clutter      7 components  52.7% / 30.1% / 10.6% / 3.9% / 1.1% / 0.8% / …
                                                             <- it is CLUTTER. All wanted.
prop-toy-engine        3 components  90.8% + 4.66% + 4.50%   <- the two small parts sit at
                       mirrored X (-0.95..-0.82 and +0.82..+0.95) over an identical Y band:
                       a symmetric pair, i.e. wheels/bogies. Both wanted.
prop-station-kiosk     2 components  98.4% + 1.58% (158 tris, Y -0.181..-0.050,
                       X -0.156..-0.039)  <- small and interior; UNRESOLVED, see below.
```

Applying "keep the largest component" to the props would delete two of the three trees, roughly
half of the desk clutter, and both wheel assemblies from the toy engine. **The rule is
cat-scoped.** Whatever predicate you freeze must either be keyed to `kind: "cat"` in the
manifest or be explicitly inapplicable to props — and your test suite should pin that, so a
later lane cannot apply it wholesale.

The multi-component props are all currently OUT of this addendum's scope; do not modify them.

`prop-station-kiosk`'s 1.58% component: **RESOLVED — leave it.** The orchestrator rendered the
kiosk from two angles and looked at it. That component sits *inside* the kiosk's footprint
(X −0.156..−0.039 within the body's −0.334..0.334) at counter height (Y −0.181..−0.050), and
reads as an interior fixture — a counter, stool, or ticket machine — not as debris. Contrast
the wave defect, which floats in empty space clearly outside the cat's silhouette. Removing
interior detail from a kiosk would be a regression, not a fix. No human ruling needed; if the
human later disagrees, it is a one-asset follow-up.

## Process

- Same branch, `task/GLB-CURATION`. RED-first: a test asserting exactly 1 component on the
  wave must fail against the current bytes before you fix it.
- Re-render before/after and **look at them** — this defect survived a code-green curation pass
  and was caught only by a human-equivalent looking at the picture.
- Preserve the provider-delivered originals exactly as you already did; the existing
  `curation-backups/GLB-CURATION-2026-08-17-16e20e3/` copies are verified byte-exact against
  ADR-0013's pinned hashes and must not be overwritten by this second pass. Use a new
  backup directory.
- **Downstream dependency you must not break:** ADR-0013 (PR #96) pins a 60-hash approval
  manifest. It is already stale for these two assets and will be re-pinned AFTER curation
  settles. Every further byte change to these assets extends that re-pin, so land this
  correction before the ADR re-pins — not after.
