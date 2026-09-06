#!/usr/bin/env bash
# Deterministic source/provenance/import gate for SFX, voices, and the wooden-toy score.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 1

fail() { echo "audio-assets.test.sh: FAIL — $1"; exit 1; }

generator="scripts/generate-game-audio.py"
asset_root="unity/Assets/Resources/Audio/CatMetro"
importer="unity/Assets/Editor/CatMetroAudioImportPipeline.cs"

[ -f "$generator" ] || fail "$generator missing"
[ -f "$importer" ] || fail "$importer missing"
python3 "$generator" --check || fail "WAV format, payload, reproducibility, or provenance drift"

# Static fallback for hosts without Unity: the Editor test performs typed read-back, while
# these pins make sure the postprocessor itself cannot disappear and leave stale .meta files green.
grep -Eq 'loadType[[:space:]]*=[[:space:]]*AudioClipLoadType\.DecompressOnLoad' "$importer" \
  || fail "import pipeline no longer pins Decompress On Load"
grep -Eq 'sampleRateSetting[[:space:]]*=[[:space:]]*AudioSampleRateSetting\.OverrideSampleRate' "$importer" \
  || fail "import pipeline no longer overrides sample rate"
grep -Eq 'sampleRateOverride[[:space:]]*=[[:space:]]*SampleRate' "$importer" \
  || fail "import pipeline no longer pins 44.1 kHz"
grep -Eq 'compressionFormat[[:space:]]*=[[:space:]]*AudioCompressionFormat\.ADPCM' "$importer" \
  || fail "import pipeline no longer pins mobile ADPCM"
grep -Eq 'preloadAudioData[[:space:]]*=[[:space:]]*true' "$importer" \
  || fail "import pipeline no longer preloads short cues"
grep -Eq 'forceToMono[[:space:]]*=[[:space:]]*true' "$importer" \
  || fail "import pipeline no longer forces mono"
grep -Eq 'FindProperty\("m_Normalize"\)' "$importer" \
  || fail "import pipeline no longer reaches Unity 6's serialized normalization flag"
grep -Eq 'normalize\.boolValue[[:space:]]*=[[:space:]]*false' "$importer" \
  || fail "import pipeline would normalize away the authored balance"

meta_count=0
for wav in "$asset_root"/*.wav; do
  meta="$wav.meta"
  [ -f "$meta" ] || fail "Unity importer metadata missing for $wav"
  meta_count=$((meta_count + 1))
  grep -q 'loadType: 0' "$meta" || fail "$meta is not Decompress On Load"
  grep -q 'sampleRateSetting: 2' "$meta" || fail "$meta does not override sample rate"
  grep -q 'sampleRateOverride: 44100' "$meta" || fail "$meta is not 44.1 kHz"
  grep -q 'compressionFormat: 2' "$meta" || fail "$meta is not ADPCM"
  grep -q 'preloadAudioData: 1' "$meta" || fail "$meta is not preloaded"
  grep -q 'forceToMono: 1' "$meta" || fail "$meta is not mono"
  grep -q 'normalize: 0' "$meta" || fail "$meta unexpectedly normalizes"
  grep -q 'loadInBackground: 0' "$meta" || fail "$meta loads in the background"
  grep -q 'ambisonic: 0' "$meta" || fail "$meta unexpectedly enables ambisonics"
  grep -q '3D: 0' "$meta" || fail "$meta unexpectedly carries the legacy 3D flag"
done
[ "$meta_count" = "18" ] || fail "expected 18 SFX import metadata files, found $meta_count"

music_count=0
for wav in "$asset_root"/music/*.wav; do
  meta="$wav.meta"
  [ -f "$meta" ] || fail "Unity importer metadata missing for $wav"
  music_count=$((music_count + 1))
  grep -q 'loadType: 2' "$meta" || fail "$meta is not streaming"
  grep -q 'compressionFormat: 1' "$meta" || fail "$meta is not Vorbis"
  grep -q 'quality: 0.5' "$meta" || fail "$meta is not quality 0.5"
  grep -q 'forceToMono: 0' "$meta" || fail "$meta is not stereo"
  grep -q 'normalize: 0' "$meta" || fail "$meta changes the authored mix"
done
[ "$music_count" = "5" ] || fail "expected four stems and Home music, found $music_count"

echo "audio-assets.test.sh: OK (18 mono ADPCM cues; 5 stereo streaming Vorbis music masters; generator-exact provenance)"
