#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

if [[ $# -ne 1 ]]; then
  echo "usage: $0 /absolute/path/to/pinned-cat-rig.glb" >&2
  exit 2
fi

rig_path="$1"
blender_bin="/opt/homebrew/bin/blender"
font_path="$repo_root/unity/Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
srgb_profile="/System/Library/ColorSync/Profiles/sRGB Profile.icc"
source_dir="docs/store/assets/source"
icon_dir="docs/store/assets/icon"
review_dir="$icon_dir/review"
feature_dir="docs/store/assets/feature"
unity_icon_dir="unity/Assets/Store/Icons"
render_path="/tmp/cat-metro-store-icon-rig-1024.png"
mask_path="/tmp/cat-metro-store-icon-head-mask-1024.png"
mask_alpha_path="/tmp/cat-metro-store-icon-head-mask-alpha-1024.png"
portrait_path="/tmp/cat-metro-store-icon-portrait-1024.png"
background_path="/tmp/cat-metro-store-icon-background-1024.png"

for command in magick "$blender_bin"; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "missing required command: $command" >&2
    exit 1
  fi
done

if [[ ! -f "$rig_path" || ! -f "$font_path" || ! -f "$srgb_profile" ]]; then
  echo "missing pinned rig, checked-in font, or system sRGB profile" >&2
  exit 1
fi

mkdir -p "$review_dir" "$feature_dir" "$unity_icon_dir"

"$blender_bin" --background --factory-startup \
  --python "$source_dir/render_icon.py" -- \
  --source "$rig_path" --output "$render_path"

# Keep this a face portrait without editing the licensed geometry. The mask is a
# presentation crop made from the fresh render; it removes the torso below the
# chin so the icon does not imply a baby head-to-body ratio.
magick -size 1024x1024 xc:black -fill white -stroke none \
  -draw "path 'M 0,0 L 1024,0 L 1024,760 C 930,790 875,810 760,832 C 675,846 595,850 512,850 C 429,850 349,846 264,832 C 149,810 94,790 0,760 Z'" \
  "$mask_path"
magick "$mask_path" -alpha copy "$mask_alpha_path"
magick "$render_path" "$mask_alpha_path" -compose DstIn -composite "$portrait_path"

# The plain roundel deliberately contains no bar, lettering, or authority geometry.
magick -size 1024x1024 "canvas:#FAF6EC" \
  -fill '#F08A3C' -draw 'circle 512,548 932,548' \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$background_path"

magick "$background_path" "$portrait_path" -compose over -composite \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$icon_dir/cat-metro-icon-master-1024.png"

cp "$icon_dir/cat-metro-icon-master-1024.png" \
  "$icon_dir/cat-metro-icon-devpost-1024.png"

magick "$icon_dir/cat-metro-icon-master-1024.png" -filter Lanczos \
  -resize 512x512 -alpha on -depth 8 -define png:color-type=6 \
  "$icon_dir/cat-metro-icon-play-512.png"

for size in 192 96 48; do
  magick "$icon_dir/cat-metro-icon-master-1024.png" -filter Lanczos \
    -resize "${size}x${size}" -colorspace sRGB -depth 8 -alpha off \
    -define png:color-type=2 "$review_dir/cat-metro-icon-${size}.png"
done

magick "$icon_dir/cat-metro-icon-master-1024.png" -gravity center \
  -crop 512x512+0+0 +repage -colorspace sRGB -depth 8 -alpha off \
  -define png:color-type=2 "$review_dir/cat-metro-icon-safe-crop-512.png"

magick "$icon_dir/cat-metro-icon-master-1024.png" \
  -stroke '#D12B8A' -strokewidth 5 -fill none \
  -draw 'rectangle 256,256 767,767' \
  -stroke '#3BAFA8' -draw 'rectangle 102,102 921,921' \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$review_dir/cat-metro-icon-safe-crop-overlay-1024.png"

magick "$icon_dir/cat-metro-icon-master-1024.png" -colorspace Gray \
  -colorspace sRGB -type TrueColor -depth 8 -alpha off -define png:color-type=2 \
  "$review_dir/cat-metro-icon-grayscale-1024.png"

for simulation in protan deutan tritan; do
  case "$simulation" in
    protan) matrix='0.152286 1.052583 -0.204868 0.114503 0.786281 0.099216 -0.003882 -0.048116 1.051998' ;;
    deutan) matrix='0.367322 0.860646 -0.227968 0.280085 0.672501 0.047413 -0.011820 0.042940 0.968881' ;;
    tritan) matrix='1.255528 -0.076749 -0.178779 -0.078411 0.930809 0.147602 0.004733 0.691367 0.303900' ;;
  esac
  magick "$icon_dir/cat-metro-icon-master-1024.png" -colorspace RGB \
    -color-matrix "$matrix" -clamp -colorspace sRGB \
    -depth 8 -alpha off -define png:color-type=2 \
    "$review_dir/cat-metro-icon-${simulation}-1024.png"
done

magick -size 1024x640 "canvas:#FAF6EC" \
  "$icon_dir/cat-metro-icon-play-512.png" -geometry +24+48 -composite \
  "$review_dir/cat-metro-icon-192.png" -geometry +554+368 -composite \
  "$review_dir/cat-metro-icon-96.png" -geometry +797+464 -composite \
  "$review_dir/cat-metro-icon-48.png" -geometry +926+512 -composite \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$review_dir/cat-metro-icon-contact-sheet.png"

# Rasterize the checked-in SVG composition with equivalent explicit primitives.
# ImageMagick's minimal SVG delegate drops inherited stroke widths on this host,
# while the direct primitives preserve the intended rail weight deterministically.
magick -size 1024x500 "canvas:#FAF6EC" \
  -fill '#F08A3C' -stroke none -draw 'roundrectangle 70,72 430,80 4,4' \
  -fill none -stroke '#D8C9AF' -strokewidth 15 \
  -draw 'line 553,462 621,485 line 579,420 648,444 line 606,378 678,403 line 635,337 710,363 line 667,298 744,324 line 703,259 781,285 line 743,222 821,247 line 787,185 864,209 line 835,150 910,174 line 886,117 958,140' \
  -stroke '#22304A' -strokewidth 13 \
  -draw "path 'M 548,456 C 626,330 704,210 894,108' path 'M 616,481 C 691,358 769,244 963,133'" \
  -stroke '#D8C9AF' -strokewidth 13 \
  -draw 'line 708,332 739,277 line 755,351 788,292 line 804,363 834,309 line 856,375 883,324 line 909,387 936,338' \
  -stroke '#22304A' -strokewidth 12 \
  -draw "path 'M 699,321 C 759,331 835,350 950,386' path 'M 737,272 C 801,281 876,302 979,339'" \
  -fill '#3BAFA8' -stroke none -draw 'roundrectangle 694,293 752,329 18,18' \
  -fill none -stroke '#F08A3C' -strokewidth 13 -draw 'line 722,304 762,266' \
  -font "$font_path" -weight 700 -pointsize 108 -fill '#22304A' -stroke none \
  -gravity northwest -annotate +66+110 'CAT' -annotate +62+222 'METRO' \
  -colorspace sRGB -depth 8 -alpha remove -alpha off -define png:color-type=2 \
  "$feature_dir/cat-metro-feature-graphic-1024x500.png"

# Unity consumes one 512 px source per icon layer and performs density downscales.
# Every Unity source is generated directly from a 1024 px native render/master.
cp "$icon_dir/cat-metro-icon-play-512.png" \
  "$unity_icon_dir/cat-metro-icon-legacy-512.png"
magick "$background_path" -filter Lanczos -resize 512x512 \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$unity_icon_dir/cat-metro-icon-background-512.png"
magick "$portrait_path" -filter Lanczos -resize 512x512 \
  -colorspace sRGB -depth 8 -alpha on -define png:color-type=6 \
  "$unity_icon_dir/cat-metro-icon-foreground-512.png"

# Attach an explicit sRGB declaration and remove volatile PNG date chunks. The
# alpha-bearing sources stay 32-bit RGBA even where every output pixel is opaque.
generated_pngs=(
  "$icon_dir/cat-metro-icon-master-1024.png"
  "$icon_dir/cat-metro-icon-devpost-1024.png"
  "$icon_dir/cat-metro-icon-play-512.png"
  "$review_dir"/*.png
  "$feature_dir/cat-metro-feature-graphic-1024x500.png"
  "$unity_icon_dir/cat-metro-icon-legacy-512.png"
  "$unity_icon_dir/cat-metro-icon-background-512.png"
  "$unity_icon_dir/cat-metro-icon-foreground-512.png"
)
for output in "${generated_pngs[@]}"; do
  normalized="${output}.normalized.png"
  stripped="${output}.stripped.png"
  color_type=2
  case "$output" in
    *icon-play-512.png|*icon-legacy-512.png|*icon-foreground-512.png) color_type=6 ;;
  esac
  magick "$output" -strip -depth 8 -define "png:color-type=$color_type" "$stripped"
  magick "$stripped" +profile '*' -profile "$srgb_profile" \
    +set date:create +set date:modify +set date:timestamp \
    -define png:exclude-chunk=time -depth 8 -define "png:color-type=$color_type" \
    "$normalized"
  rm "$stripped"
  mv "$normalized" "$output"
done

echo "store assets built from pinned rig: $rig_path"
