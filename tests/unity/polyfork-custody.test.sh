#!/usr/bin/env bash
# ADR-0011 public-repository custody gate. Licensed derivatives may exist only as
# ignored local inputs; this test never prints their contents.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"

fail() { echo "polyfork-custody.test.sh: FAIL — $1"; exit 1; }

model_root="unity/Assets/Art/Polyfork/Models"
tracked=$(git ls-files "$model_root/*.fbx" "$model_root/*.fbx.meta")
[ -z "$tracked" ] || fail "licensed FBX derivatives or metadata are tracked"

python3 - "$model_root" "${CM_REQUIRE_POLYFORK_LOCAL:-0}" <<'PY'
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import subprocess
import sys


def fail(message: str) -> None:
    print(f"polyfork-custody.test.sh: FAIL — {message}")
    raise SystemExit(1)


root = Path.cwd()
model_root = root / sys.argv[1]
require_local = sys.argv[2] == "1"
provenance = root / "unity/Assets/Art/Polyfork/PROVENANCE.md"
authoring = root / "unity/Assets/Art/Polyfork/Editor/CatMetroDioramaAuthoring.cs"
prefab_root = root / "unity/Assets/Prefabs/Diorama"

row_pattern = re.compile(
    r"^\| \[[^]]+\]\([^)]+\) \(`(?P<asset_id>[^`]+)`\)"
    r" \| (?P<tris>[0-9,]+)"
    r" \| `(?P<source>[0-9a-f]{64})`"
    r" \| `(?P<name>[^`]+\.fbx)`"
    r" \| `(?P<derivative>[0-9a-f]{64})`"
    r" \| `(?P<guid>[0-9a-f]{32})`"
    r" \| `(?P<meta>[0-9a-f]{64})` \|$"
)
rows = []
for line in provenance.read_text(encoding="utf-8").splitlines():
    match = row_pattern.match(line)
    if match:
        rows.append(match.groupdict())

if len(rows) != 9:
    fail(f"PROVENANCE must contain exactly 9 complete receipt rows, found {len(rows)}")
for key in ("asset_id", "source", "name", "derivative", "guid", "meta"):
    values = [row[key] for row in rows]
    if len(set(values)) != 9:
        fail(f"PROVENANCE {key} values must be unique")

authoring_text = authoring.read_text(encoding="utf-8")
preflight_token = "RequirePolyforkLocalCustody();"
first_mutation_token = 'EnsureFolder("Assets/Art", "Materials");'
if preflight_token not in authoring_text:
    fail("diorama authoring does not preflight the licensed local asset pack")
if authoring_text.index(preflight_token) > authoring_text.index(first_mutation_token):
    fail("diorama authoring preflight must run before asset mutation")
for row in rows:
    if authoring_text.count(f'new Dressing("{row["name"]}"') != 1:
        fail(f"authoring manifest must name {row['name']} exactly once")

prefabs = sorted(prefab_root.glob("Polyfork_*.prefab"))
if len(prefabs) != 9:
    fail(f"expected 9 Cat-Metro Polyfork prefabs, found {len(prefabs)}")
prefab_contents = {path: path.read_text(encoding="utf-8") for path in prefabs}
prefab_text = "\n".join(prefab_contents.values())
for forbidden in ("Mesh:", "m_VertexData:", "m_IndexBuffer:", "m_CompressedMesh:", "_typelessdata:"):
    if forbidden in prefab_text:
        fail(f"Cat-Metro prefabs embed mesh payload token {forbidden}")
for row in rows:
    referencing_prefabs = [
        path for path, contents in prefab_contents.items()
        if re.search(rf"guid: {re.escape(row['guid'])}\b", contents)
    ]
    if len(referencing_prefabs) != 1:
        fail(
            f"receipt GUID {row['guid']} must belong to exactly one Cat-Metro prefab, "
            f"found {len(referencing_prefabs)}"
        )

expected_fbx = {model_root / row["name"] for row in rows}
expected_meta = {Path(str(path) + ".meta") for path in expected_fbx}
actual_fbx = set(model_root.glob("*.fbx")) if model_root.is_dir() else set()
actual_meta = set(model_root.glob("*.fbx.meta")) if model_root.is_dir() else set()

unexpected = (actual_fbx - expected_fbx) | (actual_meta - expected_meta)
if unexpected:
    fail("unexpected local FBX or metadata exists in the custody directory")
present = actual_fbx | actual_meta
expected = expected_fbx | expected_meta
if present and present != expected:
    fail("local custody must contain either zero files or all 9 FBX/meta pairs")
if require_local and present != expected:
    fail("CM_REQUIRE_POLYFORK_LOCAL=1 requires all 9 FBX/meta pairs")

for row in rows:
    fbx = model_root / row["name"]
    meta = Path(str(fbx) + ".meta")
    for path in (fbx, meta):
        result = subprocess.run(
            ["git", "check-ignore", "--no-index", "--quiet", str(path.relative_to(root))],
            cwd=root,
            check=False,
        )
        if result.returncode != 0:
            fail(f"{path.relative_to(root)} is not protected by gitignore")
    if not present:
        continue
    if hashlib.sha256(fbx.read_bytes()).hexdigest() != row["derivative"]:
        fail(f"local derivative hash mismatch for {row['name']}")
    if hashlib.sha256(meta.read_bytes()).hexdigest() != row["meta"]:
        fail(f"local metadata hash mismatch for {row['name']}")
    guid_match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8"), re.MULTILINE)
    if guid_match is None or guid_match.group(1) != row["guid"]:
        fail(f"local metadata GUID mismatch for {row['name']}")

profile = "hydrated-local" if present else "clean-public"
print(f"polyfork-custody.test.sh: OK ({profile}; 9 receipts; no tracked derivatives)")
PY
