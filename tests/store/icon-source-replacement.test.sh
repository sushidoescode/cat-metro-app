#!/usr/bin/env bash
set -euo pipefail

cd "$(git rev-parse --show-toplevel)" || exit

if ! command -v magick >/dev/null 2>&1; then
  echo "icon-source-replacement: SKIP (ImageMagick is required for raster probes)"
  exit 0
fi

source_icon="docs/store/assets/source/cat-metro-icon-source-chatgpt-1254.png"
master="docs/store/assets/icon/cat-metro-icon-master-1024.png"
foreground="unity/Assets/Store/Icons/cat-metro-icon-foreground-512.png"
safe_overlay="docs/store/assets/icon/review/cat-metro-icon-safe-crop-overlay-1024.png"
expected_source_sha="aa7166bee38a90aa2b2ba9bf25a1b8c24979248a89cebd25a5eb50a9960454e5"

if [[ ! -f "$source_icon" ]]; then
  echo "icon-source-replacement: missing selected ChatGPT source: $source_icon" >&2
  exit 1
fi

actual_source_sha="$(python3 - "$source_icon" <<'PY'
from hashlib import sha256
from pathlib import Path
import sys

print(sha256(Path(sys.argv[1]).read_bytes()).hexdigest())
PY
)"
if [[ "$actual_source_sha" != "$expected_source_sha" ]]; then
  echo "icon-source-replacement: source SHA-256 mismatch: $actual_source_sha" >&2
  exit 1
fi

IFS='|' read -r source_width source_height source_colorspace source_channels source_opaque < <(
  magick identify -quiet -format '%w|%h|%[colorspace]|%[channels]|%[opaque]\n' "$source_icon"
)
source_opaque_lower="$(printf '%s' "$source_opaque" | tr '[:upper:]' '[:lower:]')"
if (( source_width < 1024 || source_height < 1024 )); then
  echo "icon-source-replacement: source would require upscaling (${source_width}x${source_height})" >&2
  exit 1
fi
if [[ "$source_width" != "$source_height" || "$source_colorspace" != "sRGB" ]]; then
  echo "icon-source-replacement: source must be square sRGB, got ${source_width}x${source_height} $source_colorspace" >&2
  exit 1
fi
if [[ "$source_channels" == *a* || "$source_opaque_lower" != "true" ]]; then
  echo "icon-source-replacement: source must be opaque RGB without alpha, got channels=$source_channels opaque=$source_opaque" >&2
  exit 1
fi

if [[ ! -f "$master" || ! -f "$foreground" || ! -f "$safe_overlay" ]]; then
  echo "icon-source-replacement: one or more generated icon artifacts are missing" >&2
  exit 1
fi

icon_probe_dir="$(mktemp -d)"
trap 'rm -rf "$icon_probe_dir"' EXIT
reference_master="$icon_probe_dir/reference-master.png"

# Decode both files and compare pixels so ICC/chunk normalization does not hide
# a stale master. This is the exact, color-correct Lanczos reduction contract.
magick "$source_icon" -colorspace RGB -filter Lanczos \
  -define filter:blur=0.9891028367558475 -resize 1024x1024! \
  -colorspace sRGB -depth 8 -alpha off -define png:color-type=2 \
  "$reference_master"

set +e
pixel_error="$(magick compare -metric AE "$reference_master" "$master" null: 2>&1)"
compare_status=$?
set -e
if (( compare_status != 0 )); then
  echo "icon-source-replacement: master is not the selected source's Lanczos downsample (AE=$pixel_error)" >&2
  exit 1
fi

# Candidate A's cap, badge, eyes, nose, and mouth are all navy/teal. The
# adaptive layer may let the orange roundel extend beyond the safe zone, but
# every blue/teal critical pixel with visible alpha must remain inside the
# centered circle whose diameter is 66% of the 512 px layer.
critical_fraction="$(magick "$foreground" -colorspace HSL -alpha on \
  -fx '(a>0.5 && r>0.43 && r<0.75 && g>0.14 && b<0.75)?1:0' \
  -format '%[fx:mean]' info:)"
outside_safe_fraction="$(magick "$foreground" -colorspace HSL -alpha on \
  -fx '(a>0.5 && r>0.43 && r<0.75 && g>0.14 && b<0.75 && hypot(i-(w-1)/2,j-(h-1)/2)>0.33*w)?1:0' \
  -format '%[fx:mean]' info:)"
if ! awk -v fraction="$critical_fraction" 'BEGIN { exit !(fraction > 0.01) }'; then
  echo "icon-source-replacement: adaptive foreground has no meaningful navy/teal critical artwork" >&2
  exit 1
fi
if ! awk -v fraction="$outside_safe_fraction" 'BEGIN { exit !(fraction == 0) }'; then
  echo "icon-source-replacement: navy/teal critical pixels escape the 66% circular safe zone ($outside_safe_fraction)" >&2
  exit 1
fi

overlay_geometry="$(magick identify -quiet -format '%w %h' "$safe_overlay")"
if [[ "$overlay_geometry" != "1024 1024" ]]; then
  echo "icon-source-replacement: safe overlay must be 1024x1024" >&2
  exit 1
fi

# Require magenta proof pixels at all four cardinal points of the 66% circle.
for geometry in 9x9+508+169 9x9+508+846 9x9+169+508 9x9+846+508; do
  proof_fraction="$(magick "$safe_overlay" -crop "$geometry" +repage -colorspace sRGB \
    -fx '(r>0.65 && r>b && b>0.35 && g<0.35)?1:0' \
    -format '%[fx:mean]' info:)"
  if ! awk -v fraction="$proof_fraction" 'BEGIN { exit !(fraction > 0.08) }'; then
    echo "icon-source-replacement: overlay does not prove the 66% circle at $geometry" >&2
    exit 1
  fi
done

echo "icon-source-replacement: OK (source, downsample, adaptive safe zone, overlay)"
