#!/usr/bin/env bash
# ADR-0011 public-repository custody gate. The licensed derivatives may exist only as
# ignored local inputs; this test never prints their contents.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"

fail() { echo "polyfork-custody.test.sh: FAIL — $1"; exit 1; }

model_root="unity/Assets/Art/Polyfork/Models"
tracked=$(git ls-files "$model_root/*.fbx" "$model_root/*.fbx.meta")
[ -z "$tracked" ] || fail "licensed FBX derivatives or metadata are tracked"

names=(
  polyfork_tram_track_tile_f3c69a.fbx
  polyfork_train_engine_180979.fbx
  polyfork_log_cabin_4fac3b.fbx
  polyfork_young_pine_0d7695.fbx
  polyfork_wooden_fence_section_5f04b7.fbx
  polyfork_wooden_bench_661da4.fbx
  polyfork_sandwich_board_sign_cb5e7c.fbx
  polyfork_street_lamp_29f365.fbx
  polyfork_coffee_cup_90be67.fbx
)
hashes=(
  7c97c3d0b170aa940edce47c2f3c9dbcf14f67da6f9174515ee857aab541d987
  e505020cd12effebdfd4f0d632bf7d46b2ed8c976e9847defdc12e3ce256e418
  1339fabc925e6832d0617d25631ca95315e4906baada5554e0ef90378691a7fc
  e7887354371ecbce519e81e2dce68a05aa1e6b9f573d381dffb17db231735fde
  a0dd008200317da8dbd46cb37cf4043d558e64be2983e78bd50eaec5cf4aba88
  8629dabcafac68d8a610bd5eb60e515dbda0dcb1980ae56fca1bd908f22eb7f9
  498223ca9062bba616ff83df73a17954e8ec2c34dc2153bbe2687cc38183eb3a
  1ec680dd882c9df00b45b9d7526d09157b2a3513e9c578591c0409eb7b7ba5e6
  df64b866c0a2e116b3308f08467004eed599f956c4bf65cf34cccdb6abe664e2
)

for index in "${!names[@]}"; do
  fbx="$model_root/${names[$index]}"
  meta="$fbx.meta"
  git check-ignore --no-index --quiet "$fbx" \
    || fail "$fbx is not protected by gitignore"
  git check-ignore --no-index --quiet "$meta" \
    || fail "$meta is not protected by gitignore"

  if [ -e "$fbx" ] || [ -e "$meta" ]; then
    [ -f "$fbx" ] && [ -f "$meta" ] \
      || fail "local custody must keep ${names[$index]} and its metadata together"
    actual=$(shasum -a 256 "$fbx" | awk '{print $1}')
    [ "$actual" = "${hashes[$index]}" ] \
      || fail "local derivative hash mismatch for ${names[$index]}"
  fi
done

echo "polyfork-custody.test.sh: OK (public tree clean; ignored local custody verified when present)"
