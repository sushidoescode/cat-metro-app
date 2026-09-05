#!/usr/bin/env bash
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

if ! command -v magick >/dev/null 2>&1; then
  echo "store-assets: SKIP (ImageMagick is required for raster probes)"
  exit 0
fi

assert_png() {
  local path="$1"
  local width="$2"
  local height="$3"

  if [[ ! -f "$path" ]]; then
    echo "store-assets: missing $path" >&2
    exit 1
  fi

  local actual
  actual="$(magick identify -quiet -format '%m %w %h %[colorspace]' "$path")"
  if [[ "$actual" != "PNG $width $height sRGB" ]]; then
    echo "store-assets: $path expected PNG $width $height sRGB, got $actual" >&2
    exit 1
  fi
}

assert_opaque() {
  local path="$1"
  local opaque
  opaque="$(magick identify -quiet -format '%[opaque]' "$path" | tr '[:upper:]' '[:lower:]')"
  if [[ "$opaque" != "true" ]]; then
    echo "store-assets: $path contains transparent pixels" >&2
    exit 1
  fi
}

assert_srgb_chunk() {
  local path="$1"
  if ! magick identify -quiet -verbose "$path" | grep 'png:sRGB: intent=' >/dev/null; then
    echo "store-assets: $path has no explicit PNG sRGB declaration" >&2
    exit 1
  fi
}

master="docs/store/assets/icon/cat-metro-icon-master-1024.png"
play="docs/store/assets/icon/cat-metro-icon-play-512.png"
devpost="docs/store/assets/icon/cat-metro-icon-devpost-1024.png"
feature="docs/store/assets/feature/cat-metro-feature-graphic-1024x500.png"

assert_png "$master" 1024 1024
assert_png "$play" 512 512
assert_png "$devpost" 1024 1024
assert_png "$feature" 1024 500

assert_opaque "$master"
assert_opaque "$play"
assert_opaque "$devpost"
assert_opaque "$feature"

for path in "$master" "$play" "$devpost" "$feature"; do
  assert_srgb_chunk "$path"
done

play_channels="$(magick identify -quiet -format '%[channels]' "$play")"
if [[ "$play_channels" != srgba* ]]; then
  echo "store-assets: Play icon must be an 8-bit RGBA PNG, got channels=$play_channels" >&2
  exit 1
fi

play_depth="$(magick identify -quiet -format '%z' "$play")"
if [[ "$play_depth" != "8" ]]; then
  echo "store-assets: Play icon must be 8-bit per channel, got depth=$play_depth" >&2
  exit 1
fi

play_bytes="$(wc -c < "$play" | tr -d '[:space:]')"
if (( play_bytes > 1048576 )); then
  echo "store-assets: Play icon exceeds 1024 KiB ($play_bytes bytes)" >&2
  exit 1
fi

feature_channels="$(magick identify -quiet -format '%[channels]' "$feature")"
if [[ "$feature_channels" != srgb* || "$feature_channels" == *a* ]]; then
  echo "store-assets: feature graphic must be 24-bit RGB with no alpha, got channels=$feature_channels" >&2
  exit 1
fi

for size in 192 96 48; do
  assert_png "docs/store/assets/icon/review/cat-metro-icon-${size}.png" "$size" "$size"
  assert_opaque "docs/store/assets/icon/review/cat-metro-icon-${size}.png"
done

assert_png "docs/store/assets/icon/review/cat-metro-icon-contact-sheet.png" 1024 640
assert_png "docs/store/assets/icon/review/cat-metro-icon-safe-crop-512.png" 512 512
assert_png "docs/store/assets/icon/review/cat-metro-icon-safe-crop-overlay-1024.png" 1024 1024

for simulation in grayscale deutan protan tritan; do
  simulation_path="docs/store/assets/icon/review/cat-metro-icon-${simulation}-1024.png"
  assert_png "$simulation_path" 1024 1024
  assert_opaque "$simulation_path"
  assert_srgb_chunk "$simulation_path"
done

teal_fraction="$(magick "$master" -colorspace sRGB \
  -fx '(g-r>0.10 && b-r>0.08 && g>0.55)?1:0' \
  -format '%[fx:mean]' info:)"
if ! awk -v fraction="$teal_fraction" 'BEGIN { exit !(fraction <= 0.02) }'; then
  echo "store-assets: teal-like pixels exceed 2% of the canvas ($teal_fraction)" >&2
  exit 1
fi

legacy="unity/Assets/Store/Icons/cat-metro-icon-legacy-512.png"
background="unity/Assets/Store/Icons/cat-metro-icon-background-512.png"
foreground="unity/Assets/Store/Icons/cat-metro-icon-foreground-512.png"
for path in "$legacy" "$background" "$foreground"; do
  assert_png "$path" 512 512
  assert_srgb_chunk "$path"
done
assert_opaque "$legacy"
assert_opaque "$background"
foreground_opaque="$(magick identify -quiet -format '%[opaque]' "$foreground" | tr '[:upper:]' '[:lower:]')"
if [[ "$foreground_opaque" != "false" ]]; then
  echo "store-assets: adaptive foreground must retain transparency" >&2
  exit 1
fi

python3 - <<'PY'
from hashlib import sha256
from pathlib import Path

manifest_path = Path("docs/store/assets/raster-sha256.txt")
declared = {}
for line in manifest_path.read_text(encoding="utf-8").splitlines():
    digest, path = line.split("  ", 1)
    declared[Path(path)] = digest

actual = sorted(
    list(Path("docs/store/assets").rglob("*.png"))
    + list(Path("unity/Assets/Store").rglob("*.png"))
)
if set(actual) != set(declared):
    missing = sorted(str(path) for path in set(actual) - set(declared))
    stale = sorted(str(path) for path in set(declared) - set(actual))
    raise SystemExit(f"store-assets: raster manifest mismatch; missing={missing}, stale={stale}")

for path in actual:
    observed = sha256(path.read_bytes()).hexdigest()
    if observed != declared[path]:
        raise SystemExit(f"store-assets: hash mismatch for {path}: {observed}")
PY

echo "store-assets: OK"
