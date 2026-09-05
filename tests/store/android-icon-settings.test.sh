#!/usr/bin/env bash
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

python3 - <<'PY'
from pathlib import Path
import re

settings_path = Path("unity/ProjectSettings/ProjectSettings.asset")
text = settings_path.read_text(encoding="utf-8")
start = text.index("  - m_BuildTarget: Android\n")
try:
    end = text.index("  - m_BuildTarget:", start + 1)
except ValueError:
    end = len(text)
block = text[start:end]

entry_pattern = re.compile(
    r"    - m_Textures:(?P<textures>.*?)"
    r"\n      m_Width: (?P<width>\d+)"
    r"\n      m_Height: (?P<height>\d+)"
    r"\n      m_Kind: (?P<kind>\d+)",
    re.DOTALL,
)
entries = []
for match in entry_pattern.finditer(block):
    refs = re.findall(
        r"\{fileID: 2800000, guid: ([0-9a-f]{32}), type: 3\}",
        match.group("textures"),
    )
    entries.append(
        (int(match.group("width")), int(match.group("height")), int(match.group("kind")), refs)
    )

adaptive_sizes = [432, 324, 216, 162, 108, 81]
round_and_legacy_sizes = [192, 144, 96, 72, 48, 36]
expected_layout = (
    [(size, size, 2) for size in adaptive_sizes]
    + [(size, size, 1) for size in round_and_legacy_sizes]
    + [(size, size, 0) for size in round_and_legacy_sizes]
)
if [(width, height, kind) for width, height, kind, _ in entries] != expected_layout:
    raise SystemExit("android-icon-settings: Android density layout changed unexpectedly")

legacy = "a2f43b6debd943af9ff42d2b2c93c9a1"
background = "c101b5aa31134936a828415d94f26cf2"
foreground = "7ab2e8d9d4fd4fb0a1f62a68d47c398e"
for width, _, kind, refs in entries:
    expected_refs = [background, foreground] if kind == 2 else [legacy]
    if refs != expected_refs:
        raise SystemExit(
            f"android-icon-settings: kind={kind} width={width} refs={refs}, expected={expected_refs}"
        )

meta_expectations = {
    Path("unity/Assets/Store/Icons/cat-metro-icon-legacy-512.png.meta"): legacy,
    Path("unity/Assets/Store/Icons/cat-metro-icon-background-512.png.meta"): background,
    Path("unity/Assets/Store/Icons/cat-metro-icon-foreground-512.png.meta"): foreground,
}
for path, expected_guid in meta_expectations.items():
    match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8"), re.MULTILINE)
    if not match or match.group(1) != expected_guid:
        raise SystemExit(f"android-icon-settings: wrong GUID in {path}")

print("android-icon-settings: OK")
PY
