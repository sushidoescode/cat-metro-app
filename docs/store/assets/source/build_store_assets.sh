#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

if [[ $# -ne 0 ]]; then
  echo "usage: $0" >&2
  echo "the selected source is pinned inside docs/store/assets/source" >&2
  exit 2
fi

source_icon="docs/store/assets/source/cat-metro-icon-source-chatgpt-1254.png"
expected_source_sha="aa7166bee38a90aa2b2ba9bf25a1b8c24979248a89cebd25a5eb50a9960454e5"
srgb_profile="/System/Library/ColorSync/Profiles/sRGB Profile.icc"
font_path="$repo_root/unity/Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
icon_dir="docs/store/assets/icon"
review_dir="$icon_dir/review"
feature_dir="docs/store/assets/feature"
unity_icon_dir="unity/Assets/Store/Icons"
manifest="docs/store/assets/raster-sha256.txt"

for command in magick python3 shasum; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "missing required command: $command" >&2
    exit 1
  fi
done

if [[ ! -f "$source_icon" || ! -f "$srgb_profile" || ! -f "$font_path" ]]; then
  echo "missing selected icon source, checked-in font, or system sRGB profile" >&2
  exit 1
fi

source_sha="$(python3 - "$source_icon" <<'PY'
from hashlib import sha256
from pathlib import Path
import sys

print(sha256(Path(sys.argv[1]).read_bytes()).hexdigest())
PY
)"
if [[ "$source_sha" != "$expected_source_sha" ]]; then
  echo "selected icon source SHA-256 mismatch: $source_sha" >&2
  exit 1
fi

IFS='|' read -r source_format source_width source_height source_colorspace source_channels source_opaque source_depth < <(
  magick identify -quiet \
    -format '%m|%w|%h|%[colorspace]|%[channels]|%[opaque]|%z\n' \
    "$source_icon"
)
source_opaque_lower="$(printf '%s' "$source_opaque" | tr '[:upper:]' '[:lower:]')"
if [[ "$source_format" != "PNG" || "$source_width" != "$source_height" ]]; then
  echo "selected icon source must be a square PNG" >&2
  exit 1
fi
if (( source_width < 1024 || source_height < 1024 )); then
  echo "selected icon source would require upscaling (${source_width}x${source_height})" >&2
  exit 1
fi
if [[ "$source_colorspace" != "sRGB" || "$source_channels" == *a* || "$source_opaque_lower" != "true" || "$source_depth" != "8" ]]; then
  echo "selected icon source must decode as opaque 8-bit sRGB without alpha" >&2
  exit 1
fi

mkdir -p "$review_dir" "$feature_dir" "$unity_icon_dir"
icon_build_tmp="$(mktemp -d)"
trap 'rm -rf "$icon_build_tmp"' EXIT

master="$icon_dir/cat-metro-icon-master-1024.png"
devpost="$icon_dir/cat-metro-icon-devpost-1024.png"
play="$icon_dir/cat-metro-icon-play-512.png"
adaptive_foreground_1024="$icon_build_tmp/adaptive-foreground-1024.png"
adaptive_background_1024="$icon_build_tmp/adaptive-background-1024.png"
adaptive_composite_1024="$icon_build_tmp/adaptive-composite-1024.png"
roundel_mask="$icon_build_tmp/roundel-mask-1024.png"
roundel_art="$icon_build_tmp/roundel-art-1024.png"
roundel_art_scaled="$icon_build_tmp/roundel-art-scaled-942.png"

# Candidate A is 1254 px, so this is an 81.6587% reduction. Resample in linear
# RGB with a tuned Lanczos window, then return to sRGB. The source is never
# modified, and the guard above makes upscaling impossible.
magick "$source_icon" -colorspace RGB -filter Lanczos \
  -define filter:blur=0.9891028367558475 -resize 1024x1024! \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$master"

cp "$master" "$devpost"

magick "$master" -colorspace RGB -filter Lanczos \
  -define filter:blur=0.9891028367558475 -resize 512x512! \
  -colorspace sRGB -alpha on -depth 8 -define png:color-type=6 \
  "$play"

for size in 192 96 48; do
  magick "$master" -colorspace RGB -filter Lanczos \
    -define filter:blur=0.9891028367558475 -resize "${size}x${size}!" \
    -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
    "$review_dir/cat-metro-icon-${size}.png"
done

magick "$master" -gravity center -crop 512x512+0+0 +repage \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$review_dir/cat-metro-icon-safe-crop-512.png"

magick "$master" -colorspace Gray -colorspace sRGB -type TrueColor \
  -depth 8 -alpha off -define png:color-type=2 \
  "$review_dir/cat-metro-icon-grayscale-1024.png"

for simulation in protan deutan tritan; do
  case "$simulation" in
    protan) matrix='0.152286 1.052583 -0.204868 0.114503 0.786281 0.099216 -0.003882 -0.048116 1.051998' ;;
    deutan) matrix='0.367322 0.860646 -0.227968 0.280085 0.672501 0.047413 -0.011820 0.042940 0.968881' ;;
    tritan) matrix='1.255528 -0.076749 -0.178779 -0.078411 0.930809 0.147602 0.004733 0.691367 0.303900' ;;
  esac
  magick "$master" -colorspace RGB -color-matrix "$matrix" -clamp \
    -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
    "$review_dir/cat-metro-icon-${simulation}-1024.png"
done

magick -size 1024x640 "canvas:#FAF6EC" \
  "$play" -geometry +24+48 -composite \
  "$review_dir/cat-metro-icon-192.png" -geometry +554+368 -composite \
  "$review_dir/cat-metro-icon-96.png" -geometry +797+464 -composite \
  "$review_dir/cat-metro-icon-48.png" -geometry +926+512 -composite \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$review_dir/cat-metro-icon-contact-sheet.png"

# Rasterize the checked-in SVG composition with equivalent explicit primitives.
# ImageMagick's minimal SVG delegate drops inherited stroke widths on this host,
# while the direct primitives preserve the intended rail weight deterministically.
# Candidate A is not used here, so the raster remains byte-identical.
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

# Candidate A is a deliberately flattened icon. For Android adaptive launchers,
# keep the selected pixels intact by clipping the full motif to its roundel,
# scaling that motif to 92% on a transparent foreground, and placing it over a
# Cream Card background. This avoids inventing a replacement matte or redrawing
# any part of the human-selected image.
magick -size 1024x1024 xc:black -fill white -stroke none \
  -draw 'circle 512,512 996,512' "$roundel_mask"
magick "$master" "$roundel_mask" -alpha off -compose CopyOpacity -composite \
  "$roundel_art"
magick "$roundel_art" -filter Lanczos -resize 942x942 \
  "$roundel_art_scaled"
magick -size 1024x1024 canvas:none "$roundel_art_scaled" \
  -gravity center -compose over -composite "$adaptive_foreground_1024"
magick -size 1024x1024 "canvas:#FAF6EC" "$adaptive_background_1024"
magick "$adaptive_background_1024" "$adaptive_foreground_1024" \
  -compose over -composite "$adaptive_composite_1024"

# The 66% Android safe circle has a 337.92 px radius on this 1024 proof.
# Only this review image carries the magenta evidence stroke.
magick "$adaptive_composite_1024" -stroke '#D12B8A' -strokewidth 7 \
  -fill none -draw 'circle 512,512 850,512' \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$review_dir/cat-metro-icon-safe-crop-overlay-1024.png"

# Unity consumes one 512 px source per icon layer and performs density
# downscales. Preserve the existing asset paths and GUID-bearing .meta files.
cp "$play" "$unity_icon_dir/cat-metro-icon-legacy-512.png"
magick "$adaptive_background_1024" -filter Lanczos -resize 512x512! \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$unity_icon_dir/cat-metro-icon-background-512.png"
magick "$adaptive_foreground_1024" -filter Lanczos -resize 512x512! \
  -colorspace sRGB -depth 8 -alpha on -define png:color-type=6 \
  "$unity_icon_dir/cat-metro-icon-foreground-512.png"

# Attach an explicit sRGB declaration and remove volatile date chunks. The
# human-selected source remains byte-identical so its C2PA/JUMBF record and
# declared SHA remain intact; derivatives intentionally receive fresh metadata.
generated_pngs=(
  "$master"
  "$devpost"
  "$play"
  "$review_dir"/*.png
  "$feature_dir/cat-metro-feature-graphic-1024x500.png"
  "$unity_icon_dir/cat-metro-icon-legacy-512.png"
  "$unity_icon_dir/cat-metro-icon-background-512.png"
  "$unity_icon_dir/cat-metro-icon-foreground-512.png"
)
for output in "${generated_pngs[@]}"; do
  stripped="$icon_build_tmp/$(basename "$output").stripped.png"
  normalized="$icon_build_tmp/$(basename "$output").normalized.png"
  color_type=2
  case "$output" in
    *icon-play-512.png|*icon-legacy-512.png|*icon-foreground-512.png) color_type=6 ;;
  esac
  magick "$output" -strip -depth 8 -define "png:color-type=$color_type" \
    "$stripped"
  magick "$stripped" +profile '*' -profile "$srgb_profile" \
    +set date:create +set date:modify +set date:timestamp \
    -define png:exclude-chunk=time -depth 8 \
    -define "png:color-type=$color_type" "$normalized"
  mv "$normalized" "$output"
done

# Keep one exhaustive, sorted digest list for every committed store raster,
# including the untouched feature graphic and the byte-identical source.
manifest_tmp="$icon_build_tmp/raster-sha256.txt"
while IFS= read -r raster; do
  shasum -a 256 "$raster"
done < <(
  find docs/store/assets unity/Assets/Store -type f -name '*.png' -print \
    | LC_ALL=C sort
) > "$manifest_tmp"
mv "$manifest_tmp" "$manifest"

echo "store icon assets built from selected ChatGPT source: $source_icon"
echo "source sha256=$source_sha"
