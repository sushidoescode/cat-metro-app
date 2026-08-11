# Polyfork asset provenance

Imported 2026-08-09 through the authenticated Polyfork API under the project owner's
Founders entitlement. Polyfork's commercial license allows use and modification in games,
with no attribution requirement and no redistribution of the asset files themselves:
<https://polyfork.dev/licensing>.

Source GLBs are intentionally not retained. Each locally held FBX is an offline derivative made
with Blender 5.1.2 and `Tools/convert_glb_to_fbx.py`; it is for Cat Metro production use only,
not standalone redistribution. The public repository contains the receipt, authoring code,
Cat-Metro-authored prefabs/materials, and rendered evidence, but never an FBX derivative or its
Unity `.meta` file. SHA-256 values make an authorized local import reproducible and auditable.

The product owner keeps the nine FBXs and their matching Unity `.meta` files only in the ignored
`unity/Assets/Art/Polyfork/Models/` local-custody path. A clean public checkout intentionally lacks
those licensed inputs. Only the owner may enable reacquisition: fetch the recorded asset ID with
the licensed account, verify the source hash, run the recorded offline conversion, verify the
derivative hash, and restore the local metadata. If the metadata is unavailable or Unity assigns
different GUIDs, run `Cat Metro/Build Diorama Assets` after import to regenerate the Cat-Metro
prefabs and scene references before any test, build, or evidence run. No CI or ordinary build may
fetch from Polyfork or receive the credential.

Public-history remediation (2026-08-11): the lane-owned `art/diorama-pass` history was rewritten
from pre-remediation head `e4b787af8bdc8341da807d1b2d243d54ccda345d` to remove all nine FBXs
and all nine matching `.meta` files from every PR-only commit. The force-push removes those paths
from the branch's reachable history; the old object IDs may nevertheless remain fetchable by SHA
from GitHub until server-side garbage collection. Cached forks, clones, or downloads cannot be
recalled. A complete hosted-object scrub may require a GitHub Support request. This receipt records
that residual rather than claiming the prior public exposure was reversed.

Acquisition receipt: on 2026-08-09 each table id was fetched with an authenticated Founders
entitlement using `GET https://polyfork.dev/dl/<asset-id>.glb`. The bearer key came only from
the gitignored root `.env`; it was never placed in a URL, log, source file, or Unity asset.
The recorded source SHA-256 is the response body receipt.

Conversion receipt (run once per table row from the repository root; `<asset-id>.glb` names the
downloaded response and `<derivative.fbx>` is the exact table filename):

```sh
/opt/homebrew/bin/blender --background --python unity/Assets/Art/Polyfork/Tools/convert_glb_to_fbx.py -- /tmp/catmetro-polyfork-glb/<asset-id>.glb unity/Assets/Art/Polyfork/Models/<derivative.fbx>
```

Blender reported `5.1.2` (`ec6e62d40fa9`, built 2026-05-19). With authorized local custody
hydrated, the EditMode provenance gate recomputes every FBX hash and imported triangle count
against the table below. The always-on shell gate separately proves that no FBX or matching
metadata is tracked and verifies every locally present derivative hash without printing content.

| Polyfork source | Tris | Source GLB SHA-256 | Unity derivative | FBX SHA-256 | Unity GUID | `.meta` SHA-256 |
|---|---:|---|---|---|---|---|
| [Tram Track Tile](https://polyfork.dev/asset/tram-track-tile-f3c69a) (`tram-track-tile-f3c69a`) | 170 | `ce8b0743d558f93b642de960430adf44766c623f076c8d10a326ee154da29f0b` | `polyfork_tram_track_tile_f3c69a.fbx` | `7c97c3d0b170aa940edce47c2f3c9dbcf14f67da6f9174515ee857aab541d987` | `73b5135d9d81848829c189a03d9a2305` | `716e8f49b2cc12c25585954bfab8475f018d87bf60754cedc8e971616fbedec3` |
| [Train Engine](https://polyfork.dev/asset/train-engine-180979) (`train-engine-180979`) | 147 | `66d5d041299bf107bfdd42086ff9133493caa58fb3d79af57ce572099fa1c3e6` | `polyfork_train_engine_180979.fbx` | `e505020cd12effebdfd4f0d632bf7d46b2ed8c976e9847defdc12e3ce256e418` | `f7048b3033b864284a1da1dcd80590a4` | `a6ce1c33660b67cf243e1b36b8824146328b1624fe94be3f7cf478b10af9ae21` |
| [Log Cabin](https://polyfork.dev/asset/log-cabin-4fac3b) (`log-cabin-4fac3b`) | 1,326 | `da22159fd03324ae3a9d38d2f135e79f062d2fd21d7b535116233e9639195de9` | `polyfork_log_cabin_4fac3b.fbx` | `1339fabc925e6832d0617d25631ca95315e4906baada5554e0ef90378691a7fc` | `e144f368bd353443ab2321bc1b1a4b37` | `ae824800de97cf73fe7257cc9245a7dc2a8fe1583b809bf3f82793065fe0e98d` |
| [Young Pine](https://polyfork.dev/asset/young-pine-0d7695) (`young-pine-0d7695`) | 414 | `d48dea3989d2f40b026a76dbec6418bd8039355d5201c99ac6f9860d237747f4` | `polyfork_young_pine_0d7695.fbx` | `e7887354371ecbce519e81e2dce68a05aa1e6b9f573d381dffb17db231735fde` | `43a97ff877c5549b298ae17d717488da` | `7ebbf6b259999f88a5bde8a94738e30befc3bd17e15f0442d365d897f4bdb084` |
| [Wooden Fence Section](https://polyfork.dev/asset/wooden-fence-section-5f04b7) (`wooden-fence-section-5f04b7`) | 428 | `5a92f3be00a52a3c0d710dc2c44fe7b042a30887664d897aa5da22aedbd7801a` | `polyfork_wooden_fence_section_5f04b7.fbx` | `a0dd008200317da8dbd46cb37cf4043d558e64be2983e78bd50eaec5cf4aba88` | `05fa41426451a4430b827a8f218bfd6a` | `ed744ecd688fed06a48ad09f87c63af565b7651d95e415dd665ab6af17bcb24f` |
| [Wooden Bench](https://polyfork.dev/asset/wooden-bench-661da4) (`wooden-bench-661da4`) | 374 | `7c8a707848f1c8c2034e77d106c977b7f460d484201a5c675a2d8b39ac5527e4` | `polyfork_wooden_bench_661da4.fbx` | `8629dabcafac68d8a610bd5eb60e515dbda0dcb1980ae56fca1bd908f22eb7f9` | `6c49e285287f34668883553f0db8fb93` | `5c299d41198dd287f592e8ac4312d9a6f20d8cf018844ac600770a0572c9c418` |
| [Sandwich Board Sign](https://polyfork.dev/asset/sandwich-board-sign-cb5e7c) (`sandwich-board-sign-cb5e7c`) | 362 | `2e6aef8a2729dfd655ce87a5fc8d94accf3d54e2cccb0f359b3e5575754e06a5` | `polyfork_sandwich_board_sign_cb5e7c.fbx` | `498223ca9062bba616ff83df73a17954e8ec2c34dc2153bbe2687cc38183eb3a` | `1cafd8f1a15ad493b86c7f51d13ec219` | `0dd52af7605de286d337b33d1f2c12787d0f49ef1663f99bb63f23f5f2a94804` |
| [Street Lamp](https://polyfork.dev/asset/street-lamp-29f365) (`street-lamp-29f365`) | 378 | `7cf040e18834219cb24e18002aa1ff7333f8000e0290b1ea8f76b353c10f5a6a` | `polyfork_street_lamp_29f365.fbx` | `1ec680dd882c9df00b45b9d7526d09157b2a3513e9c578591c0409eb7b7ba5e6` | `065d26fb6b23f4b3799508e87b9cd07f` | `0dda6bb01953c4e79f7eef4dffa4e70cdb85d525b04ac25562574a0d8ec1a6c2` |
| [Coffee Cup](https://polyfork.dev/asset/coffee-cup-90be67) (`coffee-cup-90be67`) | 96 | `db4b37cefbbd01603b236ac988aa9806bc36fda913f2112d23b96069325a5692` | `polyfork_coffee_cup_90be67.fbx` | `df64b866c0a2e116b3308f08467004eed599f956c4bf65cf34cccdb6abe664e2` | `936e185b29f664f84a96fde34e134207` | `ed1b4d4ee99fa52b61a6233534c5c5d255bbb244507e1d93cb8101a532582f33` |
