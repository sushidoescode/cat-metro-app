#!/usr/bin/env bash
# GLB-CURATION: geometry predicates, source-pair transactions, and untouched pins.
set -euo pipefail

repo_root=$(git rev-parse --show-toplevel)
rules="$repo_root/scripts/glb_curation_rules.py"
orchestrator="$repo_root/scripts/curate-assets.py"
driver="$repo_root/scripts/blender_curate.py"
metrics="$repo_root/docs/design/assets/GLB-DECIMATION-METRICS.json"
curation_manifest="$repo_root/docs/design/assets/GLB-CURATION-MANIFEST.json"
wave_manifest="$repo_root/docs/design/assets/GLB-CURATION-WAVE-MANIFEST.json"

for required in "$rules" "$orchestrator" "$driver" "$curation_manifest" "$wave_manifest"; do
  if [[ ! -f "$required" ]]; then
    printf 'glb-curation test: missing production entrypoint: %s\n' "$required" >&2
    exit 1
  fi
done

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo_root" "$rules" "$orchestrator" "$metrics" \
  "$curation_manifest" "$wave_manifest" <<'PY'
from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import stat
import tempfile
from pathlib import Path


repo_root = Path(os.sys.argv[1])
rules_path = Path(os.sys.argv[2])
orchestrator_path = Path(os.sys.argv[3])
metrics_path = Path(os.sys.argv[4])
curation_manifest_path = Path(os.sys.argv[5])
wave_manifest_path = Path(os.sys.argv[6])


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise AssertionError(f"could not load {path}")
    module = importlib.util.module_from_spec(spec)
    os.sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


rules = load("catmetro_glb_curation_rules", rules_path)
curator = load("catmetro_curate_assets", orchestrator_path)

assert rules.LOAF_CUT_HEIGHT_RATIO == 0.08
assert rules.LOAF_SELECTED_FOOTPRINT_MINIMUM == 0.95
assert rules.LOAF_RETAINED_FOOTPRINT_MAXIMUM == 0.80
assert rules.WAVE_THIN_SPAN_RATIO == 0.07
assert rules.WAVE_MIN_Y_LOCATION_RATIO == 0.01

full_bounds = {
    "minimum": (-0.3852173089981079, -0.5, -0.4085644483566284),
    "maximum": (0.3852173089981079, 0.5, 0.4085644483566284),
}
wave_components = [
    {
        "triangles": 1_383_894,
        "minimum": (-0.3852173089981079, -0.4726581275463104, -0.4085644483566284),
        "maximum": (0.3852173089981079, 0.5, 0.4024394452571869),
    },
    {
        "triangles": 71_282,
        "minimum": (-0.000002001221218961291, -0.5, -0.4085644483566284),
        "maximum": (0.3052848279476166, -0.43638890981674194, -0.2873024046421051),
    },
    {
        "triangles": 38_914,
        "minimum": (-0.2941795289516449, -0.051845185458660126, -0.3547399640083313),
        "maximum": (-0.13326361775398254, 0.04354926943778992, -0.19544756412506104),
    },
]
selected = rules.select_wave_fragments(wave_components, full_bounds)
assert selected == [1], selected

assert rules.select_non_largest_components(wave_components, kind="cat") == [1, 2]
assert rules.select_non_largest_components(
    [wave_components[0], wave_components[2]], kind="cat"
) == [1]
assert rules.select_non_largest_components([wave_components[0]], kind="cat") == []
for components, kind, expected_message in (
    (wave_components, "prop", "only applies to cats"),
    ([wave_components[0], {**wave_components[1], "triangles": 1_383_894}], "cat", "unique largest"),
):
    try:
        rules.select_non_largest_components(components, kind=kind)
    except rules.CurationRuleError as exc:
        assert expected_message in str(exc)
    else:
        raise AssertionError(
            f"largest-component guard accepted kind={kind!r} components={components!r}"
        )

# Equality laws are load-bearing: thinness is strict; min-Y location is inclusive.
full_unit = {"minimum": (0.0, 0.0, 0.0), "maximum": (1.0, 1.0, 1.0)}
thin_equal = {"triangles": 10, "minimum": (0.0, 0.0, 0.0), "maximum": (0.07, 0.5, 0.5)}
assert rules.select_wave_fragments([thin_equal], full_unit) == []
thin_below_at_location_boundary = {
    "triangles": 10,
    "minimum": (0.0, 0.01, 0.0),
    "maximum": (0.069, 0.50, 0.50),
}
assert rules.select_wave_fragments([thin_below_at_location_boundary], full_unit) == [0]
thin_below_above_location = {
    **thin_below_at_location_boundary,
    "minimum": (0.0, 0.0100001, 0.0),
}
assert rules.select_wave_fragments([thin_below_above_location], full_unit) == []

rules.validate_loaf_footprints(
    selected_width_ratio=0.95,
    selected_depth_ratio=0.95,
    retained_width_ratio=0.799999,
    retained_depth_ratio=0.799999,
)
for bad in (
    dict(selected_width_ratio=0.949999, selected_depth_ratio=1.0,
         retained_width_ratio=0.7436, retained_depth_ratio=0.5400),
    dict(selected_width_ratio=1.0, selected_depth_ratio=0.949999,
         retained_width_ratio=0.7436, retained_depth_ratio=0.5400),
    dict(selected_width_ratio=1.0, selected_depth_ratio=1.0,
         retained_width_ratio=0.80, retained_depth_ratio=0.5400),
    dict(selected_width_ratio=1.0, selected_depth_ratio=1.0,
         retained_width_ratio=0.5400, retained_depth_ratio=0.80),
):
    try:
        rules.validate_loaf_footprints(**bad)
    except rules.CurationRuleError:
        pass
    else:
        raise AssertionError(f"loaf footprint guard accepted {bad!r}")

expected_sources = {
    "cat-blue-siamese-loaf": "e3015351ec9bda2aebeafcc0ff23f5aa35512af4234c168d79cac750118070e3",
    "cat-yellow-longhair-wave": "8d7190fd24f552f874bf1d733f2870c44a24c27d6b50cfe1e32095f625fcc57c",
}
assert curator.ALLOWED_SOURCE_SHA256 == expected_sources
expected_source_sidecars = {
    "cat-blue-siamese-loaf": "ce8ea067634f88ee9fc967ea5a0dbc58df890477d3e1dc1905cc3f77a92dcec4",
    "cat-yellow-longhair-wave": "e65414b151fa1dd868e9086c0e274ac61743aef8f8f26bc7bcaa6f49f99c8936",
}
assert curator.ALLOWED_SOURCE_SIDECAR_SHA256 == expected_source_sidecars
expected_precuration_pairs = {
    "cat-blue-siamese-loaf": ((
        expected_sources["cat-blue-siamese-loaf"],
        expected_source_sidecars["cat-blue-siamese-loaf"],
    ),),
    "cat-yellow-longhair-wave": (
        (
            expected_sources["cat-yellow-longhair-wave"],
            expected_source_sidecars["cat-yellow-longhair-wave"],
        ),
        (
            "f91ccb7ff9b527ecef168d4285488ff647023fb70875f5403c31db8e2349d99d",
            "bb787a4073833edfd54af3e401cfa00e73b5279592ba2d146b015d3f1ffe90e4",
        ),
    ),
}
assert curator.ALLOWED_PRECURATION_PAIRS == expected_precuration_pairs
assert curator.EXPECTED_CURATED_TRIANGLES["cat-yellow-longhair-wave"] == 1_383_894

original_record = {
    "service": "tripo",
    "task_id": "fixture-task",
    "timestamp_utc": "2026-08-15T06:17:35Z",
    "plan_tier": "paid",
    "prompt": "fixture prompt",
    "note": "tripo model=v3.1-20260211",
    "sha256": expected_sources["cat-yellow-longhair-wave"],
}
record_snapshot = dict(original_record)
new_sha = "a" * 64
updated = curator.build_curated_source_record(
    "cat-yellow-longhair-wave", original_record, new_sha
)
assert original_record == record_snapshot
for field in ("service", "task_id", "timestamp_utc", "plan_tier", "prompt"):
    assert updated[field] == original_record[field]
assert updated["sha256"] == new_sha
assert updated["note"] == (
    "tripo model=v3.1-20260211; Cat Metro GLB-CURATION: "
    "kept largest cat component; removed detached components"
)

correction_record = {
    **original_record,
    "sha256": curator.WAVE_CORRECTION_SOURCE_SHA256,
    "note": (
        "tripo model=v3.1-20260211; Cat Metro GLB-CURATION: "
        "removed ruled min-Y foot fragment"
    ),
}
corrected = curator.build_curated_source_record(
    "cat-yellow-longhair-wave", correction_record, new_sha
)
assert corrected["note"] == updated["note"]
assert corrected["sha256"] == new_sha

for malformed, expected_message in (
    ({key: value for key, value in original_record.items() if key != "prompt"},
     "missing required fields"),
    ({**original_record, "sha256": "b" * 64}, "SHA-256 mismatch"),
):
    try:
        curator._validate_source_record(
            "cat-yellow-longhair-wave",
            malformed,
            expected_sources["cat-yellow-longhair-wave"],
            require_precuration_anchor=True,
        )
    except curator.CurationError as exc:
        assert expected_message in str(exc)
    else:
        raise AssertionError(f"malformed source sidecar passed: {malformed!r}")

try:
    curator.build_curated_source_record("cat-red-tabby", original_record, new_sha)
except curator.CurationError as exc:
    assert "outside the frozen curation allowlist" in str(exc)
else:
    raise AssertionError("out-of-scope asset ID passed source-record curation")

source_metrics_fixture = {
    "meshes": 1,
    "primitives": 1,
    "materials": 1,
    "material_primitives": 1,
    "images": 2,
    "embedded_images": 2,
    "uv_primitives": 1,
    "animations": 0,
    "cameras": 0,
    "lights": 0,
    "skins": 0,
    "morph_targets": 0,
    "external_uris": [],
    "extensions_used": [],
    "extensions_required": [],
    "vertices": 100,
    "referenced_vertices": 100,
    "degenerate_triangles": 0,
    "image_payload_sha256": ["1" * 64, "2" * 64],
    "material_texture_bindings": [
        {
            "material": 0,
            "role": "baseColor",
            "texcoord": 0,
            "payload_sha256": "1" * 64,
        }
    ],
}
candidate_metrics_fixture = {
    **source_metrics_fixture,
    "triangles": curator.EXPECTED_CURATED_TRIANGLES["cat-blue-siamese-loaf"],
    "unique_triangles": curator.EXPECTED_CURATED_TRIANGLES["cat-blue-siamese-loaf"],
    "image_payload_sha256": ["2" * 64, "1" * 64],
}
candidate_report_fixture = {
    "asset_id": "cat-blue-siamese-loaf",
    "triangles_after": curator.EXPECTED_CURATED_TRIANGLES["cat-blue-siamese-loaf"],
}
curator._candidate_structure(
    "cat-blue-siamese-loaf",
    source_metrics_fixture,
    candidate_metrics_fixture,
    candidate_report_fixture,
)
for field, replacement in (
    ("image_payload_sha256", ["1" * 64, "3" * 64]),
    (
        "material_texture_bindings",
        [{**source_metrics_fixture["material_texture_bindings"][0], "role": "normal"}],
    ),
):
    mutated = {**candidate_metrics_fixture, field: replacement}
    try:
        curator._candidate_structure(
            "cat-blue-siamese-loaf",
            source_metrics_fixture,
            mutated,
            candidate_report_fixture,
        )
    except curator.CurationError as exc:
        assert "payload" in str(exc) or "binding" in str(exc)
    else:
        raise AssertionError(f"candidate mutation passed custody validation: {field}")


def write(path: Path, payload: bytes) -> None:
    path.write_bytes(payload)


with tempfile.TemporaryDirectory(prefix="catmetro-curation-test-") as raw:
    root = Path(raw)
    final_glb = root / "asset.glb"
    final_json = root / "asset.glb.json"
    staged_glb = root / ".asset.candidate.glb"
    staged_json = root / ".asset.candidate.glb.json"
    backup_dir = root / "backup"
    write(final_glb, b"old-glb")
    write(final_json, b"old-json")
    write(staged_glb, b"new-glb")
    write(staged_json, b"new-json")
    curator.publish_pair(
        staged_glb=staged_glb,
        staged_sidecar=staged_json,
        final_glb=final_glb,
        final_sidecar=final_json,
        backup_dir=backup_dir,
    )
    assert final_glb.read_bytes() == b"new-glb"
    assert final_json.read_bytes() == b"new-json"
    assert (backup_dir / final_glb.name).read_bytes() == b"old-glb"
    assert (backup_dir / final_json.name).read_bytes() == b"old-json"
    assert not staged_glb.exists() and not staged_json.exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-rollback-") as raw:
    root = Path(raw)
    final_glb = root / "asset.glb"
    final_json = root / "asset.glb.json"
    staged_glb = root / ".asset.candidate.glb"
    staged_json = root / ".asset.candidate.glb.json"
    backup_dir = root / "backup"
    write(final_glb, b"old-glb")
    write(final_json, b"old-json")
    write(staged_glb, b"new-glb")
    write(staged_json, b"new-json")

    def fail_second(source, destination):
        if Path(source) == staged_json and Path(destination) == final_json:
            raise OSError("injected sidecar promotion failure")
        os.replace(source, destination)

    try:
        curator.publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_json,
            final_glb=final_glb,
            final_sidecar=final_json,
            backup_dir=backup_dir,
            replace_fn=fail_second,
        )
    except curator.CurationError as exc:
        assert "promotion failed" in str(exc)
    else:
        raise AssertionError("injected promotion failure unexpectedly succeeded")
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert (backup_dir / final_glb.name).read_bytes() == b"old-glb"
    assert (backup_dir / final_json.name).read_bytes() == b"old-json"


def make_pair(root: Path):
    final_glb = root / "asset.glb"
    final_json = root / "asset.glb.json"
    staged_glb = root / ".asset.candidate.glb"
    staged_json = root / ".asset.candidate.glb.json"
    backup_dir = root / "backup"
    journal = root / ".glb-curation-fixture.transaction.json"
    write(final_glb, b"old-glb")
    write(final_json, b"old-json")
    write(staged_glb, b"new-glb")
    write(staged_json, b"new-json")
    return final_glb, final_json, staged_glb, staged_json, backup_dir, journal


with tempfile.TemporaryDirectory(prefix="catmetro-curation-lock-") as raw:
    root = Path(raw)
    with curator.source_root_lock(root):
        try:
            with curator.source_root_lock(root):
                pass
        except curator.CurationError as exc:
            assert "locked" in str(exc)
        else:
            raise AssertionError("concurrent source-root lock unexpectedly succeeded")

with tempfile.TemporaryDirectory(prefix="catmetro-curation-all-journal-recovery-") as raw:
    root = Path(raw)
    blender = root / "blender"
    write(blender, b"#!/bin/sh\nexit 0\n")
    blender.chmod(0o700)
    backup_parent = root / "curation-backups"
    backup_parent.mkdir()
    arguments = curator._parse_arguments(
        [
            "--input-dir",
            str(root),
            "--backup-dir",
            str(backup_parent / "wave-original"),
            "--blender",
            str(blender),
            "--asset-id",
            curator.WAVE_ID,
        ]
    )
    observed_recoveries = []
    original_recover = curator.recover_interrupted_pair
    original_resolve = curator._resolve_input_member

    class RecoveryBoundaryReached(RuntimeError):
        pass

    def record_recovery(*, journal_path, final_glb, final_sidecar):
        observed_recoveries.append(
            (journal_path.name, final_glb.name, final_sidecar.name)
        )
        return False

    def stop_after_recovery(*args, **kwargs):
        raise RecoveryBoundaryReached

    curator.recover_interrupted_pair = record_recovery
    curator._resolve_input_member = stop_after_recovery
    try:
        try:
            curator.curate(arguments)
        except RecoveryBoundaryReached:
            pass
        else:
            raise AssertionError("curation unexpectedly passed the recovery boundary")
    finally:
        curator.recover_interrupted_pair = original_recover
        curator._resolve_input_member = original_resolve
    assert observed_recoveries == [
        (
            f".glb-curation-{curator.LOAF_ID}.transaction.json",
            curator.ASSET_FILENAMES[curator.LOAF_ID],
            f"{curator.ASSET_FILENAMES[curator.LOAF_ID]}.json",
        ),
        (
            f".glb-curation-{curator.WAVE_ID}.transaction.json",
            curator.ASSET_FILENAMES[curator.WAVE_ID],
            f"{curator.ASSET_FILENAMES[curator.WAVE_ID]}.json",
        ),
    ]

with tempfile.TemporaryDirectory(prefix="catmetro-curation-preexisting-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)
    backup_dir.mkdir()
    try:
        curator.publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_json,
            final_glb=final_glb,
            final_sidecar=final_json,
            backup_dir=backup_dir,
            journal_path=journal,
        )
    except curator.CurationError as exc:
        assert "already exists" in str(exc)
    else:
        raise AssertionError("pre-existing backup directory unexpectedly passed")
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert not journal.exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-final-anchor-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)
    try:
        curator.publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_json,
            final_glb=final_glb,
            final_sidecar=final_json,
            backup_dir=backup_dir,
            journal_path=journal,
            expected_final_glb_sha="0" * 64,
        )
    except curator.CurationError as exc:
        assert "final GLB changed" in str(exc)
    else:
        raise AssertionError("changed final anchor unexpectedly passed")
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert not backup_dir.exists() and not journal.exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-corrupt-backup-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)
    original_copy_new = curator._copy_new

    def corrupt_backup_copy(source, destination):
        original_copy_new(source, destination)
        if Path(destination) == backup_dir / final_json.name:
            with Path(destination).open("r+b") as handle:
                handle.seek(0)
                handle.write(b"bad-json")
                handle.truncate()
                handle.flush()
                os.fsync(handle.fileno())

    curator._copy_new = corrupt_backup_copy
    try:
        try:
            curator.publish_pair(
                staged_glb=staged_glb,
                staged_sidecar=staged_json,
                final_glb=final_glb,
                final_sidecar=final_json,
                backup_dir=backup_dir,
                journal_path=journal,
            )
        except curator.CurationError as exc:
            assert "backup sidecar" in str(exc) and "mismatch" in str(exc)
        else:
            raise AssertionError("corrupt completed backup unexpectedly passed")
    finally:
        curator._copy_new = original_copy_new
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert staged_glb.read_bytes() == b"new-glb"
    assert staged_json.read_bytes() == b"new-json"
    assert not backup_dir.exists()
    assert not journal.exists()
    assert not curator._transaction_next_path(journal).exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-after-effect-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)

    def replace_then_fail(source, destination):
        os.replace(source, destination)
        if Path(destination) == final_json:
            raise OSError("injected failure after sidecar replacement")

    try:
        curator.publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_json,
            final_glb=final_glb,
            final_sidecar=final_json,
            backup_dir=backup_dir,
            journal_path=journal,
            replace_fn=replace_then_fail,
        )
    except curator.CurationError as exc:
        assert "original source pair restored" in str(exc)
    else:
        raise AssertionError("after-effect promotion failure unexpectedly passed")
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert not journal.exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-interrupt-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)

    def interrupt_second(source, destination):
        if Path(source) == staged_json and Path(destination) == final_json:
            raise KeyboardInterrupt("injected interruption")
        os.replace(source, destination)

    try:
        curator.publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_json,
            final_glb=final_glb,
            final_sidecar=final_json,
            backup_dir=backup_dir,
            journal_path=journal,
            replace_fn=interrupt_second,
        )
    except KeyboardInterrupt:
        pass
    else:
        raise AssertionError("promotion interruption unexpectedly passed")
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert not journal.exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-prejournal-interrupt-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)
    original_journal_writer = curator._write_transaction_journal

    def interrupt_prepared_journal(*args, **kwargs):
        raise KeyboardInterrupt("injected prepared-journal interruption")

    curator._write_transaction_journal = interrupt_prepared_journal
    try:
        try:
            curator.publish_pair(
                staged_glb=staged_glb,
                staged_sidecar=staged_json,
                final_glb=final_glb,
                final_sidecar=final_json,
                backup_dir=backup_dir,
                journal_path=journal,
            )
        except KeyboardInterrupt:
            pass
        else:
            raise AssertionError("prepared-journal interruption unexpectedly passed")
    finally:
        curator._write_transaction_journal = original_journal_writer
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert staged_glb.read_bytes() == b"new-glb"
    assert staged_json.read_bytes() == b"new-json"
    assert not backup_dir.exists()
    assert not journal.exists()
    assert not curator._transaction_next_path(journal).exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-prejournal-fsync-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)
    fsync_calls = {"count": 0}

    def interrupt_prepared_journal_fsync(path):
        fsync_calls["count"] += 1
        if fsync_calls["count"] == 3:
            raise KeyboardInterrupt("injected prepared-journal fsync interruption")
        curator._fsync_directory(path)

    try:
        curator.publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_json,
            final_glb=final_glb,
            final_sidecar=final_json,
            backup_dir=backup_dir,
            journal_path=journal,
            fsync_directory_fn=interrupt_prepared_journal_fsync,
        )
    except KeyboardInterrupt:
        pass
    else:
        raise AssertionError("prepared-journal fsync interruption unexpectedly passed")
    assert fsync_calls["count"] == 4
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert staged_glb.read_bytes() == b"new-glb"
    assert staged_json.read_bytes() == b"new-json"
    assert not backup_dir.exists()
    assert not journal.exists()
    assert not curator._transaction_next_path(journal).exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-fsync-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)
    promoted = False
    injected = False

    def tracked_replace(source, destination):
        nonlocal_state["promoted"] = Path(destination) == final_glb or nonlocal_state["promoted"]
        os.replace(source, destination)

    def fail_first_post_promotion_fsync(path):
        if nonlocal_state["promoted"] and not nonlocal_state["injected"]:
            nonlocal_state["injected"] = True
            raise OSError("injected final-directory fsync failure")
        curator._fsync_directory(path)

    nonlocal_state = {"promoted": promoted, "injected": injected}
    try:
        curator.publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_json,
            final_glb=final_glb,
            final_sidecar=final_json,
            backup_dir=backup_dir,
            journal_path=journal,
            replace_fn=tracked_replace,
            fsync_directory_fn=fail_first_post_promotion_fsync,
        )
    except curator.CurationError as exc:
        assert "original source pair restored" in str(exc)
    else:
        raise AssertionError("promotion fsync failure unexpectedly passed")
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert not journal.exists()

with tempfile.TemporaryDirectory(prefix="catmetro-curation-recovery-") as raw:
    root = Path(raw)
    final_glb, final_json, staged_glb, staged_json, backup_dir, journal = make_pair(root)

    def fail_promotion(source, destination):
        source_path = Path(source)
        destination_path = Path(destination)
        if source_path == staged_json and destination_path == final_json:
            raise OSError("injected sidecar promotion failure")
        os.replace(source, destination)

    def fail_glb_rollback(source, destination):
        source_path = Path(source)
        destination_path = Path(destination)
        if ".rollback-" in source_path.name and destination_path == final_glb:
            raise OSError("injected rollback failure")
        os.replace(source, destination)

    try:
        curator.publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_json,
            final_glb=final_glb,
            final_sidecar=final_json,
            backup_dir=backup_dir,
            journal_path=journal,
            replace_fn=fail_promotion,
            rollback_replace_fn=fail_glb_rollback,
        )
    except curator.CurationError as exc:
        assert "rollback also failed" in str(exc)
    else:
        raise AssertionError("rollback failure unexpectedly passed")
    assert journal.is_file()
    assert final_glb.read_bytes() == b"new-glb"
    assert final_json.read_bytes() == b"old-json"
    curator.recover_interrupted_pair(
        journal_path=journal,
        final_glb=final_glb,
        final_sidecar=final_json,
    )
    assert final_glb.read_bytes() == b"old-glb"
    assert final_json.read_bytes() == b"old-json"
    assert not journal.exists()
    assert not list(root.glob("*.rollback-*"))

canonical_manifest = json.loads(
    (repo_root / "docs/design/assets/CAT-MANIFEST.json").read_text(encoding="utf-8")
)
curation_manifest = json.loads(curation_manifest_path.read_text(encoding="utf-8"))
selected_ids = [item["id"] for item in curation_manifest["assets"]]
assert selected_ids == ["cat-blue-siamese-loaf", "cat-yellow-longhair-wave"]
canonical_by_id = {item["id"]: item for item in canonical_manifest["assets"]}
assert curation_manifest["assets"] == [canonical_by_id[item] for item in selected_ids]
assert curation_manifest["_meta"]["source_manifest"] == "CAT-MANIFEST.json"
assert curation_manifest["_meta"]["scope"] == "GLB-CURATION exact two assets"
wave_manifest = json.loads(wave_manifest_path.read_text(encoding="utf-8"))
assert wave_manifest["assets"] == [canonical_by_id["cat-yellow-longhair-wave"]]
assert wave_manifest["_meta"]["source_manifest"] == "CAT-MANIFEST.json"
assert wave_manifest["_meta"]["scope"] == "GLB-CURATION wave correction exact one asset"

expected_untouched = {
    "cat-red-tabby": "9d6f3e1b0d82f23500779c570943dc2081c6caad7295da7d3fe19c1c50742b59",
    "cat-blue-siamese": "44ceea493949fa7ea92bf40c7bc05e64c4b78e3ca0bb4c08b41fa7d788ee17b7",
    "cat-yellow-longhair": "36f03503fcbcb918870463222f50d6b17b3c880281ce61f3a15c2cec6963ed3e",
    "cat-green-shorthair": "96910d69ad0bfe424c410e0b9df6e137222d858a28322a5276add6228e9186e5",
    "cat-wild-alley": "3fa010b59c3b5dccbe0eb54453e8d595736cbafa391a9f08effd9d052738479c",
    "cat-red-tabby-sitting": "3ea8e01d78cb058223c74f225e89512efc44f74f638c99133d7720675e8655b6",
    "cat-green-shorthair-sit": "a5791a945bac21cfe55e7e4cdbcd5cd3233c11997cd0f449972a12768cca93f8",
    "cat-conductor": "3b0bdbe1a0af9377bfde62ebf2b633e694881dc81438f2814e717c4c71ab9e7d",
    "prop-depot-shed": "68994c2316e7c0b23252569bfc06cbc1155c29dd41798c8effdbbaba638844b1",
    "prop-toy-engine": "f622b390cdf48fccfb382895bef2988df191b523b614e01f03dbd162e052eeaf",
    "prop-station-kiosk": "25053fb73009bf004aeeebab4a861bb664c91935b59c059f21d2fc8c9b6f52cf",
    "prop-trees": "e34f39de9a0db8f977370d7f0808f44a28b9641a458ada4957f552c62271c0dd",
    "prop-desk-clutter": "d0403b93dc3db30ec3f7e0b825ba7b48f4af7b79094c6b262c7bfa2fb268ec4d",
}
expected_untouched_sidecars = {
    "cat-red-tabby": "9ebb3638031225ab8ade57cf794cfbb69b3ee98c3ff82e500aaf0d1f8738f4db",
    "cat-blue-siamese": "743de6f299f0c70f39dc92b7a7eda5ed6e86bf203ec78eefd4b01b20f9293f29",
    "cat-yellow-longhair": "f11a40229f24436206b06d4eee04246ef72c0f10ff3b7c88034d99757be2a4ec",
    "cat-green-shorthair": "9bcd978598e942d139e573ea9cdd3afab7dd86439f0aa56524accc0d9c3b3333",
    "cat-wild-alley": "92f095b97e5c4f03116ac087c6852ebcaeabff611b67051faf2b5f2a96f7260b",
    "cat-red-tabby-sitting": "f40f32794ef55f2f2e797ea870c63fedd6c2959bd0b0facc7fd50f0f1d21d898",
    "cat-green-shorthair-sit": "360ea5e28ca3e09b51fc45c8360ebe04e5b0a6fd38c532f636252bead68439fb",
    "cat-conductor": "83b5329451479e54719cd06a83445ab74f0bc58ef4dc4749b5b6e3cc50473e6b",
    "prop-depot-shed": "0e6c7f6a9065e12b0f3da93605914672947a2a662175ed788470c81f5d736ae2",
    "prop-toy-engine": "2f1bd6850cbb836d8c569791ffaa6939c5d5f58a42487381c3954f3fb03aec1f",
    "prop-station-kiosk": "416098fc269903c81ffbcf40e6f469821bfbc6c045b3ce2018e76fd2d30e9dc3",
    "prop-trees": "96b29000ef1e8f03d0982ffbaa1ec3d5a476cfcff8241a1f35b3a29041495b34",
    "prop-desk-clutter": "e75ba87683bd0f468871608a0c079adf89e46eca86fecda248997b204da713b4",
}
metrics = json.loads(metrics_path.read_text(encoding="utf-8"))
tracked = {asset["id"]: asset for asset in metrics["assets"]}
assert {
    key: tracked[key]["derivative_sha256"] for key in expected_untouched
} == expected_untouched
assert {
    key: tracked[key]["derivative_sidecar_sha256"]
    for key in expected_untouched_sidecars
} == expected_untouched_sidecars

artifact_root_text = os.environ.get("GLB_CURATION_ARTIFACT_ROOT")
if artifact_root_text:
    artifact_root = Path(artifact_root_text)
    for identifier, expected_sha in expected_untouched.items():
        asset = tracked[identifier]
        path = artifact_root / "decimated" / asset["derivative_filename"]
        sidecar_path = artifact_root / "decimated" / f"{asset['derivative_filename']}.json"
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        sidecar_digest = hashlib.sha256(sidecar_path.read_bytes()).hexdigest()
        assert digest == expected_sha, (identifier, digest, expected_sha)
        assert sidecar_digest == expected_untouched_sidecars[identifier], (
            identifier,
            sidecar_digest,
            expected_untouched_sidecars[identifier],
        )
    expected_curated_custody = {
        "cat-blue-siamese-loaf.glb": "257e59ebac613e3260bfd1161b228ec2be4aa7024969b4b1a3fec2366ffe0097",
        "cat-blue-siamese-loaf.glb.json": "93fd18c00ec6a1b369bed7849a0bfdb4c00cba5dfe6b16358995998a86bb1f66",
        "decimated/cat-blue-siamese-loaf.glb": "9a7b2ef923f923a78466f18d8bf0cfb82140aebbd30ba3e7cddd3f814fd2953c",
        "decimated/cat-blue-siamese-loaf.glb.json": "2265679b91ff5feb5ab5ef7a277af6c3abfe1fda43e4dff2eccb5cceacc684e4",
        "cat-yellow-longhair-wave.glb": "bf4626c2a41214444a483bde1920c7fd95a06069feca202df860861edb540d64",
        "cat-yellow-longhair-wave.glb.json": "0bedeeb207fcb02277c7b0b1d0bcf8ec8118d4b0cf2e20abbaa3d85b1a64260f",
        "decimated/cat-yellow-longhair-wave.glb": "a3c4a363b06064ecc5dc03509c36ddd5ab91200a41314a3c674cd91ef4386696",
        "decimated/cat-yellow-longhair-wave.glb.json": "9c7bd939fc493caa44d0250531e2137c8c848d5b9bbfc62de320e2dbab16317e",
        "curation-backups/GLB-CURATION-2026-08-17-16e20e3/loaf-source/cat-blue-siamese-loaf.glb": "e3015351ec9bda2aebeafcc0ff23f5aa35512af4234c168d79cac750118070e3",
        "curation-backups/GLB-CURATION-2026-08-17-16e20e3/loaf-source/cat-blue-siamese-loaf.glb.json": "ce8ea067634f88ee9fc967ea5a0dbc58df890477d3e1dc1905cc3f77a92dcec4",
        "curation-backups/GLB-CURATION-2026-08-17-16e20e3/wave-source/cat-yellow-longhair-wave.glb": "8d7190fd24f552f874bf1d733f2870c44a24c27d6b50cfe1e32095f625fcc57c",
        "curation-backups/GLB-CURATION-2026-08-17-16e20e3/wave-source/cat-yellow-longhair-wave.glb.json": "e65414b151fa1dd868e9086c0e274ac61743aef8f8f26bc7bcaa6f49f99c8936",
        "curation-backups/GLB-CURATION-2026-08-17-16e20e3/derivatives-before/decimated/cat-blue-siamese-loaf.glb": "cc1ff113257d48994a94cfdff52554236034e3e6455d402de195461b8c8fc236",
        "curation-backups/GLB-CURATION-2026-08-17-16e20e3/derivatives-before/decimated/cat-blue-siamese-loaf.glb.json": "8209d8dcac1e70f31a3070801eeacd3eb3bad19654cb0135ae2c9d7416be4a59",
        "curation-backups/GLB-CURATION-2026-08-17-16e20e3/derivatives-before/decimated/cat-yellow-longhair-wave.glb": "4e20de09cee1dcfa383bb708608f03b5f8c1aa78ca4a510a3064f435f5f87a27",
        "curation-backups/GLB-CURATION-2026-08-17-16e20e3/derivatives-before/decimated/cat-yellow-longhair-wave.glb.json": "a084bae339440e74e3b22b0f578fe1a62fe80c15474f4ffd62f717ad6cb9cfb1",
        "curation-backups/GLB-CURATION-WAVE-CORRECTION-2026-08-18-841d4a3/cat-yellow-longhair-wave.glb": "f91ccb7ff9b527ecef168d4285488ff647023fb70875f5403c31db8e2349d99d",
        "curation-backups/GLB-CURATION-WAVE-CORRECTION-2026-08-18-841d4a3/cat-yellow-longhair-wave.glb.json": "bb787a4073833edfd54af3e401cfa00e73b5279592ba2d146b015d3f1ffe90e4",
        "curation-backups/GLB-CURATION-WAVE-CORRECTION-2026-08-18-841d4a3/derivatives-before/decimated/cat-yellow-longhair-wave.glb": "2eee06883d024631263485b48da067dd8042f66ef81fc669016731fa5fdaa1ef",
        "curation-backups/GLB-CURATION-WAVE-CORRECTION-2026-08-18-841d4a3/derivatives-before/decimated/cat-yellow-longhair-wave.glb.json": "b961427de158aba8377e3114cc301d4d144ee38e378df984d8140a31cb3d633e",
    }
    for relative, expected_sha in expected_curated_custody.items():
        path = artifact_root / relative
        status = path.lstat()
        assert stat.S_ISREG(status.st_mode) and status.st_nlink == 1, relative
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        assert digest == expected_sha, (relative, digest, expected_sha)

# The repository cleanup on 2026-08-27 deliberately retired the historical
# screenshot pack. This suite now claims only code/manifests/recorded metrics
# by default; explicit local artifact roots add the stronger byte checks above.
print(
    "glb-curation unit: pass rules=boundary-pinned transactions=failure-matrix "
    f"untouched={len(expected_untouched)} manifests=current"
)
PY

help_output=$(PYTHONDONTWRITEBYTECODE=1 python3 "$orchestrator" --help)
for flag in --input-dir --backup-dir --blender --asset-id; do
  if [[ "$help_output" != *"$flag"* ]]; then
    printf 'glb-curation test: help is missing %s\n' "$flag" >&2
    exit 1
  fi
done

# Strong local leg: a curated-state check must reject both old derivatives and
# accept both regenerated derivatives. CI omits ignored paid assets and skips.
if [[ -n ${GLB_CURATION_BASELINE_ROOT:-} && -n ${GLB_CURATION_ARTIFACT_ROOT:-} ]]; then
  blender_bin=${GLB_CURATION_BLENDER:-/opt/homebrew/bin/blender}
  for asset_id in cat-blue-siamese-loaf cat-yellow-longhair-wave; do
    filename="$asset_id.glb"
    if "$blender_bin" --background --factory-startup --python "$driver" -- \
      --operation verify-curated --asset-id "$asset_id" \
      --source "$GLB_CURATION_BASELINE_ROOT/decimated/$filename"; then
      printf 'glb-curation test: old derivative passed curated check: %s\n' "$asset_id" >&2
      exit 1
    fi
    "$blender_bin" --background --factory-startup --python "$driver" -- \
      --operation verify-curated --asset-id "$asset_id" \
      --source "$GLB_CURATION_ARTIFACT_ROOT/decimated/$filename"
  done
  for wave_path in \
    "$GLB_CURATION_ARTIFACT_ROOT/cat-yellow-longhair-wave.glb" \
    "$GLB_CURATION_ARTIFACT_ROOT/decimated/cat-yellow-longhair-wave.glb"; do
    wave_output=$("$blender_bin" --background --factory-startup --python "$driver" -- \
      --operation verify-curated --asset-id cat-yellow-longhair-wave \
      --source "$wave_path")
    printf '%s\n' "$wave_output"
    wave_measurement=$(PYTHONDONTWRITEBYTECODE=1 python3 -c \
      'import json, sys; report=json.loads(next(line.removeprefix("blender-curate: ") for line in sys.stdin if line.startswith("blender-curate: {"))); print("{}\t{}".format(report["components"], report["component_weld_distance"]))' \
      <<<"$wave_output")
    IFS=$'\t' read -r wave_components wave_weld_distance <<<"$wave_measurement"
    if [[ "$wave_components" != 1 ]]; then
      printf 'glb-curation test: wave must contain exactly one connected component: %s has %s\n' \
        "$wave_path" "$wave_components" >&2
      exit 1
    fi
    if [[ "$wave_weld_distance" != 1e-05 ]]; then
      printf 'glb-curation test: wave component weld distance drifted: %s\n' \
        "$wave_weld_distance" >&2
      exit 1
    fi
  done
  printf 'glb-curation local geometry: pass baseline=RED curated=GREEN\n'
else
  printf 'glb-curation local geometry: skipped (ignored artifacts not explicit)\n'
fi

# Strongest local leg: run the production orchestrator and Blender curate
# operation from recoverable original pairs in an isolated scratch root. The
# ignored originals/current artifacts must be made explicit by the caller.
if [[ -n ${GLB_CURATION_SOURCE_BASELINE_ROOT:-} && -n ${GLB_CURATION_ARTIFACT_ROOT:-} ]]; then
  blender_bin=${GLB_CURATION_BLENDER:-/opt/homebrew/bin/blender}
  scratch=$(mktemp -d "/private/tmp/catmetro-curation-production-XXXXXX")
  if [[ -z "$scratch" || ! -d "$scratch" || -L "$scratch" ]]; then
    printf 'glb-curation test: could not create safe production scratch root\n' >&2
    exit 1
  fi
  cleanup_scratch() {
    if [[ -n ${scratch:-} && -d "$scratch" && ! -L "$scratch" && "$scratch" == /private/tmp/catmetro-curation-production-* ]]; then
      rm -rf -- "${scratch:?}"
    fi
  }
  trap cleanup_scratch EXIT
  for specification in \
    'cat-blue-siamese-loaf loaf-source' \
    'cat-yellow-longhair-wave wave-source'; do
    read -r asset_id backup_member <<<"$specification"
    filename="$asset_id.glb"
    input_dir="$scratch/$asset_id/input"
    backup_parent="$input_dir/curation-backups"
    mkdir -p -- "$input_dir" "$backup_parent"
    cp -- "$GLB_CURATION_SOURCE_BASELINE_ROOT/$backup_member/$filename" "$input_dir/$filename"
    cp -- "$GLB_CURATION_SOURCE_BASELINE_ROOT/$backup_member/$filename.json" "$input_dir/$filename.json"
    PYTHONDONTWRITEBYTECODE=1 python3 "$orchestrator" \
      --input-dir "$input_dir" \
      --backup-dir "$backup_parent/original" \
      --blender "$blender_bin" \
      --asset-id "$asset_id"
    cmp -- "$input_dir/$filename" "$GLB_CURATION_ARTIFACT_ROOT/$filename"
    cmp -- "$input_dir/$filename.json" "$GLB_CURATION_ARTIFACT_ROOT/$filename.json"
    cmp -- "$backup_parent/original/$filename" \
      "$GLB_CURATION_SOURCE_BASELINE_ROOT/$backup_member/$filename"
    cmp -- "$backup_parent/original/$filename.json" \
      "$GLB_CURATION_SOURCE_BASELINE_ROOT/$backup_member/$filename.json"
    if find "$input_dir" -maxdepth 1 \
      \( -name '.glb-curation-stage-*' -o -name '.glb-curation-*.transaction.json' \) \
      -print -quit | grep -q .; then
      printf 'glb-curation test: production run left transaction residue: %s\n' "$asset_id" >&2
      exit 1
    fi
  done
  cleanup_scratch
  trap - EXIT
  printf 'glb-curation production: pass curated=2 byte-identical-to-retained\n'
else
  printf 'glb-curation production: skipped (ignored source backups not explicit)\n'
fi
