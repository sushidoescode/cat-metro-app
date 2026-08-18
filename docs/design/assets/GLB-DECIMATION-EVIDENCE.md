# GLB decimation evidence — 15 generated candidates

Run date: **2026-08-16**. The evidence folder retains the frozen
`glb-decimation-2026-08-15` name; that folder name is not the run date.

Source-curation addendum: **2026-08-17**. Lane C executed the relayed human
uniform-NO-plinth ruling under the H-1 evidentiary caveat recorded in frozen
contract commit `77d66b43d06f18873b105239e63d27e2521542ff`. The branch is stacked on
`task/GLB-DECIMATION` at exact commit
`16e20e3fe9793ecaeb0f7865e66a55521eebf7db` and must not open a PR until
PR #94 merges. The clarified wave-component correction was frozen first on
2026-08-18 at `fb3df0b0babd4ab71dd227779486f883dcb58f67`.

## Result and limits

- Frozen contract commit: `bc34c6abf6ecf580465c061c2993a7536aeacf41`.
- Branch base: `3115ebdddd23f3d7eb6836c2670f6dfc2d0a6fb4`.
- Integrated, independently reviewed pipeline and final boundary-hardening
  reproduction base: `beb9cf7562c15d227427c116c786ec03bd636d71`.
  The tracked renderer and isolated 15-asset Blender queue were rerun from
  that exact tree without changing the retained local GLBs or sidecars.
- The reviewed `--force` queue completed 15/15 with exact category and global
  triangle bands, intact source custody, valid schema-1 sidecars, and no
  staging, backup, or process residue.
- **Decimation at intended board scale: APPROVE 14/15.** The remaining asset,
  `prop-toy-engine`, is **CONDITIONAL for board scale only**. Its 10k derivative
  is rejected for a hero or Home close-up unless a higher budget or another
  method receives separate approval.
- The seam-safe import correction removed the crack/seam networks seen in the
  first run. Reviewed colors, embedded textures, and cat badges remain mapped
  to the intended geometry.
- This is **not shipping approval** and is not a hero-quality verdict.
  Separately, the current candidate set remains **SOURCE-ART SHIPPING REJECT**
  for the remaining source-art defects recorded below. The 2026-08-17 curation
  resolves the ruled loaf display disc and both detached wave components. The
  generated-asset licence ADR is also still required before any generated
  file ships.

The paid source GLBs and local derivatives remain outside Git at:

```text
/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming
/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated
```

The generated GLBs and their JSON sidecars are ignored local assets and remain
unlicensed to ship.

The original decimation renders remain under the main checkout's untracked
`.catshots/glb-decimation-2026-08-15/` tree. `.catshots/` is **not ignored**;
the render tree was deliberately left untracked and unstaged.

## 2026-08-17/18 source-curation evidence

The curation operates on ignored source copies transactionally, then invokes
the existing decimation pipeline through committed manifests. Provider
originals remain at the first path; the first-pass wave source/sidecar and
derivative/sidecar are retained at the second path:

```text
/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/curation-backups/GLB-CURATION-2026-08-17-16e20e3
/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/curation-backups/GLB-CURATION-WAVE-CORRECTION-2026-08-18-841d4a3
```

- `cat-blue-siamese-loaf`: select glTF-space vertices strictly below
  `min_y + 0.08 * height` and delete their incident faces, only when that
  vertex selection spans at
  least 95% of the full X and Z footprint while the retained geometry spans
  less than 80% on both axes. This selected only the wide min-Y display disc:
  1,427,775 source triangles became 773,061.
- `cat-yellow-longhair-wave`: the first pass removed the 71,282-triangle
  min-Y component under the original wording but retained the 38,914-triangle
  torso-height component. The correction counts seam-safe components with a
  literal `1e-5` weld, keeps the unique largest cat component, and removes all
  others. The 1,422,808-triangle first-pass source became the one-component
  1,383,894-triangle body. Running the same rule directly on the provider
  original removes both smaller components and produces identical final bytes.
- The regenerated derivatives contain 14,999 and 15,000 triangles,
  respectively. The other 13 derivative GLBs are byte-identical to their
  pre-curation files. Their GLBs and sidecars are hash-pinned by
  `tests/assets/glb-curation.test.sh`.

The committed correction rerun boundary selects exactly the wave asset:

```bash
python3 scripts/decimate-assets.py \
  --manifest "$PWD/docs/design/assets/GLB-CURATION-WAVE-MANIFEST.json" \
  --input-dir /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming \
  --output-dir /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated \
  --blender /opt/homebrew/bin/blender \
  --force
```

The manifest produced exactly one GLB plus one sidecar. The earlier committed
two-entry manifest remains the whole first-pass reproduction boundary. An
isolated 2026-08-18 correction rerun produced GLB
`a3c4a363b06064ecc5dc03509c36ddd5ab91200a41314a3c674cd91ef4386696`,
byte-identical to the retained derivative; its sidecar differed only in the
expected fresh `tool.timestamp_utc`.
The curation publisher holds an advisory source-root lock, revalidates both
original GLB/sidecar hashes after Blender, writes a durable prepared/committed
journal, fsyncs each pair transition, and normalizes an interrupted journal on
the next locked invocation.

Committed renders and their complete checksum inventory are under
`evals/results/assets/glb-curation-2026-08-17/`. Lane C viewed the source
comparison, changed-derivative comparison, full 15-asset derivative grid, and
the dedicated correction before/after sheet at original detail. The ruled
disc and both wave debris components are absent; the loaf body, paws, head,
ears, whiskers, and tail and the wave cat, raised paw, feet, and tail remain
legible. All 13 untouched before/after derivative PNG pairs are byte-identical.
The evidence README records the looked-at disposition.

## Reproducible command boundary

The accepted real queue used the reviewed worktree scripts and explicit local
paths. It did not read `.env`, use the network, open Blender's GUI, touch Unity,
or touch a device:

```bash
python3 scripts/decimate-assets.py \
  --manifest "$PWD/docs/design/assets/CAT-MANIFEST.json" \
  --input-dir /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming \
  --output-dir /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated \
  --blender /opt/homebrew/bin/blender \
  --force
```

The 13 untouched derivative sidecars record Blender `5.1.2`, build
`ec6e62d40fa9`, operation `collapse-decimate`, and their original fresh UTC
timestamps on 2026-08-17; for that queue, the local run date remained
2026-08-16. The refreshed loaf sidecar records the same tool identity and
operation at `2026-08-17T10:33:30Z`; the final wave correction records
`2026-08-18T07:14:29Z`. The metrics `run_date` remains
`2026-08-16` because it identifies the original whole-inventory queue, not the
later per-entry refresh. The pinned importer and exporter are bundled
`io_scene_gltf2` `5.1.20`. The final importer uses literal
`merge_vertices=True` and `import_shading="SMOOTH"`; the driver separately
audits the exact unmerged source triangle count before the seam-safe import.

The tracked silhouette loop was:

```bash
render_root=/Users/sushantsrikrish/cat-metro-app/.catshots/glb-decimation-2026-08-15
mkdir -p "$render_root/before" "$render_root/after"
while IFS=$'\t' read -r id out; do
  PYTHONDONTWRITEBYTECODE=1 python3 scripts/glb-silhouette.py \
    "/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/$out" \
    "$render_root/before/$id.png" 25
  PYTHONDONTWRITEBYTECODE=1 python3 scripts/glb-silhouette.py \
    "/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated/$out" \
    "$render_root/after/$id.png" 25
done < <(jq -r '.assets[] | [.id, .out] | @tsv' \
  docs/design/assets/CAT-MANIFEST.json)
```

Renderer SHA-256:
`e3e82e5e2323e4a5f60a4fb9be2602486986161a764cd0b0ccd65941938ed7b5`.
Every tracked render used yaw 25°, 520×520 RGB, splat radius 2, and the 1%
coverage gate. Contact sheets used ImageMagick
`7.1.2-25 Q16-HDRI aarch64 037e46295:20260604`, five-image `+append` rows,
and `-append`; `montage` was not used.

Before the 2026-08-18 correction, the boundary-hardened refresh rendered all
15 source/derivative pairs
into isolated `/private/tmp/catmetro-glb-final-render-r1`. All 30 individual
PNGs were byte-identical to the retained recorded files, so their hashes and
coverage values below remain unchanged. The three rebuilt contact sheets were
pixel-identical to the retained sheets; their byte streams differed only in
ImageMagick run metadata. The table below therefore continues to identify the
retained recorded sheets rather than temporary rebuilds. The current
comparison grid was viewed at original detail; because every individual PNG
is byte-identical, the prior visual disposition carries forward unchanged.

That pre-correction code base also ran the full 15-asset Blender queue into isolated
`/private/tmp/catmetro-glb-final-derivatives-r1`. All 15 derivative GLBs were
byte-identical to the retained derivatives. Each fresh sidecar was identical
after excluding only its expected fresh `tool.timestamp_utc`, and a fresh
`glb_metrics.py` inspection matched every recorded output metric and derivative
hash. The final wave row, hashes, and renders above supersede that historical
one-asset result; the other 14 records remain unchanged.

The additional material-lit diagnostic used local untracked script
`/private/tmp/catmetro_color_render.py`, SHA-256
`15b0e46eaad66895fd72313bf05a102c3722e39130af346de0d9144d451da799`,
inside headless Blender. It rendered 640×640 RGBA source/derivative pairs at
yaw 25°; alternate yaw 115° and 205° cat grids exposed badge and source-art
details. This diagnostic script is evidence tooling only and is not shipped or
tracked.

## TDD and independent-review trail

The frozen tests were RED before their production implementations and were not
weakened to obtain GREEN:

- Metrics: at `08c615b`, `bash tests/assets/glb-metrics.test.sh` exited 2 at
  the absent `scripts/glb_metrics.py`. Commit `ad8ead6` made it GREEN. An
  isolated compression-specific-guard mutant exited 1; the untouched suite
  returned GREEN.
- Silhouette: RED-only commit `060e7d2` reached the absent
  `scripts/glb-silhouette.py` and exited 2 after validating the fixture.
  Implementation `eda177a` made it GREEN. An isolated iterator mutant that
  rendered only primitive zero exited 1 at the independent 16-position check;
  the untouched suite returned GREEN.
- Pipeline: RED commits `be72525` and `a61d4b2` reached the absent
  `scripts/decimate-assets.py`; the happy path exited 1. Production began at
  `9311091` and the reviewed hardening trail ended at `39b1cba`. An isolated
  cat-target mutant from 15,000 to 10,000 exited 1 at the independent policy
  oracle; the untouched suite returned GREEN.
- Seam regression G: RED commits `38a4747` and `ec00c7a` required one executed
  import with literal `merge_vertices=True` and `import_shading="SMOOTH"`.
  Pre-fix production failed both diagnostics. Production commit `f4cdcc5`
  passed; independently reverting either flag failed only that flag's AST and
  executed-behavior diagnostics. The author did not approve this fix; an
  independent review approved the minimal two-literal production delta.
- Exact-audit regression H: RED commit `bd2d797` passed its compliant fixture
  and mutation controls, then exited 1 because production had no
  `_audit_import_source`. Production commit `11cdd0b` added the exact audit and
  effective-count ratio; `a434638` clarified the reviewed diagnostic. The
  unchanged Section H controls and Sections A–G returned GREEN before the
  all-15 run.
- Final hardening rerun: the approved pipeline RED stack ended at `15d7ed5`,
  the rewritten pipeline GREEN stack at `ac34a46`, and the combined silhouette
  accounting fix at `41e1b86`. Their reviewed blobs were integrated at
  `c6ca12f`, then the guarded real queue completed 15/15 again. All derivative
  GLB hashes, source/source-sidecar hashes, byte sizes, geometry facts, and
  bounds remained exact. Only the deliberately fresh provenance timestamps
  changed, so this record updates the 15 sidecar hashes but does not regenerate
  or re-disposition the byte-identical visual evidence.
- Exact-head evidence regression: test-only commit
  `a14e64c5341967efab38d36f764c997e2caf4e28` failed on the stale tracked
  renderer hash and all 15 stale derivative-JSON hashes before this record was
  corrected. Its CI leg requires the tracked evidence/metrics agreement even
  when ignored local artifacts are absent; an explicit local root adds the
  stronger on-disk hash comparison.

The final evidence author reran the three focused asset suites and repository
check before committing this record. Detailed ignored author/reviewer reports
remain under `.superpowers/sdd/2026-08-15-glb-decimation/`; they are not part
of the product diff.

## Exact reduction metrics

The machine-readable authority is
`docs/design/assets/GLB-DECIMATION-METRICS.json`, in exact manifest order. It
was recalculated from the final files with `scripts/glb_metrics.py` and the
schema-1 sidecars, not copied from Blender stdout.

Totals:

- bytes: 833,401,188 → 24,696,932; reduction 808,704,256
  (**97.036609%**);
- vertices: 12,682,107 → 145,110; reduction 12,536,997
  (**98.855789%**);
- triangles: 24,587,090 → 199,998; reduction 24,387,092
  (**99.186573%**);
- inventory: 15 assets = 10 cats + 5 props.

| Asset | Kind | Bytes source → output | Vertices source → output | Triangles source → output | Byte reduction | Triangle reduction |
|---|---:|---:|---:|---:|---:|---:|
| `cat-red-tabby` | cat | 75,151,784 → 2,661,904 | 1,022,982 → 10,856 | 1,985,326 → 15,000 | 96.457963% | 99.244457% |
| `cat-blue-siamese` | cat | 75,069,332 → 2,614,864 | 1,023,074 → 11,089 | 1,982,628 → 15,000 | 96.516735% | 99.243428% |
| `cat-yellow-longhair` | cat | 73,678,820 → 2,515,828 | 1,004,037 → 10,940 | 1,950,756 → 15,000 | 96.585412% | 99.231067% |
| `cat-green-shorthair` | cat | 72,580,724 → 2,455,408 | 991,419 → 11,469 | 1,916,166 → 15,000 | 96.616997% | 99.217187% |
| `cat-wild-alley` | cat | 75,029,696 → 2,503,604 | 1,023,844 → 11,067 | 1,985,458 → 15,000 | 96.663183% | 99.244507% |
| `cat-red-tabby-sitting` | cat | 40,916,992 → 792,700 | 730,535 → 9,460 | 1,428,306 → 15,000 | 98.062663% | 98.949805% |
| `cat-blue-siamese-loaf` | cat | 22,489,668 → 845,188 | 399,354 → 10,050 | 773,061 → 14,999 | 96.241883% | 98.059791% |
| `cat-yellow-longhair-wave` | cat | 39,644,760 → 812,800 | 706,698 → 9,347 | 1,383,894 → 15,000 | 97.949792% | 98.916102% |
| `cat-green-shorthair-sit` | cat | 41,414,348 → 792,960 | 739,458 → 9,517 | 1,446,088 → 15,000 | 98.085301% | 98.962719% |
| `cat-conductor` | cat | 74,835,880 → 3,031,236 | 1,015,478 → 11,656 | 1,960,372 → 15,000 | 95.949488% | 99.234839% |
| `prop-depot-shed` | prop | 41,258,120 → 735,532 | 740,755 → 8,203 | 1,428,398 → 10,000 | 98.217243% | 99.299915% |
| `prop-toy-engine` | prop | 75,055,232 → 2,624,524 | 1,023,697 → 8,725 | 1,969,352 → 10,000 | 96.503210% | 99.492219% |
| `prop-station-kiosk` | prop | 41,802,588 → 865,756 | 749,616 → 8,317 | 1,439,593 → 9,999 | 97.928942% | 99.305429% |
| `prop-trees` | prop | 42,422,872 → 591,748 | 758,596 → 6,544 | 1,485,436 → 10,000 | 98.605120% | 99.326797% |
| `prop-desk-clutter` | prop | 42,050,372 → 852,880 | 752,564 → 7,870 | 1,452,256 → 10,000 | 97.971766% | 99.311416% |

Every cat is within 13,500–15,000 triangles, every prop is within
9,000–10,000, and every output is within the global 5,000–20,000 range.

## Bounds and structural preservation

The companion JSON stores each exact source/output world-bound minimum and
maximum. The dimensionless comparator facts below were computed from those
unrounded values. Limits are center drift ≤ 0.005 of source longest extent,
longest-extent scale drift ≤ 0.01, and maximum normalized-extent drift ≤ 0.02.

| Asset | Center drift / source L | Scale drift | Max normalized-extent drift |
|---|---:|---:|---:|
| `cat-red-tabby` | 0.000327669 | 0.000130297 | 0.000262057 |
| `cat-blue-siamese` | 0.000081210 | 0.000015403 | 0.000204559 |
| `cat-yellow-longhair` | 0.000404920 | 0.000416175 | 0.000155186 |
| `cat-green-shorthair` | 0.000062786 | 0.000369420 | 0.000253963 |
| `cat-wild-alley` | 0.000211421 | 0.000187895 | 0.000258012 |
| `cat-red-tabby-sitting` | 0.000131294 | 0.000097215 | 0.000280030 |
| `cat-blue-siamese-loaf` | 0.000243604 | 0.000488147 | 0.000275818 |
| `cat-yellow-longhair-wave` | 0.000069492 | 0.000164721 | 0.000137744 |
| `cat-green-shorthair-sit` | 0.000076666 | 0.000110149 | 0.000015636 |
| `cat-conductor` | 0.000155974 | 0.000123251 | 0.000495434 |
| `prop-depot-shed` | 0.000083610 | 0.000051379 | 0.000018482 |
| `prop-toy-engine` | 0.000164655 | 0.000234385 | 0.000281589 |
| `prop-station-kiosk` | 0.000134289 | 0.000185430 | 0.000411026 |
| `prop-trees` | 0.000081688 | 0.000146925 | 0.000145009 |
| `prop-desk-clutter` | 0.000038549 | 0.000348151 | 0.000397725 |

`compare_preservation(source, output)` returned an empty diagnostic list for
all 15. Each source and output has one mesh, one triangle primitive, one bound
material, and UVs on that primitive. Meshy files retain one embedded base-color
image; Tripo files retain three embedded base-color/metallic-roughness/normal
images. For every asset, the sorted embedded-image payload SHA-256 multiset and
the resolved texture-role-to-payload SHA mapping are byte-identical from source
to output. All have zero external URIs, extensions used/required, animations,
cameras, lights, skins, and morph targets.

| Asset | Images source/output | Embedded payload bytes | Texture-role mapping | UV/material binding | Extensions |
|---|---:|---|---|---|---|
| `cat-red-tabby` | 1/1 | exact | exact: base color | 1/1 | none |
| `cat-blue-siamese` | 1/1 | exact | exact: base color | 1/1 | none |
| `cat-yellow-longhair` | 1/1 | exact | exact: base color | 1/1 | none |
| `cat-green-shorthair` | 1/1 | exact | exact: base color | 1/1 | none |
| `cat-wild-alley` | 1/1 | exact | exact: base color | 1/1 | none |
| `cat-red-tabby-sitting` | 3/3 | exact | exact: base color, metallic-roughness, normal | 1/1 | none |
| `cat-blue-siamese-loaf` | 3/3 | exact | exact: base color, metallic-roughness, normal | 1/1 | none |
| `cat-yellow-longhair-wave` | 3/3 | exact | exact: base color, metallic-roughness, normal | 1/1 | none |
| `cat-green-shorthair-sit` | 3/3 | exact | exact: base color, metallic-roughness, normal | 1/1 | none |
| `cat-conductor` | 1/1 | exact | exact: base color | 1/1 | none |
| `prop-depot-shed` | 3/3 | exact | exact: base color, metallic-roughness, normal | 1/1 | none |
| `prop-toy-engine` | 1/1 | exact | exact: base color | 1/1 | none |
| `prop-station-kiosk` | 3/3 | exact | exact: base color, metallic-roughness, normal | 1/1 | none |
| `prop-trees` | 3/3 | exact | exact: base color, metallic-roughness, normal | 1/1 | none |
| `prop-desk-clutter` | 3/3 | exact | exact: base color, metallic-roughness, normal | 1/1 | none |

## Source and derivative custody

For every row, the source JSON's claim equals the source GLB, the derivative
JSON records both source hashes and the exact source provenance, the derivative
claim equals the output GLB, and both sidecar metric subsets equal fresh
inspection. All seven custody booleans in the companion JSON are true.

| Asset | Source GLB SHA-256 | Source JSON SHA-256 | Derivative GLB SHA-256 | Derivative JSON SHA-256 |
|---|---|---|---|---|
| `cat-red-tabby` | `d18c2098353cf5688745ef820b0ff8b58c826baa64d95bbec5c3234a3977ebf1` | `f746932debb844dd0db9728ca4033a325de410f1eb27ec413a695ae9df45f009` | `9d6f3e1b0d82f23500779c570943dc2081c6caad7295da7d3fe19c1c50742b59` | `9ebb3638031225ab8ade57cf794cfbb69b3ee98c3ff82e500aaf0d1f8738f4db` |
| `cat-blue-siamese` | `6943b7284d313ef70e1e1f5056b8324bf39e655b8ee571087d0fdfb5f1c0d80a` | `c8c3a24732da9f00dfb53f8a8011bff17044441cbeccfc351b53dfa942d4a070` | `44ceea493949fa7ea92bf40c7bc05e64c4b78e3ca0bb4c08b41fa7d788ee17b7` | `743de6f299f0c70f39dc92b7a7eda5ed6e86bf203ec78eefd4b01b20f9293f29` |
| `cat-yellow-longhair` | `1530b4a5609aeb62499be627630c735c3fd6fae1368b8f58d21c169e4183a813` | `7916d941b2cf27eb0628e77ccfd95252dc8fe671c11b773ffb89b3c513db39d1` | `36f03503fcbcb918870463222f50d6b17b3c880281ce61f3a15c2cec6963ed3e` | `f11a40229f24436206b06d4eee04246ef72c0f10ff3b7c88034d99757be2a4ec` |
| `cat-green-shorthair` | `d1a40bfbc4beaba8ac5169260c8d18e4664212f2e9a656549a76dfd3ecf0790b` | `84bbb56e37c97f70c0500f82803a7359d8e7c451b294fc2524222c2fae487eee` | `96910d69ad0bfe424c410e0b9df6e137222d858a28322a5276add6228e9186e5` | `9bcd978598e942d139e573ea9cdd3afab7dd86439f0aa56524accc0d9c3b3333` |
| `cat-wild-alley` | `b1d85cf314bbc295d7ca28af9c41f9c3bddecd81cfb84603a14648b55d5f462c` | `1b9d1a08924982f38bd24ac7fc138331f5f357efc500802cc1cf3d06567a6467` | `3fa010b59c3b5dccbe0eb54453e8d595736cbafa391a9f08effd9d052738479c` | `92f095b97e5c4f03116ac087c6852ebcaeabff611b67051faf2b5f2a96f7260b` |
| `cat-red-tabby-sitting` | `418eaa31df9b65c975dd85bdac3f04ab73b64827d81248d6248b57c6e2a81b14` | `982049a7b96684a6c9d3723764a51f378a07887439b2a4d3de8d318e7216b575` | `3ea8e01d78cb058223c74f225e89512efc44f74f638c99133d7720675e8655b6` | `f40f32794ef55f2f2e797ea870c63fedd6c2959bd0b0facc7fd50f0f1d21d898` |
| `cat-blue-siamese-loaf` | `257e59ebac613e3260bfd1161b228ec2be4aa7024969b4b1a3fec2366ffe0097` | `93fd18c00ec6a1b369bed7849a0bfdb4c00cba5dfe6b16358995998a86bb1f66` | `9a7b2ef923f923a78466f18d8bf0cfb82140aebbd30ba3e7cddd3f814fd2953c` | `2265679b91ff5feb5ab5ef7a277af6c3abfe1fda43e4dff2eccb5cceacc684e4` |
| `cat-yellow-longhair-wave` | `bf4626c2a41214444a483bde1920c7fd95a06069feca202df860861edb540d64` | `0bedeeb207fcb02277c7b0b1d0bcf8ec8118d4b0cf2e20abbaa3d85b1a64260f` | `a3c4a363b06064ecc5dc03509c36ddd5ab91200a41314a3c674cd91ef4386696` | `9c7bd939fc493caa44d0250531e2137c8c848d5b9bbfc62de320e2dbab16317e` |
| `cat-green-shorthair-sit` | `2db4f94d7c59c36c01f1f1d51780b30a417001b5d91a10caf420e5e5c1d6c5b2` | `beb9e95db53e897809194921156c46da974e8519dca3c627b9787b2ca2b40cf5` | `a5791a945bac21cfe55e7e4cdbcd5cd3233c11997cd0f449972a12768cca93f8` | `360ea5e28ca3e09b51fc45c8360ebe04e5b0a6fd38c532f636252bead68439fb` |
| `cat-conductor` | `f2cc476deccbcb4e72b74bd7d7e0bbd0e4c6bd3103149ce3697b85fa5260a9d1` | `9b66a992a560a590a06f435202b5bb246c3d2006dd6fda3b10ed2e059a28d9c4` | `3b0bdbe1a0af9377bfde62ebf2b633e694881dc81438f2814e717c4c71ab9e7d` | `83b5329451479e54719cd06a83445ab74f0bc58ef4dc4749b5b6e3cc50473e6b` |
| `prop-depot-shed` | `5efb54812e9d03c15c8d7c085a840bfabb09fc3312495c3b85af2ffcf8047aed` | `4816eeb87183c47bd60283af934f43215f129c7dd7183be4455cc04d4192ac83` | `68994c2316e7c0b23252569bfc06cbc1155c29dd41798c8effdbbaba638844b1` | `0e6c7f6a9065e12b0f3da93605914672947a2a662175ed788470c81f5d736ae2` |
| `prop-toy-engine` | `724bfbccc0087992b4f767037444ad95d24a21733d58923e9f7fbbfaa4b6e4c5` | `c02b9ff1352be0c1c3bdbdb63f95aff951311c00ef7e8179d8e9c94257b7569a` | `f622b390cdf48fccfb382895bef2988df191b523b614e01f03dbd162e052eeaf` | `2f1bd6850cbb836d8c569791ffaa6939c5d5f58a42487381c3954f3fb03aec1f` |
| `prop-station-kiosk` | `5fdeeedf04d3b536fc9d3bf3483fb8837b12559557ff16d2ae0c7521c4260b49` | `0d9706c4263a777200122a86c4426ebd23d06ee0370911d35b6f9b548e6c8d4f` | `25053fb73009bf004aeeebab4a861bb664c91935b59c059f21d2fc8c9b6f52cf` | `416098fc269903c81ffbcf40e6f469821bfbc6c045b3ce2018e76fd2d30e9dc3` |
| `prop-trees` | `d94b1586f4eb44a5e61ac5499dfef4ec8e4ddc2125bdf6fc95b6d7948673d863` | `1aa63811903a869927c134161101aa57c8993adc34661c03f9e1aade049e3f0b` | `e34f39de9a0db8f977370d7f0808f44a28b9641a458ada4957f552c62271c0dd` | `96b29000ef1e8f03d0982ffbaa1ec3d5a476cfcff8241a1f35b3a29041495b34` |
| `prop-desk-clutter` | `f42232e108bce9b9b12a5db6b8472412673a0dea9293cd9d896d43874bb4e9d5` | `393aca3bc5af36b19fed58e32dcd4e975542d1beafe85c0fe85a774830946ced` | `d0403b93dc3db30ec3f7e0b825ba7b48f4af7b79094c6b262c7bfa2fb268ec4d` | `e75ba87683bd0f468871608a0c079adf89e46eca86fecda248997b204da713b4` |

## Silhouette evidence — all 30 current source/derivative renders

The table retains the original 13 unchanged source/derivative pairs and uses
the committed `source-after/` and `derivative-after/` files for the two curated
rows. Coverage is exact filled-pixel count divided by 270,400, using the
renderer's warm-paper background semantics. Every file exceeds the 0.01 gate.
The reviewed pairs preserve intelligible outer silhouettes and intentional
appendages.

| Asset | Source coverage | Source PNG SHA-256 | Derivative coverage | Derivative PNG SHA-256 |
|---|---:|---|---:|---|
| `cat-red-tabby` | 0.336623520710 | `816e58667aa20ef7748e00637ca9a959409f05054df88f91e983ebc50575ede2` | 0.194622781065 | `e4c365b84e2a4d730402775d95bb0de11534089ce7c380194cfc644ad6b631d4` |
| `cat-blue-siamese` | 0.212906804734 | `1f5c951980c716e82094d5a81042ef607cb1441d5758c04f3cbf77e1665a041b` | 0.147928994083 | `4dc258bf08c0b067e9d5c7d2a3408091bac2506593f77f119dfec6c958704d22` |
| `cat-yellow-longhair` | 0.280088757396 | `b34ceeff53766770d723aab716bd67db4e309544f3cb67a4f0296f4796eda1cf` | 0.192141272189 | `c39169620a969cd4f1590cea102189145524f43ff6946af1cca022105b56718f` |
| `cat-green-shorthair` | 0.270684171598 | `b8ff3ff0a6508ac48ab79f273c041eb55e7359838309f64bfac416ce1c145786` | 0.168287721893 | `22c6a6261c23a9b5d59d6256fdf5c6efa4257d47b28dc1a011a196f7d5651fdc` |
| `cat-wild-alley` | 0.266619822485 | `065373f36ebd527ead3809273c2c3b1e242a99780a182f14ee15dc7c20435c14` | 0.169315828402 | `43d281c384692ec8db097c8d8fbeec587c534b5bd3b0cc64d93673065a522605` |
| `cat-red-tabby-sitting` | 0.363690828402 | `642ab605fb9ccdd8efbe7e25282810ef47880bdac11473f463b99d3e626fbe00` | 0.198513313609 | `9b5fc0fd1fa3a7f54f2fe406b0293427127394e8cdcf25403466dbe6bdd0c0f9` |
| `cat-blue-siamese-loaf` | 0.352300295858 | `a08113a1a979a15c612a34135d965bd72d46c17fe83dd9058fab03aa7fd50ce2` | 0.184870562130 | `24b3e8007d9d46a5211f85522ad66ae1ea549d304860ee8a6dcd54be07cb7796` |
| `cat-yellow-longhair-wave` | 0.386453402367 | `a9cf81f8ae3347a395c47ab468434e88dde72d48579117c8d9e0fde070c6fd77` | 0.207825443787 | `88540cb6ef6362e97010cf3e923334614e196f76e9075c8fe341f30b0836d4a5` |
| `cat-green-shorthair-sit` | 0.359900147929 | `c6464f6a56fb331f9a5098b45e295f556e6986a05a29c5bfc1f8fe9c678ee39b` | 0.192381656805 | `6f22e4bf816c03537eff100f6a9520829c922cd5dd147452ba0104583028b6ec` |
| `cat-conductor` | 0.254223372781 | `31090cb98888afd5a2e93e571552a8855f371f24324c91cfa759c278830ff108` | 0.156568047337 | `bcef82bc042f636f61e9ef6099f70b26fc0a884b323eb222c9699b087f64fd01` |
| `prop-depot-shed` | 0.536630917160 | `a4b26f058d072c0b3336ad0933555291e5c37e9b74651b5f3c9d46e20ec6f790` | 0.148010355030 | `d708bdf67344d239c9014a028707b9752d3616fa6de4efba12e21936cc80186d` |
| `prop-toy-engine` | 0.309874260355 | `51a6be6112ddcee0f58e507ead1718b011b903d7090c130aed46db572dae914d` | 0.126405325444 | `894b6f615a4607ec5ecf157f45da1945311cb9a964016a7dfedb8b683bf83ff9` |
| `prop-station-kiosk` | 0.389482248521 | `15fa0bca202e43357077468a6a8dd102a0ade1b473e54d60765c342f8c7454ce` | 0.139038461538 | `260fed1504e573dc48c028506e9229dbadc4e443ba8f68409e80043bd5af8fe9` |
| `prop-trees` | 0.376475591716 | `c3243e40d60bc1e491896b044fbd8d1879153f08b4472efe837c8bcd9cd6079c` | 0.150654585799 | `8b07d5e982eb04c8f8cd6c01efb1ead3632724bcdcaa5ff793e5e9d291409e38` |
| `prop-desk-clutter` | 0.239948224852 | `dbb506d76cb1e36d59c436a789d6c631dd6405b063d601bc9ddb2ff5a09e1e01` | 0.115695266272 | `c3c8ae7aee5837b8fee8ff7d10873f335fa604f2e1df42a724d8f02f5455ca73` |

Original pre-curation silhouette contact sheets:

| Path | Dimensions | SHA-256 |
|---|---:|---|
| `.catshots/glb-decimation-2026-08-15/before-grid.png` | 2600×1560 | `01edebbbe53ada4db60855b6413d09977ace40226be5e15bb948be29af89bc54` |
| `.catshots/glb-decimation-2026-08-15/after-grid.png` | 2600×1560 | `4014f185bff0c99222b24e5399a0c1525473ab8fc8e054e47eda0287f4bce584` |
| `.catshots/glb-decimation-2026-08-15/comparison-grid.png` | 2600×3120 | `0ed07846d71b6772273e3c4b6f6eaf72be9a8214326a2b03c1988e1ba1fc4e89` |

Current committed curation contact sheets:

| Path under `evals/results/assets/glb-curation-2026-08-17/` | Dimensions | SHA-256 |
|---|---:|---|
| `source-comparison.png` | 2080×520 | `bc68c694543f65254e94dfaf92f9f2623834eeaf3bf978c846e89cce2650dccc` |
| `changed-derivative-comparison.png` | 2080×520 | `8d18ff8f208dc3b7a1eb5e492ce4199fdb3a0311dfc11c5f6b3f71f9cd666338` |
| `derivative-before-grid.png` | 2600×1560 | `1d9f09d3fde235722a1bc982fe1d9b0d3a59492b66464e9ec231afda2f926025` |
| `derivative-after-grid.png` | 2600×1560 | `81d584f29cc9a02109feb2fe83a87da10befbd64d8e7c735612c1b8eba697dec` |
| `derivative-comparison-grid.png` | 2600×3120 | `ff24f27ec788129825855ae0e85635198658ea5b3de52b3ec25cc1b4b42afab5` |
| `wave-correction-comparison.png` | 1040×1040 | `11333100c4b36b1053b24df209204f346af112044e0ced027d4ed76c65322fbc` |

The dedicated correction sheet places source before/after on its top row and
derivative before/after on its bottom row. Lane C viewed all four constituent
frames and the sheet at original detail on 2026-08-18: the detached torso-height
blob is absent after correction, while ears, face, raised paw, torso, feet, and
curled tail remain intact with no visible seam or amputation.

The pre-correction exact-head temporary rebuild hashes were
`08ab01d710bf5544a230a92f6ab71c2379283bba19b012051dcca4e813e93a26`
(before),
`38f8ac86b91da2c2ec4aff76bac3a618b903b14803faf96bca8d1df49eea3ea2`
(after), and
`12ab3eb952da7b85a4cc4c341d6a9bc09ff05ff9ae0f1ee4c2d961f5b6c34ffe`
(comparison). Each rebuild had zero pixel difference from its retained sheet
and identical non-date PNG chunks; only ImageMagick's run-time date metadata
changed, so these temporary hashes do not replace the recorded-file hashes.

The current committed comparison grid and both changed source/derivative pairs
were viewed at original detail. The ruled geometry and both detached wave
components are absent and no new silhouette loss appeared. The 13 untouched
derivative PNG pairs are
byte-identical. The existing 14/15 target-scale decimation approval and
board-scale-only condition for `prop-toy-engine` remain unchanged; the
remaining source-art shipping rejection is narrowed as recorded below.

## Material-lit color evidence and visual verdict

Silhouettes alone do not establish material appearance. The original
decimation review inspected all 30 yaw-25 material-lit individual PNGs;
five source/derivative pairs (10 individual PNGs) at yaw 205° for
`cat-red-tabby`, `cat-blue-siamese`, `cat-yellow-longhair`,
`cat-green-shorthair`, and `cat-wild-alley`; five source/derivative pairs
(10 individual PNGs) at yaw 115° for `cat-red-tabby-sitting`,
`cat-blue-siamese-loaf`, `cat-yellow-longhair-wave`,
`cat-green-shorthair-sit`, and `cat-conductor`; and the four current
comparison grids at original detail. Other PNGs present under `color-yaw205/`
are legacy diagnostics and were not used for this verdict; the recorded
five-pair grid hash below is authoritative. Every yaw-25 individual is 640×640
RGBA. The two curated-asset rows below are retained as historical
pre-curation color diagnostics; current geometry evidence for those rows is
the committed silhouette set above. Curation preserved the exact embedded
texture payloads and material-role bindings, as the machine metrics record.
The full original yaw-25 inventory and hashes are:

| Asset | Source color PNG SHA-256 | Derivative color PNG SHA-256 |
|---|---|---|
| `cat-red-tabby` | `8505d2362cc925060e5d4a14e0b405ef65e3f7a11cc6ad4654c231e5817ea543` | `73c59ab95c2c835a474ee9bcdc59d2e23b0681fdb17cee72bc6432c2b331935f` |
| `cat-blue-siamese` | `aa99261d8ab99af4244b95b7a240dd154a0aeea6064a4f7d8d62beaaae04291e` | `f77fd24417c0bec41301a2e92f4bbe3fc851a088742894cdb62aa7a7689d1da6` |
| `cat-yellow-longhair` | `958bdf362faeebc77788b49aad876963082d487a0dea60411db38023bba266d5` | `9f4c3123d76c93ac96516a2032502dc9185e3371f5e245ed3124bf259d97bf51` |
| `cat-green-shorthair` | `27de7ae2e0cf750e629beeab4d535fe1c0d3afdb8578439633b77d67320098ba` | `564090c0fb140b6b117a2c413140e1d4895d83f5b7293421ae0959cce8a8ee06` |
| `cat-wild-alley` | `e2d52ec18a409de6c2ac04a0f9ec2a5b6d41e73ccf3bb0623b05c422be106682` | `da9a8268790cab5f53b301711a0555c5b8f96276f87a7e245665df1ba37bcdf0` |
| `cat-red-tabby-sitting` | `c3552af796f06dd4c3c921ebfd0b918ef3a550a98be0a46e7b0ef8b3a1c5d545` | `11efec4c2dae21361cae095aebbca8e7437fd08c083aae1375825541a378600d` |
| `cat-blue-siamese-loaf` | `2fc2866dd5877cbf3875a154bdc8a36ecd464c24e8bb44d9fdbb0743ec05c363` | `a8819dbcb268750c04431d9396f920a6d09bf5d124a294d491d1610e62620d15` |
| `cat-yellow-longhair-wave` | `9910518b74bc7c8c37a93dc60b68f16eaca6b0968db359c32739f965fdd33d7d` | `aa4cfec86b4b3adf7b00af346cf8a9e1c43eb76b09b7849753d83d93c1cd2435` |
| `cat-green-shorthair-sit` | `f23ec73842c148e68c219813208e081da2289bfb3fe98362b45af2675e33d82b` | `f1a0f3dde0952e1602201dea2b2e5cadfddbab02095c267ce82eaca2821014c1` |
| `cat-conductor` | `ea63ee637e82dc32e8a9edd0a21c830dbe142f8aa17bcaf0cca337450c782caf` | `7da67b5111d294c6027b222e0102f70fee5f9bb6e580c1162b723d5143c804df` |
| `prop-depot-shed` | `7175a6e57a83ddbc88479d28034ef949c3ce9966d75e708c81e10d51af99493e` | `83b2288b0cba0df4c1b82c64ea92f30142b564416b34fcc499eb7e118dd477e4` |
| `prop-toy-engine` | `45150c6eb60ba0c27450e6f108b783487f80507892bbd1c811d6442372153696` | `5756f04f15bb6192027412d6706caacec5276a7b8fd3869a3ad3153c0aa7b956` |
| `prop-station-kiosk` | `a20c9abc542da868e9a6aabc789ce27ab56f14cb04fbeb581811048809a75eb9` | `69f40f338d81d61fc695e440ca38ae5fbe22b19bbe270849bf4c50d527955636` |
| `prop-trees` | `ee78bd6ed0eed4ecab20420ba479b8228a27d455e1ae550e2ec2d2043a1ee4c5` | `deb3d7387ef234b217e776363f761abe5fc626783e61204a85d5a66376ab8ed0` |
| `prop-desk-clutter` | `ad4cc1070579859e60eb0cb793b8e5dfe108142d15bf7091de6fee2397c09c55` | `fa50ffb37814eeca9bf8144d1157e9b238f9c8b38f517e7ad7718367815dd531` |

The four original reviewed color grids are:

| Path | Dimensions | SHA-256 |
|---|---:|---|
| `.catshots/glb-decimation-2026-08-15/color-cats-comparison-grid.png` | 640×3200 | `e040f4c45779f81c9705cc18451f9806fb958c4427d9f33259f12305f466e3fd` |
| `.catshots/glb-decimation-2026-08-15/color-props-comparison-grid.png` | 640×1600 | `e86551e3710fbd448a3c51529924aff4859c0783bdfcad489f15762ea5fac1c8` |
| `.catshots/glb-decimation-2026-08-15/color-yaw115/color-cats-comparison-grid.png` | 640×1600 | `8a054125571a20b19ea21eb01489706437bc078a013c486a38aa04af23df95e3` |
| `.catshots/glb-decimation-2026-08-15/color-yaw205/color-cats-comparison-grid.png` | 640×1600 | `21f1cb35637fe5f4f0a6f3f99a7c97cdc749c57ffb4faf400804fccdb367ef96` |

Per-asset visual disposition:

| Asset | Target-scale decimation verdict | Reviewed note |
|---|---|---|
| `cat-red-tabby` | APPROVE | Seam-free; circle pair and colors retained. Minor hero-scale stripe/badge-edge blockiness remains. |
| `cat-blue-siamese` | APPROVE | Former cracks are fully gone; square and color retained. Mild smooth/facet shift only. |
| `cat-yellow-longhair` | APPROVE | Triangle and color retained. Fur/stripe texture is softened at hero scale. |
| `cat-green-shorthair` | APPROVE | Former widespread seams are fully gone; diamond and color retained. Mild boundary faceting remains. |
| `cat-wild-alley` | APPROVE decimation only | Star and texture retained; no cracks. The separate source-art rejection remains. |
| `cat-red-tabby-sitting` | APPROVE | Circle and garment/face remain intact; no crack. Mild smoothing. |
| `cat-blue-siamese-loaf` | APPROVE | Curated source and regenerated derivative remove the ruled display disc; body and appendage silhouettes remain intact. Mild face/eye faceting remains. |
| `cat-yellow-longhair-wave` | APPROVE decimation only | Curated source and regenerated derivative each contain only the largest cat component; both detached debris components are gone with no new seam/crack. The incorrect badge remains a source-art rejection. |
| `cat-green-shorthair-sit` | APPROVE | Diamond, color, and silhouette remain intact; the cleanest Tripo result. |
| `cat-conductor` | APPROVE | Former seams are gone; teal/orange/cream/navy palette, hat, and scarf retained. Minor hero-scale texture blockiness. |
| `prop-depot-shed` | APPROVE | Seam network is gone; door, roof, and base preserved. Hard edges are slightly softened. |
| `prop-toy-engine` | CONDITIONAL — board scale only | Cracks/speckles are gone and train silhouette, colors, and top knobs remain, but 10k visibly turns wheel spokes into lumpy/fused forms and facets the cab window/boiler panel. Readable at board scale; **REJECT hero/Home close-up** unless a higher budget or another method is separately approved. |
| `prop-station-kiosk` | APPROVE | Seam network is gone; square sign, awning, openings, and base remain intact. Slight post/base softening. |
| `prop-trees` | APPROVE | No cracks; grouped silhouette, colors, and bases remain intact. Intentional low-poly faceting is redistributed/smoothed. |
| `prop-desk-clutter` | APPROVE | Cup, steam, pencils, tiny trains, and track remain intact. Tiny wheel/rail detail softens; not hero quality. |

## Source-art shipping rejection and curation status

The decimator did not create the following defects; they exist in the source
renders and remain in the derivatives. They nevertheless block this candidate
set from shipping as current source art:

- `cat-wild-alley`: peach/orange rather than the required violet; both ears are
  upright, with no bent ear. The star badge itself is present and survives
  decimation.
- `cat-yellow-longhair-wave`: diamond tag rather than the required triangle.
  Both detached floating components are now removed.

The uniform-NO-plinth decision and corrected wave-component ruling are now
resolved for this inventory. Lane C removed the loaf display disc and every
non-largest wave component from the sources and regenerated the selected
derivatives. The strict geometry predicates, source and derivative
before/after hashes, and looked-at renders are recorded above.

Therefore **SOURCE-ART SHIPPING REJECT** is independent of the 14/15
target-scale decimation approval. No generated candidate may be promoted into
tracked Unity art or represented as licensed to ship until the source defects,
and generated-asset licence ADR are resolved. Board/Home wiring is also
outside this evidence PR.

## Authoring validation

The final evidence gate runs from the evidence worktree at the recorded code
HEAD, with no Unity, emulator, adb, network, credential, or `.env` access:

```bash
bash tests/assets/glb-curation.test.sh
GLB_CURATION_ARTIFACT_ROOT=/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming \
GLB_CURATION_BASELINE_ROOT=/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/curation-backups/GLB-CURATION-2026-08-17-16e20e3/derivatives-before \
GLB_CURATION_SOURCE_BASELINE_ROOT=/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/curation-backups/GLB-CURATION-2026-08-17-16e20e3 \
GLB_CURATION_BLENDER=/opt/homebrew/bin/blender \
  bash tests/assets/glb-curation.test.sh
bash tests/assets/glb-decimation-evidence.test.sh
GLB_DECIMATION_ARTIFACT_ROOT=/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming \
  bash tests/assets/glb-decimation-evidence.test.sh
bash tests/assets/glb-metrics.test.sh
bash tests/assets/glb-silhouette.test.sh
bash tests/assets/glb-decimation-pipeline.test.sh
bash scripts/check.sh
bash scripts/test.sh
bash scripts/build.sh
git diff --check
```

The companion JSON is additionally checked for exact manifest order, 15/10/5
inventory, category/global bands, recomputed totals, sidecar hash agreement,
source custody, empty preservation diagnostics, exact embedded payload and
texture-role preservation, and current render hashes. The companion JSON
was recalculated only where the two curated sources invalidate prior entries;
the other 13 asset records remain exact. Generated GLBs and sidecars remain
local and ignored. The curation PNGs and their checksum inventory are
committed under `evals/results/assets/glb-curation-2026-08-17/`.

Historical decimation exact-head observed results on 2026-08-16:

- tracked evidence validator without local artifacts: exit 0,
  `glb-decimation evidence: pass assets=15 local_artifacts=skipped`;
- tracked evidence validator against the explicit ignored local root: exit 0,
  `glb-decimation evidence: pass assets=15 local_artifacts=checked` (60/60
  source, source-JSON, derivative, and derivative-JSON hashes agree);
- metrics suite: exit 0, `glb-metrics test: pass`;
- silhouette suite: exit 0, `glb-silhouette test: pass` (the logged sparse
  rejection `coverage 0.000057 below 0.100000` is its intentional negative);
- pipeline suite: exit 0, `glb-decimation pipeline test: pass`; Section H's
  compliant fixture and mutation controls passed;
- exact-head renderer: 30/30 expected PNGs, 30/30 recorded hashes and coverage
  values, and 30/30 byte-identical comparisons; all coverage exceeds 0.01;
- rebuilt contact sheets: expected 2600×1560, 2600×1560, and 2600×3120
  dimensions, zero pixel difference, and identical non-date PNG chunks versus
  the retained recorded sheets;
- repository check: exit 0,
  `check: OK (interim harness — real lint+typecheck arrive with the stack)`;
- original companion evidence-generation validator: `assets=15
  silhouette_pngs=30 color_pngs=30
  reviewed_color_grids=4 custody=15 payload_identity=15`;
- diff check: pass; transaction residue: none; live Blender/queue processes:
  none.
