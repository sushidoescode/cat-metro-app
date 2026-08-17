#!/usr/bin/env bash
# Behavioral contract for the offline GLB decimation orchestrator. The fake
# process boundary is validated independently before any production entry point
# runs, so a later RED is attributable to orchestrator behavior.
set -euo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)
decimate_script=${DECIMATE_SCRIPT:-$repo/scripts/decimate-assets.py}
fake_blender="$repo/tests/assets/fake_blender.py"
expected_driver=$(cd "$(dirname "$decimate_script")" && pwd -P)/blender_decimate.py
review_section=${GLB_DECIMATION_REVIEW_SECTION:-all}
case "$review_section" in
  all|A|B|C|D|E|F|G|H|I|J|K|L|M) ;;
  *) die_message="GLB_DECIMATION_REVIEW_SECTION must be all or A through M"
     printf 'glb-decimation pipeline test: %s\n' "$die_message" >&2
     exit 2 ;;
esac
tmp=$(mktemp -d)
marker_name="$(basename "$tmp")-argv-injection-marker"
marker="$repo/$marker_name"
marker_cleanup_armed=0

cleanup() {
  rm -rf -- "$tmp"
  if [ "$marker_cleanup_armed" -eq 1 ]; then
    rm -f -- "$marker"
  fi
}
trap cleanup EXIT

die() {
  printf 'glb-decimation pipeline test: %s\n' "$1" >&2
  exit 1
}

# Regression G: glTF attribute/normal seams commonly arrive as coincident split
# vertices. If they remain disconnected, collapse decimation can move each side
# independently and open visible cracks. Keep the import boundary seam-safe with
# explicit literal arguments; defaults or computed values make custody ambiguous.
if [ "$review_section" = all ] || [ "$review_section" = G ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - "$expected_driver" <<'PY'
from __future__ import annotations

import ast
import importlib.util
import sys
import tempfile
import types
from pathlib import Path


class ImportConfigurationError(AssertionError):
    pass


def dotted_name(node: ast.expr) -> str | None:
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        parent = dotted_name(node.value)
        if parent is not None:
            return f"{parent}.{node.attr}"
    return None


def require_seam_safe_import(source: str, label: str) -> None:
    tree = ast.parse(source, filename=label)
    import_functions = [
        node
        for node in tree.body
        if isinstance(node, ast.FunctionDef) and node.name == "_import_source"
    ]
    if len(import_functions) != 1:
        raise ImportConfigurationError(
            f"{label}: expected exactly one top-level _import_source FunctionDef; "
            f"found {len(import_functions)}"
        )

    calls = [
        node
        for node in ast.walk(import_functions[0])
        if isinstance(node, ast.Call)
        and dotted_name(node.func) == "bpy.ops.import_scene.gltf"
    ]
    if len(calls) != 1:
        raise ImportConfigurationError(
            f"{label}: expected exactly one direct bpy.ops.import_scene.gltf call "
            "inside _import_source; "
            f"found {len(calls)}"
        )

    call = calls[0]
    if any(keyword.arg is None for keyword in call.keywords):
        raise ImportConfigurationError(
            f"{label}: glTF import must not use dynamic **kwargs"
        )

    errors: list[str] = []
    for name, expected in (
        ("merge_vertices", True),
        ("import_shading", "SMOOTH"),
    ):
        matches = [keyword.value for keyword in call.keywords if keyword.arg == name]
        if len(matches) != 1:
            errors.append(f"{name} must appear exactly once")
            continue
        value = matches[0]
        if not isinstance(value, ast.Constant) or type(value.value) is not type(expected):
            errors.append(f"{name} must be the literal {expected!r}")
            continue
        if value.value != expected:
            errors.append(f"{name} must be the literal {expected!r}")

    if errors:
        raise ImportConfigurationError(f"{label}: " + "; ".join(errors))


def require_seam_safe_behavior(driver: Path, label: str) -> None:
    executed_calls: list[tuple[tuple[object, ...], dict[str, object]]] = []
    fake_bpy = types.ModuleType("bpy")

    def import_spy(*args: object, **kwargs: object) -> set[str]:
        executed_calls.append((args, dict(kwargs)))
        return {"FINISHED"}

    fake_bpy.ops = types.SimpleNamespace(
        import_scene=types.SimpleNamespace(gltf=import_spy)
    )
    prior_bpy = sys.modules.get("bpy")
    module_name = "_catmetro_blender_decimate_behavior_probe"
    prior_module = sys.modules.get(module_name)

    try:
        sys.modules["bpy"] = fake_bpy
        spec = importlib.util.spec_from_file_location(module_name, driver)
        if spec is None or spec.loader is None:
            raise ImportConfigurationError(
                f"{label}: could not create an import spec for {driver}"
            )
        module = importlib.util.module_from_spec(spec)
        sys.modules[module_name] = module
        spec.loader.exec_module(module)
        import_source = getattr(module, "_import_source", None)
        if not callable(import_source):
            raise ImportConfigurationError(
                f"{label}: imported module has no callable _import_source"
            )
        import_source(Path("behavior-probe-does-not-need-a-real-file.glb"))
    finally:
        if prior_bpy is None:
            sys.modules.pop("bpy", None)
        else:
            sys.modules["bpy"] = prior_bpy
        if prior_module is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = prior_module

    if len(executed_calls) != 1:
        raise ImportConfigurationError(
            f"{label}: expected _import_source to execute exactly one glTF import; "
            f"observed {len(executed_calls)}"
        )

    _, kwargs = executed_calls[0]
    errors: list[str] = []
    merge_vertices = kwargs.get("merge_vertices")
    if type(merge_vertices) is not bool or merge_vertices is not True:
        errors.append("executed merge_vertices must be True")
    import_shading = kwargs.get("import_shading")
    if type(import_shading) is not str or import_shading != "SMOOTH":
        errors.append("executed import_shading must be 'SMOOTH'")
    if errors:
        raise ImportConfigurationError(f"{label}: " + "; ".join(errors))


def require_fixture_behavior(source: str, label: str) -> None:
    with tempfile.TemporaryDirectory(prefix="catmetro-import-probe-") as directory:
        driver = Path(directory) / "blender_decimate.py"
        driver.write_text(source, encoding="utf-8")
        require_seam_safe_behavior(driver, label)


def assert_rejected(source: str, expected_diagnostic: str) -> None:
    try:
        require_seam_safe_import(source, "mutation control")
    except ImportConfigurationError as exc:
        if expected_diagnostic not in str(exc):
            raise AssertionError(
                f"mutation control returned the wrong diagnostic: {exc}"
            ) from exc
    else:
        raise AssertionError(
            f"mutation control unexpectedly accepted: {expected_diagnostic}"
        )


def assert_behavior_rejected(source: str, *expected_diagnostics: str) -> None:
    try:
        require_fixture_behavior(source, "behavior mutation control")
    except ImportConfigurationError as exc:
        for expected_diagnostic in expected_diagnostics:
            if expected_diagnostic not in str(exc):
                raise AssertionError(
                    f"behavior mutation returned the wrong diagnostic: {exc}"
                ) from exc
    else:
        raise AssertionError(
            "behavior mutation unexpectedly accepted: "
            + ", ".join(expected_diagnostics)
        )


compliant_fixture = """
import bpy

def _import_source(source):
    result = bpy.ops.import_scene.gltf(
        filepath="fixture.glb",
        merge_vertices=True,
        import_shading="SMOOTH",
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB import returned {result}")
"""
require_seam_safe_import(compliant_fixture, "compliant control")
require_fixture_behavior(compliant_fixture, "compliant behavior control")

# Prove each required flag independently detects the regression observed in the
# rendered assets, then prove target scope, call cardinality, and literalness.
assert_rejected(
    compliant_fixture.replace("merge_vertices=True", "merge_vertices=False"),
    "merge_vertices must be the literal True",
)
assert_rejected(
    compliant_fixture.replace('import_shading="SMOOTH"', 'import_shading="NORMALS"'),
    "import_shading must be the literal 'SMOOTH'",
)
assert_rejected(
    compliant_fixture.replace(
        "    result = bpy.ops.import_scene.gltf(",
        "    bpy.ops.import_scene.gltf(filepath='duplicate.glb')\n"
        "    result = bpy.ops.import_scene.gltf(",
    ),
    "expected exactly one direct bpy.ops.import_scene.gltf call "
    "inside _import_source; found 2",
)
assert_rejected(
    compliant_fixture.replace("merge_vertices=True", "merge_vertices=SEAM_SAFE"),
    "merge_vertices must be the literal True",
)
assert_rejected(
    compliant_fixture.replace('import_shading="SMOOTH"', "import_shading=SHADING_MODE"),
    "import_shading must be the literal 'SMOOTH'",
)

# An unrelated direct call must not change _import_source's cardinality.
unrelated_call_fixture = compliant_fixture + """

def unrelated_import():
    bpy.ops.import_scene.gltf(filepath="unrelated.glb")
"""
require_seam_safe_import(unrelated_call_fixture, "unrelated-call scope control")
require_fixture_behavior(unrelated_call_fixture, "unrelated-call behavior control")

# This is the confirmed bypass for the old global AST scan: its unreachable
# direct call is compliant, while the import that actually executes goes through
# an alias with unsafe values. The scoped AST check alone accepts the fixture;
# the fake-bpy behavior spy must reject both executed arguments.
unsafe_alias_fixture = """
import bpy

def _import_source(source):
    importer = bpy.ops.import_scene.gltf
    if False:
        bpy.ops.import_scene.gltf(
            filepath="unreachable.glb",
            merge_vertices=True,
            import_shading="SMOOTH",
        )
    result = importer(
        filepath=str(source),
        merge_vertices=False,
        import_shading="NORMALS",
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB import returned {result}")
"""
require_seam_safe_import(unsafe_alias_fixture, "unsafe-alias AST bypass control")
assert_behavior_rejected(
    unsafe_alias_fixture,
    "executed merge_vertices must be True",
    "executed import_shading must be 'SMOOTH'",
)

driver = Path(sys.argv[1])
driver_errors: list[str] = []
for check in (
    lambda: require_seam_safe_import(
        driver.read_text(encoding="utf-8"), f"{driver} AST"
    ),
    lambda: require_seam_safe_behavior(driver, f"{driver} behavior"),
):
    try:
        check()
    except ImportConfigurationError as exc:
        driver_errors.append(str(exc))

if driver_errors:
    print(
        "glb-decimation pipeline test: " + " | ".join(driver_errors),
        file=sys.stderr,
    )
    raise SystemExit(1) from None
PY
fi

if [ "$review_section" = G ]; then
  printf 'glb-decimation review G: pass\n'
  exit 0
fi

# Regression H: seam-safe welding changes Blender's in-memory topology for two
# real assets, so the outer GLB count cannot also be the collapse denominator.
# Audit the unmerged/smoothed topology exactly, then independently measure the
# welded/smoothed topology used for decimation. The fake-bpy probes below pin
# executed behavior and call order; scoped AST checks pin literal custody flags
# without allowing an unreachable direct call to bless an unsafe alias call.
if [ "$review_section" = all ] || [ "$review_section" = H ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - "$expected_driver" <<'PY'
from __future__ import annotations

import ast
import importlib.util
import inspect
import sys
import tempfile
import types
from argparse import Namespace
from pathlib import Path
from unittest import mock


sys.dont_write_bytecode = True


class AuditContractError(AssertionError):
    pass


AUDIT_IMPORT_LITERALS = {
    "loglevel": 1,
    "import_pack_images": True,
    "merge_vertices": False,
    "import_shading": "SMOOTH",
    "import_webp_texture": False,
    "import_unused_materials": False,
    "import_select_created_objects": True,
    "import_scene_extras": False,
    "import_scene_as_collection": True,
    "import_merge_material_slots": True,
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AuditContractError(message)


def dotted_name(node: ast.expr) -> str | None:
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        parent = dotted_name(node.value)
        if parent is not None:
            return f"{parent}.{node.attr}"
    return None


def top_level_function(tree: ast.Module, name: str, label: str) -> ast.FunctionDef:
    matches = [
        node
        for node in tree.body
        if isinstance(node, ast.FunctionDef) and node.name == name
    ]
    require(
        len(matches) == 1,
        f"{label}: expected exactly one top-level {name} FunctionDef; "
        f"found {len(matches)}",
    )
    return matches[0]


def require_audit_import_ast(source: str, label: str) -> int:
    tree = ast.parse(source, filename=label)
    audit_function = top_level_function(tree, "_audit_import_source", label)
    calls = [
        node
        for node in ast.walk(audit_function)
        if isinstance(node, ast.Call)
        and dotted_name(node.func) == "bpy.ops.import_scene.gltf"
    ]
    require(
        len(calls) == 1,
        f"{label}: _audit_import_source must contain exactly one direct "
        f"bpy.ops.import_scene.gltf call; found {len(calls)}",
    )
    call = calls[0]
    require(not call.args, f"{label}: audit import must not use positional arguments")
    require(
        all(keyword.arg is not None for keyword in call.keywords),
        f"{label}: audit import must not use dynamic **kwargs",
    )
    keyword_nodes = {}
    for keyword in call.keywords:
        require(
            keyword.arg not in keyword_nodes,
            f"{label}: duplicate audit import keyword {keyword.arg}",
        )
        keyword_nodes[keyword.arg] = keyword.value
    expected_names = {"filepath", *AUDIT_IMPORT_LITERALS}
    require(
        set(keyword_nodes) == expected_names,
        f"{label}: audit import keyword set must be exactly "
        f"{sorted(expected_names)}; found {sorted(keyword_nodes)}",
    )
    filepath = keyword_nodes["filepath"]
    require(
        isinstance(filepath, ast.Call)
        and isinstance(filepath.func, ast.Name)
        and filepath.func.id == "str"
        and len(filepath.args) == 1
        and isinstance(filepath.args[0], ast.Name)
        and filepath.args[0].id == "source"
        and not filepath.keywords,
        f"{label}: audit filepath must be exactly str(source)",
    )
    alternate_same_line_calls = [
        node
        for node in ast.walk(audit_function)
        if isinstance(node, ast.Call)
        and node is not call
        and node is not filepath
        and node.lineno == call.lineno
    ]
    require(
        not alternate_same_line_calls,
        f"{label}: the direct audit import line must contain no alternate "
        "call expression",
    )
    for name, expected in AUDIT_IMPORT_LITERALS.items():
        value = keyword_nodes[name]
        require(
            isinstance(value, ast.Constant)
            and type(value.value) is type(expected)
            and value.value == expected,
            f"{label}: audit import keyword {name} must be literal {expected!r}",
        )
    return call.lineno


class ImportRecorder:
    def __init__(self) -> None:
        self.calls = []
        self.result = {"FINISHED"}

    def __call__(self, *args, **kwargs):
        frame = inspect.currentframe()
        caller = frame.f_back if frame is not None else None
        caller_site = (
            caller.f_code.co_name,
            caller.f_lineno,
        ) if caller is not None else (None, None)
        self.calls.append((args, dict(kwargs), caller_site))
        del frame
        return set(self.result)


module_serial = 0


def load_driver(path: Path, label: str):
    global module_serial
    module_serial += 1
    recorder = ImportRecorder()
    fake_bpy = types.ModuleType("bpy")
    fake_bpy.ops = types.SimpleNamespace(
        import_scene=types.SimpleNamespace(gltf=recorder)
    )
    fake_bpy.app = types.SimpleNamespace(
        version=(5, 1, 2), build_hash=b"ec6e62d40fa9"
    )
    fake_bpy.data = types.SimpleNamespace(objects=[], actions=[])
    fake_bpy.context = types.SimpleNamespace(
        view_layer=types.SimpleNamespace(objects=types.SimpleNamespace(active=None))
    )
    module_name = f"_catmetro_audit_driver_{module_serial}"
    prior_bpy = sys.modules.get("bpy")
    try:
        sys.modules["bpy"] = fake_bpy
        spec = importlib.util.spec_from_file_location(module_name, path)
        require(spec is not None and spec.loader is not None, f"{label}: import spec failed")
        module = importlib.util.module_from_spec(spec)
        sys.modules[module_name] = module
        spec.loader.exec_module(module)
    except Exception:
        sys.modules.pop(module_name, None)
        raise
    finally:
        if prior_bpy is None:
            sys.modules.pop("bpy", None)
        else:
            sys.modules["bpy"] = prior_bpy
    return module, recorder, module_name


def require_signatures(module, label: str) -> None:
    expected = {
        "_audit_import_source": ("source",),
        "_audit_source": ("source", "inspected_triangles"),
        "_validated_decimation_ratio": (
            "inspected", "audited", "effective", "target"
        ),
        "_decimate": ("args", "mesh_objects", "audited_source_triangles"),
        "main": ("argv",),
    }
    for name, parameter_names in expected.items():
        function = getattr(module, name, None)
        require(callable(function), f"{label}: missing callable {name}")
        parameters = list(inspect.signature(function).parameters.values())
        require(
            tuple(parameter.name for parameter in parameters) == parameter_names,
            f"{label}: {name} parameter names/order must be {parameter_names}",
        )
        require(
            all(
                parameter.kind is inspect.Parameter.POSITIONAL_OR_KEYWORD
                and parameter.default is inspect.Parameter.empty
                for parameter in parameters
            ),
            f"{label}: {name} parameters must be required positional-or-keyword values",
        )


def expect_rejection(callback, message: str) -> None:
    try:
        callback()
    except (RuntimeError, ValueError):
        return
    raise AuditContractError(message)


def require_audit_import_behavior(
    module, recorder: ImportRecorder, direct_call_line: int, label: str
) -> None:
    source = Path("audit behavior source.glb")
    recorder.calls.clear()
    recorder.result = {"FINISHED"}
    module._audit_import_source(source)
    require(
        len(recorder.calls) == 1,
        f"{label}: _audit_import_source must execute exactly one glTF import; "
        f"observed {len(recorder.calls)}",
    )
    args, kwargs, caller_site = recorder.calls[0]
    require(
        caller_site == ("_audit_import_source", direct_call_line),
        f"{label}: _audit_import_source must execute its direct AST import call "
        f"at line {direct_call_line}; observed caller {caller_site}",
    )
    require(not args, f"{label}: executed audit import used positional arguments")
    expected = {"filepath": str(source), **AUDIT_IMPORT_LITERALS}
    require(
        kwargs == expected,
        f"{label}: executed audit import kwargs differ: {kwargs!r}",
    )
    recorder.calls.clear()
    recorder.result = {"CANCELLED"}
    expect_rejection(
        lambda: module._audit_import_source(source),
        f"{label}: _audit_import_source accepted a cancelled Blender operator",
    )
    require(
        len(recorder.calls) == 1,
        f"{label}: cancelled audit import was not executed exactly once",
    )
    recorder.calls.clear()
    recorder.result = {"FINISHED", "RUNNING_MODAL"}
    expect_rejection(
        lambda: module._audit_import_source(source),
        f"{label}: _audit_import_source accepted a non-exact FINISHED result",
    )
    require(
        len(recorder.calls) == 1,
        f"{label}: non-exact FINISHED audit import was not executed exactly once",
    )
    recorder.result = {"FINISHED"}


class ModifierCollection:
    def __init__(self, events) -> None:
        self.events = events
        self.created = []

    def new(self, name, modifier_type):
        modifier = types.SimpleNamespace(name=name, type=modifier_type)
        self.created.append(modifier)
        self.events.append(("modifier", modifier_type, modifier))
        return modifier


def require_audit_source_behavior(
    module, recorder: ImportRecorder, label: str
) -> None:
    events = []
    recorder.calls.clear()
    modifiers = ModifierCollection(events)
    mesh_object = types.SimpleNamespace(modifiers=modifiers)
    source = Path("audited-source.glb")

    def audit_import(candidate):
        events.append(("audit-import", candidate))

    def static_mesh_objects():
        events.append(("static-validate",))
        return [mesh_object]

    def apply_modifier(obj, modifier):
        events.append(("apply", obj, modifier))

    def triangle_count(objects):
        events.append(("count", tuple(objects)))
        return 12

    with (
        mock.patch.object(module, "_audit_import_source", new=audit_import),
        mock.patch.object(module, "_static_mesh_objects", new=static_mesh_objects),
        mock.patch.object(module, "apply_modifier", new=apply_modifier),
        mock.patch.object(module, "_triangle_count", new=triangle_count),
    ):
        audited = module._audit_source(source, 12)
    require(audited == 12, f"{label}: exact audit did not return counted triangles")
    require(
        [event[0] for event in events]
        == ["audit-import", "static-validate", "modifier", "apply", "count"],
        f"{label}: audit must import, statically validate, triangulate, apply, then count; "
        f"observed {[event[0] for event in events]}",
    )
    require(
        events[0][1] == source
        and events[2][1] == "TRIANGULATE"
        and events[3][1] is mesh_object
        and events[3][2] is events[2][2]
        and events[4][1] == (mesh_object,),
        f"{label}: audit triangulation/count did not use the validated mesh",
    )
    require(
        not recorder.calls,
        f"{label}: _audit_source executed a glTF importer outside "
        "_audit_import_source",
    )

    recorder.calls.clear()
    mismatch_events = []
    mismatch_modifiers = ModifierCollection(mismatch_events)
    mismatch_mesh = types.SimpleNamespace(modifiers=mismatch_modifiers)
    with (
        mock.patch.object(module, "_audit_import_source", new=lambda _source: None),
        mock.patch.object(module, "_static_mesh_objects", new=lambda: [mismatch_mesh]),
        mock.patch.object(module, "apply_modifier", new=lambda _obj, _modifier: None),
        mock.patch.object(module, "_triangle_count", new=lambda _objects: 10),
    ):
        expect_rejection(
            lambda: module._audit_source(source, 12),
            f"{label}: exact audit accepted inspected=12/audited=10",
        )
    require(
        not recorder.calls,
        f"{label}: _audit_source mismatch path executed a glTF importer outside "
        "_audit_import_source",
    )


def require_ratio_behavior(module, label: str) -> None:
    for values, expected in (
        ((12, 12, 10, 5), 0.5),
        ((12, 12, 8, 2), 0.25),
        ((20, 20, 16, 4), 0.25),
    ):
        ratio = module._validated_decimation_ratio(*values)
        require(
            type(ratio) is float and ratio == expected,
            f"{label}: effective denominator must produce {expected} for {values}; "
            f"found {ratio!r}",
        )
    for values, guard_name in (
        ((12, 10, 10, 5), "audited/inspected mismatch"),
        ((12, 12, 13, 5), "effective-over-source"),
        ((12, 12, 5, 5), "effective-at-target"),
        ((12, 12, 4, 5), "effective-below-target"),
    ):
        expect_rejection(
            lambda values=values: module._validated_decimation_ratio(*values),
            f"{label}: {guard_name} guard did not reject {values}",
        )


def exercise_decimate(module, audited_value, ratio_override=None):
    events = []
    modifiers = ModifierCollection(events)
    mesh_object = types.SimpleNamespace(modifiers=modifiers)
    counts = iter((10, 5))
    ratio_calls = []
    applied_decimate_ratios = []
    helper_was_called_before_apply = []

    def ratio_spy(inspected, audited, effective, target):
        ratio_calls.append((inspected, audited, effective, target))
        return ratio_override

    def apply_spy(_obj, modifier):
        if modifier.type == "DECIMATE":
            applied_decimate_ratios.append(getattr(modifier, "ratio", None))
            if ratio_override is not None:
                helper_was_called_before_apply.append(bool(ratio_calls))

    patches = [
        mock.patch.object(module, "apply_modifier", new=apply_spy),
        mock.patch.object(module, "_triangle_count", new=lambda _objects: next(counts)),
    ]
    if ratio_override is not None:
        patches.append(
            mock.patch.object(module, "_validated_decimation_ratio", new=ratio_spy)
        )
    with patches[0], patches[1]:
        if len(patches) == 3:
            with patches[2]:
                module._decimate(
                    Namespace(
                        source_triangles=12,
                        target_triangles=5,
                        minimum_triangles=4,
                        maximum_triangles=5,
                    ),
                    [mesh_object],
                    audited_value,
                )
        else:
            module._decimate(
                Namespace(
                    source_triangles=12,
                    target_triangles=5,
                    minimum_triangles=4,
                    maximum_triangles=5,
                ),
                [mesh_object],
                audited_value,
            )
    decimate_modifiers = [
        modifier for modifier in modifiers.created if modifier.type == "DECIMATE"
    ]
    require(len(decimate_modifiers) == 1, "_decimate must create one DECIMATE modifier")
    return (
        decimate_modifiers[0].ratio,
        ratio_calls,
        applied_decimate_ratios,
        helper_was_called_before_apply,
    )


def require_decimate_behavior(module, label: str) -> None:
    ratio, _, applied_ratios, _ = exercise_decimate(module, 12)
    require(
        ratio == 0.5,
        f"{label}: _decimate used the raw/audited denominator instead of effective=10; "
        f"ratio={ratio!r}",
    )
    require(
        applied_ratios == [0.5],
        f"{label}: applied DECIMATE ratio must use effective=10 at application "
        f"time; observed {applied_ratios}",
    )
    ratio, calls, applied_ratios, helper_before_apply = exercise_decimate(
        module, 7319, ratio_override=0.375
    )
    require(
        calls == [(12, 7319, 10, 5)],
        f"{label}: _decimate must call _validated_decimation_ratio once with "
        f"(inspected,audited,effective,target); calls={calls}",
    )
    require(
        ratio == 0.375,
        f"{label}: _decimate ignored the validated helper's effective ratio",
    )
    require(
        applied_ratios == [0.375] and helper_before_apply == [True],
        f"{label}: validated ratio must be computed before and present during "
        f"DECIMATE application; ratios={applied_ratios} "
        f"helper_before_apply={helper_before_apply}",
    )


def require_main_sequence(module, recorder: ImportRecorder, label: str) -> None:
    with tempfile.TemporaryDirectory(prefix="catmetro-audit-main-") as directory:
        root = Path(directory)
        source = root / "source.glb"
        output = root / "output.glb"
        source.write_bytes(b"glTF fixture sentinel")
        args = Namespace(
            source=source,
            output=output,
            source_triangles=12,
            target_triangles=5,
            minimum_triangles=4,
            maximum_triangles=5,
        )
        audited_sentinel = object()
        mesh_sentinel = object()
        events = []
        recorder.calls.clear()

        def audit(candidate, inspected):
            events.append(("audit", candidate, inspected))
            return audited_sentinel

        def decimate(decimate_args, meshes, audited):
            require(decimate_args is args, f"{label}: main replaced parsed arguments")
            require(meshes == [mesh_sentinel], f"{label}: main replaced safe mesh list")
            require(
                audited is audited_sentinel,
                f"{label}: main did not pass _audit_source's result into _decimate",
            )
            events.append(("decimate",))

        with (
            mock.patch.object(module, "_arguments", new=lambda _argv: args),
            mock.patch.object(module, "_require_blender_pin", new=lambda: None),
            mock.patch.object(
                module,
                "_remove_factory_objects",
                new=lambda: events.append(("clear",)),
            ),
            mock.patch.object(module, "_audit_source", new=audit),
            mock.patch.object(
                module,
                "_import_source",
                new=lambda candidate: events.append(("safe-import", candidate)),
            ),
            mock.patch.object(
                module,
                "_static_mesh_objects",
                new=lambda: events.append(("static-validate",)) or [mesh_sentinel],
            ),
            mock.patch.object(module, "_decimate", new=decimate),
            mock.patch.object(
                module,
                "_export_output",
                new=lambda candidate: events.append(("export", candidate)),
            ),
        ):
            result = module.main(["blender", "--", "fixture"])
        require(result == 0, f"{label}: compliant main sequence returned {result!r}")
        require(
            [event[0] for event in events]
            == [
                "clear",
                "audit",
                "clear",
                "safe-import",
                "static-validate",
                "decimate",
                "export",
            ],
            f"{label}: main sequence must be clear→audit→clear→safe import→"
            f"static validate→decimate→export; observed {[event[0] for event in events]}",
        )
        require(
            events[1][1:] == (source, 12)
            and events[3][1] == source
            and events[6][1] == output,
            f"{label}: main sequence used the wrong source/output custody paths",
        )
        require(
            not recorder.calls,
            f"{label}: main executed a glTF importer outside the audited/safe helpers",
        )


def validate_driver(path: Path, label: str) -> None:
    source = path.read_text(encoding="utf-8")
    direct_call_line = require_audit_import_ast(source, label)
    module, recorder, module_name = load_driver(path, label)
    try:
        require_signatures(module, label)
        require_audit_import_behavior(module, recorder, direct_call_line, label)
        require_audit_source_behavior(module, recorder, label)
        require_ratio_behavior(module, label)
        require_decimate_behavior(module, label)
        require_main_sequence(module, recorder, label)
    finally:
        sys.modules.pop(module_name, None)


compliant_fixture = '''
import argparse
import sys
from pathlib import Path
import bpy

EXIT_CODE = 97

def _arguments(argv):
    raise AssertionError("patched by the behavior fixture")

def _require_blender_pin():
    pass

def _remove_factory_objects():
    pass

def _audit_import_source(source):
    result = bpy.ops.import_scene.gltf(
        filepath=str(source),
        loglevel=1,
        import_pack_images=True,
        merge_vertices=False,
        import_shading="SMOOTH",
        import_webp_texture=False,
        import_unused_materials=False,
        import_select_created_objects=True,
        import_scene_extras=False,
        import_scene_as_collection=True,
        import_merge_material_slots=True,
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB audit import returned {result}")

def _import_source(source):
    pass

def _static_mesh_objects():
    return []

def apply_modifier(obj, modifier):
    pass

def _triangle_count(mesh_objects):
    return 0

def _audit_source(source, inspected_triangles):
    _audit_import_source(source)
    mesh_objects = _static_mesh_objects()
    for obj in mesh_objects:
        triangulate = obj.modifiers.new("CatMetroAuditTriangulate", "TRIANGULATE")
        apply_modifier(obj, triangulate)
    audited = _triangle_count(mesh_objects)
    if audited != inspected_triangles:
        raise RuntimeError("audited source triangle count disagrees with inspector")
    return audited

def _validated_decimation_ratio(inspected, audited, effective, target):
    if audited != inspected:
        raise RuntimeError("audited source triangle count disagrees with inspector")
    if effective > inspected or effective > audited:
        raise RuntimeError("effective triangle count exceeds source")
    if effective <= target:
        raise RuntimeError("effective triangle count must exceed target")
    return target / effective

def _decimate(args, mesh_objects, audited_source_triangles):
    for obj in mesh_objects:
        triangulate = obj.modifiers.new("CatMetroTriangulate", "TRIANGULATE")
        apply_modifier(obj, triangulate)
    effective = _triangle_count(mesh_objects)
    ratio = _validated_decimation_ratio(
        args.source_triangles, audited_source_triangles, effective, args.target_triangles
    )
    for obj in mesh_objects:
        decimate = obj.modifiers.new("CatMetroCollapseDecimate", "DECIMATE")
        decimate.ratio = ratio
        apply_modifier(obj, decimate)
    output_triangles = _triangle_count(mesh_objects)
    if not args.minimum_triangles <= output_triangles <= args.maximum_triangles:
        raise RuntimeError("decimated count outside band")

def _export_output(output):
    pass

def main(argv):
    try:
        args = _arguments(argv)
        _require_blender_pin()
        if not args.source.is_file():
            raise RuntimeError("source GLB is missing")
        if not args.output.parent.is_dir():
            raise RuntimeError("output directory is missing")
        if args.output.exists():
            raise RuntimeError("staged output already exists")
        _remove_factory_objects()
        audited = _audit_source(args.source, args.source_triangles)
        _remove_factory_objects()
        _import_source(args.source)
        mesh_objects = _static_mesh_objects()
        _decimate(args, mesh_objects, audited)
        _export_output(args.output)
        return 0
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"blender-decimate: {exc}", file=sys.stderr)
        return EXIT_CODE
'''


def replaced_once(source: str, old: str, new: str, label: str) -> str:
    require(
        source.count(old) == 1,
        f"mutation fixture {label}: expected one replacement target; "
        f"found {source.count(old)}",
    )
    return source.replace(old, new, 1)


def assert_mutation_rejected(source: str, label: str, diagnostic: str) -> None:
    with tempfile.TemporaryDirectory(prefix="catmetro-audit-mutation-") as directory:
        path = Path(directory) / "blender_decimate.py"
        path.write_text(source, encoding="utf-8")
        try:
            validate_driver(path, f"mutation {label}")
        except AuditContractError as exc:
            require(
                diagnostic in str(exc),
                f"mutation {label}: wrong test diagnostic: {exc}",
            )
        else:
            raise AuditContractError(f"mutation {label} unexpectedly passed")


with tempfile.TemporaryDirectory(prefix="catmetro-audit-compliant-") as directory:
    compliant_path = Path(directory) / "blender_decimate.py"
    compliant_path.write_text(compliant_fixture, encoding="utf-8")
    validate_driver(compliant_path, "compliant Task 8c control")


# Independently kill every literal audit-import custody flag.
flag_mutations = {
    "loglevel": ("        loglevel=1,", "        loglevel=2,"),
    "import_pack_images": (
        "        import_pack_images=True,", "        import_pack_images=False,"
    ),
    "merge_vertices": (
        "        merge_vertices=False,", "        merge_vertices=True,"
    ),
    "import_shading": (
        '        import_shading="SMOOTH",', '        import_shading="NORMALS",'
    ),
    "import_webp_texture": (
        "        import_webp_texture=False,", "        import_webp_texture=True,"
    ),
    "import_unused_materials": (
        "        import_unused_materials=False,",
        "        import_unused_materials=True,",
    ),
    "import_select_created_objects": (
        "        import_select_created_objects=True,",
        "        import_select_created_objects=False,",
    ),
    "import_scene_extras": (
        "        import_scene_extras=False,", "        import_scene_extras=True,"
    ),
    "import_scene_as_collection": (
        "        import_scene_as_collection=True,",
        "        import_scene_as_collection=False,",
    ),
    "import_merge_material_slots": (
        "        import_merge_material_slots=True,",
        "        import_merge_material_slots=False,",
    ),
}
for flag, (old, new) in flag_mutations.items():
    assert_mutation_rejected(
        replaced_once(compliant_fixture, old, new, flag),
        f"audit flag {flag}",
        f"audit import keyword {flag} must be literal",
    )


audit_block = '''def _audit_import_source(source):
    result = bpy.ops.import_scene.gltf(
        filepath=str(source),
        loglevel=1,
        import_pack_images=True,
        merge_vertices=False,
        import_shading="SMOOTH",
        import_webp_texture=False,
        import_unused_materials=False,
        import_select_created_objects=True,
        import_scene_extras=False,
        import_scene_as_collection=True,
        import_merge_material_slots=True,
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB audit import returned {result}")
'''
alias_bypass_block = '''def _audit_import_source(source):
    importer = bpy.ops.import_scene.gltf
    if False:
        bpy.ops.import_scene.gltf(
            filepath=str(source),
            loglevel=1,
            import_pack_images=True,
            merge_vertices=False,
            import_shading="SMOOTH",
            import_webp_texture=False,
            import_unused_materials=False,
            import_select_created_objects=True,
            import_scene_extras=False,
            import_scene_as_collection=True,
            import_merge_material_slots=True,
        )
    result = importer(
        filepath=str(source),
        loglevel=1,
        import_pack_images=True,
        merge_vertices=False,
        import_shading="SMOOTH",
        import_webp_texture=False,
        import_unused_materials=False,
        import_select_created_objects=True,
        import_scene_extras=False,
        import_scene_as_collection=True,
        import_merge_material_slots=True,
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB audit import returned {result}")
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture, audit_block, alias_bypass_block, "unreachable direct alias bypass"
    ),
    "unreachable direct alias bypass",
    "must execute its direct AST import call",
)
same_line_alias_bypass_block = '''def _audit_import_source(source):
    importer = bpy.ops.import_scene.gltf
    result = bpy.ops.import_scene.gltf(filepath=str(source), loglevel=1, import_pack_images=True, merge_vertices=False, import_shading="SMOOTH", import_webp_texture=False, import_unused_materials=False, import_select_created_objects=True, import_scene_extras=False, import_scene_as_collection=True, import_merge_material_slots=True) if False else importer(filepath=str(source), loglevel=1, import_pack_images=True, merge_vertices=False, import_shading="SMOOTH", import_webp_texture=False, import_unused_materials=False, import_select_created_objects=True, import_scene_extras=False, import_scene_as_collection=True, import_merge_material_slots=True)
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB audit import returned {result}")
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        audit_block,
        same_line_alias_bypass_block,
        "same-line unreachable direct alias bypass",
    ),
    "same-line unreachable direct alias bypass",
    "direct audit import line must contain no alternate call expression",
)
shadowed_str_alias_bypass_block = '''def _audit_import_source(source):
    filepath = source.__str__()
    str = bpy.ops.import_scene.gltf
    result = bpy.ops.import_scene.gltf(filepath=str(source), loglevel=1, import_pack_images=True, merge_vertices=False, import_shading="SMOOTH", import_webp_texture=False, import_unused_materials=False, import_select_created_objects=True, import_scene_extras=False, import_scene_as_collection=True, import_merge_material_slots=True) if False else str(filepath=filepath, loglevel=1, import_pack_images=True, merge_vertices=False, import_shading="SMOOTH", import_webp_texture=False, import_unused_materials=False, import_select_created_objects=True, import_scene_extras=False, import_scene_as_collection=True, import_merge_material_slots=True)
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB audit import returned {result}")
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        audit_block,
        shadowed_str_alias_bypass_block,
        "same-line shadowed-str alias bypass",
    ),
    "same-line shadowed-str alias bypass",
    "direct audit import line must contain no alternate call expression",
)

exact_result_check = '''    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB audit import returned {result}")
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        exact_result_check,
        '''    if "FINISHED" not in result:
        raise RuntimeError(f"GLB audit import returned {result}")
''',
        "non-exact audit operator result",
    ),
    "non-exact audit operator result",
    "accepted a non-exact FINISHED result",
)

# Exact means exact: deleting the comparison or allowing the observed magic -2
# must both fail, while the import/static/triangulation/count legs stay live.
exact_check = '''    if audited != inspected_triangles:
        raise RuntimeError("audited source triangle count disagrees with inspector")
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        exact_check,
        '''    if False:
        raise RuntimeError("audited source triangle count disagrees with inspector")
''',
        "missing exact audit",
    ),
    "missing exact audit",
    "exact audit accepted inspected=12/audited=10",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        exact_check,
        '''    if abs(audited - inspected_triangles) > 2:
        raise RuntimeError("audited source triangle count disagrees with inspector")
''',
        "magic minus-two audit tolerance",
    ),
    "magic minus-two audit tolerance",
    "exact audit accepted inspected=12/audited=10",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        exact_check,
        '''    if audited != inspected_triangles:
        importer = bpy.ops.import_scene.gltf
        importer(filepath=str(source), merge_vertices=True)
        raise RuntimeError("audited source triangle count disagrees with inspector")
''',
        "mismatch-branch alias import",
    ),
    "mismatch-branch alias import",
    "mismatch path executed a glTF importer outside _audit_import_source",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        "    _audit_import_source(source)\n",
        "    pass  # mutation: dead audit importer\n",
        "dead audit importer",
    ),
    "dead audit importer",
    "audit must import, statically validate, triangulate, apply, then count",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        "    _audit_import_source(source)\n    mesh_objects = _static_mesh_objects()\n",
        "    _audit_import_source(source)\n"
        "    importer = bpy.ops.import_scene.gltf\n"
        "    importer(filepath=str(source), merge_vertices=True)\n"
        "    mesh_objects = _static_mesh_objects()\n",
        "audit source alias-extra import",
    ),
    "audit source alias-extra import",
    "_audit_source executed a glTF importer outside _audit_import_source",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        "    _audit_import_source(source)\n    mesh_objects = _static_mesh_objects()\n",
        "    _audit_import_source(source)\n"
        "    mesh_objects = []  # mutation: static validation bypassed\n",
        "static validation bypass",
    ),
    "static validation bypass",
    "audit must import, statically validate, triangulate, apply, then count",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        "    for obj in mesh_objects:\n        triangulate = obj.modifiers.new(\"CatMetroAuditTriangulate\", \"TRIANGULATE\")\n        apply_modifier(obj, triangulate)\n",
        "    for obj in ():\n        triangulate = obj.modifiers.new(\"CatMetroAuditTriangulate\", \"TRIANGULATE\")\n        apply_modifier(obj, triangulate)\n",
        "audit triangulation bypass",
    ),
    "audit triangulation bypass",
    "audit must import, statically validate, triangulate, apply, then count",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        "    audited = _triangle_count(mesh_objects)\n",
        "    audited = inspected_triangles  # mutation: count bypassed\n",
        "audit count bypass",
    ),
    "audit count bypass",
    "audit must import, statically validate, triangulate, apply, then count",
)

# Independently kill each ratio guard and the raw-denominator regression.
ratio_guard_mutations = (
    (
        "mismatch guard",
        '''    if audited != inspected:
        raise RuntimeError("audited source triangle count disagrees with inspector")
''',
        '''    if False:
        raise RuntimeError("audited source triangle count disagrees with inspector")
''',
        "audited/inspected mismatch guard did not reject",
    ),
    (
        "effective-over-source guard",
        '''    if effective > inspected or effective > audited:
        raise RuntimeError("effective triangle count exceeds source")
''',
        '''    if False:
        raise RuntimeError("effective triangle count exceeds source")
''',
        "effective-over-source guard did not reject",
    ),
    (
        "effective-at-or-below-target guard",
        '''    if effective <= target:
        raise RuntimeError("effective triangle count must exceed target")
''',
        '''    if False:
        raise RuntimeError("effective triangle count must exceed target")
''',
        "effective-at-target guard did not reject",
    ),
)
for mutation_label, old, new, diagnostic in ratio_guard_mutations:
    assert_mutation_rejected(
        replaced_once(compliant_fixture, old, new, mutation_label),
        mutation_label,
        diagnostic,
    )
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        "    return target / effective\n",
        "    return target / inspected  # mutation: raw denominator\n",
        "raw denominator helper",
    ),
    "raw denominator helper",
    "effective denominator must produce 0.5",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        "    return target / effective\n",
        "    return 0.5  # mutation: one-vector constant\n",
        "constant ratio helper",
    ),
    "constant ratio helper",
    "effective denominator must produce 0.25",
)
validated_call = '''    ratio = _validated_decimation_ratio(
        args.source_triangles, audited_source_triangles, effective, args.target_triangles
    )
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        validated_call,
        "    ratio = args.target_triangles / args.source_triangles\n",
        "raw denominator in decimate",
    ),
    "raw denominator in decimate",
    "_decimate used the raw/audited denominator",
)
ratio_application_block = '''    effective = _triangle_count(mesh_objects)
    ratio = _validated_decimation_ratio(
        args.source_triangles, audited_source_triangles, effective, args.target_triangles
    )
    for obj in mesh_objects:
        decimate = obj.modifiers.new("CatMetroCollapseDecimate", "DECIMATE")
        decimate.ratio = ratio
        apply_modifier(obj, decimate)
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        ratio_application_block,
        '''    effective = _triangle_count(mesh_objects)
    ratio = args.target_triangles / args.source_triangles
    for obj in mesh_objects:
        decimate = obj.modifiers.new("CatMetroCollapseDecimate", "DECIMATE")
        decimate.ratio = ratio
        apply_modifier(obj, decimate)
    validated_ratio = _validated_decimation_ratio(
        args.source_triangles, audited_source_triangles, effective, args.target_triangles
    )
    decimate.ratio = validated_ratio
''',
        "post-apply ratio rewrite",
    ),
    "post-apply ratio rewrite",
    "applied DECIMATE ratio must use effective=10 at application time",
)

# A correct but dead helper, a reordered import, or missing audit cleanup cannot
# satisfy the dynamic main contract.
main_audit_call = "        audited = _audit_source(args.source, args.source_triangles)\n"
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        main_audit_call,
        "        audited = args.source_triangles  # mutation: dead audit helper\n",
        "dead audit helper",
    ),
    "dead audit helper",
    "main did not pass _audit_source's result into _decimate",
)
cleanup_sequence = '''        audited = _audit_source(args.source, args.source_triangles)
        _remove_factory_objects()
        _import_source(args.source)
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        cleanup_sequence,
        '''        audited = _audit_source(args.source, args.source_triangles)
        _import_source(args.source)
''',
        "audit cleanup bypass",
    ),
    "audit cleanup bypass",
    "main sequence must be",
)
ordered_sequence = '''        _remove_factory_objects()
        audited = _audit_source(args.source, args.source_triangles)
        _remove_factory_objects()
        _import_source(args.source)
'''
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        ordered_sequence,
        '''        _remove_factory_objects()
        _import_source(args.source)
        audited = _audit_source(args.source, args.source_triangles)
        _remove_factory_objects()
''',
        "main audit/import reorder",
    ),
    "main audit/import reorder",
    "main sequence must be",
)
assert_mutation_rejected(
    replaced_once(
        compliant_fixture,
        "        _import_source(args.source)\n"
        "        mesh_objects = _static_mesh_objects()\n",
        "        _import_source(args.source)\n"
        "        importer = bpy.ops.import_scene.gltf\n"
        "        importer(filepath=str(args.source), merge_vertices=True)\n"
        "        mesh_objects = _static_mesh_objects()\n",
        "main alias-extra import",
    ),
    "main alias-extra import",
    "main executed a glTF importer outside the audited/safe helpers",
)


print(
    "glb-decimation review H: compliant fixture and mutation controls pass",
    flush=True,
)
driver = Path(sys.argv[1])
try:
    validate_driver(driver, str(driver))
except AuditContractError as exc:
    print(f"glb-decimation pipeline test: Task 8c audit contract: {exc}", file=sys.stderr)
    raise SystemExit(1) from None
PY
fi

if [ "$review_section" = H ]; then
  printf 'glb-decimation review H: pass\n'
  exit 0
fi

test ! -e "$marker" || die "shell-evaluation marker already exists"
marker_cleanup_armed=1
cd "$repo"

mkdir -p "$tmp/bin"
# shellcheck disable=SC2016 # sentinel variables expand when the generated script runs
printf '%s\n' \
  '#!/usr/bin/env bash' \
  'set -euo pipefail' \
  ': "${CURL_SENTINEL_LOG:?}"' \
  'printf "curl called\\n" >>"$CURL_SENTINEL_LOG"' \
  'exit 91' \
  >"$tmp/bin/curl"
chmod +x "$tmp/bin/curl"
export CURL_SENTINEL_LOG="$tmp/curl-called.log"
export PATH="$tmp/bin:$PATH"

assert_no_external_effects() {
  test ! -e "$CURL_SENTINEL_LOG" || die "curl sentinel was called"
  test ! -e "$marker" || die "a metacharacter path was evaluated by a shell"
}

sha256_file() {
  PYTHONDONTWRITEBYTECODE=1 python3 - "$1" <<'PY'
import hashlib
import sys
from pathlib import Path

print(hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest())
PY
}

magic_hex() {
  PYTHONDONTWRITEBYTECODE=1 python3 - "$1" <<'PY'
import sys
from pathlib import Path

print(Path(sys.argv[1]).read_bytes()[:4].hex())
PY
}

write_fixture() {
  PYTHONDONTWRITEBYTECODE=1 python3 "$repo/tests/assets/glb_fixture.py" "$@"
}

write_sidecar() {
  local source=$1
  local service=$2
  local prompt=$3
  local tier=${4:-paid}
  local claimed_sha=${5:-}
  if [ -z "$claimed_sha" ]; then
    claimed_sha=$(sha256_file "$source")
  fi
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$source.json" "$service" "$prompt" "$tier" "$claimed_sha" <<'PY'
import json
import re
import sys
from pathlib import Path

path, service, prompt, tier, claimed_sha = sys.argv[1:]
record = {
    "service": service,
    "task_id": f"fixture-{service}-task",
    "timestamp_utc": "2026-08-15T12:34:56Z",
    "plan_tier": tier,
    "prompt": prompt,
    "note": "local paid fixture",
    "sha256": claimed_sha,
}
Path(path).write_text(json.dumps(record, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY
}

write_single_manifest() {
  local path=$1
  local asset_id=$2
  local kind=$3
  local service=$4
  local output=$5
  local prompt=$6
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$path" "$asset_id" "$kind" "$service" "$output" "$prompt" <<'PY'
import json
import sys
from pathlib import Path

path, asset_id, kind, service, output, prompt = sys.argv[1:]
document = {
    "assets": [{
        "id": asset_id,
        "kind": kind,
        "service": service,
        "out": output,
        "prompt": prompt,
    }]
}
Path(path).write_text(json.dumps(document, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY
}

write_happy_manifest() {
  local path=$1
  PYTHONDONTWRITEBYTECODE=1 python3 - "$path" <<'PY'
import json
import sys
from pathlib import Path

document = {
    "assets": [
        {
            "id": "fixture-cat",
            "kind": "cat",
            "service": "meshy",
            "out": "cat-source.glb",
            "prompt": "round fixture cat",
        },
        {
            "id": "fixture-prop",
            "kind": "prop",
            "service": "tripo",
            "out": "prop-source.glb",
            "prompt": "rounded fixture prop",
        },
    ]
}
Path(sys.argv[1]).write_text(
    json.dumps(document, indent=2, sort_keys=True) + "\n",
    encoding="utf-8",
)
PY
}

fingerprint_tree() {
  PYTHONDONTWRITEBYTECODE=1 python3 - "$1" <<'PY'
import hashlib
import json
import os
import sys
from pathlib import Path

root = Path(sys.argv[1])
records = []
for directory, names, filenames in os.walk(root, followlinks=False):
    directory_path = Path(directory)
    for name in sorted(names + filenames):
        path = directory_path / name
        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            records.append([relative, "symlink", os.readlink(path)])
        elif path.is_dir():
            records.append([relative, "directory"])
        else:
            records.append([
                relative,
                "file",
                hashlib.sha256(path.read_bytes()).hexdigest(),
            ])
print(json.dumps(records, separators=(",", ":")))
PY
}

line_count() {
  if [ -f "$1" ]; then
    wc -l <"$1" | tr -d ' '
  else
    printf '0\n'
  fi
}

run_decimator() {
  local mode=$1
  local log=$2
  local stdout=$3
  local stderr=$4
  shift 4
  env \
    FAKE_BLENDER_MODE="$mode" \
    FAKE_BLENDER_LOG="$log" \
    FAKE_BLENDER_AUDIT="$log.audit" \
    FAKE_BLENDER_VERSION="${CASE_BLENDER_VERSION:-5.1.2}" \
    FAKE_BLENDER_BUILD_HASH="${CASE_BLENDER_BUILD_HASH:-ec6e62d40fa9}" \
    FAKE_BLENDER_VERSION_BANNER="${CASE_BLENDER_VERSION_BANNER:-0}" \
    FAKE_BLENDER_BANNER_VERSION="${CASE_BLENDER_BANNER_VERSION:-${CASE_BLENDER_VERSION:-5.1.2}}" \
    FAKE_BLENDER_BANNER_BUILD_HASH="${CASE_BLENDER_BANNER_BUILD_HASH:-${CASE_BLENDER_BUILD_HASH:-ec6e62d40fa9}}" \
    PIPELINE_SENTINEL_KEY="environment-sentinel-1" \
    PIPELINE_SENTINEL_TOKEN="environment-sentinel-2" \
    PIPELINE_SENTINEL_SECRET="environment-sentinel-3" \
    PIPELINE_SENTINEL_AUTH="environment-sentinel-4" \
    PIPELINE_SENTINEL_CREDENTIAL="environment-sentinel-5" \
    PIPELINE_SENTINEL_BEARER="environment-sentinel-6" \
    PYTHONDONTWRITEBYTECODE=1 \
    python3 "$decimate_script" \
      --manifest "$manifest" \
      --input-dir "$input_dir" \
      --output-dir "$output_dir" \
      --blender "$fake_blender" \
      "$@" \
      >"$stdout" 2>"$stderr"
}

assert_exact_provenance() {
  local source=$1
  local final=$2
  local proof=$3
  local category=$4
  local target=$5
  local minimum=$6
  local service=$7
  local prompt=$8
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$repo/scripts" "$source" "$final" "$proof" \
    "$category" "$target" "$minimum" "$service" "$prompt" <<'PY'
import hashlib
import json
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import compare_preservation, inspect_glb

source = Path(sys.argv[2])
final = Path(sys.argv[3])
proof_path = Path(sys.argv[4])
category = sys.argv[5]
target = int(sys.argv[6])
minimum = int(sys.argv[7])
service = sys.argv[8]
prompt = sys.argv[9]
source_sidecar_path = Path(f"{source}.json")

source_metrics = inspect_glb(source)
output_metrics = inspect_glb(final)
source_sidecar = json.loads(source_sidecar_path.read_text(encoding="utf-8"))
record = json.loads(proof_path.read_text(encoding="utf-8"))
metric_names = (
    "triangles", "vertices", "primitives", "materials",
    "material_primitives", "images", "embedded_images", "uv_primitives",
    "animations", "cameras", "lights", "skins", "morph_targets",
    "extensions_used", "extensions_required", "world_bounds",
)

assert set(record) == {"schema_version", "source", "derivative", "tool", "geometry"}
assert record["schema_version"] == 1
assert set(record["source"]) == {"filename", "sha256", "sidecar_sha256", "provenance"}
assert record["source"]["filename"] == source.name
assert record["source"]["sha256"] == hashlib.sha256(source.read_bytes()).hexdigest()
assert record["source"]["sidecar_sha256"] == hashlib.sha256(source_sidecar_path.read_bytes()).hexdigest()
assert record["source"]["provenance"] == {
    key: source_sidecar[key]
    for key in sorted({"service", "task_id", "timestamp_utc", "plan_tier", "prompt", "note"})
}
assert source_sidecar["service"] == service
assert source_sidecar["prompt"] == prompt
assert source_sidecar["plan_tier"] == "paid"

assert record["derivative"] == {
    "filename": final.name,
    "sha256": hashlib.sha256(final.read_bytes()).hexdigest(),
}
assert set(record["tool"]) == {"name", "version", "build_hash", "operation", "timestamp_utc"}
assert record["tool"]["name"] == "Blender"
assert record["tool"]["version"] == "5.1.2"
assert record["tool"]["build_hash"] == "ec6e62d40fa9"
assert record["tool"]["operation"] == "collapse-decimate"
assert re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", record["tool"]["timestamp_utc"])

geometry = record["geometry"]
assert set(geometry) == {
    "category", "target_triangles", "accepted_minimum",
    "accepted_maximum", "source", "output",
}
assert geometry["category"] == category
assert geometry["target_triangles"] == target
assert geometry["accepted_minimum"] == minimum
assert geometry["accepted_maximum"] == target
assert geometry["source"] == {name: source_metrics[name] for name in metric_names}
assert geometry["output"] == {name: output_metrics[name] for name in metric_names}
assert minimum <= output_metrics["triangles"] <= target
assert 5000 <= output_metrics["triangles"] <= 20000
assert compare_preservation(source_metrics, output_metrics) == []

forbidden = re.compile(
    r"api[_-]?key|token|secret|authorization|credential|bearer|https?://",
    re.IGNORECASE,
)
def scan(value):
    if isinstance(value, dict):
        for key, child in value.items():
            assert not forbidden.search(str(key)), key
            scan(child)
    elif isinstance(value, list):
        for child in value:
            scan(child)
    elif isinstance(value, str):
        assert not forbidden.search(value), value

scan(record)
PY
}

# Review regression E: manifest IDs enter operator logs and therefore must be
# non-empty printable single-line values. Reject controls and Unicode line
# separators before even the fake Blender version probe; retain ordinary broad
# printable IDs rather than inventing a lowercase naming convention.
if [ "$review_section" = all ] || [ "$review_section" = E ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-manifest-id" "$repo" "$fake_blender" <<'PY'
import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path

script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from glb_fixture import write_glb

errors = []

def check(condition, message):
    if not condition:
        errors.append(message)

def digest(value):
    return hashlib.sha256(value).hexdigest()

def lines(path):
    if not path.exists():
        return []
    return path.read_text(encoding="utf-8").splitlines()

def prepare_case(label, identifier):
    case_root = root / label
    input_dir = case_root / "input"
    output_dir = case_root / "output"
    input_dir.mkdir(parents=True)
    output_dir.mkdir()
    source = input_dir / "asset.glb"
    source_sidecar = Path(f"{source}.json")
    manifest = case_root / "manifest.json"
    fake_log = case_root / "fake.log"
    fake_audit = case_root / "fake.audit"
    write_glb(source, triangles=30000)
    source_bytes = source.read_bytes()
    source_sidecar.write_text(json.dumps({
        "service": "meshy",
        "task_id": "fixture-meshy-task",
        "timestamp_utc": "2026-08-15T12:34:56Z",
        "plan_tier": "paid",
        "prompt": "fixture cat",
        "note": "local paid fixture",
        "sha256": digest(source_bytes),
    }, sort_keys=True) + "\n", encoding="utf-8")
    sidecar_bytes = source_sidecar.read_bytes()
    manifest.write_text(json.dumps({"assets": [{
        "id": identifier,
        "kind": "cat",
        "service": "meshy",
        "out": "asset.glb",
        "prompt": "fixture cat",
    }]}, sort_keys=True) + "\n", encoding="utf-8")
    environment = {
        "PATH": os.environ["PATH"],
        "CURL_SENTINEL_LOG": os.environ["CURL_SENTINEL_LOG"],
        "FAKE_BLENDER_MODE": "success",
        "FAKE_BLENDER_LOG": str(fake_log),
        "FAKE_BLENDER_AUDIT": str(fake_audit),
        "PYTHONDONTWRITEBYTECODE": "1",
    }
    try:
        result = subprocess.run(
            [
                sys.executable,
                str(script),
                "--manifest", str(manifest),
                "--input-dir", str(input_dir),
                "--output-dir", str(output_dir),
                "--blender", str(fake_blender),
            ],
            check=False,
            capture_output=True,
            text=True,
            timeout=20,
            env=environment,
        )
    except subprocess.TimeoutExpired as exc:
        raise AssertionError(f"{label}: bounded CLI invocation timed out") from exc
    return {
        "result": result,
        "input_dir": input_dir,
        "output_dir": output_dir,
        "source": source,
        "source_bytes": source_bytes,
        "source_sidecar": source_sidecar,
        "sidecar_bytes": sidecar_bytes,
        "fake_log": fake_log,
        "fake_audit": fake_audit,
    }

unsafe_ids = (
    ("empty", ""),
    ("line-feed", "unsafe\nglb-decimation: asset=forged-line-feed"),
    ("carriage-return", "unsafe\rglb-decimation: asset=forged-carriage-return"),
    ("tab", "unsafe\tglb-decimation: asset=forged-tab"),
    ("escape", "unsafe\x1bglb-decimation: asset=forged-escape"),
    ("unicode-line-separator", "unsafe\u2028glb-decimation: asset=forged-line-separator"),
    ("unicode-paragraph-separator", "unsafe\u2029glb-decimation: asset=forged-paragraph-separator"),
)
expected_diagnostic = (
    "glb-decimation: invalid manifest: "
    "id must be a printable single-line string\n"
)

for label, identifier in unsafe_ids:
    case = prepare_case(label, identifier)
    result = case["result"]
    combined = result.stdout + result.stderr
    check(result.returncode != 0, f"{label}: unsafe manifest ID was accepted")
    check(result.stdout == "", f"{label}: rejection wrote stdout: {result.stdout!r}")
    check(
        result.stderr == expected_diagnostic,
        f"{label}: diagnostic was not exact/non-interpolating: {combined!r}",
    )
    check(
        result.stderr.endswith("\n") and result.stderr[:-1].isprintable(),
        f"{label}: diagnostic contains a nonprintable/control character: {result.stderr!r}",
    )
    if identifier:
        check(identifier not in combined, f"{label}: raw unsafe ID was interpolated")
    check(lines(case["fake_audit"]) == [], f"{label}: fake version/asset execution was reached")
    check(lines(case["fake_log"]) == [], f"{label}: fake asset execution was logged")
    check(
        "glb-decimation: asset=" not in combined
        and "output_triangles=" not in combined,
        f"{label}: forged start/acceptance record escaped: {combined!r}",
    )
    check(case["source"].read_bytes() == case["source_bytes"], f"{label}: source GLB changed")
    check(
        case["source_sidecar"].read_bytes() == case["sidecar_bytes"],
        f"{label}: source sidecar changed",
    )
    check(
        sorted(path.name for path in case["input_dir"].iterdir())
        == ["asset.glb", "asset.glb.json"],
        f"{label}: source custody membership changed",
    )
    check(list(case["output_dir"].rglob("*")) == [], f"{label}: output/residue was created")

safe_identifier = "Cat Metro № 7 – café"
check(safe_identifier.isprintable(), "safe fixture is not printable")
safe = prepare_case("safe-printable", safe_identifier)
safe_result = safe["result"]
safe_lines = safe_result.stdout.splitlines()
check(safe_result.returncode == 0, f"safe printable ID was rejected: {safe_result.stderr!r}")
check(safe_result.stderr == "", f"safe printable ID wrote stderr: {safe_result.stderr!r}")
check(
    len(safe_lines) == 2
    and all(f"asset={safe_identifier}" in line for line in safe_lines),
    f"safe printable ID did not remain on two exact physical records: {safe_lines!r}",
)
check(lines(safe["fake_audit"]) == ["version", "asset"], "safe printable ID missed fake phases")
check(len(lines(safe["fake_log"])) == 1, "safe printable ID missed fake asset execution")
check(safe["source"].read_bytes() == safe["source_bytes"], "safe printable ID changed source GLB")
check(
    safe["source_sidecar"].read_bytes() == safe["sidecar_bytes"],
    "safe printable ID changed source sidecar",
)
check(
    sorted(path.name for path in safe["output_dir"].iterdir())
    == ["asset.glb", "asset.glb.json"],
    "safe printable ID did not produce one exact final pair",
)

if errors:
    raise AssertionError("manifest ID log-safety regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = E ]; then
  printf 'glb-decimation review E: pass\n'
  exit 0
fi

# Review regression F: Blender 5.1.2's official version output includes a
# banner before the exact version and build identity lines. The banner is an
# accepted prefix only; it must not weaken either pinned identity check.
if [ "$review_section" = all ] || [ "$review_section" = F ]; then
  banner_audit="$tmp/official-banner.audit"
  banner_output=$(
    FAKE_BLENDER_VERSION_BANNER=1 \
      FAKE_BLENDER_AUDIT="$banner_audit" \
      PYTHONDONTWRITEBYTECODE=1 \
      "$fake_blender" --background --version
  )
  expected_banner_output=$'Blender 5.1.2 (hash ec6e62d40fa9 built 2026-05-19 01:30:33)\nBlender 5.1.2\n\tbuild hash: ec6e62d40fa9'
  test "$banner_output" = "$expected_banner_output" || \
    die "fake Blender official banner surface is wrong"
  test "$(cat "$banner_audit")" = version || \
    die "fake Blender official banner missed its version audit"
  independent_banner_output=$(
    FAKE_BLENDER_VERSION_BANNER=1 \
      FAKE_BLENDER_VERSION=5.2.0 \
      FAKE_BLENDER_BUILD_HASH=wrong-build \
      FAKE_BLENDER_BANNER_VERSION=5.1.2 \
      FAKE_BLENDER_BANNER_BUILD_HASH=ec6e62d40fa9 \
      PYTHONDONTWRITEBYTECODE=1 \
      "$fake_blender" --background --version
  )
  expected_independent_banner=$'Blender 5.1.2 (hash ec6e62d40fa9 built 2026-05-19 01:30:33)\nBlender 5.2.0\n\tbuild hash: wrong-build'
  test "$independent_banner_output" = "$expected_independent_banner" || \
    die "fake Blender banner/plain identity overrides are not independent"

  prepare_version_banner_case() {
    local label=$1
    version_case="$tmp/review-version-banner/$label"
    input_dir="$version_case/input"
    output_dir="$version_case/output"
    manifest="$version_case/manifest.json"
    version_log="$version_case/fake.log"
    version_stdout="$version_case/stdout"
    version_stderr="$version_case/stderr"
    mkdir -p "$input_dir" "$output_dir"
    write_fixture "$input_dir/asset.glb" --triangles 30000
    write_sidecar \
      "$input_dir/asset.glb" meshy "official banner fixture cat"
    write_single_manifest \
      "$manifest" official-banner-cat cat meshy asset.glb \
      "official banner fixture cat"
    version_input_before=$(fingerprint_tree "$input_dir")
  }

  assert_version_banner_custody() {
    local label=$1
    local expected_terminal=${2:-empty}
    test "$version_input_before" = "$(fingerprint_tree "$input_dir")" || \
      die "$label changed its source custody tree"
    if [ "$expected_terminal" = pair ]; then
      test "$(LC_ALL=C command ls -1A "$output_dir" | sort)" = \
        $'asset.glb\nasset.glb.json' || \
        die "$label changed the public output tree"
    else
      test -z "$(find "$output_dir" -mindepth 1 -print -quit)" || \
        die "$label changed the public output tree"
    fi
    assert_no_external_effects
  }

  run_rejected_version_banner_case() {
    local label=$1
    local version=$2
    local build_hash=$3
    local diagnostic=$4
    local banner_version=${5:-$version}
    local banner_build_hash=${6:-$build_hash}
    prepare_version_banner_case "$label"
    set +e
    CASE_BLENDER_VERSION_BANNER=1 \
      CASE_BLENDER_VERSION="$version" \
      CASE_BLENDER_BUILD_HASH="$build_hash" \
      CASE_BLENDER_BANNER_VERSION="$banner_version" \
      CASE_BLENDER_BANNER_BUILD_HASH="$banner_build_hash" \
      run_decimator \
        success "$version_log" "$version_stdout" "$version_stderr"
    local rc=$?
    set -e
    test "$rc" -ne 0 || die "$label accepted the wrong Blender identity"
    test ! -s "$version_stdout" || die "$label wrote an acceptance record"
    rg -q "^$diagnostic$" "$version_stderr" || {
      sed -n '1,80p' "$version_stderr" >&2
      die "$label lacked its pinned Blender diagnostic"
    }
    test "$(cat "$version_log.audit")" = version || \
      die "$label passed the fake version phase"
    test "$(line_count "$version_log")" -eq 0 || \
      die "$label reached the fake asset phase"
    if find "$output_dir" -mindepth 1 -print -quit | grep -q .; then
      find "$output_dir" -mindepth 1 -print >&2
      die "$label created an output"
    fi
    assert_version_banner_custody "$label"
  }

  run_rejected_version_banner_case \
    official-banner-wrong-version 5.2.0 ec6e62d40fa9 \
    'glb-decimation: requires Blender 5\.1\.2'
  run_rejected_version_banner_case \
    official-banner-wrong-build 5.1.2 wrong-build \
    'glb-decimation: requires Blender (5\.1\.2|build ec6e62d40fa9)'
  run_rejected_version_banner_case \
    official-banner-authoritative-version-mismatch 5.2.0 ec6e62d40fa9 \
    'glb-decimation: requires Blender 5\.1\.2' \
    5.1.2 ec6e62d40fa9
  run_rejected_version_banner_case \
    official-banner-authoritative-build-mismatch 5.1.2 wrong-build \
    'glb-decimation: requires Blender (5\.1\.2|build ec6e62d40fa9)' \
    5.1.2 ec6e62d40fa9

  prepare_version_banner_case official-banner-correct
  set +e
  CASE_BLENDER_VERSION_BANNER=1 \
    run_decimator \
      success "$version_log" "$version_stdout" "$version_stderr"
  banner_rc=$?
  set -e
  if [ "$banner_rc" -eq 0 ]; then
    assert_version_banner_custody "official pinned Blender banner" pair
  else
    assert_version_banner_custody "official pinned Blender banner"
  fi
  if [ "$banner_rc" -ne 0 ]; then
    test ! -s "$version_stdout" || \
      die "rejected official Blender banner wrote an acceptance record"
    test "$(cat "$version_log.audit")" = version || \
      die "rejected official Blender banner crossed the version boundary"
    test "$(line_count "$version_log")" -eq 0 || \
      die "rejected official Blender banner reached the asset phase"
    if find "$output_dir" -mindepth 1 -print -quit | grep -q .; then
      find "$output_dir" -mindepth 1 -print >&2
      die "rejected official Blender banner created an output"
    fi
    rg -q '^glb-decimation: requires Blender 5\.1\.2$' \
      "$version_stderr" || {
      sed -n '1,80p' "$version_stderr" >&2
      die "official Blender banner failed for an unexpected reason"
    }
    sed -n '1,80p' "$version_stderr" >&2
    die "official Blender 5.1.2 banner was rejected"
  fi
  test ! -s "$version_stderr" || \
    die "official Blender banner success wrote stderr"
  test "$(cat "$version_log.audit")" = $'version\nasset' || \
    die "official Blender banner missed its safe asset path"
  test "$(line_count "$version_log")" -eq 1 || \
    die "official Blender banner did not run one asset"
  test "$version_input_before" = "$(fingerprint_tree "$input_dir")" || \
    die "official Blender banner changed its source custody tree"
  test "$(LC_ALL=C command ls -1A "$output_dir" | sort)" = \
    $'asset.glb\nasset.glb.json' || \
    die "official Blender banner did not produce one exact final pair"
  assert_version_banner_custody "official pinned Blender banner" pair
fi

if [ "$review_section" = F ]; then
  printf 'glb-decimation review F: pass\n'
  exit 0
fi

# Validate syntax without generating bytecode, then exercise every fake mode
# from a foreign working directory. This proves the fixture import is relative
# to __file__, the version/build surface is exact, and each negative mode is a
# real, distinguishable output before the orchestrator can be blamed.
PYTHONDONTWRITEBYTECODE=1 python3 - "$fake_blender" "$repo/tests/assets/glb_fixture.py" <<'PY'
import sys
from pathlib import Path

for filename in sys.argv[1:]:
    compile(Path(filename).read_bytes(), filename, "exec")
PY
test -x "$fake_blender" || die "fake Blender is not executable"

version_audit="$tmp/version.audit"
version_output=$(
  FAKE_BLENDER_AUDIT="$version_audit" \
    PYTHONDONTWRITEBYTECODE=1 "$fake_blender" --background --version
)
test "$version_output" = $'Blender 5.1.2\nbuild hash: ec6e62d40fa9' || \
  die "fake Blender version surface is wrong"
test "$(cat "$version_audit")" = version || \
  die "fake Blender did not safely audit its version phase"
wrong_version_output=$(
  FAKE_BLENDER_VERSION=5.2.0 FAKE_BLENDER_BUILD_HASH=wrong \
    PYTHONDONTWRITEBYTECODE=1 "$fake_blender" --background --version
)
test "$wrong_version_output" = $'Blender 5.2.0\nbuild hash: wrong' || \
  die "fake Blender version overrides are wrong"

forbidden_audit="$tmp/forbidden-environment.audit"
forbidden_log="$tmp/forbidden-environment.log"
sentinel_names=(
  PIPELINE_SENTINEL_KEY PIPELINE_SENTINEL_TOKEN PIPELINE_SENTINEL_SECRET
  PIPELINE_SENTINEL_AUTH PIPELINE_SENTINEL_CREDENTIAL PIPELINE_SENTINEL_BEARER
)
sentinel_number=0
for sentinel_name in "${sentinel_names[@]}"; do
  sentinel_number=$((sentinel_number + 1))
  sentinel_value="environment-probe-$sentinel_number"
  for phase in version asset; do
    before_audit=$(line_count "$forbidden_audit")
    before_log=$(line_count "$forbidden_log")
    set +e
    if [ "$phase" = version ]; then
      env "$sentinel_name=$sentinel_value" \
        FAKE_BLENDER_AUDIT="$forbidden_audit" \
        FAKE_BLENDER_LOG="$forbidden_log" \
        PYTHONDONTWRITEBYTECODE=1 \
        "$fake_blender" --background --version \
        >"$tmp/forbidden.stdout" 2>"$tmp/forbidden.stderr"
    else
      env "$sentinel_name=$sentinel_value" \
        FAKE_BLENDER_AUDIT="$forbidden_audit" \
        FAKE_BLENDER_LOG="$forbidden_log" \
        PYTHONDONTWRITEBYTECODE=1 \
        "$fake_blender" --background --factory-startup -- \
        >"$tmp/forbidden.stdout" 2>"$tmp/forbidden.stderr"
    fi
    forbidden_rc=$?
    set -e
    test "$forbidden_rc" -eq 86 || \
      die "fake Blender accepted $sentinel_name on its $phase phase"
    rg -q '^fake-blender: forbidden environment sentinel present$' \
      "$tmp/forbidden.stderr" || \
      die "fake Blender lacked its safe environment rejection"
    if rg -Fq "$sentinel_value" "$tmp/forbidden.stdout" "$tmp/forbidden.stderr"; then
      die "fake Blender logged a forbidden environment value"
    fi
    test "$(line_count "$forbidden_audit")" -eq "$before_audit" || \
      die "fake Blender audited a rejected environment"
    test "$(line_count "$forbidden_log")" -eq "$before_log" || \
      die "fake Blender logged a rejected environment"
  done
done

preflight="$tmp/fake-preflight"
mkdir -p "$preflight/caller"
write_fixture "$preflight/source.glb" --triangles 20000
preflight_log="$preflight/fake.log"
fake_modes=(
  success over_budget under_budget malformed_output missing_uv
  missing_material missing_image bounds_drift external_image
  unsupported_extension unexpected_scene_content fail
)
for mode in "${fake_modes[@]}"; do
  output="$preflight/$mode.glb"
  set +e
  (
    cd "$preflight/caller"
    FAKE_BLENDER_MODE="$mode" \
      FAKE_BLENDER_LOG="$preflight_log" \
      PYTHONDONTWRITEBYTECODE=1 \
      "$fake_blender" \
        --background --factory-startup --offline-mode --disable-autoexec \
        --threads 1 --python-exit-code 97 --python "$expected_driver" -- \
        --source "$preflight/source.glb" \
        --output "$output" \
        --source-triangles 20000 \
        --target-triangles 10000 \
        --minimum-triangles 9000 \
        --maximum-triangles 10000
  )
  fake_rc=$?
  set -e
  if [ "$mode" = fail ]; then
    test "$fake_rc" -eq 17 || die "fake Blender fail mode did not exit 17"
  else
    test "$fake_rc" -eq 0 || die "fake Blender mode $mode failed setup"
  fi
done

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo/scripts" "$preflight" "$preflight_log" "$fake_blender" "$expected_driver" <<'PY'
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import GlbError, inspect_glb

root = Path(sys.argv[2])
records = [json.loads(line) for line in Path(sys.argv[3]).read_text(encoding="utf-8").splitlines()]
fake = Path(sys.argv[4]).resolve()
driver = Path(sys.argv[5]).resolve()
modes = [
    "success", "over_budget", "under_budget", "malformed_output",
    "missing_uv", "missing_material", "missing_image", "bounds_drift",
    "external_image", "unsupported_extension", "unexpected_scene_content",
    "fail",
]
assert len(records) == len(modes)
for record, mode in zip(records, modes):
    argv = record["argv"]
    assert Path(argv[0]).resolve() == fake
    assert "--" in argv
    assert Path(argv[argv.index("--python") + 1]).resolve() == driver
    assert record["target"] == 10000
    post = argv[argv.index("--") + 1:]
    assert post[0::2] == [
        "--source", "--output", "--source-triangles", "--target-triangles",
        "--minimum-triangles", "--maximum-triangles",
    ]

assert not (root / "fail.glb").exists()
assert (root / "malformed_output.glb").read_bytes() == b"not glTF"
metrics = {
    mode: inspect_glb(root / f"{mode}.glb")
    for mode in modes
    if mode not in {"fail", "malformed_output", "missing_uv"}
}
assert metrics["success"]["triangles"] == 10000
assert metrics["over_budget"]["triangles"] == 10001
assert metrics["under_budget"]["triangles"] == 7999
try:
    inspect_glb(root / "missing_uv.glb")
except GlbError as exc:
    diagnostic = str(exc)
    assert "TEXCOORD_0" in diagnostic, diagnostic
    assert "material references missing" in diagnostic, diagnostic
else:
    raise AssertionError(
        "direct inspection accepted a material-referenced missing TEXCOORD_0"
    )
assert metrics["missing_material"]["materials"] == 0
assert metrics["missing_material"]["material_primitives"] == 0
assert metrics["missing_image"]["images"] == 0
assert metrics["missing_image"]["embedded_images"] == 0
assert metrics["bounds_drift"]["world_bounds"] == {
    "min": [99.0, -1.0, -1.0], "max": [101.0, 1.0, 1.0]
}
assert metrics["external_image"]["external_uris"] == ["fixture-external.png"]
assert metrics["unsupported_extension"]["extensions_used"] == ["VENDOR_unreviewed"]
scene = metrics["unexpected_scene_content"]
assert [scene[name] for name in ("animations", "cameras", "lights", "skins", "morph_targets")] == [1, 1, 1, 1, 1]
PY
assert_no_external_effects

# Review hardening I: promotion inputs and absent-destination commits are a
# custody boundary, not merely pathnames. Staged members must be lstat-regular,
# private single-link files. Their hashes and types are frozen before the first
# rename and reverified only after both final names exist. A failed second rename
# must retire the candidate pair intact outside the final namespace even when
# unlink persistently fails; no caller may observe a lone final. Every fault runs
# in a bounded child so an accidental retry loop cannot retain the test process.
if [ "$review_section" = all ] || [ "$review_section" = I ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-promotion-custody" <<'PY'
import hashlib
import importlib.util
import multiprocessing
import os
import stat
import sys
import traceback
from pathlib import Path
from unittest import mock


script = Path(sys.argv[1])
root = Path(sys.argv[2])
root.mkdir()
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location(
    "decimate_assets_promotion_custody_test", script
)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
process_context = multiprocessing.get_context("fork")
errors = []


def digest_bytes(value):
    return hashlib.sha256(value).hexdigest()


def lstat_regular_single(path):
    try:
        status = os.lstat(path)
    except FileNotFoundError:
        return False
    return stat.S_ISREG(status.st_mode) and status.st_nlink == 1


def regular_payload(path, payload):
    return (
        lstat_regular_single(path)
        and path.read_bytes() == payload
        and digest_bytes(path.read_bytes()) == digest_bytes(payload)
    )


def new_case(name):
    directory = root / name
    directory.mkdir()
    return {
        "directory": directory,
        "staged_glb": directory / "staged.glb",
        "staged_json": directory / "staged.json",
        "final_glb": directory / "final.glb",
        "final_json": directory / "final.glb.json",
        "glb_bytes": f"candidate GLB for {name}".encode(),
        "json_bytes": f"candidate JSON for {name}".encode(),
    }


def exercise_compliant_control():
    case = new_case("compliant-regular-single-link")
    case["staged_glb"].write_bytes(case["glb_bytes"])
    case["staged_json"].write_bytes(case["json_bytes"])
    assert lstat_regular_single(case["staged_glb"])
    assert lstat_regular_single(case["staged_json"])
    module.promote_pair(
        case["staged_glb"],
        case["staged_json"],
        case["final_glb"],
        case["final_json"],
        False,
    )
    assert regular_payload(case["final_glb"], case["glb_bytes"])
    assert regular_payload(case["final_json"], case["json_bytes"])
    assert set(case["directory"].iterdir()) == {
        case["final_glb"], case["final_json"]
    }


def exercise_staged_type(name, member, kind):
    case = new_case(name)
    target = case[f"staged_{member}"]
    other_member = "json" if member == "glb" else "glb"
    other = case[f"staged_{other_member}"]
    target_payload = case[f"{member}_bytes"]
    other_payload = case[f"{other_member}_bytes"]
    other.write_bytes(other_payload)
    referent = case["directory"] / f"{member}-referent"
    if kind == "symlink":
        referent.write_bytes(target_payload)
        target.symlink_to(referent.name)
        assert target.is_symlink() and target.read_bytes() == target_payload
    elif kind == "hardlink":
        referent.write_bytes(target_payload)
        os.link(referent, target)
        assert os.lstat(target).st_nlink == 2 and os.path.samefile(target, referent)
    elif kind == "fifo":
        os.mkfifo(target, 0o600)
        assert stat.S_ISFIFO(os.lstat(target).st_mode)
    elif kind == "directory":
        target.mkdir()
    else:
        raise AssertionError(f"unsupported staged type {kind}")
    before = {
        path.name: (
            "symlink", os.readlink(path)
        ) if path.is_symlink() else (
            "regular", path.read_bytes(), os.lstat(path).st_nlink
        ) if stat.S_ISREG(os.lstat(path).st_mode) else (
            "mode", stat.S_IFMT(os.lstat(path).st_mode)
        )
        for path in case["directory"].iterdir()
    }
    caught = None
    try:
        module.promote_pair(
            case["staged_glb"],
            case["staged_json"],
            case["final_glb"],
            case["final_json"],
            False,
        )
    except BaseException as exc:
        caught = exc
    findings = []
    if caught is None:
        findings.append(f"{name}: staged {kind} {member} was accepted")
    elif not isinstance(caught, module.DecimationError):
        findings.append(
            f"{name}: staged rejection raised {type(caught).__name__}, not DecimationError"
        )
    if case["final_glb"].exists() or case["final_glb"].is_symlink():
        findings.append(f"{name}: rejected staged custody left a final GLB")
    if case["final_json"].exists() or case["final_json"].is_symlink():
        findings.append(f"{name}: rejected staged custody left a final JSON")
    after = {}
    for path in case["directory"].iterdir():
        if path.is_symlink():
            after[path.name] = ("symlink", os.readlink(path))
        else:
            status = os.lstat(path)
            if stat.S_ISREG(status.st_mode):
                after[path.name] = ("regular", path.read_bytes(), status.st_nlink)
            else:
                after[path.name] = ("mode", stat.S_IFMT(status.st_mode))
    if after != before:
        findings.append(
            f"{name}: staged rejection changed exact custody membership/type/bytes; "
            f"before={before!r} after={after!r}"
        )
    return findings


def exercise_between_renames(name, mutation):
    case = new_case(name)
    case["staged_glb"].write_bytes(case["glb_bytes"])
    case["staged_json"].write_bytes(case["json_bytes"])
    foreign = case["directory"] / "foreign-referent"
    foreign.write_bytes(b"foreign bytes that are never a candidate")
    permitted = {foreign}
    same_bytes_referent = None
    if mutation.endswith("link-same"):
        member = "glb" if mutation.startswith("glb-") else "json"
        same_bytes_referent = case["directory"] / f"same-{member}-referent"
        same_bytes_referent.write_bytes(case[f"{member}_bytes"])
        permitted.add(same_bytes_referent)
    real_replace = os.replace
    first_reached = False
    second_reached = False

    def replacing(source, destination):
        nonlocal first_reached, second_reached
        source_path = Path(source)
        destination_path = Path(destination)
        if source_path == case["staged_glb"] and destination_path == case["final_glb"]:
            result = real_replace(source_path, destination_path)
            first_reached = True
            if mutation == "glb-bytes":
                case["final_glb"].write_bytes(b"foreign GLB after first rename")
            elif mutation == "glb-symlink":
                case["final_glb"].unlink()
                case["final_glb"].symlink_to(foreign.name)
            elif mutation == "json-bytes":
                case["staged_json"].write_bytes(b"foreign JSON before second rename")
            elif not mutation.startswith(("glb-post-pair-", "json-post-pair-")):
                raise AssertionError(f"unknown between-rename mutation {mutation}")
            return result
        if source_path == case["staged_json"] and destination_path == case["final_json"]:
            second_reached = True
            result = real_replace(source_path, destination_path)
            if mutation == "glb-post-pair-bytes":
                case["final_glb"].write_bytes(b"foreign GLB after complete rename pair")
            elif mutation == "json-post-pair-bytes":
                case["final_json"].write_bytes(b"foreign JSON after complete rename pair")
            elif mutation == "glb-post-pair-symlink-same":
                case["final_glb"].unlink()
                case["final_glb"].symlink_to(same_bytes_referent.name)
            elif mutation == "json-post-pair-symlink-same":
                case["final_json"].unlink()
                case["final_json"].symlink_to(same_bytes_referent.name)
            elif mutation == "glb-post-pair-hardlink-same":
                case["final_glb"].unlink()
                os.link(same_bytes_referent, case["final_glb"])
            elif mutation == "json-post-pair-hardlink-same":
                case["final_json"].unlink()
                os.link(same_bytes_referent, case["final_json"])
            return result
        return real_replace(source_path, destination_path)

    caught = None
    with mock.patch.object(module.os, "replace", new=replacing):
        try:
            module.promote_pair(
                case["staged_glb"],
                case["staged_json"],
                case["final_glb"],
                case["final_json"],
                False,
            )
        except BaseException as exc:
            caught = exc
    findings = []
    if not first_reached or not second_reached:
        findings.append(
            f"{name}: mutation did not span both renames: "
            f"first={first_reached} second={second_reached}"
        )
    if caught is None:
        findings.append(f"{name}: foreign pair returned success")
    elif not isinstance(caught, module.DecimationError):
        findings.append(
            f"{name}: post-promotion verification raised "
            f"{type(caught).__name__}, not DecimationError"
        )
    if os.path.lexists(case["final_glb"]) or os.path.lexists(case["final_json"]):
        findings.append(f"{name}: failed post-promotion verification left a final member")
    actual = set(case["directory"].iterdir())
    staged_pair = {case["staged_glb"], case["staged_json"]}
    if frozenset(actual) not in {
        frozenset(permitted), frozenset(permitted | staged_pair)
    }:
        findings.append(
            f"{name}: unexpected post-verification residue: "
            f"{sorted(path.name for path in actual)}"
        )
    if actual == permitted | staged_pair:
        for path in staged_pair:
            if not lstat_regular_single(path):
                findings.append(f"{name}: retired staged member is not regular/single-link")
        if not regular_payload(case["staged_glb"], case["glb_bytes"]):
            findings.append(f"{name}: retired staged GLB is not the exact candidate")
        if not regular_payload(case["staged_json"], case["json_bytes"]):
            findings.append(f"{name}: retired staged JSON is not the exact candidate")
    if foreign.read_bytes() != b"foreign bytes that are never a candidate":
        findings.append(f"{name}: foreign referent changed")
    if same_bytes_referent is not None:
        member = "glb" if mutation.startswith("glb-") else "json"
        if not regular_payload(same_bytes_referent, case[f"{member}_bytes"]):
            findings.append(f"{name}: same-byte referent lost exact custody")
    return findings


def exercise_persistent_unlink():
    name = "second-rename-persistent-final-unlink"
    case = new_case(name)
    case["staged_glb"].write_bytes(case["glb_bytes"])
    case["staged_json"].write_bytes(case["json_bytes"])
    real_replace = os.replace
    real_unlink = os.unlink
    real_remove = os.remove
    real_path_unlink = Path.unlink
    second_reached = False
    unlink_attempts = 0

    def replacing(source, destination):
        nonlocal second_reached
        source_path = Path(source)
        destination_path = Path(destination)
        if source_path == case["staged_json"] and destination_path == case["final_json"]:
            second_reached = True
            raise OSError("injected second rename failure before effect")
        return real_replace(source_path, destination_path)

    def unlinking(path, *args, **kwargs):
        nonlocal unlink_attempts
        candidate = Path(path)
        if candidate == case["final_glb"]:
            unlink_attempts += 1
            if unlink_attempts > 16:
                raise AssertionError("unbounded persistent final-GLB unlink retry")
            raise OSError("injected persistent final-GLB unlink failure")
        return real_unlink(path, *args, **kwargs)

    def removing(path, *args, **kwargs):
        candidate = Path(path)
        if candidate == case["final_glb"]:
            return unlinking(path, *args, **kwargs)
        return real_remove(path, *args, **kwargs)

    def path_unlinking(self, *args, **kwargs):
        if Path(self) == case["final_glb"]:
            return unlinking(self, *args, **kwargs)
        return real_path_unlink(self, *args, **kwargs)

    caught = None
    with (
        mock.patch.object(module.os, "replace", new=replacing),
        mock.patch.object(module.os, "unlink", new=unlinking),
        mock.patch.object(module.os, "remove", new=removing),
        mock.patch.object(Path, "unlink", new=path_unlinking),
    ):
        try:
            module.promote_pair(
                case["staged_glb"],
                case["staged_json"],
                case["final_glb"],
                case["final_json"],
                False,
            )
        except BaseException as exc:
            caught = exc
    findings = []
    if caught is None:
        findings.append(f"{name}: second-rename failure was swallowed")
    elif not isinstance(caught, (OSError, module.DecimationError)):
        findings.append(
            f"{name}: recovery raised unexpected {type(caught).__name__}"
        )
    if not second_reached:
        findings.append(f"{name}: second rename injection was not reached")
    if unlink_attempts > 16:
        findings.append(f"{name}: unlink retries exceeded the explicit bound")
    if os.path.lexists(case["final_glb"]) or os.path.lexists(case["final_json"]):
        findings.append(f"{name}: terminal still contains a final member")
    actual = set(case["directory"].iterdir())
    payloads = []
    for path in actual:
        if not lstat_regular_single(path):
            payloads.append(None)
        else:
            payloads.append(path.read_bytes())
    if (
        len(actual) != 2
        or sorted(payloads, key=lambda value: b"" if value is None else value)
        != sorted((case["glb_bytes"], case["json_bytes"]))
    ):
        findings.append(
            f"{name}: terminal must contain exactly the privately retired candidate pair; "
            f"found {sorted(path.name for path in actual)}"
        )
    return findings


def exercise_force_postrename_alias(name, member, kind, rollback):
    """Race force verification with a same-byte alias after a real rename."""
    case = new_case(name)
    case["old_glb_bytes"] = f"old GLB for {name}".encode()
    case["old_json_bytes"] = f"old JSON for {name}".encode()
    case["staged_glb"].write_bytes(case["glb_bytes"])
    case["staged_json"].write_bytes(case["json_bytes"])
    case["final_glb"].write_bytes(case["old_glb_bytes"])
    case["final_json"].write_bytes(case["old_json_bytes"])
    backups = {
        "glb": case["directory"] / ".old-glb",
        "json": case["directory"] / ".old-json",
    }
    attacked_final = case[f"final_{member}"]
    attacked_payload = (
        case[f"old_{member}_bytes"] if rollback else case[f"{member}_bytes"]
    )
    referent = case["directory"] / f"attacker-{member}-{kind}"
    referent.write_bytes(attacked_payload)
    real_replace = os.replace
    primary_reached = False
    alias_injections = 0

    def unique_backup(path):
        candidate = Path(path)
        if candidate == case["final_glb"]:
            return backups["glb"]
        if candidate == case["final_json"]:
            return backups["json"]
        raise AssertionError(f"{name}: unexpected backup source {candidate}")

    def install_alias():
        nonlocal alias_injections
        attacked_final.unlink(missing_ok=True)
        if kind == "symlink":
            attacked_final.symlink_to(referent.name)
        elif kind == "hardlink":
            os.link(referent, attacked_final)
        else:
            raise AssertionError(f"{name}: unsupported alias kind {kind}")
        alias_injections += 1

    def replacing(source, destination):
        nonlocal primary_reached
        source_path = Path(source)
        destination_path = Path(destination)
        result = real_replace(source_path, destination_path)
        completed_candidate_pair = (
            source_path == case["staged_json"]
            and destination_path == case["final_json"]
        )
        restoring_attacked_member = (
            rollback
            and primary_reached
            and destination_path == attacked_final
        )
        if restoring_attacked_member:
            # Persistent by construction: every restore attempt is raced.
            install_alias()
        if completed_candidate_pair:
            if rollback and not primary_reached:
                primary_reached = True
                raise OSError("injected force promotion failure after second rename")
            if not rollback and alias_injections == 0:
                install_alias()
        return result

    caught = None
    with (
        mock.patch.object(module, "_unique_backup", new=unique_backup),
        mock.patch.object(module.os, "replace", new=replacing),
    ):
        try:
            module.promote_pair(
                case["staged_glb"],
                case["staged_json"],
                case["final_glb"],
                case["final_json"],
                True,
            )
        except BaseException as exc:
            caught = exc

    findings = []
    if alias_injections < 1:
        findings.append(f"{name}: post-rename alias mutation was not reached")
    if rollback:
        if not primary_reached:
            findings.append(f"{name}: force rollback trigger was not reached")
        if caught is None:
            findings.append(f"{name}: injected force rollback failure was swallowed")
        elif not isinstance(caught, (OSError, module.DecimationError)):
            findings.append(f"{name}: force rollback raised {type(caught).__name__}")
        expected_payloads = {
            "glb": case["old_glb_bytes"],
            "json": case["old_json_bytes"],
        }
    elif caught is None:
        expected_payloads = {
            "glb": case["glb_bytes"],
            "json": case["json_bytes"],
        }
    elif isinstance(caught, module.DecimationError):
        expected_payloads = {
            "glb": case["old_glb_bytes"],
            "json": case["old_json_bytes"],
        }
    else:
        findings.append(
            f"{name}: successful force race raised unexpected {type(caught).__name__}"
        )
        expected_payloads = {
            "glb": case["old_glb_bytes"],
            "json": case["old_json_bytes"],
        }

    final_pair = {case["final_glb"], case["final_json"]}
    backup_pair = {backups["glb"], backups["json"]}
    if all(os.path.lexists(path) for path in final_pair):
        terminal = {"glb": case["final_glb"], "json": case["final_json"]}
    elif (
        rollback
        and not any(os.path.lexists(path) for path in final_pair)
        and all(os.path.lexists(path) for path in backup_pair)
    ):
        terminal = {"glb": backups["glb"], "json": backups["json"]}
    else:
        terminal = {}
        findings.append(f"{name}: terminal pair is split or missing")

    for terminal_member, path in terminal.items():
        expected = expected_payloads[terminal_member]
        if not regular_payload(path, expected):
            findings.append(
                f"{name}: terminal {terminal_member} is not regular, single-link, "
                "and the exact expected hash"
            )
    expected_membership = set(terminal.values()) | {referent}
    actual_membership = set(case["directory"].iterdir())
    if actual_membership != expected_membership:
        findings.append(
            f"{name}: unexpected force terminal residue: "
            f"{sorted(path.name for path in actual_membership)}"
        )
    if not regular_payload(referent, attacked_payload):
        findings.append(f"{name}: attacker referent was modified or retained a link")
    return findings


def child(sender, scenario, arguments):
    try:
        if scenario == "control":
            exercise_compliant_control()
            findings = []
        elif scenario == "staged-type":
            findings = exercise_staged_type(*arguments)
        elif scenario == "between-renames":
            findings = exercise_between_renames(*arguments)
        elif scenario == "persistent-unlink":
            findings = exercise_persistent_unlink()
        elif scenario == "force-alias":
            findings = exercise_force_postrename_alias(*arguments)
        else:
            raise AssertionError(f"unknown promotion scenario {scenario}")
        sender.send(("result", findings))
    except BaseException:
        sender.send(("crash", traceback.format_exc()))
    finally:
        sender.close()


def run_bounded(scenario, arguments, label):
    receiver, sender = process_context.Pipe(duplex=False)
    process = process_context.Process(
        target=child,
        args=(sender, scenario, arguments),
        name=f"promotion-custody-{label}",
        daemon=True,
    )
    process.start()
    sender.close()
    process.join(4)
    if process.is_alive():
        process.terminate()
        process.join(2)
        if process.is_alive():
            process.kill()
            process.join(2)
        errors.append(f"{label}: scenario exceeded four-second bound")
    payload = None
    if receiver.poll():
        try:
            payload = receiver.recv()
        except EOFError:
            payload = None
    receiver.close()
    if payload is None:
        if not any(error.startswith(f"{label}:") for error in errors):
            errors.append(f"{label}: child exited {process.exitcode} without evidence")
    else:
        kind, value = payload
        if kind == "result":
            errors.extend(value)
        else:
            errors.append(f"{label}: child crashed:\n{value}")
    process.close()


run_bounded("control", (), "compliant-regular-single-link")
for staged_kind in ("symlink", "hardlink", "fifo", "directory"):
    for staged_member in ("glb", "json"):
        label = f"staged-{staged_member}-{staged_kind}"
        run_bounded(
            "staged-type",
            (label, staged_member, staged_kind),
            label,
        )
for mutation in (
    "glb-bytes",
    "glb-symlink",
    "json-bytes",
    "glb-post-pair-bytes",
    "json-post-pair-bytes",
    "glb-post-pair-symlink-same",
    "json-post-pair-symlink-same",
    "glb-post-pair-hardlink-same",
    "json-post-pair-hardlink-same",
):
    label = f"between-renames-{mutation}"
    run_bounded("between-renames", (label, mutation), label)
run_bounded("persistent-unlink", (), "second-rename-persistent-final-unlink")
for force_rollback in (False, True):
    force_mode = "persistent-rollback" if force_rollback else "success"
    for alias_kind in ("symlink", "hardlink"):
        for alias_member in ("glb", "json"):
            label = (
                f"force-{force_mode}-postrename-{alias_member}-{alias_kind}-same-bytes"
            )
            run_bounded(
                "force-alias",
                (label, alias_member, alias_kind, force_rollback),
                label,
            )

if errors:
    raise AssertionError("promotion custody hardening regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = I ]; then
  printf 'glb-decimation review I: pass\n'
  exit 0
fi

# Review hardening J: validated source bytes cannot remain a mutable pathname
# contract. Freeze a private, no-follow, read-only, single-link source+sidecar
# snapshot before any child executable runs, pass the source snapshot to Blender,
# and retain original lineage in provenance. Child hygiene is an explicit minimal
# name allowlist with private HOME/XDG/temp directories. This is environment
# isolation only; the test deliberately makes no OS network-sandbox claim.
if [ "$review_section" = all ] || [ "$review_section" = J ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-source-environment" "$repo" \
    "$fake_blender" <<'PY'
import contextlib
import hashlib
import importlib.util
import io
import json
import multiprocessing
import os
import shutil
import stat
import subprocess
import sys
import threading
import traceback
from pathlib import Path
from unittest import mock


script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from glb_fixture import write_glb


errors = []


def check(condition, message):
    if not condition:
        errors.append(message)


def digest_bytes(value):
    return hashlib.sha256(value).hexdigest()


def digest(path):
    return digest_bytes(path.read_bytes())


def write_source_sidecar(source, prompt):
    sidecar = Path(f"{source}.json")
    sidecar.write_text(json.dumps({
        "service": "meshy",
        "task_id": "fixture-meshy-task",
        "timestamp_utc": "2026-08-15T12:34:56Z",
        "plan_tier": "paid",
        "prompt": prompt,
        "note": "trusted local fixture",
        "sha256": digest(source),
    }, sort_keys=True) + "\n", encoding="utf-8")
    return sidecar


def write_manifest(path, filename, prompt):
    path.write_text(json.dumps({"assets": [{
        "id": "snapshot-cat",
        "kind": "cat",
        "service": "meshy",
        "out": filename,
        "prompt": prompt,
    }]}, sort_keys=True) + "\n", encoding="utf-8")


def cli_arguments(manifest, input_dir, output_dir):
    return [
        sys.executable,
        str(script),
        "--manifest", str(manifest),
        "--input-dir", str(input_dir),
        "--output-dir", str(output_dir),
        "--blender", str(fake_blender),
    ]


def regular_private_snapshot(observation, trusted_sha, original, label):
    check(
        Path(observation["source"]) != original,
        f"{label}: Blender received the mutable original source path",
    )
    check(
        observation["source_sha256"] == trusted_sha,
        f"{label}: Blender did not read the trusted source snapshot",
    )
    check(
        observation["source_lstat_regular"] is True
        and observation["source_lstat_symlink"] is False
        and observation["source_nlink"] == 1,
        f"{label}: Blender source was not lstat-regular/single-link: {observation}",
    )
    check(
        observation["source_mode"] & 0o222 == 0,
        f"{label}: Blender source snapshot remained writable: "
        f"{observation['source_mode']:o}",
    )
    check(
        observation.get("source_uid") == os.getuid(),
        f"{label}: Blender source snapshot is not owned by the invoking user",
    )
    check(
        observation["source_parent_mode"] == 0o700
        and observation.get("source_parent_uid") == os.getuid(),
        f"{label}: Blender source snapshot parent was not owned mode-0700: "
        f"mode={observation['source_parent_mode']:o} "
        f"uid={observation.get('source_parent_uid')!r}",
    )


def require_snapshot_cleanup(observation, original, label):
    snapshot = Path(observation["source"])
    if snapshot == original:
        return
    check(not os.path.lexists(snapshot), f"{label}: source snapshot file leaked")
    check(
        not os.path.lexists(snapshot.parent),
        f"{label}: source snapshot tree leaked",
    )


# J1: the fake swaps the original to a different valid GLB, reads the exact
# --source argument, then restores the original before the orchestrator's final
# custody check. Secure code succeeds only because Blender was given the frozen
# copy. Current original-path code also returns zero, so the observed hash/path
# assertions are the discriminating oracle.
swap_root = root / "swap-back-cli"
swap_input = swap_root / "input"
swap_output = swap_root / "output"
swap_input.mkdir(parents=True)
swap_output.mkdir()
swap_source = swap_input / "asset.glb"
swap_payload = swap_root / "foreign.glb"
swap_manifest = swap_root / "manifest.json"
swap_log = swap_root / "fake.log"
swap_audit = swap_root / "fake.audit"
write_glb(swap_source, triangles=30000)
write_glb(swap_payload, triangles=31000, translation=(0.125, 0.0, 0.0))
swap_sidecar = write_source_sidecar(swap_source, "snapshot fixture cat")
write_manifest(swap_manifest, swap_source.name, "snapshot fixture cat")
trusted_source_bytes = swap_source.read_bytes()
trusted_sidecar_bytes = swap_sidecar.read_bytes()
trusted_source_sha = digest_bytes(trusted_source_bytes)
foreign_sha = digest(swap_payload)
check(foreign_sha != trusted_source_sha, "swap-back fixture hashes unexpectedly match")
swap_environment = os.environ.copy()
swap_environment.update({
    "FAKE_BLENDER_MODE": "success",
    "FAKE_BLENDER_LOG": str(swap_log),
    "FAKE_BLENDER_AUDIT": str(swap_audit),
    "FAKE_BLENDER_SWAP_PATH": str(swap_source),
    "FAKE_BLENDER_SWAP_PAYLOAD_PATH": str(swap_payload),
})
try:
    swap_result = subprocess.run(
        cli_arguments(swap_manifest, swap_input, swap_output),
        check=False,
        capture_output=True,
        text=True,
        timeout=20,
        env=swap_environment,
    )
except subprocess.TimeoutExpired as exc:
    raise AssertionError("source swap-back CLI exceeded 20-second bound") from exc
check(
    swap_result.returncode == 0,
    "source swap-back run did not safely neutralize the attack: "
    f"stdout={swap_result.stdout!r} stderr={swap_result.stderr!r}",
)
check(swap_source.read_bytes() == trusted_source_bytes, "swap-back changed source bytes")
check(swap_sidecar.read_bytes() == trusted_sidecar_bytes, "swap-back changed sidecar bytes")
swap_records = (
    [json.loads(line) for line in swap_log.read_text(encoding="utf-8").splitlines()]
    if swap_log.exists() else []
)
check(len(swap_records) == 1, f"swap-back fake records differ: {swap_records}")
if len(swap_records) == 1:
    observation = swap_records[0]
    check(
        observation["source_swap_performed"] is True,
        "source swap-back: fake mutation controls were stripped",
    )
    regular_private_snapshot(
        observation, trusted_source_sha, swap_source, "source swap-back"
    )
    require_snapshot_cleanup(observation, swap_source, "source swap-back")
    check(
        observation["source_sha256"] != foreign_sha,
        "source swap-back: Blender consumed the foreign source bytes",
    )
check(
    swap_audit.exists()
    and swap_audit.read_text(encoding="utf-8").splitlines() == ["version", "asset"],
    "source swap-back did not exercise both fake Blender phases",
)
final_glb = swap_output / swap_source.name
final_json = Path(f"{final_glb}.json")
check(final_glb.is_file() and final_json.is_file(), "source swap-back lacks final pair")
if final_json.is_file():
    proof = json.loads(final_json.read_text(encoding="utf-8"))
    check(
        proof["source"]["filename"] == swap_source.name
        and proof["source"]["sha256"] == trusted_source_sha
        and proof["source"]["sidecar_sha256"] == digest_bytes(trusted_sidecar_bytes),
        f"source swap-back provenance lost original lineage: {proof.get('source')!r}",
    )
check(
    set(swap_output.iterdir()) == {final_glb, final_json},
    "source swap-back left snapshot/staging residue in output",
)


# J2: swap both originals at the version-executable seam, restore them at the
# processing boundary, and then inspect the prepared pair. A late snapshot has
# frozen the foreign bytes; an original-path implementation instead fails the
# private-path oracle. Only a pair frozen before *any* child executable passes.
spec = importlib.util.spec_from_file_location(
    "decimate_assets_snapshot_seam_test", script
)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
process_context = multiprocessing.get_context("fork")


def exercise_snapshot_seam(case_name, *, hang, fail_process, progress):
    case_errors = []

    def require(condition, message):
        if not condition:
            case_errors.append(message)

    seam_root = root / case_name
    seam_input = seam_root / "input"
    seam_output = seam_root / "output"
    seam_input.mkdir(parents=True)
    seam_output.mkdir()
    seam_source = seam_input / "asset.glb"
    seam_manifest = seam_root / "manifest.json"
    write_glb(seam_source, triangles=30000)
    seam_sidecar = write_source_sidecar(seam_source, "pre-version fixture cat")
    write_manifest(seam_manifest, seam_source.name, "pre-version fixture cat")
    seam_source_bytes = seam_source.read_bytes()
    seam_sidecar_bytes = seam_sidecar.read_bytes()
    malicious_source_bytes = b"foreign source present only across version seam"
    malicious_sidecar_bytes = b"foreign sidecar present only across version seam"
    seam_observation = {}
    version_seam_reached = False

    def mutating_version_check(_blender, _child_env):
        nonlocal version_seam_reached
        version_seam_reached = True
        seam_source.write_bytes(malicious_source_bytes)
        seam_sidecar.write_bytes(malicious_sidecar_bytes)
        progress.set()
        if hang:
            threading.Event().wait()

    def observing_process(_asset, prepared, *_arguments):
        # Model the attacker's swap-back before Blender consumes its source.
        seam_source.write_bytes(seam_source_bytes)
        seam_sidecar.write_bytes(seam_sidecar_bytes)
        source_snapshot = Path(prepared["source_path"])
        sidecar_snapshot = Path(prepared["source_sidecar_path"])
        for member_name, path, trusted in (
            ("source", source_snapshot, seam_source_bytes),
            ("sidecar", sidecar_snapshot, seam_sidecar_bytes),
        ):
            status = os.lstat(path)
            seam_observation[member_name] = {
                "path": path,
                "bytes": path.read_bytes(),
                "regular": stat.S_ISREG(status.st_mode),
                "symlink": stat.S_ISLNK(status.st_mode),
                "nlink": status.st_nlink,
                "mode": stat.S_IMODE(status.st_mode),
                "uid": status.st_uid,
                "parent_mode": stat.S_IMODE(os.lstat(path.parent).st_mode),
                "parent_uid": os.lstat(path.parent).st_uid,
                "trusted": trusted,
            }
        seam_observation["prepared_source_sha"] = prepared["source_sha"]
        seam_observation["prepared_sidecar_sha"] = prepared["source_sidecar_sha"]
        if fail_process:
            raise module.DecimationError("injected processing failure after snapshot observation")

    seam_stdout = io.StringIO()
    seam_stderr = io.StringIO()
    seam_result = None
    try:
        with (
            mock.patch.object(
                module, "_check_blender_version", new=mutating_version_check
            ),
            mock.patch.object(module, "_process_asset", new=observing_process),
            contextlib.redirect_stdout(seam_stdout),
            contextlib.redirect_stderr(seam_stderr),
        ):
            seam_result = module.main([
                "--manifest", str(seam_manifest),
                "--input-dir", str(seam_input),
                "--output-dir", str(seam_output),
                "--blender", str(fake_blender),
            ])
    finally:
        seam_source.write_bytes(seam_source_bytes)
        seam_sidecar.write_bytes(seam_sidecar_bytes)

    expected_result = 1 if fail_process else 0
    require(
        seam_result == expected_result,
        f"pre-version snapshot seam returned {seam_result}, expected {expected_result}",
    )
    require(version_seam_reached, "pre-version mutation seam was not reached")
    require(
        set(seam_observation) >= {"source", "sidecar"},
        "snapshot pair was not observed",
    )
    for member_name, original, trusted in (
        ("source", seam_source, seam_source_bytes),
        ("sidecar", seam_sidecar, seam_sidecar_bytes),
    ):
        observation = seam_observation.get(member_name)
        if observation is None:
            continue
        snapshot = Path(observation["path"])
        require(
            snapshot != original,
            f"{member_name} snapshot reused mutable original path",
        )
        require(
            observation["bytes"] == trusted,
            f"{member_name} snapshot captured foreign seam bytes",
        )
        require(
            observation["regular"] and not observation["symlink"]
            and observation["nlink"] == 1,
            f"{member_name} snapshot is not lstat-regular/single-link: {observation}",
        )
        require(
            observation["mode"] & 0o222 == 0,
            f"{member_name} snapshot remained writable",
        )
        require(
            observation["uid"] == os.getuid(),
            f"{member_name} snapshot is not owned by the invoking user",
        )
        require(
            observation["parent_mode"] == 0o700
            and observation["parent_uid"] == os.getuid(),
            f"{member_name} snapshot parent is not owned mode-0700",
        )
        if snapshot != original:
            require(
                not os.path.lexists(snapshot),
                f"{member_name} snapshot file leaked after completion",
            )
            require(
                not os.path.lexists(snapshot.parent),
                f"{member_name} snapshot tree leaked after completion",
            )
    require(
        seam_observation.get("prepared_source_sha")
        == digest_bytes(seam_source_bytes),
        "prepared source hash is not the trusted original hash",
    )
    require(
        seam_observation.get("prepared_sidecar_sha")
        == digest_bytes(seam_sidecar_bytes),
        "prepared sidecar hash is not the trusted original hash",
    )
    require(seam_source.read_bytes() == seam_source_bytes, "seam changed source")
    require(seam_sidecar.read_bytes() == seam_sidecar_bytes, "seam changed sidecar")
    require(list(seam_output.iterdir()) == [], "snapshot seam left output residue")
    return case_errors


def snapshot_seam_child(sender, progress, case_name, hang, fail_process):
    try:
        findings = exercise_snapshot_seam(
            case_name,
            hang=hang,
            fail_process=fail_process,
            progress=progress,
        )
        sender.send(("result", findings))
    except BaseException:
        sender.send(("crash", traceback.format_exc()))
    finally:
        sender.close()


def stop_snapshot_child(process):
    process.terminate()
    process.join(2)
    if process.is_alive():
        process.kill()
        process.join(2)
    check(not process.is_alive(), "snapshot seam child could not be terminated")


def run_snapshot_seam_bounded(
    case_name, *, expect_hang=False, expect_failure=False
):
    receiver, sender = process_context.Pipe(duplex=False)
    progress = process_context.Event()
    process = process_context.Process(
        target=snapshot_seam_child,
        args=(sender, progress, case_name, expect_hang, expect_failure),
        name=f"snapshot-seam-{case_name}",
        daemon=True,
    )
    process.start()
    sender.close()
    if expect_hang:
        reached = progress.wait(4)
        check(reached, "snapshot hang mutation did not reach the version seam")
        if reached:
            process.join(0.25)
            check(process.is_alive(), "snapshot hang mutation unexpectedly returned")
        if process.is_alive():
            stop_snapshot_child(process)
    else:
        process.join(10)
        if process.is_alive():
            stop_snapshot_child(process)
            errors.append("snapshot seam exceeded its ten-second bound")

    payload = None
    if receiver.poll():
        try:
            payload = receiver.recv()
        except EOFError:
            payload = None
    receiver.close()
    if not expect_hang:
        if payload is None:
            errors.append(
                f"snapshot seam child exited {process.exitcode} without evidence"
            )
        else:
            result_kind, value = payload
            if result_kind == "result":
                errors.extend(value)
            else:
                errors.append(f"snapshot seam child crashed:\n{value}")
    process.close()
    if expect_hang:
        hang_root = root / case_name
        if hang_root.exists():
            shutil.rmtree(hang_root)


run_snapshot_seam_bounded("pre-version-snapshot")
run_snapshot_seam_bounded(
    "pre-version-snapshot-failure", expect_failure=True
)
run_snapshot_seam_bounded("pre-version-snapshot-hang", expect_hang=True)


# J2b: replace the original sidecar with a different *valid* record before real
# asset processing starts, keep it live throughout processing, then restore it.
# Secure code reads only its frozen sidecar snapshot and emits the original
# record without the test replacing its source_record argument. The companion
# mutation deliberately rereads the live original path, proving the lineage
# oracle fires against that vulnerable implementation shape.
def exercise_sidecar_provenance(case_name, *, late_reread):
    findings = []

    def require(condition, message):
        if not condition:
            findings.append(message)

    case_root = root / case_name
    input_dir = case_root / "input"
    output_dir = case_root / "output"
    input_dir.mkdir(parents=True)
    output_dir.mkdir()
    source = input_dir / "asset.glb"
    manifest = case_root / "manifest.json"
    fake_log = case_root / "fake.log"
    fake_audit = case_root / "fake.audit"
    write_glb(source, triangles=30000)
    sidecar = write_source_sidecar(source, "sidecar swap-back fixture cat")
    write_manifest(manifest, source.name, "sidecar swap-back fixture cat")
    source_bytes = source.read_bytes()
    sidecar_bytes = sidecar.read_bytes()
    original_record = json.loads(sidecar_bytes)
    alternate_record = dict(original_record)
    alternate_record.update({
        "task_id": "alternate-valid-task",
        "timestamp_utc": "2026-08-15T13:14:15Z",
        "note": "valid alternate visible only during the provenance seam",
    })
    alternate_bytes = (
        json.dumps(alternate_record, sort_keys=True) + "\n"
    ).encode("utf-8")
    asset = {
        "id": "snapshot-cat",
        "kind": "cat",
        "service": "meshy",
        "out": source.name,
        "prompt": "sidecar swap-back fixture cat",
    }
    # The alternate is not malformed bait: it satisfies the real validator.
    module._validate_source_record(alternate_record, asset, digest(source))
    real_process_asset = module._process_asset
    real_provenance = module._provenance_record
    attack_reached = False
    late_reread_reached = False
    mutation_snapshot_root = case_root / "mutation-sidecar-snapshot"
    mutation_sidecar_snapshot = mutation_snapshot_root / sidecar.name
    if late_reread:
        mutation_snapshot_root.mkdir(mode=0o700)
        mutation_sidecar_snapshot.write_bytes(sidecar_bytes)
        mutation_sidecar_snapshot.chmod(0o400)

    def attacking_process_asset(*args, **kwargs):
        nonlocal attack_reached
        attack_reached = True
        sidecar.write_bytes(alternate_bytes)
        try:
            positional = list(args)
            named = dict(kwargs)
            if late_reread:
                # Isolate the mutation to a late provenance reread even against
                # today's original-path implementation: model only the fixed
                # sidecar-path snapshot boundary, leaving source_record intact.
                if len(positional) >= 2:
                    prepared = dict(positional[1])
                    prepared["source_sidecar_path"] = mutation_sidecar_snapshot
                    positional[1] = prepared
                elif "prepared" in named:
                    prepared = dict(named["prepared"])
                    prepared["source_sidecar_path"] = mutation_sidecar_snapshot
                    named["prepared"] = prepared
                else:
                    raise AssertionError("process prepared argument is missing")
            return real_process_asset(*positional, **named)
        finally:
            sidecar.write_bytes(sidecar_bytes)

    def late_reread_provenance(*args, **kwargs):
        nonlocal late_reread_reached
        late_reread_reached = True
        live_record = json.loads(sidecar.read_text(encoding="utf-8"))
        positional = list(args)
        named = dict(kwargs)
        if len(positional) >= 4:
            positional[3] = live_record
        elif "source_record" in named:
            named["source_record"] = live_record
        else:
            raise AssertionError("provenance source_record argument is missing")
        return real_provenance(*positional, **named)

    environment = {
        "FAKE_BLENDER_MODE": "success",
        "FAKE_BLENDER_LOG": str(fake_log),
        "FAKE_BLENDER_AUDIT": str(fake_audit),
    }
    stdout = io.StringIO()
    stderr = io.StringIO()
    provenance_patch = (
        mock.patch.object(
            module, "_provenance_record", new=late_reread_provenance
        )
        if late_reread
        else contextlib.nullcontext()
    )
    try:
        with (
            mock.patch.dict(os.environ, environment, clear=False),
            mock.patch.object(
                module, "_process_asset", new=attacking_process_asset
            ),
            provenance_patch,
            contextlib.redirect_stdout(stdout),
            contextlib.redirect_stderr(stderr),
        ):
            result = module.main([
                "--manifest", str(manifest),
                "--input-dir", str(input_dir),
                "--output-dir", str(output_dir),
                "--blender", str(fake_blender),
            ])
    finally:
        sidecar.write_bytes(sidecar_bytes)
        if mutation_snapshot_root.exists():
            shutil.rmtree(mutation_snapshot_root)

    final_glb = output_dir / source.name
    final_json = Path(f"{final_glb}.json")
    proof = None
    if final_json.is_file():
        proof = json.loads(final_json.read_text(encoding="utf-8"))
    expected_provenance = {
        name: original_record[name]
        for name in sorted(module.REQUIRED_SOURCE_FIELDS - {"sha256"})
    }
    alternate_provenance = {
        name: alternate_record[name]
        for name in sorted(module.REQUIRED_SOURCE_FIELDS - {"sha256"})
    }
    lineage_is_original = bool(
        isinstance(proof, dict)
        and proof.get("source", {}).get("sidecar_sha256")
        == digest_bytes(sidecar_bytes)
        and proof.get("source", {}).get("provenance") == expected_provenance
    )
    mutation_emitted_live_record = bool(
        isinstance(proof, dict)
        and proof.get("source", {}).get("sidecar_sha256")
        == digest_bytes(sidecar_bytes)
        and proof.get("source", {}).get("provenance") == alternate_provenance
    )
    require(attack_reached, f"{case_name}: sidecar swap-back seam was not reached")
    if late_reread:
        require(
            late_reread_reached,
            f"{case_name}: late original-path reread mutation was not reached",
        )
        require(
            not os.path.lexists(mutation_snapshot_root),
            f"{case_name}: mutation-control sidecar snapshot leaked",
        )
    require(source.read_bytes() == source_bytes, f"{case_name}: source changed")
    require(sidecar.read_bytes() == sidecar_bytes, f"{case_name}: sidecar changed")
    records = (
        [json.loads(line) for line in fake_log.read_text(encoding="utf-8").splitlines()]
        if fake_log.exists() else []
    )
    if not late_reread:
        require(result == 0, f"{case_name}: real processing returned {result}: {stderr.getvalue()!r}")
        require(
            lineage_is_original,
            f"{case_name}: final provenance lacks snapshotted original sidecar values",
        )
        require(
            set(output_dir.iterdir()) == {final_glb, final_json},
            f"{case_name}: real processing left unexpected output residue",
        )
        require(
            fake_audit.exists()
            and fake_audit.read_text(encoding="utf-8").splitlines()
            == ["version", "asset"],
            f"{case_name}: real processing missed a fake phase",
        )
        require(len(records) == 1, f"{case_name}: fake asset count differs")
        if len(records) == 1:
            observation = records[0]
            snapshot = Path(observation["source"])
            require(snapshot != source, f"{case_name}: fake received original source")
            require(
                observation["source_sha256"] == digest_bytes(source_bytes),
                f"{case_name}: fake source hash differs",
            )
            require(
                observation["source_lstat_regular"] is True
                and observation["source_lstat_symlink"] is False
                and observation["source_nlink"] == 1,
                f"{case_name}: fake source was not regular/single-link",
            )
            require(
                observation["source_mode"] & 0o222 == 0,
                f"{case_name}: fake source remained writable",
            )
            require(
                observation.get("source_uid") == os.getuid(),
                f"{case_name}: fake source was not owned by the invoking user",
            )
            require(
                observation["source_parent_mode"] == 0o700
                and observation.get("source_parent_uid") == os.getuid(),
                f"{case_name}: fake source parent was not owned mode-0700",
            )
            if snapshot != source:
                require(
                    not os.path.lexists(snapshot),
                    f"{case_name}: source snapshot file leaked",
                )
                require(
                    not os.path.lexists(snapshot.parent),
                    f"{case_name}: source snapshot tree leaked",
                )
    mutation_detected = (
        result == 0
        and late_reread_reached
        and mutation_emitted_live_record
        and not lineage_is_original
    )
    return {
        "findings": findings,
        "mutation_detected": mutation_detected,
        "result": result,
        "attack_reached": attack_reached,
        "late_reread_reached": late_reread_reached,
    }


def sidecar_provenance_child(sender, case_name, late_reread):
    try:
        sender.send((
            "result",
            exercise_sidecar_provenance(case_name, late_reread=late_reread),
        ))
    except BaseException:
        sender.send(("crash", traceback.format_exc()))
    finally:
        sender.close()


def run_sidecar_provenance_bounded(case_name, *, late_reread):
    receiver, sender = process_context.Pipe(duplex=False)
    process = process_context.Process(
        target=sidecar_provenance_child,
        args=(sender, case_name, late_reread),
        name=f"sidecar-provenance-{case_name}",
        daemon=True,
    )
    process.start()
    sender.close()
    process.join(20)
    if process.is_alive():
        stop_snapshot_child(process)
        errors.append(f"{case_name}: sidecar processing exceeded twenty seconds")
    payload = None
    if receiver.poll():
        try:
            payload = receiver.recv()
        except EOFError:
            payload = None
    receiver.close()
    if payload is None:
        errors.append(f"{case_name}: sidecar child exited without evidence")
    else:
        result_kind, value = payload
        if result_kind == "crash":
            errors.append(f"{case_name}: sidecar child crashed:\n{value}")
        else:
            errors.extend(value["findings"])
            if late_reread:
                if not value["attack_reached"]:
                    errors.append(f"{case_name}: late-reread mutation missed its seam")
                if not value["late_reread_reached"]:
                    errors.append(
                        f"{case_name}: late original-path reread was not exercised"
                    )
                if not value["mutation_detected"]:
                    errors.append(
                        f"{case_name}: late-reread mutation did not emit the live "
                        "alternate record for the lineage oracle to reject"
                    )
    process.close()


run_sidecar_provenance_bounded("sidecar-swap-back-control", late_reread=False)
run_sidecar_provenance_bounded("sidecar-late-reread-mutation", late_reread=True)


# J3: a controlled parent environment carries hazardous loader, Python,
# Blender-user, proxy, cloud-profile, credential, and unrelated variables. The
# fake records only child variable names and private-directory lstat facts.
env_root = root / "minimal-child-environment"
env_input = env_root / "input"
env_output = env_root / "output"
env_input.mkdir(parents=True)
env_output.mkdir()
env_source = env_input / "asset.glb"
env_manifest = env_root / "manifest.json"
env_log = env_root / "fake.log"
env_audit = env_root / "fake.audit"
env_capture = env_root / "environment.jsonl"
write_glb(env_source, triangles=30000)
env_sidecar = write_source_sidecar(env_source, "environment fixture cat")
write_manifest(env_manifest, env_source.name, "environment fixture cat")
env_source_bytes = env_source.read_bytes()
env_sidecar_bytes = env_sidecar.read_bytes()
hostile_home = env_root / "inherited-home"
hostile_tmp = env_root / "inherited-temp"
hostile_xdg = env_root / "inherited-xdg"
for directory in (hostile_home, hostile_tmp, hostile_xdg):
    directory.mkdir(mode=0o755)
parent_environment = {
    "PATH": os.environ["PATH"],
    "HOME": str(hostile_home),
    "TMPDIR": str(hostile_tmp),
    "TMP": str(hostile_tmp),
    "TEMP": str(hostile_tmp),
    "XDG_CONFIG_HOME": str(hostile_xdg),
    "LANG": "C.UTF-8",
    "FAKE_BLENDER_MODE": "success",
    "FAKE_BLENDER_LOG": str(env_log),
    "FAKE_BLENDER_AUDIT": str(env_audit),
    "FAKE_BLENDER_ENV_LOG": str(env_capture),
    "FAKE_BLENDER_VERSION": "5.1.2",
    "FAKE_BLENDER_BUILD_HASH": "ec6e62d40fa9",
    "LD_PRELOAD": "",
    "LD_LIBRARY_PATH": str(env_root / "loader"),
    "DYLD_INSERT_LIBRARIES": "",
    "DYLD_LIBRARY_PATH": str(env_root / "dyld"),
    "PYTHONPATH": str(env_root / "pythonpath"),
    "PYTHONSTARTUP": str(env_root / "startup.py"),
    "PYTHONBREAKPOINT": "0",
    "PYTHONUSERBASE": str(env_root / "python-user"),
    "PYTHONSAFEPATH": "1",
    "PYTHONNOUSERSITE": "1",
    "BLENDER_USER_CONFIG": str(env_root / "blender-config"),
    "BLENDER_USER_SCRIPTS": str(env_root / "blender-scripts"),
    "BLENDER_USER_DATAFILES": str(env_root / "blender-data"),
    "HTTP_PROXY": "http://127.0.0.1:9",
    "HTTPS_PROXY": "http://127.0.0.1:9",
    "ALL_PROXY": "socks5://127.0.0.1:9",
    "NO_PROXY": "*",
    "http_proxy": "http://127.0.0.1:9",
    "https_proxy": "http://127.0.0.1:9",
    "all_proxy": "socks5://127.0.0.1:9",
    "no_proxy": "*",
    "AWS_PROFILE": "hostile-profile",
    "AWS_CONFIG_FILE": str(env_root / "aws-config"),
    "AWS_ACCESS_KEY_ID": "dummy-access-key-never-log",
    "AWS_SECRET_ACCESS_KEY": "dummy-secret-key-never-log",
    "AWS_SESSION_TOKEN": "dummy-session-token-never-log",
    "CLOUDSDK_CONFIG": str(env_root / "gcloud"),
    "CLOUDSDK_AUTH_CREDENTIAL_FILE_OVERRIDE": str(env_root / "gcloud.json"),
    "AZURE_CONFIG_DIR": str(env_root / "azure"),
    "AZURE_CLIENT_ID": "dummy-client-id-never-log",
    "AZURE_CLIENT_SECRET": "dummy-client-secret-never-log",
    "KUBECONFIG": str(env_root / "kubeconfig"),
    "GOOGLE_APPLICATION_CREDENTIALS": str(env_root / "google.json"),
    "GOOGLE_API_KEY": "dummy-google-key-never-log",
    "GITHUB_TOKEN": "dummy-token-never-log",
    "UNRELATED_PARENT_SETTING": "must-not-cross-child-boundary",
}
try:
    env_result = subprocess.run(
        cli_arguments(env_manifest, env_input, env_output),
        check=False,
        capture_output=True,
        text=True,
        timeout=20,
        env=parent_environment,
    )
except subprocess.TimeoutExpired as exc:
    raise AssertionError("minimal child-environment CLI exceeded 20 seconds") from exc
check(
    env_result.returncode == 0,
    "minimal child-environment control failed: "
    f"stdout={env_result.stdout!r} stderr={env_result.stderr!r}",
)
environment_records = (
    [json.loads(line) for line in env_capture.read_text(encoding="utf-8").splitlines()]
    if env_capture.exists() else []
)
check(
    [record.get("phase") for record in environment_records] == ["version", "asset"],
    f"child environment phases differ: {environment_records}",
)
required_controls = {
    "FAKE_BLENDER_MODE",
    "FAKE_BLENDER_LOG",
    "FAKE_BLENDER_AUDIT",
    "FAKE_BLENDER_ENV_LOG",
    "FAKE_BLENDER_VERSION",
    "FAKE_BLENDER_BUILD_HASH",
}
required_private = {
    "HOME", "TMPDIR", "TMP", "TEMP", "XDG_CONFIG_HOME",
    "XDG_CACHE_HOME", "XDG_DATA_HOME", "XDG_STATE_HOME",
}
allowed_names = required_controls | required_private | {
    "PATH", "LANG", "LC_ALL", "LC_CTYPE", "__CF_USER_TEXT_ENCODING",
}
hazardous_names = {
    "LD_PRELOAD", "LD_LIBRARY_PATH", "DYLD_INSERT_LIBRARIES",
    "DYLD_LIBRARY_PATH", "PYTHONPATH", "PYTHONHOME", "PYTHONSTARTUP",
    "PYTHONINSPECT", "PYTHONWARNINGS", "PYTHONBREAKPOINT",
    "PYTHONUSERBASE", "PYTHONSAFEPATH", "PYTHONNOUSERSITE",
    "BLENDER_USER_CONFIG", "BLENDER_USER_SCRIPTS", "BLENDER_USER_DATAFILES",
    "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY",
    "http_proxy", "https_proxy", "all_proxy", "no_proxy",
    "AWS_PROFILE", "AWS_CONFIG_FILE", "CLOUDSDK_CONFIG", "AZURE_CONFIG_DIR",
    "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN",
    "CLOUDSDK_AUTH_CREDENTIAL_FILE_OVERRIDE", "AZURE_CLIENT_ID",
    "AZURE_CLIENT_SECRET", "KUBECONFIG", "GOOGLE_APPLICATION_CREDENTIALS",
    "GOOGLE_API_KEY", "GITHUB_TOKEN",
    "UNRELATED_PARENT_SETTING",
}
inherited_values = {str(hostile_home), str(hostile_tmp), str(hostile_xdg)}


def private_cleanup_findings(records, label):
    findings = []
    seen = set()
    for record in records:
        facts = list(record.get("private_paths", {}).values())
        for fact in facts:
            if not isinstance(fact, dict):
                continue
            path_value = fact.get("path")
            if not isinstance(path_value, str):
                continue
            if (
                fact.get("exists") is not True
                or fact.get("is_directory") is not True
                or fact.get("is_symlink") is not False
                or path_value in inherited_values
                or path_value in seen
            ):
                continue
            seen.add(path_value)
            if os.path.lexists(path_value):
                findings.append(f"{label}: private child tree leaked: {path_value}")
    return findings


for record in environment_records:
    names = set(record.get("names", []))
    check(required_controls <= names, f"{record.get('phase')}: fake controls were stripped")
    check(required_private <= names, f"{record.get('phase')}: private env homes are incomplete")
    check(not names & hazardous_names, f"{record.get('phase')}: hazardous vars crossed: {sorted(names & hazardous_names)}")
    check(
        names <= allowed_names,
        f"{record.get('phase')}: child env is not a minimal allowlist: "
        f"{sorted(names - allowed_names)}",
    )
    private_paths = record.get("private_paths", {})
    child_values = []
    for name in sorted(required_private):
        fact = private_paths.get(name)
        check(isinstance(fact, dict), f"{record.get('phase')}: missing private fact {name}")
        if not isinstance(fact, dict):
            continue
        check(
            fact.get("exists") is True and fact.get("is_directory") is True
            and fact.get("is_symlink") is False,
            f"{record.get('phase')}: {name} is not a real private directory: {fact}",
        )
        check(
            fact.get("mode") == 0o700 and fact.get("uid") == os.getuid(),
            f"{record.get('phase')}: {name} is not invoking-user-owned "
            f"mode-0700: {fact}",
        )
        child_values.append(fact.get("path"))
    check(
        all(value not in inherited_values for value in child_values),
        f"{record.get('phase')}: inherited HOME/XDG/temp was reused",
    )
errors.extend(private_cleanup_findings(environment_records, "environment success"))

leak_oracle = env_root / "oracle-leaked-private-tree"
leak_oracle.mkdir(mode=0o700)
leak_fact = {
    "path": str(leak_oracle),
    "exists": True,
    "is_directory": True,
    "is_symlink": False,
    "mode": 0o700,
    "uid": os.getuid(),
}
leak_findings = private_cleanup_findings(
    [{"private_paths": {"HOME": leak_fact}}],
    "cleanup-oracle",
)
check(bool(leak_findings), "private-tree cleanup oracle accepted a leaked directory")
shutil.rmtree(leak_oracle)
check(env_source.read_bytes() == env_source_bytes, "environment run changed source")
check(env_sidecar.read_bytes() == env_sidecar_bytes, "environment run changed sidecar")
check(
    env_audit.exists()
    and env_audit.read_text(encoding="utf-8").splitlines() == ["version", "asset"],
    "environment run missed a fake phase",
)
check(
    sorted(path.name for path in env_output.iterdir()) == ["asset.glb", "asset.glb.json"],
    "environment run left unexpected output/sandbox residue",
)
env_asset_records = (
    [json.loads(line) for line in env_log.read_text(encoding="utf-8").splitlines()]
    if env_log.exists() else []
)
check(len(env_asset_records) == 1, "environment run lacks one fake asset record")
if len(env_asset_records) == 1:
    regular_private_snapshot(
        env_asset_records[0], digest_bytes(env_source_bytes), env_source,
        "environment run",
    )
    require_snapshot_cleanup(env_asset_records[0], env_source, "environment run")
combined = env_result.stdout + env_result.stderr
for forbidden_value in (
    "dummy-token-never-log", "dummy-secret-key-never-log",
    "dummy-client-secret-never-log", "must-not-cross-child-boundary",
    "hostile-profile",
):
    check(forbidden_value not in combined, "environment run logged a hostile value")


# The same private roots and source snapshots are retired on an asset-process
# failure, not only after a successful promotion.
failure_root = root / "failure-private-cleanup"
failure_input = failure_root / "input"
failure_output = failure_root / "output"
failure_input.mkdir(parents=True)
failure_output.mkdir()
failure_source = failure_input / "asset.glb"
failure_manifest = failure_root / "manifest.json"
failure_log = failure_root / "fake.log"
failure_audit = failure_root / "fake.audit"
failure_capture = failure_root / "environment.jsonl"
write_glb(failure_source, triangles=30000)
failure_sidecar = write_source_sidecar(failure_source, "failure cleanup fixture cat")
write_manifest(failure_manifest, failure_source.name, "failure cleanup fixture cat")
failure_source_bytes = failure_source.read_bytes()
failure_sidecar_bytes = failure_sidecar.read_bytes()
failure_environment = dict(parent_environment)
failure_environment.update({
    "FAKE_BLENDER_MODE": "fail",
    "FAKE_BLENDER_LOG": str(failure_log),
    "FAKE_BLENDER_AUDIT": str(failure_audit),
    "FAKE_BLENDER_ENV_LOG": str(failure_capture),
})
try:
    failure_result = subprocess.run(
        cli_arguments(failure_manifest, failure_input, failure_output),
        check=False,
        capture_output=True,
        text=True,
        timeout=20,
        env=failure_environment,
    )
except subprocess.TimeoutExpired as exc:
    raise AssertionError("failure cleanup CLI exceeded 20 seconds") from exc
check(failure_result.returncode != 0, "failure cleanup control unexpectedly succeeded")
failure_environment_records = (
    [
        json.loads(line)
        for line in failure_capture.read_text(encoding="utf-8").splitlines()
    ]
    if failure_capture.exists() else []
)
check(
    [record.get("phase") for record in failure_environment_records]
    == ["version", "asset"],
    "failure cleanup missed a child environment phase",
)
for record in failure_environment_records:
    names = set(record.get("names", []))
    check(required_controls <= names, "failure cleanup stripped fake controls")
    check(required_private <= names, "failure cleanup lacks private env homes")
    check(not names & hazardous_names, "failure cleanup passed hazardous variables")
    check(names <= allowed_names, "failure cleanup child env is not minimal")
    for name in sorted(required_private):
        fact = record.get("private_paths", {}).get(name)
        check(
            isinstance(fact, dict)
            and fact.get("exists") is True
            and fact.get("is_directory") is True
            and fact.get("is_symlink") is False
            and fact.get("mode") == 0o700
            and fact.get("uid") == os.getuid(),
            f"failure cleanup {name} was not invoking-user-owned mode-0700 "
            "during the child",
        )
errors.extend(
    private_cleanup_findings(failure_environment_records, "environment failure")
)
failure_asset_records = (
    [json.loads(line) for line in failure_log.read_text(encoding="utf-8").splitlines()]
    if failure_log.exists() else []
)
check(len(failure_asset_records) == 1, "failure cleanup lacks one fake asset record")
if len(failure_asset_records) == 1:
    regular_private_snapshot(
        failure_asset_records[0], digest_bytes(failure_source_bytes),
        failure_source, "failure cleanup",
    )
    require_snapshot_cleanup(
        failure_asset_records[0], failure_source, "failure cleanup"
    )
check(failure_source.read_bytes() == failure_source_bytes, "failure changed source")
check(
    failure_sidecar.read_bytes() == failure_sidecar_bytes,
    "failure changed source sidecar",
)
check(list(failure_output.iterdir()) == [], "failure left output/staging residue")


if errors:
    raise AssertionError("source/environment hardening regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = J ]; then
  printf 'glb-decimation review J: pass\n'
  exit 0
fi

# Review hardening K: every untrusted file is rejected by lstat before an
# unbounded read or child process. The current 15-file envelope leaves generous
# fixed ceilings (1 MiB metadata, 64 manifest entries, 128 MiB source GLB,
# 64 MiB derivative GLB). Output names and every CLI diagnostic are printable,
# one physical line, and bounded even when glTF extension/URI fields are hostile.
if [ "$review_section" = all ] || [ "$review_section" = K ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-preflight-diagnostics" "$repo" \
    "$fake_blender" <<'PY'
import builtins
import contextlib
import hashlib
import importlib.util
import io
import json
import mmap
import multiprocessing
import os
import re
import shutil
import stat
import struct
import subprocess
import sys
import traceback
from pathlib import Path
from unittest import mock


script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from glb_fixture import write_glb

spec = importlib.util.spec_from_file_location(
    "decimate_assets_preflight_diagnostics_test", script
)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
process_context = multiprocessing.get_context("fork")


MAX_METADATA_BYTES = 1_048_576
MAX_MANIFEST_ASSETS = 64
MAX_SOURCE_BYTES = 128 * 1024 * 1024
MAX_DERIVATIVE_BYTES = 64 * 1024 * 1024
MAX_DIAGNOSTIC_BYTES = 512
errors = []


def check(condition, message):
    if not condition:
        errors.append(message)


def digest_bytes(value):
    return hashlib.sha256(value).hexdigest()


def digest(path):
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest()


def tree_snapshot(directory):
    records = []
    for current, directory_names, filenames in os.walk(directory, followlinks=False):
        current_path = Path(current)
        for name in sorted(directory_names + filenames):
            path = current_path / name
            relative = path.relative_to(directory).as_posix()
            status = os.lstat(path)
            mode_type = stat.S_IFMT(status.st_mode)
            if stat.S_ISLNK(status.st_mode):
                records.append((relative, "symlink", os.readlink(path)))
            elif stat.S_ISREG(status.st_mode):
                records.append(
                    (relative, "regular", status.st_nlink, status.st_size, digest(path))
                )
            elif stat.S_ISDIR(status.st_mode):
                records.append((relative, "directory", stat.S_IMODE(status.st_mode)))
            else:
                records.append((relative, "special", mode_type, stat.S_IMODE(status.st_mode)))
    return records


def line_count(path):
    if not path.exists():
        return 0
    return len(path.read_text(encoding="utf-8").splitlines())


def write_sidecar(source, prompt="preflight fixture cat"):
    sidecar = Path(f"{source}.json")
    sidecar.write_text(json.dumps({
        "service": "meshy",
        "task_id": "fixture-meshy-task",
        "timestamp_utc": "2026-08-15T12:34:56Z",
        "plan_tier": "paid",
        "prompt": prompt,
        "note": "trusted fixture metadata",
        "sha256": digest(source),
    }, sort_keys=True) + "\n", encoding="utf-8")
    return sidecar


def write_manifest(path, filename="asset.glb", prompt="preflight fixture cat"):
    path.write_text(json.dumps({"assets": [{
        "id": "preflight-cat",
        "kind": "cat",
        "service": "meshy",
        "out": filename,
        "prompt": prompt,
    }]}, sort_keys=True) + "\n", encoding="utf-8")


def prepare_valid(name, filename="asset.glb", prompt="preflight fixture cat"):
    case_root = root / name
    input_dir = case_root / "input"
    output_dir = case_root / "output"
    input_dir.mkdir(parents=True)
    output_dir.mkdir()
    source = input_dir / filename
    manifest = case_root / "manifest.json"
    fake_log = case_root / "fake.log"
    fake_audit = case_root / "fake.audit"
    write_glb(source, triangles=30000)
    sidecar = write_sidecar(source, prompt)
    write_manifest(manifest, filename, prompt)
    return {
        "root": case_root,
        "input": input_dir,
        "output": output_dir,
        "source": source,
        "sidecar": sidecar,
        "manifest": manifest,
        "fake_log": fake_log,
        "fake_audit": fake_audit,
    }


def pad_valid_glb_to_size(path, size):
    current_size = path.stat().st_size
    padding_size = size - current_size - 8
    assert padding_size >= 0 and padding_size % 4 == 0
    with path.open("r+b") as handle:
        magic, version, declared = struct.unpack("<4sII", handle.read(12))
        assert magic == b"glTF" and version == 2 and declared == current_size
        handle.seek(8)
        handle.write(struct.pack("<I", size))
        handle.seek(0, os.SEEK_END)
        handle.write(struct.pack("<I4s", padding_size, b"PAD "))
        handle.truncate(size)


def arguments(case, force=False):
    result = [
        sys.executable,
        str(script),
        "--manifest", str(case["manifest"]),
        "--input-dir", str(case["input"]),
        "--output-dir", str(case["output"]),
        "--blender", str(fake_blender),
    ]
    if force:
        result.append("--force")
    return result


def environment(case, extra=None):
    value = os.environ.copy()
    value.update({
        "FAKE_BLENDER_MODE": "success",
        "FAKE_BLENDER_LOG": str(case["fake_log"]),
        "FAKE_BLENDER_AUDIT": str(case["fake_audit"]),
    })
    if extra:
        value.update(extra)
    return value


def run_case(case, *, force=False, extra_environment=None, timeout=5):
    try:
        result = subprocess.run(
            arguments(case, force),
            check=False,
            capture_output=True,
            text=True,
            timeout=timeout,
            env=environment(case, extra_environment),
        )
    except subprocess.TimeoutExpired as exc:
        return None, exc
    return result, None


def require_boundary_success(
    case, label, *, extra_environment=None, expected_glb_size=None, timeout=45
):
    source_before = tree_snapshot(case["input"])
    result, timeout_error = run_case(
        case,
        extra_environment=extra_environment,
        timeout=timeout,
    )
    check(timeout_error is None, f"{label}: exact-boundary control timed out")
    if result is not None:
        check(
            result.returncode == 0,
            f"{label}: exact boundary was rejected: {result.stderr!r}",
        )
        check(result.stderr == "", f"{label}: success wrote stderr")
    final_glb = case["output"] / case["source"].name
    final_json = Path(f"{final_glb}.json")
    check(
        set(case["output"].iterdir()) == {final_glb, final_json},
        f"{label}: success did not leave exactly one final pair",
    )
    if expected_glb_size is not None and final_glb.is_file():
        check(
            final_glb.stat().st_size == expected_glb_size,
            f"{label}: final GLB lost its exact accepted boundary size",
        )
    check(
        line_count(case["fake_audit"]) == 2
        and line_count(case["fake_log"]) == 1,
        f"{label}: success did not reach exactly version+asset fake phases",
    )
    check(
        tree_snapshot(case["input"]) == source_before,
        f"{label}: success changed source custody",
    )


# Exact acceptance boundaries are behavior, not just constants. In particular,
# 64 entries subsumes the known 15-asset queue and 128 MiB subsumes its ~75 MiB
# largest source without weakening the requested ceilings.
manifest_boundary = prepare_valid("manifest-exact-one-mib")
manifest_payload = manifest_boundary["manifest"].read_bytes()
manifest_boundary["manifest"].write_bytes(
    manifest_payload + b" " * (MAX_METADATA_BYTES - len(manifest_payload))
)
assert manifest_boundary["manifest"].stat().st_size == MAX_METADATA_BYTES
require_boundary_success(manifest_boundary, "manifest exactly one MiB")

sidecar_boundary = prepare_valid("sidecar-exact-one-mib")
sidecar_record = json.loads(
    sidecar_boundary["sidecar"].read_text(encoding="utf-8")
)
sidecar_record["note"] = ""
empty_note_payload = (
    json.dumps(sidecar_record, sort_keys=True) + "\n"
).encode("utf-8")
sidecar_record["note"] = "N" * (MAX_METADATA_BYTES - len(empty_note_payload))
sidecar_payload = (
    json.dumps(sidecar_record, sort_keys=True) + "\n"
).encode("utf-8")
assert len(sidecar_payload) == MAX_METADATA_BYTES
sidecar_boundary["sidecar"].write_bytes(sidecar_payload)
require_boundary_success(sidecar_boundary, "source sidecar exactly one MiB")

manifest_count_boundary_root = root / "manifest-exact-64"
manifest_count_boundary_input = manifest_count_boundary_root / "input"
manifest_count_boundary_output = manifest_count_boundary_root / "output"
manifest_count_boundary_input.mkdir(parents=True)
manifest_count_boundary_output.mkdir()
manifest_count_boundary_path = manifest_count_boundary_root / "manifest.json"
manifest_count_boundary_prompt = "manifest count boundary fixture"
manifest_count_boundary_entries = [
    {
        "id": f"boundary-{index:02d}",
        "kind": "cat",
        "service": "meshy",
        "out": f"boundary-{index:02d}.glb",
        "prompt": manifest_count_boundary_prompt,
    }
    for index in range(MAX_MANIFEST_ASSETS)
]
for entry in manifest_count_boundary_entries:
    boundary_source = manifest_count_boundary_input / entry["out"]
    write_glb(boundary_source, triangles=30000)
    write_sidecar(boundary_source, manifest_count_boundary_prompt)
manifest_count_boundary_path.write_text(
    json.dumps({"assets": manifest_count_boundary_entries}, sort_keys=True) + "\n",
    encoding="utf-8",
)
manifest_count_boundary = {
    "root": manifest_count_boundary_root,
    "input": manifest_count_boundary_input,
    "output": manifest_count_boundary_output,
    "manifest": manifest_count_boundary_path,
    "fake_log": manifest_count_boundary_root / "fake.log",
    "fake_audit": manifest_count_boundary_root / "fake.audit",
}
manifest_count_input_before = tree_snapshot(manifest_count_boundary_input)
manifest_count_result, manifest_count_timeout = run_case(
    manifest_count_boundary,
    timeout=60,
)
check(manifest_count_timeout is None, "manifest with exactly 64 entries timed out")
if manifest_count_result is not None:
    check(
        manifest_count_result.returncode == 0,
        "manifest with exactly 64 valid entries was rejected: "
        f"{manifest_count_result.stderr!r}",
    )
check(
    line_count(manifest_count_boundary["fake_audit"])
    == MAX_MANIFEST_ASSETS + 1
    and line_count(manifest_count_boundary["fake_log"])
    == MAX_MANIFEST_ASSETS,
    "manifest with exactly 64 entries missed a version or asset phase",
)
expected_count_outputs = {
    manifest_count_boundary_output / entry["out"]
    for entry in manifest_count_boundary_entries
} | {
    manifest_count_boundary_output / f"{entry['out']}.json"
    for entry in manifest_count_boundary_entries
}
check(
    set(manifest_count_boundary_output.iterdir()) == expected_count_outputs,
    "manifest with exactly 64 entries lost a final pair",
)
check(
    tree_snapshot(manifest_count_boundary_input) == manifest_count_input_before,
    "manifest with exactly 64 entries changed source custody",
)

source_boundary = prepare_valid("source-exact-128-mib")
pad_valid_glb_to_size(source_boundary["source"], MAX_SOURCE_BYTES)
source_boundary_record = json.loads(
    source_boundary["sidecar"].read_text(encoding="utf-8")
)
source_boundary_record["sha256"] = digest(source_boundary["source"])
source_boundary["sidecar"].write_text(
    json.dumps(source_boundary_record, sort_keys=True) + "\n",
    encoding="utf-8",
)
require_boundary_success(
    source_boundary,
    "source GLB exactly 128 MiB",
    timeout=60,
)

derivative_boundary = prepare_valid("derivative-exact-64-mib")
require_boundary_success(
    derivative_boundary,
    "derivative GLB exactly 64 MiB",
    extra_environment={
        "FAKE_BLENDER_OUTPUT_EXACT_SIZE": str(MAX_DERIVATIVE_BYTES)
    },
    expected_glb_size=MAX_DERIVATIVE_BYTES,
    timeout=45,
)


def require_preflight_rejection(case, label, pattern, *, force=False):
    case_before = tree_snapshot(case["root"])
    result, timeout = run_case(case, force=force)
    if timeout is not None:
        errors.append(f"{label}: preflight hung and was killed at five seconds")
    else:
        check(result.returncode != 0, f"{label}: unsafe file/envelope was accepted")
        combined = result.stdout + result.stderr
        check(
            re.search(pattern, result.stderr, re.IGNORECASE) is not None,
            f"{label}: wrong preflight diagnostic: {combined!r}",
        )
        check(result.stdout == "", f"{label}: preflight rejection wrote stdout")
        check(
            result.stderr.endswith("\n")
            and len(result.stderr.splitlines()) == 1
            and result.stderr[:-1].isprintable()
            and len(result.stderr.encode("utf-8")) <= MAX_DIAGNOSTIC_BYTES,
            f"{label}: preflight diagnostic is not printable/one-line/capped: "
            f"{result.stderr!r}",
        )
    check(line_count(case["fake_log"]) == 0, f"{label}: fake asset phase was reached")
    check(line_count(case["fake_audit"]) == 0, f"{label}: fake version phase was reached")
    check(
        tree_snapshot(case["root"]) == case_before,
        f"{label}: case membership/type/links/bytes changed",
    )


class WholeReadAttempt(RuntimeError):
    """Raised before a guarded oversize file can return any data bytes."""


def exercise_size_preflight(
    case,
    label,
    pattern,
    *,
    member,
    extra_environment=None,
    expected_fake_audit=0,
    expected_fake_log=0,
):
    """Prove size is rejected through lstat before any whole-file reader.

    The reader-layer oracle permits a no-follow/nonblocking descriptor open,
    fstat, and close with zero data read. It guards Path/file-object reads,
    descriptor reads, and copies independently. Synthetic mutations keep the
    oracle discriminating without treating a safe metadata-only open as a read.
    """

    case_before = tree_snapshot(case["input"])
    if member == "manifest":
        guarded_path = case["manifest"]
        size_limit = MAX_METADATA_BYTES
    elif member == "sidecar":
        guarded_path = case["sidecar"]
        size_limit = MAX_METADATA_BYTES
    elif member == "source":
        guarded_path = case["source"]
        size_limit = MAX_SOURCE_BYTES
    elif member == "derivative":
        guarded_path = case["root"] / "reader-oracle-oversize.glb"
        with guarded_path.open("wb") as handle:
            handle.write(b"glTF")
            handle.truncate(MAX_DERIVATIVE_BYTES + 1)
        size_limit = MAX_DERIVATIVE_BYTES
    else:
        raise AssertionError(f"unsupported guarded member {member}")

    guarded_absolute = Path(os.path.realpath(os.path.abspath(guarded_path)))
    output_absolute = Path(os.path.realpath(os.path.abspath(case["output"])))
    receive, send = process_context.Pipe(duplex=False)

    def child():
        guarded_fds = set()
        read_attempted = {"value": False}

        def descriptor_is_guarded(descriptor):
            if descriptor in guarded_fds:
                return True
            try:
                descriptor_status = os.fstat(descriptor)
            except OSError:
                return False
            candidates = [guarded_absolute]
            if member == "derivative":
                for current, _directories, filenames in os.walk(
                    output_absolute, followlinks=False
                ):
                    candidates.extend(Path(current) / name for name in filenames)
            for candidate in candidates:
                try:
                    candidate_status = os.lstat(candidate)
                except OSError:
                    continue
                if (
                    stat.S_ISREG(candidate_status.st_mode)
                    and candidate_status.st_size > size_limit
                    and (candidate_status.st_dev, candidate_status.st_ino)
                    == (descriptor_status.st_dev, descriptor_status.st_ino)
                ):
                    guarded_fds.add(descriptor)
                    return True
            return False

        def is_guarded(raw_path):
            if isinstance(raw_path, int):
                return descriptor_is_guarded(raw_path)
            try:
                candidate = Path(
                    os.path.realpath(os.path.abspath(os.fsdecode(raw_path)))
                )
            except (TypeError, ValueError):
                return False
            if candidate == guarded_absolute:
                return True
            if member != "derivative" or not candidate.is_relative_to(output_absolute):
                return False
            try:
                status = os.lstat(candidate)
            except OSError:
                return False
            return stat.S_ISREG(status.st_mode) and status.st_size > size_limit

        def reject_read(operation):
            read_attempted["value"] = True
            raise WholeReadAttempt(
                f"{operation} attempted data access for guarded {member}"
            )

        class GuardedReader:
            def __init__(self, handle, descriptor=None):
                self._handle = handle
                self._descriptor = descriptor

            def _reject(self, operation):
                reject_read(operation)

            def read(self, size=-1, *args):
                if size == 0:
                    return self._handle.read(size, *args)
                self._reject("file read")

            def read1(self, size=-1):
                if size == 0:
                    return self._handle.read1(size)
                self._reject("buffered read")

            def readall(self):
                self._reject("full read")

            def readinto(self, buffer):
                if len(memoryview(buffer)) == 0:
                    return self._handle.readinto(buffer)
                self._reject("readinto")

            def readinto1(self, buffer):
                if len(memoryview(buffer)) == 0:
                    return self._handle.readinto1(buffer)
                self._reject("buffered readinto")

            def readline(self, size=-1):
                if size == 0:
                    return self._handle.readline(size)
                self._reject("line read")

            def readlines(self, hint=-1):
                self._reject("line reads")

            def peek(self, size=-1):
                # BufferedReader.peek(0) may still fill and expose bytes.
                self._reject("peek")

            def __iter__(self):
                return self

            def __next__(self):
                self._reject("iterator read")

            def __enter__(self):
                return self

            def __exit__(self, _exc_type, _exc, _traceback):
                self.close()
                return False

            def close(self):
                try:
                    self._handle.close()
                finally:
                    if self._descriptor is not None:
                        guarded_fds.discard(self._descriptor)

            def _wrap_exposed_handle(self, handle):
                descriptor = self._descriptor
                if descriptor is None:
                    try:
                        descriptor = handle.fileno()
                    except (AttributeError, OSError, ValueError):
                        descriptor = None
                if descriptor is not None:
                    guarded_fds.add(descriptor)
                return GuardedReader(handle, descriptor)

            @property
            def raw(self):
                return self._wrap_exposed_handle(self._handle.raw)

            @property
            def buffer(self):
                return self._wrap_exposed_handle(self._handle.buffer)

            def detach(self):
                return self._wrap_exposed_handle(self._handle.detach())

            def __getattr__(self, name):
                return getattr(self._handle, name)

        real_builtin_open = builtins.open
        real_io_open = io.open
        real_os_open = os.open
        real_os_close = os.close
        real_os_read = os.read
        real_os_fdopen = os.fdopen
        real_copyfile = shutil.copyfile
        real_copy = shutil.copy
        real_copy2 = shutil.copy2
        real_copyfileobj = shutil.copyfileobj
        real_mmap = mmap.mmap

        def wrap_reader(handle, value, descriptor=None):
            if is_guarded(value):
                if descriptor is None:
                    try:
                        descriptor = handle.fileno()
                    except (AttributeError, OSError, ValueError):
                        descriptor = None
                    if descriptor is not None:
                        guarded_fds.add(descriptor)
                return GuardedReader(handle, descriptor)
            return handle

        def guarded_builtin_open(value, *args, **kwargs):
            handle = real_builtin_open(value, *args, **kwargs)
            return wrap_reader(handle, value)

        def guarded_io_open(value, *args, **kwargs):
            handle = real_io_open(value, *args, **kwargs)
            return wrap_reader(handle, value)

        def guarded_os_open(value, *args, **kwargs):
            descriptor = real_os_open(value, *args, **kwargs)
            if is_guarded(value):
                guarded_fds.add(descriptor)
            return descriptor

        def guarded_os_close(descriptor):
            guarded_fds.discard(descriptor)
            return real_os_close(descriptor)

        def guarded_os_read(descriptor, count):
            if descriptor_is_guarded(descriptor) and count != 0:
                reject_read("descriptor read")
            return real_os_read(descriptor, count)

        def guarded_os_fdopen(descriptor, *args, **kwargs):
            handle = real_os_fdopen(descriptor, *args, **kwargs)
            return wrap_reader(handle, descriptor, descriptor)

        def guarded_copyfile(source, destination, *args, **kwargs):
            if is_guarded(source):
                reject_read("copy")
            return real_copyfile(source, destination, *args, **kwargs)

        def guarded_copy(source, destination, *args, **kwargs):
            if is_guarded(source):
                reject_read("shutil.copy")
            return real_copy(source, destination, *args, **kwargs)

        def guarded_copy2(source, destination, *args, **kwargs):
            if is_guarded(source):
                reject_read("shutil.copy2")
            return real_copy2(source, destination, *args, **kwargs)

        def guarded_copyfileobj(source, destination, *args, **kwargs):
            try:
                source_descriptor = source.fileno()
            except (AttributeError, OSError, ValueError):
                source_descriptor = None
            if (
                source_descriptor is not None
                and descriptor_is_guarded(source_descriptor)
            ):
                reject_read("shutil.copyfileobj")
            return real_copyfileobj(source, destination, *args, **kwargs)

        def guarded_mmap(descriptor, *args, **kwargs):
            if descriptor_is_guarded(descriptor):
                reject_read("mmap")
            return real_mmap(descriptor, *args, **kwargs)

        optional_patches = []
        if hasattr(os, "pread"):
            real_pread = os.pread

            def guarded_pread(descriptor, count, offset):
                if descriptor_is_guarded(descriptor) and count != 0:
                    reject_read("pread")
                return real_pread(descriptor, count, offset)

            optional_patches.append(mock.patch.object(os, "pread", new=guarded_pread))
        if hasattr(os, "readv"):
            real_readv = os.readv

            def guarded_readv(descriptor, buffers):
                if descriptor_is_guarded(descriptor) and any(
                    len(value) for value in buffers
                ):
                    reject_read("readv")
                return real_readv(descriptor, buffers)

            optional_patches.append(mock.patch.object(os, "readv", new=guarded_readv))
        if hasattr(os, "preadv"):
            real_preadv = os.preadv

            def guarded_preadv(descriptor, buffers, offset, *args, **kwargs):
                if descriptor_is_guarded(descriptor) and any(
                    len(value) for value in buffers
                ):
                    reject_read("preadv")
                return real_preadv(descriptor, buffers, offset, *args, **kwargs)

            optional_patches.append(
                mock.patch.object(os, "preadv", new=guarded_preadv)
            )
        if hasattr(os, "sendfile"):
            real_sendfile = os.sendfile

            def guarded_sendfile(
                destination_descriptor,
                source_descriptor,
                offset,
                count,
                *args,
                **kwargs,
            ):
                if descriptor_is_guarded(source_descriptor) and count != 0:
                    reject_read("sendfile")
                return real_sendfile(
                    destination_descriptor,
                    source_descriptor,
                    offset,
                    count,
                    *args,
                    **kwargs,
                )

            optional_patches.append(
                mock.patch.object(os, "sendfile", new=guarded_sendfile)
            )
        if hasattr(os, "copy_file_range"):
            real_copy_file_range = os.copy_file_range

            def guarded_copy_file_range(
                source_descriptor,
                destination_descriptor,
                count,
                *args,
                **kwargs,
            ):
                if descriptor_is_guarded(source_descriptor) and count != 0:
                    reject_read("copy_file_range")
                return real_copy_file_range(
                    source_descriptor,
                    destination_descriptor,
                    count,
                    *args,
                    **kwargs,
                )

            optional_patches.append(
                mock.patch.object(
                    os, "copy_file_range", new=guarded_copy_file_range
                )
            )
        for fastcopy_name in (
            "_fastcopy_fcopyfile",
            "_fastcopy_sendfile",
            "_fastcopy_copy_file_range",
        ):
            if not hasattr(shutil, fastcopy_name):
                continue
            real_fastcopy = getattr(shutil, fastcopy_name)

            def guarded_fastcopy(source, *args, _real=real_fastcopy, **kwargs):
                try:
                    source_descriptor = source.fileno()
                except (AttributeError, OSError, ValueError):
                    source_descriptor = None
                if (
                    source_descriptor is not None
                    and descriptor_is_guarded(source_descriptor)
                ):
                    reject_read("shutil fast-copy")
                return _real(source, *args, **kwargs)

            optional_patches.append(
                mock.patch.object(shutil, fastcopy_name, new=guarded_fastcopy)
            )

        mutation_copy = case["root"] / "reader-oracle-copy"
        mutation_results = {}
        try:
            with contextlib.ExitStack() as stack:
                for patcher in (
                    mock.patch.object(builtins, "open", new=guarded_builtin_open),
                    mock.patch.object(io, "open", new=guarded_io_open),
                    mock.patch.object(os, "open", new=guarded_os_open),
                    mock.patch.object(os, "close", new=guarded_os_close),
                    mock.patch.object(os, "read", new=guarded_os_read),
                    mock.patch.object(os, "fdopen", new=guarded_os_fdopen),
                    mock.patch.object(shutil, "copyfile", new=guarded_copyfile),
                    mock.patch.object(shutil, "copy", new=guarded_copy),
                    mock.patch.object(shutil, "copy2", new=guarded_copy2),
                    mock.patch.object(
                        shutil, "copyfileobj", new=guarded_copyfileobj
                    ),
                    mock.patch.object(mmap, "mmap", new=guarded_mmap),
                    *optional_patches,
                ):
                    stack.enter_context(patcher)

                safe_flags = (
                    os.O_RDONLY
                    | getattr(os, "O_NOFOLLOW", 0)
                    | getattr(os, "O_NONBLOCK", 0)
                )
                descriptor_only_passed = False
                descriptor = None
                try:
                    descriptor = os.open(guarded_path, safe_flags)
                    descriptor_status = os.fstat(descriptor)
                    zero_read = os.read(descriptor, 0)
                    descriptor_only_passed = (
                        getattr(os, "O_NOFOLLOW", 0) != 0
                        and getattr(os, "O_NONBLOCK", 0) != 0
                        and stat.S_ISREG(descriptor_status.st_mode)
                        and descriptor_status.st_size > size_limit
                        and zero_read == b""
                    )
                except WholeReadAttempt:
                    descriptor_only_passed = False
                finally:
                    if descriptor is not None:
                        os.close(descriptor)
                descriptor_only_passed = (
                    descriptor_only_passed and not read_attempted["value"]
                )

                with guarded_path.open("rb") as zero_handle:
                    descriptor_only_passed = (
                        descriptor_only_passed
                        and zero_handle.read(0) == b""
                        and zero_handle.raw.read(0) == b""
                    )
                with io.open(
                    guarded_path, "r", encoding="ascii"
                ) as zero_text_handle:
                    descriptor_only_passed = (
                        descriptor_only_passed
                        and zero_text_handle.buffer.read(0) == b""
                    )
                zero_detach_handle = io.open(guarded_path, "rb")
                zero_detached = zero_detach_handle.detach()
                try:
                    descriptor_only_passed = (
                        descriptor_only_passed
                        and zero_detached.read(0) == b""
                    )
                finally:
                    zero_detached.close()
                descriptor_only_passed = (
                    descriptor_only_passed and not read_attempted["value"]
                )

                def descriptor_reader(operation):
                    def reader():
                        descriptor_value = os.open(guarded_path, safe_flags)
                        try:
                            return operation(descriptor_value)
                        finally:
                            os.close(descriptor_value)
                    return reader

                def transfer_reader(operation):
                    def reader():
                        source_descriptor = os.open(guarded_path, safe_flags)
                        destination_descriptor = os.open(
                            mutation_copy,
                            os.O_WRONLY | os.O_CREAT | os.O_TRUNC,
                            0o600,
                        )
                        try:
                            return operation(
                                source_descriptor, destination_descriptor
                            )
                        finally:
                            os.close(destination_descriptor)
                            os.close(source_descriptor)
                    return reader

                def file_reader(operation, *, use_builtin=False):
                    def reader():
                        opener = builtins.open if use_builtin else io.open
                        with opener(guarded_path, "rb") as handle:
                            return operation(handle)
                    return reader

                def mmap_read(descriptor_value):
                    mapping = mmap.mmap(
                        descriptor_value, 1, access=mmap.ACCESS_READ
                    )
                    try:
                        return mapping[0]
                    finally:
                        mapping.close()

                def mmap_zero_length_read(descriptor_value):
                    mapping = mmap.mmap(
                        descriptor_value, 0, access=mmap.ACCESS_READ
                    )
                    try:
                        return mapping[0]
                    finally:
                        mapping.close()

                def copyfileobj_read():
                    with (
                        io.open(guarded_path, "rb") as source_handle,
                        io.open(mutation_copy, "wb") as destination_handle,
                    ):
                        shutil.copyfileobj(
                            source_handle, destination_handle, length=1
                        )

                def detached_file_read():
                    source_handle = io.open(guarded_path, "rb")
                    detached_handle = source_handle.detach()
                    try:
                        return detached_handle.read(1)
                    finally:
                        detached_handle.close()

                def text_buffer_read():
                    with io.open(
                        guarded_path, "r", encoding="ascii"
                    ) as source_handle:
                        return source_handle.buffer.read(1)

                mutation_readers = {
                    "path.read_bytes": lambda: guarded_path.read_bytes(),
                    "file.read": file_reader(lambda handle: handle.read(1)),
                    "builtins.read": file_reader(
                        lambda handle: handle.read(1), use_builtin=True
                    ),
                    "file.readinto": file_reader(
                        lambda handle: handle.readinto(bytearray(1))
                    ),
                    "file.readline": file_reader(
                        lambda handle: handle.readline(1)
                    ),
                    "file.peek-zero": file_reader(lambda handle: handle.peek(0)),
                    "file.raw.read": file_reader(
                        lambda handle: handle.raw.read(1)
                    ),
                    "file.detach.read": detached_file_read,
                    "file.buffer.read": text_buffer_read,
                    "file.iteration": file_reader(lambda handle: next(handle)),
                    "os.read": descriptor_reader(
                        lambda descriptor_value: os.read(descriptor_value, 1)
                    ),
                    "mmap": descriptor_reader(mmap_read),
                    "mmap-zero-length": descriptor_reader(mmap_zero_length_read),
                    "shutil.copyfile": lambda: shutil.copyfile(
                        guarded_path, mutation_copy
                    ),
                    "shutil.copy": lambda: shutil.copy(
                        guarded_path, mutation_copy
                    ),
                    "shutil.copy2": lambda: shutil.copy2(
                        guarded_path, mutation_copy
                    ),
                    "shutil.copyfileobj": copyfileobj_read,
                }
                if hasattr(os, "pread"):
                    mutation_readers["os.pread"] = descriptor_reader(
                        lambda descriptor_value: os.pread(
                            descriptor_value, 1, 0
                        )
                    )
                if hasattr(os, "readv"):
                    mutation_readers["os.readv"] = descriptor_reader(
                        lambda descriptor_value: os.readv(
                            descriptor_value, [bytearray(1)]
                        )
                    )
                if hasattr(os, "preadv"):
                    mutation_readers["os.preadv"] = descriptor_reader(
                        lambda descriptor_value: os.preadv(
                            descriptor_value, [bytearray(1)], 0
                        )
                    )
                if hasattr(os, "sendfile"):
                    def sendfile_read(
                        source_descriptor, destination_descriptor
                    ):
                        return os.sendfile(
                            destination_descriptor,
                            source_descriptor,
                            0,
                            1,
                        )

                    mutation_readers["os.sendfile"] = transfer_reader(
                        sendfile_read
                    )
                if hasattr(os, "copy_file_range"):
                    def copy_file_range_read(
                        source_descriptor, destination_descriptor
                    ):
                        return os.copy_file_range(
                            source_descriptor,
                            destination_descriptor,
                            1,
                        )

                    mutation_readers["os.copy_file_range"] = transfer_reader(
                        copy_file_range_read
                    )
                for mutation_name, reader in mutation_readers.items():
                    read_attempted["value"] = False
                    mutation_caught = False
                    try:
                        reader()
                    except WholeReadAttempt:
                        mutation_caught = True
                    finally:
                        mutation_results[mutation_name] = (
                            mutation_caught and read_attempted["value"]
                        )
                        mutation_copy.unlink(missing_ok=True)

                os.environ.clear()
                os.environ.update(environment(case, extra_environment))
                stdout = io.StringIO()
                stderr = io.StringIO()
                read_attempted["value"] = False
                returncode = None
                try:
                    with (
                        contextlib.redirect_stdout(stdout),
                        contextlib.redirect_stderr(stderr),
                    ):
                        returncode = module.main(arguments(case)[2:])
                except WholeReadAttempt:
                    pass
                payload = {
                    "descriptor_only_passed": descriptor_only_passed,
                    "mutation_results": mutation_results,
                    "production_read_attempted": read_attempted["value"],
                    "returncode": returncode,
                    "stdout": stdout.getvalue(),
                    "stderr": stderr.getvalue(),
                }
            send.send(payload)
        except BaseException:
            send.send({"traceback": traceback.format_exc()})
        finally:
            send.close()

    process = process_context.Process(target=child)
    process.start()
    send.close()
    process.join(20)
    if process.is_alive():
        process.terminate()
        process.join(2)
        if process.is_alive():
            process.kill()
            process.join(2)
        errors.append(f"{label}: bounded size-preflight child hung")
        receive.close()
        guarded_path.unlink(missing_ok=True)
        process.close()
        return
    payload = None
    if receive.poll(1):
        try:
            payload = receive.recv()
        except EOFError:
            payload = None
    receive.close()
    child_exitcode = process.exitcode
    process.close()
    if payload is None:
        errors.append(
            f"{label}: size-preflight child exited {child_exitcode} without a record"
        )
        guarded_path.unlink(missing_ok=True)
        return
    if "traceback" in payload:
        errors.append(f"{label}: size-preflight child crashed:\n{payload['traceback']}")
        guarded_path.unlink(missing_ok=True)
        return

    check(
        payload["descriptor_only_passed"],
        f"{label}: safe O_NOFOLLOW/O_NONBLOCK open+fstat+close/read(0) "
        "was rejected",
    )
    expected_mutations = {
        "path.read_bytes",
        "file.read",
        "builtins.read",
        "file.readinto",
        "file.readline",
        "file.peek-zero",
        "file.raw.read",
        "file.detach.read",
        "file.buffer.read",
        "file.iteration",
        "os.read",
        "mmap",
        "mmap-zero-length",
        "shutil.copyfile",
        "shutil.copy",
        "shutil.copy2",
        "shutil.copyfileobj",
    }
    for capability in (
        "pread", "readv", "preadv", "sendfile", "copy_file_range"
    ):
        if hasattr(os, capability):
            expected_mutations.add(f"os.{capability}")
    mutation_results = payload["mutation_results"]
    check(
        set(mutation_results) == expected_mutations,
        f"{label}: reader oracle capability matrix differs: "
        f"actual={sorted(mutation_results)} expected={sorted(expected_mutations)}",
    )
    missed_mutations = sorted(
        name for name, detected in mutation_results.items() if not detected
    )
    check(
        not missed_mutations,
        f"{label}: reader oracle missed stdlib mutations: {missed_mutations}",
    )
    check(
        not payload["production_read_attempted"],
        f"{label}: whole read/copy attempted before lstat size rejection",
    )
    if not payload["production_read_attempted"]:
        check(payload["returncode"] == 1, f"{label}: oversize member was accepted")
        if expected_fake_log == 0:
            check(payload["stdout"] == "", f"{label}: preflight rejection wrote stdout")
        else:
            check(
                payload["stdout"].endswith("\n")
                and len(payload["stdout"].splitlines()) == 1
                and payload["stdout"][:-1].isprintable()
                and len(payload["stdout"].encode("utf-8"))
                <= MAX_DIAGNOSTIC_BYTES,
                f"{label}: progress stdout is not printable/one-line/capped: "
                f"{payload['stdout']!r}",
            )
        check(
            re.search(pattern, payload["stderr"], re.IGNORECASE) is not None,
            f"{label}: wrong size diagnostic: "
            f"{(payload['stdout'] + payload['stderr'])!r}",
        )
        check(
            payload["stderr"].endswith("\n")
            and len(payload["stderr"].splitlines()) == 1
            and payload["stderr"][:-1].isprintable()
            and len(payload["stderr"].encode("utf-8")) <= MAX_DIAGNOSTIC_BYTES,
            f"{label}: size diagnostic is not printable/one-line/capped: "
            f"{payload['stderr']!r}",
        )
    check(
        line_count(case["fake_audit"]) == expected_fake_audit,
        f"{label}: wrong fake audit phase count",
    )
    check(
        line_count(case["fake_log"]) == expected_fake_log,
        f"{label}: wrong fake asset phase count",
    )
    check(
        tree_snapshot(case["input"]) == case_before,
        f"{label}: source custody changed",
    )
    check(
        list(case["output"].iterdir()) == [],
        f"{label}: final or staging residue remains",
    )
    guarded_path.unlink(missing_ok=True)


def manifest_type_case(kind):
    label = f"manifest-{kind}"
    case = prepare_valid(label)
    path = case["manifest"]
    witness = path.with_name(f"{path.name}.{kind}-witness")
    if kind == "symlink":
        os.replace(path, witness)
        path.symlink_to(witness.name)
    elif kind == "hardlink":
        os.link(path, witness)
    elif kind == "fifo":
        path.unlink()
        os.mkfifo(path, 0o600)
    elif kind == "directory":
        path.unlink()
        path.mkdir()
    else:
        raise AssertionError(f"unsupported manifest kind {kind}")
    return case, label


def source_type_case(kind, member):
    label = f"source-{member}-{kind}"
    case = prepare_valid(label)
    path = case["source"] if member == "glb" else case["sidecar"]
    witness = path.with_name(f"{path.name}.{kind}-witness")
    if kind == "symlink":
        os.replace(path, witness)
        path.symlink_to(witness.name)
    elif kind == "hardlink":
        os.link(path, witness)
    elif kind == "fifo":
        path.unlink()
        os.mkfifo(path, 0o600)
    elif kind == "directory":
        path.unlink()
        path.mkdir()
    else:
        raise AssertionError(f"unsupported source kind {kind}")
    return case, label


def install_old_pair(case):
    final_glb = case["output"] / "asset.glb"
    final_json = case["output"] / "asset.glb.json"
    final_glb.write_bytes(b"old derivative bytes")
    final_json.write_bytes(b"old provenance bytes")
    return final_glb, final_json


def final_type_case(kind, member):
    label = f"final-{member}-{kind}"
    case = prepare_valid(label)
    final_glb, final_json = install_old_pair(case)
    path = final_glb if member == "glb" else final_json
    witness = path.with_name(f"{path.name}.{kind}-witness")
    if kind == "symlink":
        os.replace(path, witness)
        path.symlink_to(witness.name)
    elif kind == "hardlink":
        os.link(path, witness)
    elif kind == "fifo":
        path.unlink()
        os.mkfifo(path, 0o600)
    elif kind == "directory":
        path.unlink()
        path.mkdir()
    else:
        raise AssertionError(f"unsupported final kind {kind}")
    return case, label


regular_single_pattern = r"regular[^\n]*(single.?link|one link)|single.?link[^\n]*regular|file custody"
for file_kind in ("symlink", "hardlink", "fifo", "directory"):
    case, label = manifest_type_case(file_kind)
    require_preflight_rejection(case, label, regular_single_pattern)
    for source_member in ("glb", "sidecar"):
        case, label = source_type_case(file_kind, source_member)
        require_preflight_rejection(case, label, regular_single_pattern)
    for final_member in ("glb", "json"):
        case, label = final_type_case(file_kind, final_member)
        require_preflight_rejection(
            case, label, regular_single_pattern, force=True
        )


# Metadata and manifest count ceilings are checked before content traversal or
# fake version execution. Padding keeps the oversize manifest valid JSON.
manifest_size = prepare_valid("manifest-over-one-mib")
manifest_payload = manifest_size["manifest"].read_bytes()
assert len(manifest_payload) < MAX_METADATA_BYTES
manifest_size["manifest"].write_bytes(
    manifest_payload + b" " * (MAX_METADATA_BYTES + 1 - len(manifest_payload))
)
assert manifest_size["manifest"].stat().st_size == MAX_METADATA_BYTES + 1
exercise_size_preflight(
    manifest_size,
    "manifest over one MiB",
    r"manifest[^\n]*(too large|size|limit|1048576)",
    member="manifest",
)

sidecar_size = prepare_valid("sidecar-over-one-mib")
sidecar_record = json.loads(sidecar_size["sidecar"].read_text(encoding="utf-8"))
sidecar_record["note"] = "N" * MAX_METADATA_BYTES
sidecar_size["sidecar"].write_text(
    json.dumps(sidecar_record, sort_keys=True) + "\n", encoding="utf-8"
)
assert sidecar_size["sidecar"].stat().st_size > MAX_METADATA_BYTES
exercise_size_preflight(
    sidecar_size,
    "source sidecar over one MiB",
    r"source sidecar[^\n]*(too large|size|limit|1048576)",
    member="sidecar",
)

manifest_count = prepare_valid("manifest-over-count")
manifest_entries = [
    {
        "id": f"asset-{index:03d}",
        "kind": "cat",
        "service": "meshy",
        "out": f"asset-{index:03d}.glb",
        "prompt": "count fixture cat",
    }
    for index in range(MAX_MANIFEST_ASSETS + 1)
]
manifest_count["manifest"].write_text(
    json.dumps({"assets": manifest_entries}, sort_keys=True) + "\n",
    encoding="utf-8",
)
require_preflight_rejection(
    manifest_count,
    "manifest over asset-count envelope",
    r"manifest[^\n]*(too many|count|limit|64)",
)

source_size = prepare_valid("source-over-128-mib")
source_size["source"].unlink()
with source_size["source"].open("wb") as handle:
    handle.write(b"glTF")
    handle.truncate(MAX_SOURCE_BYTES + 1)
source_size["sidecar"].write_text(json.dumps({
    "service": "meshy",
    "task_id": "fixture-meshy-task",
    "timestamp_utc": "2026-08-15T12:34:56Z",
    "plan_tier": "paid",
    "prompt": "preflight fixture cat",
    "note": "oversize sparse source",
    "sha256": "0" * 64,
}, sort_keys=True) + "\n", encoding="utf-8")
exercise_size_preflight(
    source_size,
    "source GLB over 128 MiB",
    r"source[^\n]*(too large|size|limit|134217728)",
    member="source",
)


# A Blender-created candidate has its own tighter envelope before inspect_glb
# can read it. This is intentionally post-child, but still leaves no final or
# staging residue and preserves the source pair exactly.
derivative_size = prepare_valid("derivative-over-64-mib")
exercise_size_preflight(
    derivative_size,
    "derivative over 64 MiB",
    r"derivative[^\n]*(too large|size|limit|67108864)",
    member="derivative",
    extra_environment={
        "FAKE_BLENDER_OUTPUT_SIZE": str(MAX_DERIVATIVE_BYTES + 1)
    },
    expected_fake_audit=2,
    expected_fake_log=1,
)


# Output filenames are both portable enough for the selected filesystem and
# safe to interpolate in records. Broad printable Unicode remains supported.
unsafe_output_names = (
    ("line-feed", "unsafe\nforged.glb"),
    ("carriage-return", "unsafe\rforged.glb"),
    ("tab", "unsafe\tforged.glb"),
    ("escape", "unsafe\x1bforged.glb"),
    ("unicode-line-separator", "unsafe\u2028forged.glb"),
    ("unicode-paragraph-separator", "unsafe\u2029forged.glb"),
    ("overlong", "x" * 300 + ".glb"),
)
for label, filename in unsafe_output_names:
    case_root = root / f"output-name-{label}"
    input_dir = case_root / "input"
    output_dir = case_root / "output"
    input_dir.mkdir(parents=True)
    output_dir.mkdir()
    case = {
        "root": case_root,
        "input": input_dir,
        "output": output_dir,
        "manifest": case_root / "manifest.json",
        "fake_log": case_root / "fake.log",
        "fake_audit": case_root / "fake.audit",
    }
    write_manifest(case["manifest"], filename)
    result, timeout = run_case(case)
    check(timeout is None, f"output name {label}: rejection hung")
    if result is None:
        check(line_count(case["fake_audit"]) == 0, f"output name {label}: fake was reached")
        check(list(output_dir.iterdir()) == [], f"output name {label}: output was created")
        continue
    check(result.returncode != 0, f"output name {label}: unsafe name was accepted")
    check(result.stdout == "", f"output name {label}: rejection wrote stdout")
    check(
        re.search(
            r"out[^\n]*(printable|single.?line|filename length|too long)",
            result.stderr,
            re.IGNORECASE,
        ) is not None,
        f"output name {label}: wrong diagnostic {result.stderr!r}",
    )
    check(
        result.stderr.endswith("\n")
        and len(result.stderr.splitlines()) == 1
        and result.stderr[:-1].isprintable()
        and len(result.stderr.encode("utf-8")) <= MAX_DIAGNOSTIC_BYTES,
        f"output name {label}: diagnostic is not printable/one-line/capped",
    )
    check(filename not in result.stderr, f"output name {label}: raw hostile name leaked")
    check(line_count(case["fake_audit"]) == 0, f"output name {label}: fake was reached")
    check(list(output_dir.iterdir()) == [], f"output name {label}: output was created")

safe_name = "Café Cat № 7.glb"
safe_output = prepare_valid(
    "safe-printable-output-name", safe_name, "safe printable fixture cat"
)
safe_result, safe_timeout = run_case(safe_output)
check(safe_timeout is None, "safe printable output name hung")
if safe_result is not None:
    check(
        safe_result.returncode == 0,
        f"safe printable output name was rejected: {safe_result.stderr!r}",
    )
check(
    sorted(path.name for path in safe_output["output"].iterdir())
    == [safe_name, f"{safe_name}.json"],
    "safe printable Unicode output name lost its exact final pair",
)


# Hostile glTF strings cross the fake process boundary as data. The policy class
# remains legible, but raw fields never forge a second record or expand stderr
# without bound. Both routes prove the CLI-level formatter is centralized.
def hostile_diagnostic_case(name, control_name, policy_pattern):
    case = prepare_valid(name)
    source_before = tree_snapshot(case["input"])
    marker = f"FORGED-{name.upper()}-RECORD"
    hostile = f"bad\n{marker}\r\x1b[31m\u2028" + "Z" * 4096
    result, timeout = run_case(
        case,
        extra_environment={control_name: hostile},
        timeout=10,
    )
    check(timeout is None, f"{name}: hostile diagnostic run hung")
    if result is None:
        check(tree_snapshot(case["input"]) == source_before, f"{name}: source changed")
        check(list(case["output"].iterdir()) == [], f"{name}: final/staging residue remains")
        return
    check(result.returncode != 0, f"{name}: hostile derivative was accepted")
    check(
        re.search(policy_pattern, result.stderr, re.IGNORECASE) is not None,
        f"{name}: sanitized diagnostic lost its policy class: {result.stderr!r}",
    )
    check(
        result.stderr.startswith("glb-decimation: ")
        and result.stderr.endswith("\n")
        and len(result.stderr.splitlines()) == 1
        and result.stderr[:-1].isprintable()
        and len(result.stderr.encode("utf-8")) <= MAX_DIAGNOSTIC_BYTES,
        f"{name}: diagnostic is not centralized printable/one-line/capped: "
        f"{result.stderr!r}",
    )
    check(
        result.stdout.startswith("glb-decimation: ")
        and result.stdout.endswith("\n")
        and len(result.stdout.splitlines()) == 1
        and result.stdout[:-1].isprintable()
        and len(result.stdout.encode("utf-8")) <= MAX_DIAGNOSTIC_BYTES,
        f"{name}: stdout is not printable/one-line/capped: {result.stdout!r}",
    )
    check(
        line_count(case["fake_audit"]) == 2
        and line_count(case["fake_log"]) == 1,
        f"{name}: hostile field did not cross exactly one fake asset boundary",
    )
    check(tree_snapshot(case["input"]) == source_before, f"{name}: source changed")
    check(list(case["output"].iterdir()) == [], f"{name}: final/staging residue remains")


hostile_diagnostic_case(
    "hostile-extension",
    "FAKE_BLENDER_OUTPUT_EXTENSION",
    r"extension",
)
hostile_diagnostic_case(
    "hostile-uri",
    "FAKE_BLENDER_OUTPUT_URI",
    r"external[^\n]*uri",
)


if errors:
    raise AssertionError("preflight/diagnostic hardening regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = K ]; then
  printf 'glb-decimation review K: pass\n'
  exit 0
fi

# Review hardening L: acceptance is a byte-bound custody decision, not a
# validation of one pathname followed by publication of whatever later occupies
# it. Bind the validated candidate/provenance hashes through promotion, check
# the immutable original pair before any final name or success record appears,
# enforce the derivative cap at the inspector boundary, redact an entire signed
# URI rather than keyword fragments, and reserve the public `lost UV` category
# for the exact material-referenced missing-TEXCOORD diagnostic. Every injected
# race runs in its own bounded process and private temporary subtree.
if [ "$review_section" = all ] || [ "$review_section" = L ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-acceptance-binding" "$repo" \
    "$fake_blender" <<'PY'
from __future__ import annotations

import builtins
import contextlib
import hashlib
import importlib.util
import io
import json
import mmap
import multiprocessing
import os
import queue as queue_module
import shutil
import signal
import stat
import struct
import sys
import traceback
import types
from pathlib import Path
from unittest import mock


script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from fake_blender import _pad_glb_to_size  # noqa: E402
from glb_fixture import write_glb  # noqa: E402

spec = importlib.util.spec_from_file_location(
    "decimate_assets_acceptance_binding_test", script
)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

process_context = multiprocessing.get_context("fork")
errors: list[str] = []


def digest_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def digest_file(path: Path) -> str:
    return digest_bytes(path.read_bytes())


def setup_case(label: str, *, force: bool = False) -> types.SimpleNamespace:
    case_root = root / label
    case_root.mkdir()
    input_dir = case_root / "input"
    output_dir = case_root / "output"
    input_dir.mkdir()
    output_dir.mkdir()

    source = input_dir / "candidate.glb"
    source_sidecar = input_dir / "candidate.glb.json"
    write_glb(source, triangles=30_000)
    source_record = {
        "service": "meshy",
        "task_id": "fixture-task",
        "timestamp_utc": "2026-08-15T12:34:56Z",
        "plan_tier": "paid",
        "prompt": "acceptance binding fixture",
        "note": "local fixture",
        "sha256": digest_file(source),
    }
    source_sidecar.write_text(
        json.dumps(source_record, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    manifest = case_root / "manifest.json"
    manifest.write_text(
        json.dumps(
            {
                "assets": [
                    {
                        "id": "binding-fixture",
                        "kind": "cat",
                        "service": "meshy",
                        "out": "candidate.glb",
                        "prompt": "acceptance binding fixture",
                    }
                ]
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )

    final_glb = output_dir / "candidate.glb"
    final_json = output_dir / "candidate.glb.json"
    old_pair = None
    if force:
        write_glb(final_glb, triangles=14_000)
        final_json.write_text(
            json.dumps({"generation": "frozen-old-pair"}, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        old_pair = (final_glb.read_bytes(), final_json.read_bytes())

    fake_log = case_root / "fake.log"
    fake_audit = case_root / "fake.audit"
    arguments = [
        "--manifest",
        str(manifest),
        "--input-dir",
        str(input_dir),
        "--output-dir",
        str(output_dir),
        "--blender",
        str(fake_blender),
    ]
    if force:
        arguments.append("--force")
    return types.SimpleNamespace(
        root=case_root,
        input=input_dir,
        output=output_dir,
        source=source,
        source_sidecar=source_sidecar,
        source_bytes=source.read_bytes(),
        sidecar_bytes=source_sidecar.read_bytes(),
        final_glb=final_glb,
        final_json=final_json,
        old_pair=old_pair,
        force=force,
        fake_log=fake_log,
        fake_audit=fake_audit,
        arguments=arguments,
    )


def run_main(case: types.SimpleNamespace) -> tuple[int, str, str]:
    environment = {
        "PATH": os.environ.get("PATH", os.defpath),
        "FAKE_BLENDER_MODE": "success",
        "FAKE_BLENDER_LOG": str(case.fake_log),
        "FAKE_BLENDER_AUDIT": str(case.fake_audit),
        "PYTHONDONTWRITEBYTECODE": "1",
    }
    stdout = io.StringIO()
    stderr = io.StringIO()
    with (
        mock.patch.dict(os.environ, environment, clear=True),
        contextlib.redirect_stdout(stdout),
        contextlib.redirect_stderr(stderr),
    ):
        result = module.main(case.arguments)
    assert isinstance(result, int)
    return result, stdout.getvalue(), stderr.getvalue()


def assert_fake_reached(case: types.SimpleNamespace) -> None:
    assert case.fake_audit.read_text(encoding="utf-8").splitlines() == [
        "version",
        "asset",
    ]
    records = [
        json.loads(line)
        for line in case.fake_log.read_text(encoding="utf-8").splitlines()
    ]
    assert len(records) == 1
    assert records[0]["target"] == 15_000


def staged_output_from_fake_record(case: types.SimpleNamespace) -> Path | None:
    if not case.fake_log.exists():
        return None
    records = [
        json.loads(line)
        for line in case.fake_log.read_text(encoding="utf-8").splitlines()
    ]
    if not records:
        return None
    argv = records[-1].get("argv")
    if not isinstance(argv, list) or "--output" not in argv:
        raise AssertionError("fake asset record has no output argument")
    index = argv.index("--output")
    if index + 1 >= len(argv) or not isinstance(argv[index + 1], str):
        raise AssertionError("fake asset record has an invalid output argument")
    return Path(argv[index + 1])


def assert_source_pair(
    case: types.SimpleNamespace,
    expected_source: bytes,
    expected_sidecar: bytes,
) -> None:
    assert case.source.read_bytes() == expected_source
    assert case.source_sidecar.read_bytes() == expected_sidecar


def assert_one_diagnostic(stderr: str, policy_fragment: str | None = None) -> None:
    assert stderr.startswith("glb-decimation: "), repr(stderr)
    assert stderr.endswith("\n"), repr(stderr)
    assert len(stderr.splitlines()) == 1, repr(stderr)
    assert stderr[:-1].isprintable(), repr(stderr)
    assert len(stderr.encode("utf-8")) <= 512, repr(stderr)
    if policy_fragment is not None:
        assert policy_fragment in stderr, repr(stderr)


def assert_failed_terminal(
    case: types.SimpleNamespace,
    result: int,
    stdout: str,
    stderr: str,
) -> None:
    assert result != 0, (result, stdout, stderr)
    assert "output_triangles=" not in stdout, stdout
    assert_one_diagnostic(stderr)
    if case.force:
        assert case.old_pair is not None
        assert case.final_glb.read_bytes() == case.old_pair[0]
        assert case.final_json.read_bytes() == case.old_pair[1]
        assert set(path.name for path in case.output.iterdir()) == {
            case.final_glb.name,
            case.final_json.name,
        }
    else:
        assert not case.final_glb.exists()
        assert not case.final_json.exists()
        assert list(case.output.iterdir()) == []


def control_case(label: str, force: bool) -> None:
    case = setup_case(label, force=force)
    result, stdout, stderr = run_main(case)
    assert result == 0, (stdout, stderr)
    assert_fake_reached(case)
    assert_source_pair(case, case.source_bytes, case.sidecar_bytes)
    assert stderr == ""
    assert stdout.count("output_triangles=") == 1
    assert case.final_glb.read_bytes()[:4] == b"glTF"
    record = json.loads(case.final_json.read_text(encoding="utf-8"))
    assert record["derivative"]["sha256"] == digest_file(case.final_glb)
    assert set(path.name for path in case.output.iterdir()) == {
        case.final_glb.name,
        case.final_json.name,
    }


def staged_mutation_case(
    label: str,
    member: str,
    force: bool,
    mutation: str,
) -> None:
    case = setup_case(label, force=force)
    real_promote = module.promote_pair
    mutation_reached = False
    sensitive_value = "staged-sensitive-value"

    def mutate_then_promote(*args, **kwargs):
        nonlocal mutation_reached
        mutation_reached = True
        staged_glb = Path(args[0])
        staged_json = Path(args[1])
        target = staged_glb if member == "glb" else staged_json
        accepted_bytes = target.read_bytes()
        accepted_sha = digest_bytes(accepted_bytes)
        if mutation == "invalid" and member == "glb":
            target.write_bytes(b"not a GLB")
        elif mutation == "invalid":
            record = json.loads(staged_json.read_text(encoding="utf-8"))
            record["source"]["provenance"]["task_id"] = "forged-lineage"
            record["tool"]["name"] = "forged-tool"
            record["forbidden_" + "secret"] = sensitive_value
            staged_json.write_text(
                json.dumps(record, indent=2, sort_keys=True) + "\n",
                encoding="utf-8",
            )
        elif mutation == "same-length" and member == "glb":
            accepted_metrics = module.inspect_glb(staged_glb)
            json_length, chunk_kind = struct.unpack_from("<I4s", accepted_bytes, 12)
            assert chunk_kind == b"JSON"
            chunk_end = 20 + json_length
            json_chunk = accepted_bytes[20:chunk_end]
            padding = len(json_chunk) - len(json_chunk.rstrip(b" "))
            assert padding > 0
            unpadded_json = json_chunk[:-padding]
            assert b"," in unpadded_json
            replacement_chunk = unpadded_json.replace(b",", b", ", 1)
            replacement_chunk += b" " * (padding - 1)
            assert len(replacement_chunk) == len(json_chunk)
            replacement = bytearray(accepted_bytes)
            replacement[20:chunk_end] = replacement_chunk
            replacement_bytes = bytes(replacement)
            assert len(replacement_bytes) == len(accepted_bytes)
            assert json.loads(unpadded_json) == json.loads(
                replacement_bytes[20:chunk_end].rstrip(b" ")
            )
            staged_glb.write_bytes(replacement_bytes)
            replacement_metrics = module.inspect_glb(staged_glb)
            for key in set(accepted_metrics) - {"path", "sha256"}:
                assert replacement_metrics[key] == accepted_metrics[key], key
        elif mutation == "same-length":
            accepted_record = json.loads(accepted_bytes)
            marker = b"\n  \""
            assert marker in accepted_bytes
            replacement_bytes = accepted_bytes.replace(
                marker, b"\n\t \"", 1
            )
            assert len(replacement_bytes) == len(accepted_bytes)
            assert json.loads(replacement_bytes) == accepted_record
            staged_json.write_bytes(replacement_bytes)
        else:
            raise AssertionError(f"unsupported staged mutation {mutation}")
        replacement_bytes = target.read_bytes()
        assert digest_bytes(replacement_bytes) != accepted_sha
        if mutation == "same-length":
            assert len(replacement_bytes) == len(accepted_bytes)
        return real_promote(*args, **kwargs)

    with mock.patch.object(module, "promote_pair", mutate_then_promote):
        result, stdout, stderr = run_main(case)
    assert mutation_reached
    assert_fake_reached(case)
    assert_source_pair(case, case.source_bytes, case.sidecar_bytes)
    assert sensitive_value not in stdout + stderr
    assert_failed_terminal(case, result, stdout, stderr)
    for path in case.output.iterdir():
        if path.is_file():
            assert sensitive_value.encode("utf-8") not in path.read_bytes()


def original_mutation_case(label: str, member: str, force: bool) -> None:
    case = setup_case(label, force=force)
    real_write = module.write_staged_provenance
    real_promote = module.promote_pair
    real_replace = os.replace
    mutation_reached = False
    promotion_calls = 0
    final_rename_touches: list[tuple[Path, Path]] = []
    changed_source = case.source_bytes
    changed_sidecar = case.sidecar_bytes

    def write_then_mutate(path, record):
        nonlocal mutation_reached, changed_source, changed_sidecar
        real_write(path, record)
        mutation_reached = True
        if member == "source":
            changed_source = case.source_bytes + b"\0\0\0\0"
            case.source.write_bytes(changed_source)
        else:
            changed = json.loads(case.sidecar_bytes)
            changed["note"] = "changed after validation"
            case.source_sidecar.write_text(
                json.dumps(changed, indent=2, sort_keys=True) + "\n",
                encoding="utf-8",
            )
            changed_sidecar = case.source_sidecar.read_bytes()

    def observing_promote(*args, **kwargs):
        nonlocal promotion_calls
        promotion_calls += 1
        return real_promote(*args, **kwargs)

    def observing_replace(source, destination, *args, **kwargs):
        source_path = Path(os.path.realpath(os.path.abspath(source)))
        destination_path = Path(os.path.realpath(os.path.abspath(destination)))
        finals = {
            Path(os.path.realpath(case.final_glb)),
            Path(os.path.realpath(case.final_json)),
        }
        if source_path in finals or destination_path in finals:
            final_rename_touches.append((source_path, destination_path))
        return real_replace(source, destination, *args, **kwargs)

    with (
        mock.patch.object(module, "write_staged_provenance", write_then_mutate),
        mock.patch.object(module, "promote_pair", observing_promote),
        mock.patch.object(module.os, "replace", observing_replace),
    ):
        result, stdout, stderr = run_main(case)
    assert mutation_reached
    assert_fake_reached(case)
    # The pipeline detects but never repairs or otherwise rewrites an input that
    # another actor permanently changed.
    assert_source_pair(case, changed_source, changed_sidecar)
    assert_failed_terminal(case, result, stdout, stderr)
    assert promotion_calls == 0, "original custody was checked after promotion began"
    assert final_rename_touches == [], (
        "original custody was checked only after a final-name rename",
        final_rename_touches,
    )


def diagnostic_control_case() -> None:
    stdout = io.StringIO()
    stderr = io.StringIO()
    with (
        mock.patch.object(
            module,
            "_run",
            side_effect=module.DecimationError("external URI rejected by policy"),
        ),
        contextlib.redirect_stdout(stdout),
        contextlib.redirect_stderr(stderr),
    ):
        result = module.main([])
    assert result != 0
    assert stdout.getvalue() == ""
    assert_one_diagnostic(stderr.getvalue(), "external URI rejected by policy")


def signed_uri_diagnostic_case() -> None:
    scheme = "https"
    user_value = "redact-user-value"
    password_value = "redact-password-value"
    host_value = "private-host.example.invalid"
    path_value = "signed-path-value"
    credential_value = "redact-credential-value"
    signature_value = "redact-signature-value"
    token_value = "redact-token-value"
    fragment_value = "redact-fragment-value"
    signed_uri = (
        f"{scheme}://{user_value}:{password_value}@{host_value}/{path_value}"
        f"?X-Amz-Credential={credential_value}"
        f"&X-Amz-Signature={signature_value}"
        f"&token={token_value}#{fragment_value}"
    )
    stdout = io.StringIO()
    stderr = io.StringIO()
    with (
        mock.patch.object(
            module,
            "_run",
            side_effect=module.DecimationError(
                f"external URI rejected by policy: {signed_uri}"
            ),
        ),
        contextlib.redirect_stdout(stdout),
        contextlib.redirect_stderr(stderr),
    ):
        result = module.main([])
    diagnostic = stderr.getvalue()
    assert result != 0
    assert stdout.getvalue() == ""
    assert_one_diagnostic(diagnostic, "external URI rejected by policy")
    folded_diagnostic = diagnostic.casefold()
    for forbidden in (
        scheme,
        user_value,
        password_value,
        host_value,
        path_value,
        "X-Amz-Credential",
        credential_value,
        "X-Amz-Signature",
        signature_value,
        "token=",
        token_value,
        fragment_value,
    ):
        assert forbidden.casefold() not in folded_diagnostic, (forbidden, diagnostic)


class CandidateReadLimit(RuntimeError):
    pass


class CountingReader:
    def __init__(self, handle, guard) -> None:
        self.handle = handle
        self.guard = guard

    def _payload(self, operation: str, payload):
        self.guard.observe(len(payload), operation)
        return payload

    def read(self, *args, **kwargs):
        return self._payload("file.read", self.handle.read(*args, **kwargs))

    def read1(self, *args, **kwargs):
        return self._payload("file.read1", self.handle.read1(*args, **kwargs))

    def readall(self):
        return self._payload("file.readall", self.handle.readall())

    def readinto(self, buffer):
        count = self.handle.readinto(buffer)
        self.guard.observe(0 if count is None else count, "file.readinto")
        return count

    def readinto1(self, buffer):
        count = self.handle.readinto1(buffer)
        self.guard.observe(0 if count is None else count, "file.readinto1")
        return count

    def readline(self, *args, **kwargs):
        return self._payload("file.readline", self.handle.readline(*args, **kwargs))

    def readlines(self, *args, **kwargs):
        lines = self.handle.readlines(*args, **kwargs)
        self.guard.observe(sum(len(line) for line in lines), "file.readlines")
        return lines

    def peek(self, *args, **kwargs):
        return self._payload("file.peek", self.handle.peek(*args, **kwargs))

    def __iter__(self):
        return self

    def __next__(self):
        return self._payload("file iteration", next(self.handle))

    def __enter__(self):
        self.handle.__enter__()
        return self

    def __exit__(self, *args):
        return self.handle.__exit__(*args)

    def __getattr__(self, name):
        return getattr(self.handle, name)


class CumulativeReadGuard:
    def __init__(self, maximum_bytes: int, *, strict: bool) -> None:
        self.maximum_bytes = maximum_bytes
        self.strict = strict
        self.identity: tuple[int, int] | None = None
        self.total_bytes = 0
        self.over_limit = False
        self.target_opens = 0
        self.operations: list[str] = []
        self.real_builtin_open = builtins.open
        self.real_io_open = io.open
        self.real_os_open = os.open
        self.real_fdopen = os.fdopen
        self.real_read = os.read
        self.real_copyfile = shutil.copyfile
        self.real_copy = shutil.copy
        self.real_copy2 = shutil.copy2
        self.real_copyfileobj = shutil.copyfileobj
        self.real_mmap = mmap.mmap

    def arm(self, path: Path) -> None:
        status = os.lstat(path)
        assert stat.S_ISREG(status.st_mode)
        self.identity = (status.st_dev, status.st_ino)

    def fd_is_target(self, descriptor: int) -> bool:
        if self.identity is None:
            return False
        try:
            status = os.fstat(descriptor)
        except OSError:
            return False
        return (status.st_dev, status.st_ino) == self.identity

    def path_is_target(self, path) -> bool:
        if isinstance(path, int):
            return self.fd_is_target(path)
        if self.identity is None:
            return False
        try:
            status = os.lstat(path)
        except (OSError, TypeError, ValueError):
            return False
        return (status.st_dev, status.st_ino) == self.identity

    def observe(self, count: int, operation: str) -> None:
        if count <= 0:
            return
        self.total_bytes += count
        self.operations.append(operation)
        if self.total_bytes > self.maximum_bytes:
            self.over_limit = True
            if self.strict:
                raise CandidateReadLimit(operation)

    def wrap_handle(self, handle):
        if isinstance(handle, CountingReader) and handle.guard is self:
            return handle
        return CountingReader(handle, self)

    def guarded_builtin_open(self, file, *args, **kwargs):
        handle = self.real_builtin_open(file, *args, **kwargs)
        if self.path_is_target(file):
            self.target_opens += 1
            return self.wrap_handle(handle)
        return handle

    def guarded_io_open(self, file, *args, **kwargs):
        handle = self.real_io_open(file, *args, **kwargs)
        if self.path_is_target(file):
            self.target_opens += 1
            return self.wrap_handle(handle)
        return handle

    def guarded_os_open(self, path, *args, **kwargs):
        descriptor = self.real_os_open(path, *args, **kwargs)
        if self.fd_is_target(descriptor):
            self.target_opens += 1
        return descriptor

    def guarded_fdopen(self, descriptor, *args, **kwargs):
        handle = self.real_fdopen(descriptor, *args, **kwargs)
        if self.fd_is_target(descriptor):
            return self.wrap_handle(handle)
        return handle

    def guarded_read(self, descriptor, count):
        payload = self.real_read(descriptor, count)
        if self.fd_is_target(descriptor):
            self.observe(len(payload), "os.read")
        return payload

    def _copy_observed(self, operation, source, destination, *args, **kwargs):
        target = self.path_is_target(source)
        before = self.total_bytes
        result = operation(source, destination, *args, **kwargs)
        if target and self.total_bytes == before:
            self.observe(os.lstat(destination).st_size, operation.__name__)
        return result

    def guarded_copyfile(self, source, destination, *args, **kwargs):
        return self._copy_observed(
            self.real_copyfile, source, destination, *args, **kwargs
        )

    def guarded_copy(self, source, destination, *args, **kwargs):
        return self._copy_observed(
            self.real_copy, source, destination, *args, **kwargs
        )

    def guarded_copy2(self, source, destination, *args, **kwargs):
        return self._copy_observed(
            self.real_copy2, source, destination, *args, **kwargs
        )

    def guarded_copyfileobj(self, source, destination, *args, **kwargs):
        before = self.total_bytes
        target = False
        try:
            target = self.fd_is_target(source.fileno())
        except (AttributeError, OSError, ValueError):
            pass
        result = self.real_copyfileobj(source, destination, *args, **kwargs)
        if target and self.total_bytes == before:
            destination.flush()
            self.observe(os.fstat(destination.fileno()).st_size, "shutil.copyfileobj")
        return result

    def guarded_mmap(self, descriptor, length, *args, **kwargs):
        if self.fd_is_target(descriptor):
            count = os.fstat(descriptor).st_size if length == 0 else length
            self.observe(count, "mmap")
        return self.real_mmap(descriptor, length, *args, **kwargs)

    def patches(self):
        stack = contextlib.ExitStack()
        stack.enter_context(mock.patch.object(builtins, "open", self.guarded_builtin_open))
        stack.enter_context(mock.patch.object(io, "open", self.guarded_io_open))
        stack.enter_context(mock.patch.object(os, "open", self.guarded_os_open))
        stack.enter_context(mock.patch.object(os, "fdopen", self.guarded_fdopen))
        stack.enter_context(mock.patch.object(os, "read", self.guarded_read))
        stack.enter_context(mock.patch.object(shutil, "copyfile", self.guarded_copyfile))
        stack.enter_context(mock.patch.object(shutil, "copy", self.guarded_copy))
        stack.enter_context(mock.patch.object(shutil, "copy2", self.guarded_copy2))
        stack.enter_context(
            mock.patch.object(shutil, "copyfileobj", self.guarded_copyfileobj)
        )
        stack.enter_context(mock.patch.object(mmap, "mmap", self.guarded_mmap))

        if hasattr(os, "pread"):
            real_pread = os.pread

            def guarded_pread(descriptor, count, offset):
                payload = real_pread(descriptor, count, offset)
                if self.fd_is_target(descriptor):
                    self.observe(len(payload), "os.pread")
                return payload

            stack.enter_context(mock.patch.object(os, "pread", guarded_pread))
        if hasattr(os, "readv"):
            real_readv = os.readv

            def guarded_readv(descriptor, buffers):
                count = real_readv(descriptor, buffers)
                if self.fd_is_target(descriptor):
                    self.observe(count, "os.readv")
                return count

            stack.enter_context(mock.patch.object(os, "readv", guarded_readv))
        if hasattr(os, "preadv"):
            real_preadv = os.preadv

            def guarded_preadv(descriptor, buffers, offset, *args):
                count = real_preadv(descriptor, buffers, offset, *args)
                if self.fd_is_target(descriptor):
                    self.observe(count, "os.preadv")
                return count

            stack.enter_context(mock.patch.object(os, "preadv", guarded_preadv))
        if hasattr(os, "sendfile"):
            real_sendfile = os.sendfile

            def guarded_sendfile(destination, source, offset, count, *args, **kwargs):
                transferred = real_sendfile(
                    destination, source, offset, count, *args, **kwargs
                )
                if self.fd_is_target(source):
                    self.observe(transferred, "os.sendfile")
                return transferred

            stack.enter_context(mock.patch.object(os, "sendfile", guarded_sendfile))
        if hasattr(os, "copy_file_range"):
            real_copy_range = os.copy_file_range

            def guarded_copy_range(source, destination, count, *args, **kwargs):
                transferred = real_copy_range(
                    source, destination, count, *args, **kwargs
                )
                if self.fd_is_target(source):
                    self.observe(transferred, "os.copy_file_range")
                return transferred

            stack.enter_context(
                mock.patch.object(os, "copy_file_range", guarded_copy_range)
            )
        return stack


def prove_cumulative_reader_oracle(case: types.SimpleNamespace) -> None:
    mib = 1024 * 1024
    control_cap = 2 * mib
    control_size = control_cap + 4
    control = case.root / "reader-control.bin"
    control.write_bytes(b"R" * control_size)
    scratch = case.root / "reader-copy.bin"

    def file_chunks(opener, method="read"):
        with opener(control, "rb") as handle:
            while True:
                if method == "readinto":
                    count = handle.readinto(bytearray(mib))
                    if not count:
                        break
                else:
                    payload = getattr(handle, method)(mib)
                    if not payload:
                        break

    def descriptor_chunks(operation):
        descriptor = os.open(control, os.O_RDONLY)
        try:
            offset = 0
            while True:
                count = operation(descriptor, offset)
                if not count:
                    break
                offset += count
        finally:
            os.close(descriptor)

    def fdopen_chunks():
        descriptor = os.open(control, os.O_RDONLY)
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            while handle.read(mib):
                pass

    def mmap_whole():
        descriptor = os.open(control, os.O_RDONLY)
        try:
            mapping = mmap.mmap(descriptor, 0, access=mmap.ACCESS_READ)
            mapping.close()
        finally:
            os.close(descriptor)

    def copyfileobj_chunks():
        with (control.open("rb") as source, scratch.open("wb") as destination):
            shutil.copyfileobj(source, destination, length=mib)

    def mixed_read_surfaces():
        with builtins.open(control, "rb") as handle:
            assert len(handle.read(mib)) == mib
        descriptor = os.open(control, os.O_RDONLY)
        try:
            os.lseek(descriptor, mib, os.SEEK_SET)
            assert len(os.read(descriptor, mib)) == mib
        finally:
            os.close(descriptor)
        with io.open(control, "rb") as handle:
            handle.seek(2 * mib)
            handle.read(4)

    readers = {
        "path.read_bytes": control.read_bytes,
        "builtins.open": lambda: file_chunks(builtins.open),
        "io.open": lambda: file_chunks(io.open),
        "file.readinto": lambda: file_chunks(io.open, "readinto"),
        "os.read": lambda: descriptor_chunks(
            lambda descriptor, _offset: len(os.read(descriptor, mib))
        ),
        "os.fdopen": fdopen_chunks,
        "mmap": mmap_whole,
        "shutil.copyfile": lambda: shutil.copyfile(control, scratch),
        "shutil.copy": lambda: shutil.copy(control, scratch),
        "shutil.copy2": lambda: shutil.copy2(control, scratch),
        "shutil.copyfileobj": copyfileobj_chunks,
        "mixed public surfaces": mixed_read_surfaces,
    }
    if hasattr(os, "pread"):
        readers["os.pread"] = lambda: descriptor_chunks(
            lambda descriptor, offset: len(os.pread(descriptor, mib, offset))
        )
    if hasattr(os, "readv"):
        readers["os.readv"] = lambda: descriptor_chunks(
            lambda descriptor, _offset: os.readv(descriptor, [bytearray(mib)])
        )
    if hasattr(os, "preadv"):
        readers["os.preadv"] = lambda: descriptor_chunks(
            lambda descriptor, offset: os.preadv(
                descriptor, [bytearray(mib)], offset
            )
        )
    sendfile_regular_file_supported = False
    if hasattr(os, "sendfile"):
        source_descriptor = os.open(control, os.O_RDONLY)
        destination_descriptor = os.open(
            scratch, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600
        )
        try:
            try:
                sendfile_regular_file_supported = (
                    os.sendfile(
                        destination_descriptor,
                        source_descriptor,
                        0,
                        1,
                    )
                    == 1
                )
            except OSError:
                pass
        finally:
            os.close(destination_descriptor)
            os.close(source_descriptor)
            scratch.unlink(missing_ok=True)
    if sendfile_regular_file_supported:
        def sendfile_chunks():
            source = os.open(control, os.O_RDONLY)
            destination = os.open(scratch, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
            try:
                offset = 0
                while True:
                    count = os.sendfile(destination, source, offset, mib)
                    if not count:
                        break
                    offset += count
            finally:
                os.close(destination)
                os.close(source)

        readers["os.sendfile"] = sendfile_chunks
    if hasattr(os, "copy_file_range"):
        def copy_range_chunks():
            source = os.open(control, os.O_RDONLY)
            destination = os.open(scratch, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
            try:
                while os.copy_file_range(source, destination, mib):
                    pass
            finally:
                os.close(destination)
                os.close(source)

        readers["os.copy_file_range"] = copy_range_chunks

    for name, reader in readers.items():
        guard = CumulativeReadGuard(control_cap, strict=True)
        guard.arm(control)
        caught = False
        try:
            with guard.patches():
                reader()
        except CandidateReadLimit:
            caught = True
        finally:
            scratch.unlink(missing_ok=True)
        assert caught, f"reader oracle missed {name}"
        assert guard.over_limit, f"reader oracle did not cross its cap for {name}"
        assert guard.total_bytes == control_size, (
            name,
            guard.total_bytes,
            control_size,
            guard.operations,
        )
    control.unlink()


def candidate_cap_race_case() -> None:
    case = setup_case("candidate-cap-race")
    derivative_cap = 64 * 1024 * 1024
    guard = CumulativeReadGuard(derivative_cap, strict=False)
    real_inspect = module.inspect_glb
    mutation_count = 0

    prove_cumulative_reader_oracle(case)

    def grow_at_inspection(path):
        nonlocal mutation_count
        candidate = staged_output_from_fake_record(case)
        inspected = Path(path)
        if candidate is not None and inspected == candidate and mutation_count == 0:
            _pad_glb_to_size(inspected, derivative_cap + 4)
            guard.arm(inspected)
            mutation_count += 1
        return real_inspect(path)

    with (
        guard.patches(),
        mock.patch.object(module, "inspect_glb", grow_at_inspection),
    ):
        result, stdout, stderr = run_main(case)
    assert mutation_count == 1
    assert_fake_reached(case)
    assert_source_pair(case, case.source_bytes, case.sidecar_bytes)
    assert_failed_terminal(case, result, stdout, stderr)
    assert not guard.over_limit, (
        "candidate read exceeded the derivative cap",
        guard.total_bytes,
        guard.operations,
        {"rejected_before_data_read": guard.total_bytes == 0},
    )


def error_classification_case(
    label: str,
    inspector_message: str,
    expect_lost_uv: bool,
) -> None:
    case = setup_case(label)
    real_inspect = module.inspect_glb
    injected = False

    def fail_derivative(path):
        nonlocal injected
        candidate = Path(path)
        staged_output = staged_output_from_fake_record(case)
        if staged_output is not None and candidate == staged_output:
            injected = True
            raise module.GlbError(inspector_message)
        return real_inspect(path)

    with mock.patch.object(module, "inspect_glb", fail_derivative):
        result, stdout, stderr = run_main(case)
    assert injected
    assert_fake_reached(case)
    assert_source_pair(case, case.source_bytes, case.sidecar_bytes)
    assert_failed_terminal(case, result, stdout, stderr)
    if expect_lost_uv:
        assert "lost UV" in stderr, stderr
    else:
        assert "lost UV" not in stderr, stderr


def arbitrary_uv_mapping_case() -> None:
    # Select three runtime-derived values per axis, then exercise their full
    # Cartesian product. This contains mesh-only, primitive-only, UV-only,
    # pairwise, and all-axis changes without publishing a finite tuple lookup.
    seed = os.lstat(root).st_ino ^ os.getpid()
    meshes = (seed % 97 + 1, seed % 97 + 102, seed % 997 + 1000)
    primitives = (seed % 89 + 1, seed % 89 + 96, seed % 991 + 1000)
    uv_sets = (seed % 5 + 1, seed % 5 + 8, seed % 983 + 1000)
    selected = {
        (mesh, primitive, uv_set)
        for mesh in meshes
        for primitive in primitives
        for uv_set in uv_sets
    }
    assert len(selected) == 27
    base = (meshes[0], primitives[0], uv_sets[0])
    axis_witnesses = {
        "mesh-only": (meshes[2], primitives[0], uv_sets[0]),
        "primitive-only": (meshes[0], primitives[2], uv_sets[0]),
        "uv-only": (meshes[0], primitives[0], uv_sets[2]),
        "all-axes": (meshes[2], primitives[2], uv_sets[2]),
    }
    assert all(witness in selected for witness in axis_witnesses.values())
    assert sum(left != right for left, right in zip(base, axis_witnesses["mesh-only"])) == 1
    assert sum(left != right for left, right in zip(base, axis_witnesses["primitive-only"])) == 1
    assert sum(left != right for left, right in zip(base, axis_witnesses["uv-only"])) == 1
    assert sum(left != right for left, right in zip(base, axis_witnesses["all-axes"])) == 3
    for axis, label in enumerate(("mesh-only", "primitive-only", "uv-only")):
        witness = axis_witnesses[label]
        assert witness[axis] >= 1000 and len(str(witness[axis])) >= 4
        assert not all(len(str(value)) <= 3 for value in witness), (
            "digit-width mutant accepted a large-axis witness",
            label,
            witness,
        )
    for ordinal, (mesh, primitive, uv_set) in enumerate(sorted(selected)):
        error_classification_case(
            f"arbitrary-uv-{ordinal}",
            f"meshes[{mesh}].primitives[{primitive}] material references "
            f"missing TEXCOORD_{uv_set}",
            True,
        )


def child_entry(result_queue, function, arguments) -> None:
    try:
        try:
            os.setsid()
        except OSError:
            pass
        function(*arguments)
    except BaseException:
        result_queue.put(("error", traceback.format_exc()))
    else:
        result_queue.put(("ok", ""))


def terminate_process_tree(process) -> None:
    if not process.is_alive():
        return
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except (ProcessLookupError, PermissionError):
        process.terminate()
    process.join(2)
    if process.is_alive():
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except (ProcessLookupError, PermissionError):
            process.kill()
        process.join(2)


def run_bounded(label: str, function, *arguments) -> None:
    result_queue = process_context.Queue()
    process = process_context.Process(
        target=child_entry,
        args=(result_queue, function, arguments),
        name=f"glb-review-l-{label}",
    )
    process.start()
    process.join(15)
    if process.is_alive():
        terminate_process_tree(process)
        errors.append(f"{label}: bounded probe hung")
    else:
        try:
            state, detail = result_queue.get(timeout=1)
        except queue_module.Empty:
            errors.append(f"{label}: child exited {process.exitcode} without a result")
        else:
            if state != "ok":
                errors.append(f"{label}:\n{detail}")
        if process.exitcode not in (0, None):
            errors.append(f"{label}: child exit was {process.exitcode}")
    result_queue.close()
    result_queue.join_thread()
    process.close()


# Positive controls ensure the real fake-Blender/promotion path is live in both
# absent-destination and force modes before any fault is injected.
run_bounded("control-absent", control_case, "control-absent", False)
run_bounded("control-force", control_case, "control-force", True)

for force in (False, True):
    mode = "force" if force else "absent"
    for member in ("glb", "json"):
        run_bounded(
            f"staged-{member}-invalid-{mode}",
            staged_mutation_case,
            f"staged-{member}-invalid-{mode}",
            member,
            force,
            "invalid",
        )
        run_bounded(
            f"staged-{member}-same-length-{mode}",
            staged_mutation_case,
            f"staged-{member}-same-length-{mode}",
            member,
            force,
            "same-length",
        )
    for member in ("source", "sidecar"):
        run_bounded(
            f"original-{member}-{mode}",
            original_mutation_case,
            f"original-{member}-{mode}",
            member,
            force,
        )

run_bounded("diagnostic-control", diagnostic_control_case)
run_bounded("signed-uri-redaction", signed_uri_diagnostic_case)
run_bounded("candidate-cap-race", candidate_cap_race_case)
run_bounded(
    "exact-lost-uv-control",
    error_classification_case,
    "exact-lost-uv-control",
    "meshes[0].primitives[0] material references missing TEXCOORD_0",
    True,
)
run_bounded(
    "indexed-lost-uv-control",
    error_classification_case,
    "indexed-lost-uv-control",
    "meshes[7].primitives[11] material references missing TEXCOORD_3",
    True,
)
run_bounded("arbitrary-selected-uv-mapping", arbitrary_uv_mapping_case)
run_bounded(
    "lost-uv-prefix-near-miss",
    error_classification_case,
    "lost-uv-prefix-near-miss",
    "context: meshes[0].primitives[0] material references missing TEXCOORD_0",
    False,
)
run_bounded(
    "lost-uv-suffix-near-miss",
    error_classification_case,
    "lost-uv-suffix-near-miss",
    "meshes[0].primitives[0] material references missing TEXCOORD_0; context",
    False,
)
run_bounded(
    "generic-inspector-error-control",
    error_classification_case,
    "generic-inspector-error-control",
    "derivative integrity check failed",
    False,
)

if errors:
    raise AssertionError(
        "acceptance/publish hardening regressions:\n- " + "\n- ".join(errors)
    )
PY
  assert_no_external_effects
fi

if [ "$review_section" = L ]; then
  printf 'glb-decimation review L: pass\n'
  exit 0
fi

# Review hardening M: a transaction reports the terminal state it actually
# leaves, every later read retains the member's original class cap, generated
# transaction names fit the selected filesystem, child environment names are an
# exact allowlist, and each child stream is bounded to the existing 1 MiB
# metadata envelope before any optional 512-byte public record. The 1 MiB child
# stream ceiling is a test-frozen implementation clarification: the frozen
# contract requires bounded subprocess output but did not assign a second cap.
if [ "$review_section" = all ] || [ "$review_section" = M ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - "$repo" <<'PY'
import ast
import sys
from pathlib import Path


repo = Path(sys.argv[1])
drivers = (
    repo / "tests/assets/glb-decimation-pipeline.test.sh",
    repo / "tests/assets/glb-metrics.test.sh",
    repo / "tests/assets/glb-silhouette.test.sh",
    repo / "tests/assets/glb_fixture.py",
    repo / "tests/assets/fake_blender.py",
)


def embedded_python_sources(path):
    text = path.read_text(encoding="utf-8")
    if path.suffix == ".py":
        return [(str(path), text)]
    lines = text.splitlines()
    sources = []
    index = 0
    while index < len(lines):
        if "<<'PY'" not in lines[index]:
            index += 1
            continue
        start = index + 1
        end = start
        while end < len(lines) and lines[end] != "PY":
            end += 1
        if end == len(lines):
            raise AssertionError(f"{path}:{index + 1}: unterminated Python driver")
        sources.append(
            (
                f"{path}:{start + 1}-{end}",
                "\n".join(lines[start:end]) + "\n",
            )
        )
        index = end + 1
    return sources


def portability_violations(label, source):
    try:
        tree = ast.parse(source, filename=label, feature_version=9)
    except SyntaxError as exc:
        return [f"{label}: not Python 3.9 grammar: {exc.msg} at line {exc.lineno}"]

    future_annotations = any(
        isinstance(node, ast.ImportFrom)
        and node.module == "__future__"
        and any(alias.name == "annotations" for alias in node.names)
        for node in tree.body
    )
    failures = []
    for node in ast.walk(tree):
        annotation = None
        if isinstance(node, ast.arg):
            annotation = node.annotation
        elif isinstance(node, ast.AnnAssign):
            annotation = node.annotation
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            annotation = node.returns
        if annotation is not None and any(
            isinstance(child, ast.BinOp) and isinstance(child.op, ast.BitOr)
            for child in ast.walk(annotation)
        ) and not future_annotations:
            failures.append(
                f"{label}:{node.lineno}: runtime-evaluated union annotation"
            )
        if (
            isinstance(node, ast.Call)
            and isinstance(node.func, ast.Name)
            and node.func.id == "zip"
            and any(keyword.arg == "strict" for keyword in node.keywords)
        ):
            failures.append(f"{label}:{node.lineno}: zip keyword requires Python 3.10")
    return failures


assert not portability_violations(
    "compatible-control",
    "from __future__ import annotations\ndef f(value: str | None) -> None:\n    pass\n",
)
assert any(
    "runtime-evaluated union annotation" in failure
    for failure in portability_violations(
        "union-mutation",
        "def f(value: str | None):\n    pass\n",
    )
)
assert any(
    "zip keyword requires Python 3.10" in failure
    for failure in portability_violations(
        "zip-mutation",
        "list(zip((1,), (2,), strict=True))\n",
    )
)

violations = []
for driver in drivers:
    for label, source in embedded_python_sources(driver):
        violations.extend(portability_violations(label, source))
if violations:
    raise AssertionError("Python 3.9 driver regressions:\n- " + "\n- ".join(violations))
PY

  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-transaction-terminal" "$repo" \
    "$fake_blender" <<'PY'
from __future__ import annotations

import builtins
import contextlib
import hashlib
import importlib.util
import io
import json
import mmap
import multiprocessing
import os
import queue as queue_module
import select
import selectors
import shutil
import signal
import stat
import struct
import subprocess
import sys
import time
import traceback
import types
from pathlib import Path
from unittest import mock


script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from fake_blender import _pad_glb_to_size  # noqa: E402
from glb_fixture import write_glb  # noqa: E402

spec = importlib.util.spec_from_file_location(
    "decimate_assets_transaction_terminal_test", script
)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

MAX_METADATA_BYTES = 1_048_576
MAX_PROVENANCE_BYTES = 2_097_152
MAX_DERIVATIVE_BYTES = 67_108_864
MAX_CHILD_STREAM_BYTES = MAX_METADATA_BYTES
MAX_DIAGNOSTIC_BYTES = 512
MAX_TRANSACTION_SAFE_FILENAME_BYTES = 208
MAX_OVERFLOW_SECONDS = 4
MAX_PUBLIC_CAPTURE_BYTES = 4 * MAX_DIAGNOSTIC_BYTES

process_context = multiprocessing.get_context("fork")
errors: list[str] = []


def digest_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def digest_file(path: Path) -> str:
    return digest_bytes(path.read_bytes())


def setup_case(
    label: str,
    *,
    force: bool = False,
    filename: str = "candidate.glb",
    blender: Path | None = None,
) -> types.SimpleNamespace:
    case_root = root / label
    case_root.mkdir()
    input_dir = case_root / "input"
    output_dir = case_root / "output"
    input_dir.mkdir()
    output_dir.mkdir()
    source = input_dir / filename
    source_sidecar = Path(f"{source}.json")
    write_glb(source, triangles=30_000)
    source_sidecar.write_text(
        json.dumps(
            {
                "service": "meshy",
                "task_id": "fixture-task",
                "timestamp_utc": "2026-08-15T12:34:56Z",
                "plan_tier": "paid",
                "prompt": "transaction terminal fixture",
                "note": "local fixture",
                "sha256": digest_file(source),
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest = case_root / "manifest.json"
    manifest.write_text(
        json.dumps(
            {
                "assets": [
                    {
                        "id": "terminal-fixture",
                        "kind": "cat",
                        "service": "meshy",
                        "out": filename,
                        "prompt": "transaction terminal fixture",
                    }
                ]
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    final_glb = output_dir / filename
    final_json = Path(f"{final_glb}.json")
    old_pair = None
    old_identities = None
    if force:
        write_glb(final_glb, triangles=14_000)
        final_json.write_text(
            json.dumps({"generation": "frozen-old-pair"}, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        old_pair = (final_glb.read_bytes(), final_json.read_bytes())
        old_identities = {
            "glb": (os.lstat(final_glb).st_dev, os.lstat(final_glb).st_ino),
            "json": (os.lstat(final_json).st_dev, os.lstat(final_json).st_ino),
        }
    fake_log = case_root / "fake.log"
    fake_audit = case_root / "fake.audit"
    selected_blender = blender or fake_blender
    arguments = [
        "--manifest",
        str(manifest),
        "--input-dir",
        str(input_dir),
        "--output-dir",
        str(output_dir),
        "--blender",
        str(selected_blender),
    ]
    if force:
        arguments.append("--force")
    return types.SimpleNamespace(
        root=case_root,
        input=input_dir,
        output=output_dir,
        source=source,
        source_sidecar=source_sidecar,
        source_bytes=source.read_bytes(),
        sidecar_bytes=source_sidecar.read_bytes(),
        manifest=manifest,
        final_glb=final_glb,
        final_json=final_json,
        old_pair=old_pair,
        old_identities=old_identities,
        force=force,
        fake_log=fake_log,
        fake_audit=fake_audit,
        blender=selected_blender,
        arguments=arguments,
    )


def setup_batch_case(label: str, *, force: bool) -> types.SimpleNamespace:
    case = setup_case(label, force=force, filename="first.glb")
    second_source = case.input / "second.glb"
    second_sidecar = Path(f"{second_source}.json")
    write_glb(second_source, triangles=30_000)
    second_sidecar.write_text(
        json.dumps(
            {
                "service": "meshy",
                "task_id": "fixture-task-second",
                "timestamp_utc": "2026-08-15T12:34:56Z",
                "plan_tier": "paid",
                "prompt": "transaction terminal fixture second",
                "note": "local fixture",
                "sha256": digest_file(second_source),
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest = json.loads(case.manifest.read_text(encoding="utf-8"))
    manifest["assets"].append(
        {
            "id": "terminal-fixture-second",
            "kind": "cat",
            "service": "meshy",
            "out": second_source.name,
            "prompt": "transaction terminal fixture second",
        }
    )
    case.manifest.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    second_final_glb = case.output / second_source.name
    second_final_json = Path(f"{second_final_glb}.json")
    second_old_pair = None
    if force:
        write_glb(second_final_glb, triangles=14_000)
        second_final_json.write_text(
            json.dumps({"generation": "frozen-old-pair-second"}, sort_keys=True)
            + "\n",
            encoding="utf-8",
        )
        second_old_pair = (
            second_final_glb.read_bytes(),
            second_final_json.read_bytes(),
        )
    case.batch_pairs = [
        (case.final_glb, case.final_json, case.old_pair),
        (second_final_glb, second_final_json, second_old_pair),
    ]
    case.batch_sources = [
        (case.source, case.source_bytes),
        (case.source_sidecar, case.sidecar_bytes),
        (second_source, second_source.read_bytes()),
        (second_sidecar, second_sidecar.read_bytes()),
    ]
    return case


def setup_mixed_force_case(label: str) -> types.SimpleNamespace:
    case = setup_batch_case(label, force=True)
    second_final_glb, second_final_json, second_old_pair = case.batch_pairs[1]
    assert second_old_pair is not None
    second_final_glb.unlink()
    second_final_json.unlink()
    case.batch_pairs[1] = (second_final_glb, second_final_json, None)
    return case


def child_environment(
    case: types.SimpleNamespace,
    *,
    mode: str = "success",
    extra: dict[str, str] | None = None,
) -> dict[str, str]:
    environment = {
        "PATH": os.environ.get("PATH", os.defpath),
        "FAKE_BLENDER_MODE": mode,
        "FAKE_BLENDER_LOG": str(case.fake_log),
        "FAKE_BLENDER_AUDIT": str(case.fake_audit),
        "PYTHONDONTWRITEBYTECODE": "1",
    }
    if extra:
        environment.update(extra)
    return environment


def run_main(
    case: types.SimpleNamespace,
    *,
    mode: str = "success",
    extra_environment: dict[str, str] | None = None,
) -> tuple[int | None, str, str, BaseException | None]:
    stdout = io.StringIO()
    stderr = io.StringIO()
    caught = None
    result = None
    with (
        mock.patch.dict(
            os.environ,
            child_environment(case, mode=mode, extra=extra_environment),
            clear=True,
        ),
        contextlib.redirect_stdout(stdout),
        contextlib.redirect_stderr(stderr),
    ):
        try:
            result = module.main(case.arguments)
        except BaseException as exc:  # fault-injection exceptions are observed
            caught = exc
    return result, stdout.getvalue(), stderr.getvalue(), caught


def run_cli(
    case: types.SimpleNamespace,
    *,
    mode: str = "success",
    timeout: int = 15,
) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        [sys.executable, str(script), *case.arguments],
        check=False,
        capture_output=True,
        timeout=timeout,
        env=child_environment(case, mode=mode),
    )


def audit_lines(case: types.SimpleNamespace) -> list[str]:
    if not case.fake_audit.exists():
        return []
    return case.fake_audit.read_text(encoding="utf-8").splitlines()


def fake_records(case: types.SimpleNamespace) -> list[dict[str, object]]:
    if not case.fake_log.exists():
        return []
    return [
        json.loads(line)
        for line in case.fake_log.read_text(encoding="utf-8").splitlines()
    ]


def fake_argument_path(case: types.SimpleNamespace, flag: str) -> Path:
    records = fake_records(case)
    assert len(records) == 1
    argv = records[0].get("argv")
    assert isinstance(argv, list) and flag in argv
    index = argv.index(flag)
    assert index + 1 < len(argv) and isinstance(argv[index + 1], str)
    return Path(argv[index + 1])


def path_identity(path: Path) -> tuple[int, int] | None:
    try:
        status = os.lstat(path)
    except OSError:
        return None
    return status.st_dev, status.st_ino


def observed_path_key(path: Path) -> str:
    return os.path.realpath(os.path.abspath(os.fspath(path)))


def same_observed_path(left: Path, right: Path) -> bool:
    return observed_path_key(left) == observed_path_key(right)


def public_final_members(case: types.SimpleNamespace) -> set[Path]:
    pairs = getattr(
        case,
        "batch_pairs",
        [(case.final_glb, case.final_json, case.old_pair)],
    )
    return {
        member
        for final_glb, final_json, _ in pairs
        for member in (final_glb, final_json)
    }


def assert_fake_reached(case: types.SimpleNamespace) -> None:
    assert audit_lines(case) == ["version", "asset"]
    records = fake_records(case)
    assert len(records) == 1 and records[0]["target"] == 15_000


def assert_source_unchanged(case: types.SimpleNamespace) -> None:
    assert case.source.read_bytes() == case.source_bytes
    assert case.source_sidecar.read_bytes() == case.sidecar_bytes


def assert_one_diagnostic(stderr: str) -> None:
    assert stderr.startswith("glb-decimation: "), repr(stderr)
    assert stderr.endswith("\n"), repr(stderr)
    assert len(stderr.splitlines()) == 1, repr(stderr)
    assert stderr[:-1].isprintable(), repr(stderr)
    assert len(stderr.encode("utf-8")) <= MAX_DIAGNOSTIC_BYTES, repr(stderr)


def assert_success_pair(
    case: types.SimpleNamespace,
    result: int | None,
    stdout: str,
    stderr: str,
    caught: BaseException | None,
    *,
    success_records: int = 1,
) -> None:
    assert caught is None
    assert result == 0, (result, stdout, stderr)
    assert stderr == ""
    assert stdout.count("output_triangles=") == success_records
    assert case.final_glb.read_bytes()[:4] == b"glTF"
    record = json.loads(case.final_json.read_text(encoding="utf-8"))
    assert record["derivative"]["sha256"] == digest_file(case.final_glb)
    assert set(path.name for path in case.output.iterdir()) == {
        case.final_glb.name,
        case.final_json.name,
    }


def assert_old_public_pair(case: types.SimpleNamespace) -> None:
    assert case.old_pair is not None
    actual_entries = set(case.output.iterdir())
    public_members = public_final_members(case)
    observed = {
        "file_count": len(actual_entries),
        "nonpublic_count": len(actual_entries - public_members),
        "glb": (
            "old"
            if case.final_glb.is_file()
            and case.final_glb.read_bytes() == case.old_pair[0]
            else "non-old"
        ),
        "json": (
            "old"
            if case.final_json.is_file()
            and case.final_json.read_bytes() == case.old_pair[1]
            else "non-old"
        ),
    }
    assert case.final_glb.read_bytes() == case.old_pair[0], observed
    assert case.final_json.read_bytes() == case.old_pair[1], observed
    assert actual_entries == {case.final_glb, case.final_json}, observed


def assert_batch_sources_unchanged(case: types.SimpleNamespace) -> None:
    for path, expected in case.batch_sources:
        assert path.read_bytes() == expected


def assert_batch_terminal(case: types.SimpleNamespace, *, old: bool) -> None:
    expected_entries = set()
    states = []
    for final_glb, final_json, old_pair in case.batch_pairs:
        if not final_glb.exists() and not final_json.exists():
            state = "absent"
        elif final_glb.is_file() and final_json.is_file():
            state = (
                "old"
                if old_pair is not None
                and final_glb.read_bytes() == old_pair[0]
                and final_json.read_bytes() == old_pair[1]
                else "non-old"
            )
        else:
            state = "partial"
        states.append(state)
        if old:
            assert old_pair is not None
            expected_entries.update((final_glb, final_json))
        else:
            assert old_pair is None
    actual_entries = set(case.output.iterdir())
    nonpublic_entries = actual_entries - public_final_members(case)
    observed = {
        "states": states,
        "file_count": len(actual_entries),
        "nonpublic_count": len(nonpublic_entries),
    }
    expected_state = "old" if old else "absent"
    assert states == [expected_state] * len(case.batch_pairs), observed
    assert actual_entries == expected_entries, observed


def lock_release_terminal_case() -> None:
    case = setup_case("lock-release-terminal")
    real_promote = module.promote_pair
    real_guard = module._promotion_guard
    armed = False
    injected = False

    def armed_promote(*args, **kwargs):
        nonlocal armed
        armed = True
        return real_promote(*args, **kwargs)

    @contextlib.contextmanager
    def release_after_effect(*args, **kwargs):
        nonlocal injected
        with real_guard(*args, **kwargs):
            yield
        if armed and not injected:
            injected = True
            raise OSError("injected lock-release failure after effect")

    with (
        mock.patch.object(module, "promote_pair", armed_promote),
        mock.patch.object(module, "_promotion_guard", release_after_effect),
    ):
        result, stdout, stderr, caught = run_main(case)
    assert injected
    assert caught is None
    assert_source_unchanged(case)
    if result == 0:
        assert_success_pair(case, result, stdout, stderr, caught)
    else:
        assert result == 1
        assert "output_triangles=" not in stdout
        assert_one_diagnostic(stderr)
        assert list(case.output.iterdir()) == [], (
            "lock-release failure reported non-success with committed finals"
        )


def force_cleanup_terminal_case() -> None:
    case = setup_case("force-cleanup-terminal", force=True)
    injected_attempts = 0

    def reject_old_member_removal(event, arguments):
        nonlocal injected_attempts
        if event != "os.remove" or case.old_identities is None:
            return
        path = arguments[0]
        directory_descriptor = arguments[1]
        try:
            status = os.stat(
                path,
                dir_fd=None if directory_descriptor == -1 else directory_descriptor,
                follow_symlinks=False,
            )
        except (OSError, TypeError, ValueError):
            return
        if (status.st_dev, status.st_ino) in set(case.old_identities.values()):
            injected_attempts += 1
            raise OSError("injected persistent old-member cleanup failure")

    sys.addaudithook(reject_old_member_removal)
    result, stdout, stderr, caught = run_main(case)
    assert injected_attempts >= 2
    assert caught is None
    assert_source_unchanged(case)
    if result == 0:
        assert_success_pair(case, result, stdout, stderr, caught)
    else:
        assert result == 1
        assert "output_triangles=" not in stdout
        assert_one_diagnostic(stderr)
        assert_old_public_pair(case)


def temporary_cleanup_terminal_case(force: bool, after_effect: bool) -> None:
    destination = "force" if force else "absent"
    effect = "after-effect" if after_effect else "before-effect"
    case = setup_case(
        f"temporary-cleanup-{destination}-{effect}",
        force=force,
    )
    real_temporary_directory = module.tempfile.TemporaryDirectory
    injected = False
    held = []
    targeted = []

    class CleanupFault:
        def __init__(self, *args, **kwargs) -> None:
            self.inner = real_temporary_directory(*args, **kwargs)
            self.target = False
            held.append(self)

        def __enter__(self):
            return self.inner.__enter__()

        def __exit__(self, *args):
            nonlocal injected
            self.target = (
                not targeted
                and case.final_glb.is_file()
                and case.final_json.is_file()
            )
            if self.target:
                targeted.append(self)
            if (
                self.target
                and
                not injected
                and case.final_glb.is_file()
                and case.final_json.is_file()
            ):
                injected = True
                if after_effect:
                    self.inner.__exit__(*args)
                raise OSError(f"injected temporary cleanup failure {effect}")
            return self.inner.__exit__(*args)

    with mock.patch.object(
        module.tempfile,
        "TemporaryDirectory",
        new=CleanupFault,
    ):
        result = run_main(case)
    try:
        assert injected, f"temporary cleanup {effect} seam was not reached"
        assert len(targeted) == 1
        assert_source_unchanged(case)
        if after_effect:
            assert_success_pair(case, *result)
            assert not Path(targeted[0].inner.name).exists()
            return

        main_result, stdout, stderr, caught = result
        assert caught is None
        assert main_result == 1
        assert stdout == (
            "glb-decimation: asset=terminal-fixture category=cat target=15000 "
            "source_triangles=30000\n"
        ), repr(stdout)
        assert "output_triangles=" not in stdout
        assert_one_diagnostic(stderr)
        assert "cleanup" in stderr.lower()
        residue = Path(targeted[0].inner.name)
        assert residue.is_dir()
        expected_entries = {residue}
        if force:
            assert case.old_pair is not None
            assert case.final_glb.read_bytes() == case.old_pair[0]
            assert case.final_json.read_bytes() == case.old_pair[1]
            expected_entries.update({case.final_glb, case.final_json})
        else:
            assert not case.final_glb.exists()
            assert not case.final_json.exists()
        actual_entries = set(case.output.iterdir())
        assert {
            observed_path_key(path) for path in actual_entries
        } == {
            observed_path_key(path) for path in expected_entries
        }, (
            sorted(path.name for path in actual_entries),
            sorted(path.name for path in expected_entries),
        )
    finally:
        for wrapper in held:
            wrapper.inner.cleanup()

    if force:
        assert_old_public_pair(case)
    else:
        assert list(case.output.iterdir()) == []


def later_asset_failure_rolls_back_batch_case(force: bool) -> None:
    destination = "force" if force else "absent"
    case = setup_batch_case(
        f"later-asset-failure-{destination}",
        force=force,
    )
    real_process_asset = module._process_asset
    calls = 0

    def fail_second_asset(*args, **kwargs):
        nonlocal calls
        calls += 1
        if calls == 2:
            raise module.DecimationError("injected later asset failure")
        return real_process_asset(*args, **kwargs)

    with mock.patch.object(module, "_process_asset", new=fail_second_asset):
        result, stdout, stderr, caught = run_main(case)
    assert calls == 2
    assert caught is None
    assert result == 1
    assert stdout.count("source_triangles=") == 1, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert "later asset failure" in stderr
    assert_batch_sources_unchanged(case)
    assert_batch_terminal(case, old=force)


def interruption_after_first_publication_case(force: bool) -> None:
    destination = "force" if force else "absent"
    case = setup_batch_case(
        f"interruption-after-publication-{destination}",
        force=force,
    )
    real_process_asset = module._process_asset
    calls = 0

    def interrupt_second_asset(*args, **kwargs):
        nonlocal calls
        calls += 1
        if calls == 2:
            raise KeyboardInterrupt("injected batch interruption")
        return real_process_asset(*args, **kwargs)

    with mock.patch.object(
        module,
        "_process_asset",
        new=interrupt_second_asset,
    ):
        result, stdout, stderr, caught = run_main(case)
    assert calls == 2
    assert result is None
    assert isinstance(caught, KeyboardInterrupt), repr(caught)
    assert stdout.count("source_triangles=") == 1, repr(stdout)
    assert "output_triangles=" not in stdout
    assert stderr == ""
    assert_batch_sources_unchanged(case)
    assert_batch_terminal(case, old=force)


def persistent_absent_rollback_retires_candidate_case() -> None:
    case = setup_batch_case(
        "persistent-absent-rollback-retirement",
        force=False,
    )
    real_process_asset = module._process_asset
    real_unlink_pair = module._unlink_pair_bounded
    calls = 0
    expected_hashes: tuple[str, str] | None = None

    def fail_second_asset(*args, **kwargs):
        nonlocal calls, expected_hashes
        calls += 1
        if calls == 2:
            raise module.DecimationError("injected later asset failure")
        pending = real_process_asset(*args, **kwargs)
        expected_hashes = (
            pending["expected_glb_sha"],
            pending["expected_json_sha"],
        )
        return pending

    def fail_rollback_unlink(first, second, message):
        if "cleanup rollback could not remove published pair" in message:
            raise module.DecimationError(
                "injected persistent rollback unlink failure"
            )
        return real_unlink_pair(first, second, message)

    with (
        mock.patch.object(module, "_process_asset", new=fail_second_asset),
        mock.patch.object(module, "_unlink_pair_bounded", new=fail_rollback_unlink),
    ):
        result, stdout, stderr, caught = run_main(case)
    assert calls == 2
    assert caught is None, repr(caught)
    assert result == 1, (result, stdout, stderr)
    assert stdout.count("source_triangles=") == 1, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert expected_hashes is not None
    for final_glb, final_json, _ in case.batch_pairs:
        assert not final_glb.exists() and not final_json.exists()
    retired_paths = list(case.output.iterdir())
    assert len(retired_paths) == 2, [path.name for path in retired_paths]
    observed_hashes = set()
    for path in retired_paths:
        status = os.lstat(path)
        assert stat.S_ISREG(status.st_mode) and status.st_nlink == 1
        observed_hashes.add(digest_file(path))
    assert observed_hashes == set(expected_hashes)
    assert_batch_sources_unchanged(case)


def asymmetric_retired_cleanup_restores_exact_pair_case() -> None:
    case = setup_batch_case(
        "asymmetric-retired-cleanup",
        force=False,
    )
    real_process_asset = module._process_asset
    real_path_unlink = Path.unlink
    real_receipt = module._sha256_receipt
    calls = 0
    rollback_armed = False
    expected_hashes: tuple[str, str] | None = None
    blocked_hash: str | None = None
    blocked_attempts = 0
    successful_removals = 0
    retired_receipt_limits: dict[str, int] = {}

    def fail_second_asset(*args, **kwargs):
        nonlocal calls, rollback_armed, expected_hashes
        calls += 1
        if calls == 2:
            rollback_armed = True
            raise module.DecimationError("injected later asset failure")
        pending = real_process_asset(*args, **kwargs)
        expected_hashes = (
            pending["expected_glb_sha"],
            pending["expected_json_sha"],
        )
        assert len(set(expected_hashes)) == 2
        return pending

    def fail_one_retired_member(path, *args, **kwargs):
        nonlocal blocked_hash, blocked_attempts, successful_removals
        candidate = Path(path)
        if not rollback_armed or expected_hashes is None:
            return real_path_unlink(candidate, *args, **kwargs)
        try:
            status = os.lstat(candidate)
        except OSError:
            return real_path_unlink(candidate, *args, **kwargs)
        is_public = any(
            same_observed_path(candidate, public_member)
            for final_glb, final_json, _ in case.batch_pairs
            for public_member in (final_glb, final_json)
        )
        if (
            is_public
            or not same_observed_path(candidate.parent, case.output)
            or not stat.S_ISREG(status.st_mode)
            or status.st_nlink != 1
        ):
            return real_path_unlink(candidate, *args, **kwargs)
        observed_hash = digest_file(candidate)
        if observed_hash not in expected_hashes:
            return real_path_unlink(candidate, *args, **kwargs)
        if blocked_hash is None:
            blocked_hash = observed_hash
        if observed_hash == blocked_hash:
            blocked_attempts += 1
            assert blocked_attempts <= 4
            raise OSError("injected asymmetric retirement cleanup failure")
        result = real_path_unlink(candidate, *args, **kwargs)
        successful_removals += 1
        return result

    def observe_retired_receipt(path, label, maximum_bytes):
        receipt = real_receipt(path, label, maximum_bytes)
        candidate = Path(path)
        if (
            rollback_armed
            and expected_hashes is not None
            and same_observed_path(candidate.parent, case.output)
            and receipt[0] in expected_hashes
        ):
            retired_receipt_limits[receipt[0]] = maximum_bytes
        return receipt

    with (
        mock.patch.object(module, "_process_asset", new=fail_second_asset),
        mock.patch.object(Path, "unlink", new=fail_one_retired_member),
        mock.patch.object(module, "_sha256_receipt", new=observe_retired_receipt),
    ):
        result, stdout, stderr, caught = run_main(case)
    assert calls == 2
    assert blocked_hash is not None
    assert blocked_attempts == 2, blocked_attempts
    assert successful_removals == 1, successful_removals
    assert caught is None, repr(caught)
    assert result == 1, (result, stdout, stderr)
    assert stdout.count("source_triangles=") == 1, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert expected_hashes is not None
    for final_glb, final_json, _ in case.batch_pairs:
        assert not final_glb.exists() and not final_json.exists()
    retired_paths = list(case.output.iterdir())
    assert len(retired_paths) == 2, [path.name for path in retired_paths]
    observed_hashes = set()
    for path in retired_paths:
        status = os.lstat(path)
        assert stat.S_ISREG(status.st_mode) and status.st_nlink == 1
        observed_hashes.add(digest_file(path))
    assert observed_hashes == set(expected_hashes)
    assert retired_receipt_limits == {
        expected_hashes[0]: MAX_DERIVATIVE_BYTES,
        expected_hashes[1]: MAX_PROVENANCE_BYTES,
    }
    assert_batch_sources_unchanged(case)


def sequential_retirement_rename_failure_case() -> None:
    case = setup_batch_case(
        "sequential-retirement-rename-failure",
        force=False,
    )
    real_process_asset = module._process_asset
    real_replace = module.os.replace
    real_receipt = module._sha256_receipt
    real_path_unlink = Path.unlink
    calls = 0
    rollback_armed = False
    expected_hashes: tuple[str, str] | None = None
    first_private_moves = 0
    second_rename_failures = 0
    post_fault_receipt_limits: dict[str, int] = {}
    exact_private_pair_before_public_cleanup = False

    def is_public(path: Path) -> bool:
        return any(
            same_observed_path(path, member)
            for final_glb, final_json, _ in case.batch_pairs
            for member in (final_glb, final_json)
        )

    def fail_second_asset(*args, **kwargs):
        nonlocal calls, rollback_armed, expected_hashes
        calls += 1
        if calls == 2:
            rollback_armed = True
            raise module.DecimationError("injected later asset failure")
        pending = real_process_asset(*args, **kwargs)
        expected_hashes = (
            pending["expected_glb_sha"],
            pending["expected_json_sha"],
        )
        assert len(set(expected_hashes)) == 2
        return pending

    def fail_second_public_retirement(source, destination, *args, **kwargs):
        nonlocal first_private_moves, second_rename_failures
        source_path = Path(source)
        destination_path = Path(destination)
        if (
            rollback_armed
            and expected_hashes is not None
            and is_public(source_path)
            and same_observed_path(destination_path.parent, case.output)
            and not is_public(destination_path)
            and source_path.is_file()
        ):
            observed_hash = digest_file(source_path)
            if observed_hash == expected_hashes[0]:
                result = real_replace(source, destination, *args, **kwargs)
                first_private_moves += 1
                return result
            if observed_hash == expected_hashes[1]:
                assert first_private_moves == 1
                second_rename_failures += 1
                assert second_rename_failures <= 4
                raise OSError("injected second retirement rename failure")
        return real_replace(source, destination, *args, **kwargs)

    def observe_post_fault_receipt(path, label, maximum_bytes):
        receipt = real_receipt(path, label, maximum_bytes)
        if (
            rollback_armed
            and second_rename_failures > 0
            and expected_hashes is not None
            and receipt[0] in expected_hashes
        ):
            post_fault_receipt_limits[receipt[0]] = maximum_bytes
        return receipt

    def observe_public_cleanup(path, *args, **kwargs):
        nonlocal exact_private_pair_before_public_cleanup
        candidate = Path(path)
        if (
            rollback_armed
            and second_rename_failures > 0
            and expected_hashes is not None
            and is_public(candidate)
            and candidate.exists()
        ):
            private_members = [
                member
                for member in case.output.iterdir()
                if not is_public(member)
            ]
            assert len(private_members) == 2
            observed_hashes = set()
            for member in private_members:
                status = os.lstat(member)
                assert stat.S_ISREG(status.st_mode) and status.st_nlink == 1
                observed_hashes.add(digest_file(member))
            assert observed_hashes == set(expected_hashes)
            exact_private_pair_before_public_cleanup = True
        return real_path_unlink(candidate, *args, **kwargs)

    with (
        mock.patch.object(module, "_process_asset", new=fail_second_asset),
        mock.patch.object(module.os, "replace", new=fail_second_public_retirement),
        mock.patch.object(
            module,
            "_sha256_receipt",
            new=observe_post_fault_receipt,
        ),
        mock.patch.object(Path, "unlink", new=observe_public_cleanup),
    ):
        result, stdout, stderr, caught = run_main(case)
    assert calls == 2
    assert first_private_moves == 1
    assert 1 <= second_rename_failures <= 4
    assert post_fault_receipt_limits == {
        expected_hashes[0]: MAX_DERIVATIVE_BYTES,
        expected_hashes[1]: MAX_PROVENANCE_BYTES,
    }
    assert exact_private_pair_before_public_cleanup
    assert caught is None, repr(caught)
    assert result == 1, (result, stdout, stderr)
    assert stdout.count("source_triangles=") == 1, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert "later asset failure" in stderr
    assert expected_hashes is not None
    for final_glb, final_json, _ in case.batch_pairs:
        assert not final_glb.exists() and not final_json.exists()
    residue = list(case.output.iterdir())
    if residue:
        assert len(residue) == 2, [path.name for path in residue]
        residue_hashes = set()
        for path in residue:
            status = os.lstat(path)
            assert stat.S_ISREG(status.st_mode) and status.st_nlink == 1
            residue_hashes.add(digest_file(path))
        assert residue_hashes == set(expected_hashes)
    assert_batch_sources_unchanged(case)


def mixed_force_success_uses_actual_lineage_case() -> None:
    case = setup_mixed_force_case("mixed-force-success")
    real_process_asset = module._process_asset
    pending_items: list[dict[str, object]] = []

    def capture_pending(*args, **kwargs):
        pending = real_process_asset(*args, **kwargs)
        pending_items.append(pending)
        return pending

    with mock.patch.object(module, "_process_asset", new=capture_pending):
        result, stdout, stderr, caught = run_main(case)
    assert caught is None
    assert result == 0, (result, stdout, stderr)
    assert stderr == ""
    assert stdout.count("source_triangles=") == 2, repr(stdout)
    assert stdout.count("output_triangles=") == 2, repr(stdout)
    assert len(pending_items) == 2
    assert [
        pending["publication_receipt"]["destination_was_present"]
        for pending in pending_items
    ] == [True, False]
    expected_entries = set()
    for final_glb, final_json, old_pair in case.batch_pairs:
        assert final_glb.is_file() and final_json.is_file()
        if old_pair is not None:
            assert (final_glb.read_bytes(), final_json.read_bytes()) != old_pair
        record = json.loads(final_json.read_text(encoding="utf-8"))
        assert record["derivative"]["sha256"] == digest_file(final_glb)
        expected_entries.update((final_glb, final_json))
    assert set(case.output.iterdir()) == expected_entries
    assert_batch_sources_unchanged(case)


def mixed_force_later_failure_rolls_back_actual_lineage_case() -> None:
    case = setup_mixed_force_case("mixed-force-later-failure")
    real_process_asset = module._process_asset
    real_rollback_bytes = module._publication_rollback_bytes
    pending_items: list[dict[str, object]] = []
    accounting_calls = 0

    def capture_pending(*args, **kwargs):
        pending = real_process_asset(*args, **kwargs)
        pending_items.append(pending)
        return pending

    def fail_after_second_publication(pending):
        nonlocal accounting_calls
        observed = real_rollback_bytes(pending)
        accounting_calls += 1
        if accounting_calls == 2:
            raise module.DecimationError("injected mixed force later failure")
        return observed

    with (
        mock.patch.object(module, "_process_asset", new=capture_pending),
        mock.patch.object(
            module,
            "_publication_rollback_bytes",
            new=fail_after_second_publication,
        ),
    ):
        result, stdout, stderr, caught = run_main(case)
    assert accounting_calls == 2
    assert caught is None
    assert result == 1, (result, stdout, stderr)
    assert stdout.count("source_triangles=") == 2, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert "mixed force later failure" in stderr
    assert len(pending_items) == 2
    assert [
        pending["publication_receipt"]["destination_was_present"]
        for pending in pending_items
    ] == [True, False]
    first_glb, first_json, first_old_pair = case.batch_pairs[0]
    second_glb, second_json, second_old_pair = case.batch_pairs[1]
    assert first_old_pair is not None and second_old_pair is None
    assert first_glb.read_bytes() == first_old_pair[0]
    assert first_json.read_bytes() == first_old_pair[1]
    assert not second_glb.exists() and not second_json.exists()
    assert set(case.output.iterdir()) == {first_glb, first_json}
    assert_batch_sources_unchanged(case)


def rollback_verification_retry_case(force: bool) -> None:
    destination = "force" if force else "absent"
    case = setup_batch_case(
        f"rollback-verification-retry-{destination}",
        force=force,
    )
    first_final = case.batch_pairs[0][0]
    real_process_asset = module._process_asset
    real_verified_member = module._verified_transaction_member
    process_calls = 0
    rollback_armed = False
    target_checks = 0

    def fail_second_asset(*args, **kwargs):
        nonlocal process_calls, rollback_armed
        process_calls += 1
        if process_calls == 2:
            rollback_armed = True
            raise module.DecimationError("injected later asset failure")
        return real_process_asset(*args, **kwargs)

    def fail_first_rollback_check(path, *args, **kwargs):
        nonlocal target_checks
        if rollback_armed and same_observed_path(Path(path), first_final):
            target_checks += 1
            if target_checks == 1:
                return False
        return real_verified_member(path, *args, **kwargs)

    with (
        mock.patch.object(module, "_process_asset", new=fail_second_asset),
        mock.patch.object(
            module,
            "_verified_transaction_member",
            new=fail_first_rollback_check,
        ),
    ):
        result, stdout, stderr, caught = run_main(case)
    assert process_calls == 2
    assert target_checks == 2, target_checks
    assert caught is None
    assert result == 1, (result, stdout, stderr)
    assert stdout.count("source_triangles=") == 1, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert "later asset failure" in stderr
    assert_batch_sources_unchanged(case)
    assert_batch_terminal(case, old=force)


def sequential_force_commit_failure_rolls_back_batch_case() -> None:
    case = setup_batch_case("sequential-force-commit-failure", force=True)
    real_unlink_pair = module._unlink_pair_bounded
    cleanup_order = []
    injected = False

    def fail_second_cleanup(first, second, message):
        nonlocal injected
        first_path = Path(first)
        if "commit old-backup cleanup" in message:
            member = (
                "second"
                if case.batch_pairs[1][0].name in first_path.name
                else "first"
            )
            cleanup_order.append(member)
            if member == "second" and not injected:
                injected = True
                raise module.DecimationError(
                    "injected second publication cleanup failure"
                )
        return real_unlink_pair(first, second, message)

    with mock.patch.object(
        module,
        "_unlink_pair_bounded",
        new=fail_second_cleanup,
    ):
        result, stdout, stderr, caught = run_main(case)
    assert injected
    assert cleanup_order == ["first", "second"], cleanup_order
    assert caught is None
    assert result == 1
    assert stdout.count("source_triangles=") == 2, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert "second publication cleanup failure" in stderr
    assert_batch_sources_unchanged(case)
    assert_batch_terminal(case, old=True)


def preunlink_receipt_read_failure_restores_single_publication_case() -> None:
    case = setup_case("preunlink-receipt-read-failure", force=True)
    assert case.old_identities is not None
    real_receipt = module._sha256_receipt
    real_unlink_pair = module._unlink_pair_bounded
    injected = 0
    commit_unlinks = 0
    commit_active = False

    def fail_old_json_receipt(path, label, maximum_bytes):
        nonlocal injected
        candidate = Path(path)
        if (
            commit_active
            and same_observed_path(candidate.parent, case.output)
            and not same_observed_path(candidate, case.final_json)
            and path_identity(candidate) == case.old_identities["json"]
        ):
            injected += 1
            raise OSError("injected old JSON receipt read failure")
        return real_receipt(path, label, maximum_bytes)

    def observe_unlink(first, second, message):
        nonlocal commit_unlinks
        if commit_active:
            commit_unlinks += 1
        return real_unlink_pair(first, second, message)

    def direct_single_commit(completed_publications):
        nonlocal commit_active
        assert len(completed_publications) == 1
        commit_active = True
        try:
            return module._commit_publication(completed_publications[0])
        finally:
            commit_active = False

    with (
        mock.patch.object(module, "_sha256_receipt", new=fail_old_json_receipt),
        mock.patch.object(module, "_unlink_pair_bounded", new=observe_unlink),
        mock.patch.object(
            module,
            "_commit_completed_publications",
            new=direct_single_commit,
        ),
    ):
        result, stdout, stderr, caught = run_main(case)
    assert injected == 1
    assert commit_unlinks == 0
    assert caught is None
    assert result == 1
    assert stdout.count("source_triangles=") == 1, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert "receipt read failure" in stderr
    assert_source_unchanged(case)
    assert_old_public_pair(case)


def publication_recovery_aggregate_boundary_case() -> None:
    exact_case = setup_case("publication-recovery-aggregate-exact", force=True)
    assert exact_case.old_pair is not None
    exact_limit = sum(len(member) for member in exact_case.old_pair)
    with mock.patch.object(
        module,
        "MAX_PUBLICATION_ROLLBACK_BYTES",
        exact_limit,
        create=True,
    ):
        exact_result = run_main(exact_case)
    assert_success_pair(exact_case, *exact_result)
    assert_source_unchanged(exact_case)

    over_case = setup_batch_case(
        "publication-recovery-aggregate-plus-one-pair",
        force=True,
    )
    assert over_case.batch_pairs[0][2] is not None
    over_limit = sum(len(member) for member in over_case.batch_pairs[0][2])
    assert over_limit == exact_limit
    second_final_glb, second_final_json, second_old_pair = over_case.batch_pairs[1]
    assert second_old_pair is not None
    second_identities = {
        second_final_glb: path_identity(second_final_glb),
        second_final_json: path_identity(second_final_json),
    }
    real_promote_pair = module.promote_pair
    real_receipt = module._sha256_receipt
    real_replace = module.os.replace
    observing_second = False
    second_receipt_reads = 0
    second_public_renames = 0

    def observe_second_promotion(*args, **kwargs):
        nonlocal observing_second
        selected = same_observed_path(Path(args[2]), second_final_glb)
        if selected:
            observing_second = True
        try:
            return real_promote_pair(*args, **kwargs)
        finally:
            if selected:
                observing_second = False

    def observe_receipt(path, *args, **kwargs):
        nonlocal second_receipt_reads
        if observing_second and any(
            same_observed_path(Path(path), member) for member in second_identities
        ):
            second_receipt_reads += 1
        return real_receipt(path, *args, **kwargs)

    def observe_replace(source, destination, *args, **kwargs):
        nonlocal second_public_renames
        if observing_second and any(
            same_observed_path(Path(candidate), member)
            for candidate in (source, destination)
            for member in second_identities
        ):
            second_public_renames += 1
        return real_replace(source, destination, *args, **kwargs)

    with (
        mock.patch.object(
            module,
            "MAX_PUBLICATION_ROLLBACK_BYTES",
            over_limit,
            create=True,
        ),
        mock.patch.object(module, "promote_pair", new=observe_second_promotion),
        mock.patch.object(module, "_sha256_receipt", new=observe_receipt),
        mock.patch.object(module.os, "replace", new=observe_replace),
    ):
        result, stdout, stderr, caught = run_main(over_case)
    assert caught is None
    actual_entries = set(over_case.output.iterdir())
    nonpublic_entries = actual_entries - public_final_members(over_case)
    assert result == 1, {
        "return_code": result,
        "file_count": len(actual_entries),
        "nonpublic_count": len(nonpublic_entries),
    }
    assert stdout.count("source_triangles=") == 2, repr(stdout)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert "rollback" in stderr.lower() and "limit" in stderr.lower()
    assert second_receipt_reads == 0, second_receipt_reads
    assert second_public_renames == 0, second_public_renames
    assert second_final_glb.read_bytes() == second_old_pair[0]
    assert second_final_json.read_bytes() == second_old_pair[1]
    assert path_identity(second_final_glb) == second_identities[second_final_glb]
    assert path_identity(second_final_json) == second_identities[second_final_json]
    assert_batch_sources_unchanged(over_case)
    assert_batch_terminal(over_case, old=True)


def diagnostic_whole_message_redaction_case() -> None:
    case = setup_case("diagnostic-whole-message-redaction")
    manifest = json.loads(case.manifest.read_text(encoding="utf-8"))
    sentinel = "NEUTRAL_PRIVATE_SENTINEL"
    key_shape = "credential"
    manifest["assets"][0]["id"] = f"{key_shape} {sentinel}"
    manifest["assets"][0]["kind"] = "unsupported"
    case.manifest.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    result, stdout, stderr, caught = run_main(case)
    assert caught is None
    assert result == 1, (result, stdout, stderr)
    assert stdout == ""
    assert_one_diagnostic(stderr)
    assert sentinel not in stderr
    assert key_shape not in stderr.lower()
    assert "unsupported" not in stderr.lower()
    assert audit_lines(case) == []
    assert list(case.output.iterdir()) == []


def publication_recovery_production_ceiling_case() -> None:
    assert getattr(module, "MAX_PUBLICATION_ROLLBACK_BYTES", None) == (
        128 * 1024 * 1024
    )


@contextlib.contextmanager
def interpreter_integer_limit_disabled():
    setter = getattr(sys, "set_int_max_str_digits", None)
    getter = getattr(sys, "get_int_max_str_digits", None)
    previous = getter() if getter is not None else None
    if setter is not None:
        setter(0)
    try:
        yield
    finally:
        if setter is not None and previous is not None:
            setter(previous)


def glb_with_unknown_integer(source: Path, digits: int) -> bytes:
    payload = source.read_bytes()
    json_length, json_kind = struct.unpack_from("<I4s", payload, 12)
    assert json_kind == b"JSON"
    json_payload = payload[20:20 + json_length].rstrip(b" ")
    assert json_payload.endswith(b"}")
    changed_json = (
        json_payload[:-1]
        + b',"numericBoundary":'
        + b"7" * digits
        + b"}"
    )
    changed_json += b" " * (-len(changed_json) % 4)
    suffix = payload[20 + json_length:]
    total = 12 + 8 + len(changed_json) + len(suffix)
    return (
        struct.pack("<4sII", b"glTF", 2, total)
        + struct.pack("<I4s", len(changed_json), b"JSON")
        + changed_json
        + suffix
    )


def json_numeric_boundary_case() -> None:
    case = setup_case("json-numeric-boundary")
    exact_digits = 4_300
    oversized_digits = exact_digits + 1
    exact_metadata = b'{"value":' + b"7" * exact_digits + b"}"
    oversized_metadata = b'{"value":' + b"7" * oversized_digits + b"}"
    exact_glb = glb_with_unknown_integer(case.source, exact_digits)
    oversized_glb = glb_with_unknown_integer(case.source, oversized_digits)

    with interpreter_integer_limit_disabled():
        exact = module._decode_json_bytes(exact_metadata, "numeric boundary")
        assert isinstance(exact, dict) and set(exact) == {"value"}
        try:
            module._decode_json_bytes(oversized_metadata, "numeric boundary")
        except module.DecimationError as exc:
            assert "invalid numeric boundary" in str(exc)
        else:
            raise AssertionError("metadata decoder accepted an oversized integer token")

        exact_metrics = module._inspect_verified_glb_payload(
            case.source,
            exact_glb,
        )
        assert exact_metrics["triangles"] == 30_000
        try:
            module._inspect_verified_glb_payload(case.source, oversized_glb)
        except module.GlbError as exc:
            folded = str(exc).lower()
            assert "integer" in folded and "limit" in folded
        else:
            raise AssertionError("orchestrator GLB loader accepted an oversized integer token")


def provenance_finite_number_case() -> None:
    case = setup_case("provenance-finite-number")
    for name, value in (("nan", float("nan")), ("infinity", float("inf"))):
        path = case.output / f"{name}.json"
        try:
            module.write_staged_provenance(path, {"value": value})
        except (module.DecimationError, ValueError):
            pass
        else:
            raise AssertionError(f"provenance writer accepted {name}")
        assert not path.exists()


def success_record_write_case(after_effect: bool) -> None:
    effect = "after-effect" if after_effect else "before-effect"
    case = setup_case(f"success-record-{effect}")
    real_emit_record = module._emit_record
    injected = False

    def faulting_emit(message) -> None:
        nonlocal injected
        if not injected and "output_triangles=" in str(message):
            injected = True
            if after_effect:
                real_emit_record(message)
            raise OSError(f"injected success-record failure {effect}")
        real_emit_record(message)

    with mock.patch.object(module, "_emit_record", new=faulting_emit):
        result = run_main(case)
    assert injected, f"success-record {effect} seam was not reached"
    assert_source_unchanged(case)
    assert_success_pair(
        case,
        *result,
        success_records=1 if after_effect else 0,
    )


class BoundedReadAttempt(RuntimeError):
    pass


class BoundedReader:
    def __init__(self, handle, guard) -> None:
        self.handle = handle
        self.guard = guard

    def _payload(self, operation: str, payload):
        self.guard.observe(len(payload), operation)
        return payload

    def read(self, *args, **kwargs):
        return self._payload("file.read", self.handle.read(*args, **kwargs))

    def read1(self, *args, **kwargs):
        return self._payload("file.read1", self.handle.read1(*args, **kwargs))

    def readall(self):
        return self._payload("file.readall", self.handle.readall())

    def readinto(self, buffer):
        count = self.handle.readinto(buffer)
        self.guard.observe(0 if count is None else count, "file.readinto")
        return count

    def readinto1(self, buffer):
        count = self.handle.readinto1(buffer)
        self.guard.observe(0 if count is None else count, "file.readinto1")
        return count

    def readline(self, *args, **kwargs):
        return self._payload("file.readline", self.handle.readline(*args, **kwargs))

    def readlines(self, *args, **kwargs):
        lines = self.handle.readlines(*args, **kwargs)
        self.guard.observe(sum(len(line) for line in lines), "file.readlines")
        return lines

    def peek(self, *args, **kwargs):
        # The returned payload, not the requested size, is authoritative:
        # BufferedReader.peek(0) may fill and expose its internal buffer.
        return self._payload("file.peek", self.handle.peek(*args, **kwargs))

    def __iter__(self):
        return self

    def __next__(self):
        return self._payload("file iteration", next(self.handle))

    def __enter__(self):
        self.handle.__enter__()
        return self

    def __exit__(self, *args):
        return self.handle.__exit__(*args)

    @property
    def raw(self):
        return BoundedReader(self.handle.raw, self.guard)

    @property
    def buffer(self):
        return BoundedReader(self.handle.buffer, self.guard)

    def detach(self):
        return BoundedReader(self.handle.detach(), self.guard)

    def __getattr__(self, name):
        return getattr(self.handle, name)


class BoundedReadGuard:
    def __init__(self, maximum_bytes: int) -> None:
        self.maximum_bytes = maximum_bytes
        self.identity: tuple[int, int] | None = None
        self.total_bytes = 0
        self.over_limit = False
        self.operations: list[str] = []
        self.real_builtin_open = builtins.open
        self.real_io_open = io.open
        self.real_fdopen = os.fdopen
        self.real_read = os.read
        self.real_copyfile = shutil.copyfile
        self.real_copy = shutil.copy
        self.real_copy2 = shutil.copy2
        self.real_copyfileobj = shutil.copyfileobj
        self.real_mmap = mmap.mmap

    def arm(self, path: Path) -> None:
        status = os.lstat(path)
        assert stat.S_ISREG(status.st_mode)
        self.identity = (status.st_dev, status.st_ino)

    def fd_is_target(self, descriptor: int) -> bool:
        if self.identity is None:
            return False
        try:
            status = os.fstat(descriptor)
        except OSError:
            return False
        return (status.st_dev, status.st_ino) == self.identity

    def path_is_target(self, path) -> bool:
        if isinstance(path, int):
            return self.fd_is_target(path)
        if self.identity is None:
            return False
        try:
            status = os.lstat(path)
        except (OSError, TypeError, ValueError):
            return False
        return (status.st_dev, status.st_ino) == self.identity

    def observe(self, count: int, operation: str) -> None:
        if count <= 0:
            return
        self.total_bytes += count
        self.operations.append(operation)
        if self.total_bytes > self.maximum_bytes:
            self.over_limit = True
            raise BoundedReadAttempt(operation)

    def guarded_builtin_open(self, file, *args, **kwargs):
        handle = self.real_builtin_open(file, *args, **kwargs)
        return BoundedReader(handle, self) if self.path_is_target(file) else handle

    def guarded_io_open(self, file, *args, **kwargs):
        handle = self.real_io_open(file, *args, **kwargs)
        return BoundedReader(handle, self) if self.path_is_target(file) else handle

    def guarded_fdopen(self, descriptor, *args, **kwargs):
        handle = self.real_fdopen(descriptor, *args, **kwargs)
        return BoundedReader(handle, self) if self.fd_is_target(descriptor) else handle

    def guarded_read(self, descriptor, count):
        payload = self.real_read(descriptor, count)
        if self.fd_is_target(descriptor):
            self.observe(len(payload), "os.read")
        return payload

    def _copy_observed(self, operation, source, destination, *args, **kwargs):
        target = self.path_is_target(source)
        before = self.total_bytes
        result = operation(source, destination, *args, **kwargs)
        if target and self.total_bytes == before:
            self.observe(os.lstat(destination).st_size, operation.__name__)
        return result

    def guarded_copyfile(self, source, destination, *args, **kwargs):
        return self._copy_observed(
            self.real_copyfile, source, destination, *args, **kwargs
        )

    def guarded_copy(self, source, destination, *args, **kwargs):
        return self._copy_observed(
            self.real_copy, source, destination, *args, **kwargs
        )

    def guarded_copy2(self, source, destination, *args, **kwargs):
        return self._copy_observed(
            self.real_copy2, source, destination, *args, **kwargs
        )

    def guarded_copyfileobj(self, source, destination, *args, **kwargs):
        before = self.total_bytes
        try:
            descriptor = source.fileno()
        except (AttributeError, OSError, ValueError):
            descriptor = -1
        target = descriptor >= 0 and self.fd_is_target(descriptor)
        result = self.real_copyfileobj(source, destination, *args, **kwargs)
        if target and self.total_bytes == before:
            destination.flush()
            self.observe(
                os.fstat(destination.fileno()).st_size,
                "shutil.copyfileobj",
            )
        return result

    def guarded_mmap(self, descriptor, length, *args, **kwargs):
        if self.fd_is_target(descriptor):
            mapped_bytes = os.fstat(descriptor).st_size if length == 0 else length
            self.observe(mapped_bytes, "mmap")
        return self.real_mmap(descriptor, length, *args, **kwargs)

    def patches(self):
        stack = contextlib.ExitStack()
        stack.enter_context(mock.patch.object(builtins, "open", self.guarded_builtin_open))
        stack.enter_context(mock.patch.object(io, "open", self.guarded_io_open))
        stack.enter_context(mock.patch.object(os, "fdopen", self.guarded_fdopen))
        stack.enter_context(mock.patch.object(os, "read", self.guarded_read))
        stack.enter_context(mock.patch.object(shutil, "copyfile", self.guarded_copyfile))
        stack.enter_context(mock.patch.object(shutil, "copy", self.guarded_copy))
        stack.enter_context(mock.patch.object(shutil, "copy2", self.guarded_copy2))
        stack.enter_context(
            mock.patch.object(shutil, "copyfileobj", self.guarded_copyfileobj)
        )
        stack.enter_context(mock.patch.object(mmap, "mmap", self.guarded_mmap))

        if hasattr(os, "pread"):
            real_pread = os.pread

            def guarded_pread(descriptor, count, offset):
                payload = real_pread(descriptor, count, offset)
                if self.fd_is_target(descriptor):
                    self.observe(len(payload), "os.pread")
                return payload

            stack.enter_context(mock.patch.object(os, "pread", guarded_pread))
        if hasattr(os, "readv"):
            real_readv = os.readv

            def guarded_readv(descriptor, buffers):
                count = real_readv(descriptor, buffers)
                if self.fd_is_target(descriptor):
                    self.observe(count, "os.readv")
                return count

            stack.enter_context(mock.patch.object(os, "readv", guarded_readv))
        if hasattr(os, "preadv"):
            real_preadv = os.preadv

            def guarded_preadv(descriptor, buffers, offset, *args):
                count = real_preadv(descriptor, buffers, offset, *args)
                if self.fd_is_target(descriptor):
                    self.observe(count, "os.preadv")
                return count

            stack.enter_context(mock.patch.object(os, "preadv", guarded_preadv))
        if hasattr(os, "sendfile"):
            real_sendfile = os.sendfile

            def guarded_sendfile(destination, source, offset, count, *args, **kwargs):
                transferred = real_sendfile(
                    destination, source, offset, count, *args, **kwargs
                )
                if self.fd_is_target(source):
                    self.observe(transferred, "os.sendfile")
                return transferred

            stack.enter_context(mock.patch.object(os, "sendfile", guarded_sendfile))
        if hasattr(os, "copy_file_range"):
            real_copy_range = os.copy_file_range

            def guarded_copy_range(source, destination, count, *args, **kwargs):
                transferred = real_copy_range(
                    source, destination, count, *args, **kwargs
                )
                if self.fd_is_target(source):
                    self.observe(transferred, "os.copy_file_range")
                return transferred

            stack.enter_context(
                mock.patch.object(os, "copy_file_range", guarded_copy_range)
            )
        return stack

def bounded_read_oracle_case() -> None:
    case_root = root / "bounded-read-oracle"
    case_root.mkdir()
    target = case_root / "target.bin"
    scratch = case_root / "copy.bin"
    target.write_bytes(b"0123456789ab")
    oracle_limit = 8

    def cumulative_reads():
        with builtins.open(target, "rb") as handle:
            assert handle.read(5) == b"01234"
            handle.read(5)

    def zero_length_peek():
        with builtins.open(target, "rb") as handle:
            handle.peek(0)

    def zero_length_mapping():
        descriptor = os.open(target, os.O_RDONLY)
        try:
            with mmap.mmap(descriptor, 0, access=mmap.ACCESS_READ) as mapping:
                return mapping[0]
        finally:
            os.close(descriptor)

    def raw_read():
        with builtins.open(target, "rb") as handle:
            return handle.raw.read()

    def detached_read():
        handle = builtins.open(target, "rb")
        detached = handle.detach()
        try:
            return detached.read()
        finally:
            detached.close()

    def text_buffer_read():
        with builtins.open(target, "r", encoding="ascii") as handle:
            return handle.buffer.read()

    controls = {
        "cumulative reads": cumulative_reads,
        "zero-length buffered peek": zero_length_peek,
        "zero-length mapping": zero_length_mapping,
        "raw handle escape": raw_read,
        "detached handle escape": detached_read,
        "text buffer escape": text_buffer_read,
        "whole-path read": target.read_bytes,
        "copy": lambda: shutil.copyfile(target, scratch),
    }
    for label, control in controls.items():
        guard = BoundedReadGuard(oracle_limit)
        guard.arm(target)
        caught = False
        try:
            with guard.patches():
                control()
        except BoundedReadAttempt:
            caught = True
        finally:
            scratch.unlink(missing_ok=True)
        assert caught, f"bounded reader oracle missed {label}"
        assert guard.over_limit, f"bounded reader oracle did not cross for {label}"
        assert guard.total_bytes > oracle_limit, (label, guard.total_bytes)

    zero_guard = BoundedReadGuard(0)
    zero_guard.arm(target)
    with zero_guard.patches():
        with builtins.open(target, "rb") as handle:
            assert handle.read(0) == b""
            assert handle.read1(0) == b""
            assert handle.readinto(bytearray()) == 0
            assert handle.readinto1(bytearray()) == 0
            assert handle.readline(0) == b""
            assert handle.raw.read(0) == b""
        with builtins.open(target, "r", encoding="ascii") as handle:
            assert handle.buffer.read(0) == b""
        handle = builtins.open(target, "rb")
        detached = handle.detach()
        try:
            assert detached.read(0) == b""
        finally:
            detached.close()
        descriptor = os.open(target, os.O_RDONLY)
        try:
            assert os.read(descriptor, 0) == b""
        finally:
            os.close(descriptor)
    assert zero_guard.total_bytes == 0 and not zero_guard.over_limit


def derivative_delegated_open_race_case() -> None:
    case = setup_case("derivative-delegated-open-race")
    guard = BoundedReadGuard(MAX_DERIVATIVE_BYTES)
    real_open = os.open
    mutation_count = 0

    def grow_at_delegated_open(path, flags, *args, **kwargs):
        nonlocal mutation_count
        candidate = None if isinstance(path, int) else Path(path)
        if (
            candidate is not None
            and mutation_count == 0
            and candidate.parent.name.startswith("asset-")
            and candidate.name == case.source.name
        ):
            _pad_glb_to_size(candidate, MAX_DERIVATIVE_BYTES + 4)
            guard.arm(candidate)
            mutation_count += 1
        return real_open(path, flags, *args, **kwargs)

    with (
        guard.patches(),
        mock.patch.object(module.os, "open", new=grow_at_delegated_open),
    ):
        result, stdout, stderr, caught = run_main(case)
    assert mutation_count == 1, "derivative delegated-open race was not reached"
    assert not guard.over_limit, (
        "derivative delegated open crossed the 64 MiB role boundary",
        guard.total_bytes,
        guard.operations,
    )
    assert guard.total_bytes <= MAX_DERIVATIVE_BYTES
    assert caught is None, caught
    assert result == 1, (result, stdout, stderr)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert list(case.output.iterdir()) == []
    assert_source_unchanged(case)


def hash_role_race_case(role: str) -> None:
    case_root = root / f"hash-role-race-{role}"
    case_root.mkdir()
    target = case_root / ("member.glb" if role == "glb" else "member.json")
    maximum = MAX_DERIVATIVE_BYTES if role == "glb" else MAX_PROVENANCE_BYTES
    if role == "glb":
        write_glb(target, triangles=14_000)
    elif role == "json":
        target.write_text('{"generation":"old"}\n', encoding="utf-8")
    else:
        raise AssertionError(role)
    expected = digest_file(target)
    guard = BoundedReadGuard(maximum)
    real_sha256 = module._sha256
    mutation_count = 0

    def grow_before_hash(path):
        nonlocal mutation_count
        candidate = Path(path)
        if candidate == target and mutation_count == 0:
            if role == "glb":
                _pad_glb_to_size(candidate, maximum + 4)
            else:
                pad_json_like(candidate, maximum + 4)
            guard.arm(candidate)
            mutation_count += 1
        return real_sha256(candidate)

    with (
        guard.patches(),
        mock.patch.object(module, "_sha256", new=grow_before_hash),
    ):
        decision = module._sha256_match_status(target, expected, maximum)
    assert mutation_count == 1, f"{role} hash role race was not reached"
    assert decision is False, (role, decision)
    assert not guard.over_limit, (
        f"{role} hash race crossed its role boundary",
        guard.total_bytes,
        guard.operations,
    )
    assert guard.total_bytes <= maximum


def remove_non_old_final_role_case(role: str) -> None:
    case_root = root / f"remove-non-old-final-role-{role}"
    case_root.mkdir()
    target = case_root / ("candidate.glb" if role == "glb" else "candidate.json")
    maximum = MAX_DERIVATIVE_BYTES if role == "glb" else MAX_PROVENANCE_BYTES
    if role == "glb":
        write_glb(target, triangles=14_000)
    elif role == "json":
        target.write_text('{"generation":"candidate"}\n', encoding="utf-8")
    else:
        raise AssertionError(role)
    guard = BoundedReadGuard(maximum)
    real_sha256 = module._sha256
    mutation_count = 0

    def grow_before_hash(path):
        nonlocal mutation_count
        candidate = Path(path)
        if candidate == target and mutation_count == 0:
            if role == "glb":
                _pad_glb_to_size(candidate, maximum + 4)
            else:
                pad_json_like(candidate, maximum + 4)
            guard.arm(candidate)
            mutation_count += 1
        return real_sha256(candidate)

    with (
        guard.patches(),
        mock.patch.object(module, "_sha256", new=grow_before_hash),
    ):
        module._remove_non_old_final(
            target,
            digest_bytes(b"different old member"),
            maximum,
        )
    assert mutation_count == 1, f"{role} removal role race was not reached"
    assert not os.path.lexists(target), f"{role} non-old final was retained"
    assert not guard.over_limit, (
        f"{role} non-old final removal crossed its role boundary",
        guard.total_bytes,
        guard.operations,
    )
    assert guard.total_bytes <= maximum


def pad_json_like(path: Path, size: int) -> None:
    path.chmod(0o600)
    current = path.stat().st_size
    assert current < size
    with path.open("ab") as handle:
        handle.write(b" " * (size - current))
    assert path.stat().st_size == size


def late_class_cap_case(kind: str) -> None:
    force = kind in {"existing-derivative", "backup-derivative"}
    case = setup_case(f"late-cap-{kind}", force=force)
    class_limits = {
        "metadata-snapshot": MAX_METADATA_BYTES,
        "provenance": MAX_PROVENANCE_BYTES,
        "existing-derivative": MAX_DERIVATIVE_BYTES,
        "final-derivative": MAX_DERIVATIVE_BYTES,
        "backup-derivative": MAX_DERIVATIVE_BYTES,
    }
    guard = BoundedReadGuard(class_limits[kind])
    mutation_count = 0
    observed_existing_bytes = None
    role_observations: list[tuple[bool, bool, bool]] = []
    patchers = []

    if kind == "metadata-snapshot":
        real_preservation = module._candidate_preservation
        real_process_asset = module._process_asset
        observed_metadata = None

        def observe_metadata_dataflow(asset, prepared, *args, **kwargs):
            nonlocal observed_metadata
            candidate = prepared.get("source_sidecar_path")
            assert isinstance(candidate, Path)
            observed_metadata = candidate
            return real_process_asset(asset, prepared, *args, **kwargs)

        def mutate_metadata(*args, **kwargs):
            nonlocal mutation_count
            result = real_preservation(*args, **kwargs)
            assert observed_metadata is not None
            target = observed_metadata
            pad_json_like(target, MAX_METADATA_BYTES + 4)
            guard.arm(target)
            mutation_count += 1
            return result

        patchers.extend(
            [
                mock.patch.object(module, "_process_asset", observe_metadata_dataflow),
                mock.patch.object(module, "_candidate_preservation", mutate_metadata),
            ]
        )
    elif kind == "provenance":
        real_write = module.write_staged_provenance
        real_guard = module._promotion_guard
        staged_json = None

        def capture_provenance(path, record):
            nonlocal staged_json
            real_write(path, record)
            staged_json = Path(path)

        @contextlib.contextmanager
        def mutate_under_guard(*args, **kwargs):
            nonlocal mutation_count
            with real_guard(*args, **kwargs):
                if staged_json is not None and mutation_count == 0:
                    pad_json_like(staged_json, MAX_PROVENANCE_BYTES + 4)
                    guard.arm(staged_json)
                    mutation_count += 1
                yield

        patchers.extend(
            [
                mock.patch.object(module, "write_staged_provenance", capture_provenance),
                mock.patch.object(module, "_promotion_guard", mutate_under_guard),
            ]
        )
    elif kind == "existing-derivative":
        real_promote = module.promote_pair

        def grow_before_promote(
            staged_glb, staged_json, final_glb, final_json, force
        ):
            nonlocal mutation_count, observed_existing_bytes
            assert same_observed_path(Path(final_glb), case.final_glb), (
                "public promotion destination did not identify the final role"
            )
            if mutation_count == 0:
                _pad_glb_to_size(case.final_glb, MAX_DERIVATIVE_BYTES + 4)
                observed_existing_bytes = case.final_glb.read_bytes()
                guard.arm(case.final_glb)
                mutation_count += 1
            return real_promote(
                staged_glb, staged_json, final_glb, final_json, force
            )

        patchers.append(mock.patch.object(module, "promote_pair", grow_before_promote))
    elif kind in {"final-derivative", "backup-derivative"}:
        real_replace = os.replace

        def grow_after_replace(source, destination, *args, **kwargs):
            nonlocal mutation_count
            source_path = Path(source)
            destination_path = Path(destination)
            source_identity = path_identity(source_path)
            staged_glb = None
            if len(fake_records(case)) == 1:
                staged_glb = fake_argument_path(case, "--output")
            staged_identity = (
                None if staged_glb is None else path_identity(staged_glb)
            )
            if staged_glb is not None:
                role_observations.append(
                    (
                        same_observed_path(source_path, staged_glb),
                        same_observed_path(destination_path, case.final_glb),
                        source_identity == staged_identity,
                    )
                )
            final_role = (
                staged_glb is not None
                and same_observed_path(destination_path, case.final_glb)
                and source_identity == staged_identity
            )
            backup_role = (
                case.old_identities is not None
                and source_identity == case.old_identities["glb"]
            )
            result = real_replace(source, destination, *args, **kwargs)
            should_mutate = (
                kind == "final-derivative" and final_role
            ) or (
                kind == "backup-derivative" and backup_role
            )
            if should_mutate and mutation_count == 0:
                _pad_glb_to_size(destination_path, MAX_DERIVATIVE_BYTES + 4)
                guard.arm(destination_path)
                mutation_count += 1
            return result

        patchers.append(mock.patch.object(os, "replace", grow_after_replace))
    else:
        raise AssertionError(kind)

    with contextlib.ExitStack() as stack:
        stack.enter_context(guard.patches())
        for patcher in patchers:
            stack.enter_context(patcher)
        result, stdout, stderr, caught = run_main(case)
    assert mutation_count == 1, (
        kind,
        "expected one role-derived mutation",
        result,
        caught,
        role_observations,
    )
    assert not guard.over_limit, (
        f"{kind}: later reads exceeded the {guard.maximum_bytes}-byte class cap",
        guard.total_bytes,
        guard.operations,
    )
    assert guard.total_bytes <= guard.maximum_bytes
    assert caught is None, (kind, caught)
    assert result == 1, (kind, result, stdout, stderr)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert_source_unchanged(case)
    if kind in {"metadata-snapshot", "provenance", "final-derivative"}:
        assert list(case.output.iterdir()) == []
    elif kind == "existing-derivative":
        assert observed_existing_bytes is not None
        assert case.final_glb.read_bytes() == observed_existing_bytes
        assert case.old_pair is not None
        assert case.final_json.read_bytes() == case.old_pair[1]
        assert set(case.output.iterdir()) == {case.final_glb, case.final_json}
    else:
        assert_old_public_pair(case)


def provenance_exact_boundary_case() -> None:
    case = setup_case("provenance-exact-boundary")
    guard = BoundedReadGuard(MAX_PROVENANCE_BYTES)
    real_write = module.write_staged_provenance
    observed_path = None
    write_count = 0

    def write_at_boundary(path, record):
        nonlocal observed_path, write_count
        real_write(path, record)
        observed_path = Path(path)
        pad_json_like(observed_path, MAX_PROVENANCE_BYTES)
        guard.arm(observed_path)
        write_count += 1

    with (
        guard.patches(),
        mock.patch.object(module, "write_staged_provenance", write_at_boundary),
    ):
        result = run_main(case)
    assert write_count == 1 and observed_path is not None
    assert not guard.over_limit, (
        "exact-boundary provenance crossed its 2 MiB read envelope",
        guard.total_bytes,
        guard.operations,
    )
    assert guard.total_bytes <= MAX_PROVENANCE_BYTES
    assert_fake_reached(case)
    assert_source_unchanged(case)
    assert_success_pair(case, *result)
    assert case.final_json.stat().st_size == MAX_PROVENANCE_BYTES


def transaction_name_boundary_case() -> None:
    safe_name = (
        "n" * (MAX_TRANSACTION_SAFE_FILENAME_BYTES - len(".glb")) + ".glb"
    )
    unsafe_name = (
        "n" * (MAX_TRANSACTION_SAFE_FILENAME_BYTES + 1 - len(".glb"))
        + ".glb"
    )
    assert len(os.fsencode(safe_name)) == MAX_TRANSACTION_SAFE_FILENAME_BYTES
    assert len(os.fsencode(unsafe_name)) == MAX_TRANSACTION_SAFE_FILENAME_BYTES + 1

    unicode_stem = "猫" * 68
    unicode_safe_name = f"{unicode_stem}.glb"
    unicode_unsafe_name = f"{unicode_stem}n.glb"
    assert unicode_stem.isprintable()
    assert len(os.fsencode(unicode_safe_name)) == MAX_TRANSACTION_SAFE_FILENAME_BYTES
    assert len(os.fsencode(unicode_unsafe_name)) == MAX_TRANSACTION_SAFE_FILENAME_BYTES + 1
    assert len(unicode_unsafe_name) < MAX_TRANSACTION_SAFE_FILENAME_BYTES

    def assert_unsafe_name_rejected(case):
        result, stdout, stderr, caught = run_main(case)
        assert caught is None
        assert result == 1, (result, stdout, stderr)
        assert stdout == ""
        assert_one_diagnostic(stderr)
        assert audit_lines(case) == [] and fake_records(case) == []
        assert_source_unchanged(case)
        assert_old_public_pair(case)

    safe = setup_case("transaction-name-safe", force=True, filename=safe_name)
    safe_result = run_main(safe)
    assert_fake_reached(safe)
    assert_source_unchanged(safe)
    assert_success_pair(safe, *safe_result)

    unsafe = setup_case("transaction-name-plus-one", force=True, filename=unsafe_name)
    assert_unsafe_name_rejected(unsafe)

    unicode_safe = setup_case(
        "transaction-name-unicode-safe", force=True, filename=unicode_safe_name
    )
    unicode_safe_result = run_main(unicode_safe)
    assert_fake_reached(unicode_safe)
    assert_source_unchanged(unicode_safe)
    assert_success_pair(unicode_safe, *unicode_safe_result)

    unicode_unsafe = setup_case(
        "transaction-name-unicode-plus-one",
        force=True,
        filename=unicode_unsafe_name,
    )
    assert_unsafe_name_rejected(unicode_unsafe)


def exact_child_environment_case() -> None:
    case = setup_case("exact-child-environment")
    environment_log = case.root / "environment.log"
    unlisted_name = "FAKE_BLENDER_UNLISTED_VALUE"
    unlisted_value = "generic-unlisted-value"
    result = run_main(
        case,
        extra_environment={
            "FAKE_BLENDER_ENV_LOG": str(environment_log),
            unlisted_name: unlisted_value,
        },
    )
    assert_success_pair(case, *result)
    assert_source_unchanged(case)
    records = [
        json.loads(line)
        for line in environment_log.read_text(encoding="utf-8").splitlines()
    ]
    assert [record["phase"] for record in records] == ["version", "asset"]
    for record in records:
        assert unlisted_name not in record["names"], (
            "unlisted test-prefix environment name reached a child"
        )
    assert unlisted_value not in result[1] + result[2]


def write_blender_wrapper(
    case: types.SimpleNamespace, profile: str
) -> tuple[Path, Path, Path]:
    wrapper = case.root / "blender-wrapper"
    marker = case.root / "stream-child.json"
    release = case.root / "stream-release"
    helper_directory = str(repo / "tests" / "assets")
    wrapper.write_text(
        f'''#!/usr/bin/env python3
import contextlib
import io
import json
import os
import sys
import time
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, {helper_directory!r})
from fake_blender import main as fake_main

argv = sys.argv[1:]
profile = {profile!r}
is_version = "--version" in argv
overflow = profile in {{"version-stdout-over", "version-stderr-over"}} and is_version
overflow = overflow or (
    profile in {{"asset-stdout-over", "asset-stderr-over"}} and not is_version
)
exact = profile in {{"version-stdout-exact", "version-stderr-exact"}} and is_version
exact = exact or (
    profile in {{"asset-stdout-exact", "asset-stderr-exact"}} and not is_version
)
selected = overflow or exact
if selected:
    try:
        os.setsid()
    except OSError:
        pass
marker_path = Path({str(marker)!r})
marker_draft = marker_path.with_name(marker_path.name + "-next")
release_path = Path({str(release)!r})
emitted_bytes = 0

def record_state(attempted_bytes, state):
    marker_draft.write_text(
        json.dumps({{
            "pid": os.getpid(),
            "pgrp": os.getpgrp(),
            "profile": profile,
            "phase": "version" if is_version else "asset",
            "attempted_bytes": attempted_bytes,
            "emitted_bytes": emitted_bytes,
            "state": state,
        }}, sort_keys=True),
        encoding="utf-8",
    )
    os.replace(marker_draft, marker_path)

def emit_selected(stream, payload):
    global emitted_bytes
    record_state(emitted_bytes + len(payload), "EMITTING")
    stream.write(payload)
    stream.flush()
    emitted_bytes += len(payload)
    record_state(emitted_bytes, "EMITTING")

def stream_at_limit(stream, token, suffix=""):
    suffix_bytes = suffix.encode("utf-8")
    assert len(suffix_bytes) <= {MAX_CHILD_STREAM_BYTES}
    remaining = {MAX_CHILD_STREAM_BYTES} - len(suffix_bytes)
    while remaining:
        payload = token * min(remaining, 16384)
        emit_selected(stream, payload)
        remaining -= len(payload)
    if suffix:
        emit_selected(stream, suffix)
    assert emitted_bytes == {MAX_CHILD_STREAM_BYTES}
    record_state(emitted_bytes, "AT_LIMIT")

def wait_for_release():
    while not release_path.exists():
        time.sleep(0.01)

def stream_one_byte_past_limit(stream, token):
    global emitted_bytes
    attempted = emitted_bytes + 1
    record_state(attempted, "OVER_LIMIT_ATTEMPT")
    stream.write(token)
    stream.flush()
    emitted_bytes = attempted
    record_state(attempted, "OVER_LIMIT_EMITTED")
    while True:
        time.sleep(0.1)

if selected:
    selected_stream = sys.stdout if "-stdout-" in profile else sys.stderr
    selected_token = "S" if selected_stream is sys.stdout else "E"
    suffix = ""
    if is_version and selected_stream is sys.stdout:
        captured_stdout = io.StringIO()
        with contextlib.redirect_stdout(captured_stdout):
            status = fake_main(argv)
        suffix = "\\n" + captured_stdout.getvalue()
        sys.stderr.write("e")
        sys.stderr.flush()
    else:
        status = fake_main(argv)
        if not is_version:
            opposite_stream = sys.stderr if selected_stream is sys.stdout else sys.stdout
            opposite_stream.write("x")
            opposite_stream.flush()
    stream_at_limit(selected_stream, selected_token, suffix)
    wait_for_release()
    if overflow:
        stream_one_byte_past_limit(selected_stream, selected_token)
    record_state(emitted_bytes, "RELEASED")
    raise SystemExit(status)

if profile == "version-small" and is_version:
    print("small-version-child-line")
    print("small-version-error-line", file=sys.stderr)
elif profile in {{"asset-small", "asset-fail"}} and not is_version:
    print("child-" + "sec" + "ret" + "=child-sensitive-value\\nbenign-output-line")
    print("child-" + "sec" + "ret" + "=child-sensitive-value\\nbenign-error-line", file=sys.stderr)
raise SystemExit(fake_main(argv))
''',
        encoding="utf-8",
    )
    wrapper.chmod(0o755)
    return wrapper, marker, release


def assert_public_records(payload: bytes) -> str:
    decoded = payload.decode("utf-8")
    if not decoded:
        return decoded
    assert decoded.endswith("\n"), "public record is not newline terminated"
    for line in decoded.splitlines():
        assert line.startswith("glb-decimation: "), (
            "child stream bypassed the centralized record boundary"
        )
        assert line.isprintable(), "public child record contains controls"
        assert len((line + "\n").encode("utf-8")) <= MAX_DIAGNOSTIC_BYTES
    assert len(payload) <= 4 * MAX_DIAGNOSTIC_BYTES
    return decoded


def stream_marker(marker: Path) -> dict[str, object] | None:
    try:
        value = json.loads(marker.read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError, OSError):
        return None
    if not isinstance(value, dict):
        return None
    pid = value.get("pid")
    process_group = value.get("pgrp")
    attempted = value.get("attempted_bytes")
    emitted = value.get("emitted_bytes")
    state = value.get("state")
    if not all(
        isinstance(item, int)
        for item in (pid, process_group, attempted, emitted)
    ) or not isinstance(state, str):
        return None
    if pid <= 1 or process_group <= 1:
        return None
    if attempted < 0 or emitted < 0 or emitted > attempted:
        return None
    return value


def marked_process_alive(pid: int, process_group: int) -> bool:
    try:
        return os.getpgid(pid) == process_group
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def process_group_alive(process_group: int) -> bool:
    try:
        os.killpg(process_group, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def signal_process_group(process_group: int, selected_signal: signal.Signals) -> None:
    assert process_group > 1 and process_group != os.getpgrp()
    try:
        os.killpg(process_group, selected_signal)
    except ProcessLookupError:
        pass


def wait_for_marked_tree(marker_record: dict[str, object], timeout: float) -> bool:
    pid = marker_record["pid"]
    process_group = marker_record["pgrp"]
    assert isinstance(pid, int) and isinstance(process_group, int)
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if not marked_process_alive(pid, process_group) and not process_group_alive(
            process_group
        ):
            return True
        time.sleep(0.02)
    return not marked_process_alive(pid, process_group) and not process_group_alive(
        process_group
    )


def start_bounded_capture(
    case: types.SimpleNamespace, *, mode: str
) -> types.SimpleNamespace:
    process = subprocess.Popen(
        [sys.executable, str(script), *case.arguments],
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        start_new_session=True,
        env=child_environment(case, mode=mode),
    )
    assert process.stdout is not None and process.stderr is not None
    selector = selectors.DefaultSelector()
    streams = {"stdout": process.stdout, "stderr": process.stderr}
    buffers = {"stdout": bytearray(), "stderr": bytearray()}
    for name, stream in streams.items():
        os.set_blocking(stream.fileno(), False)
        selector.register(stream, selectors.EVENT_READ, name)
    return types.SimpleNamespace(
        process=process,
        selector=selector,
        streams=streams,
        buffers=buffers,
        capture_overflow=set(),
        started=time.monotonic(),
        closed=False,
    )


def drain_bounded_capture(capture: types.SimpleNamespace, timeout: float) -> None:
    if capture.closed:
        return
    for key, _events in capture.selector.select(timeout):
        stream = key.fileobj
        try:
            payload = os.read(stream.fileno(), 64 * 1024)
        except BlockingIOError:
            continue
        if not payload:
            capture.selector.unregister(stream)
            continue
        name = key.data
        buffer = capture.buffers[name]
        room = max(0, MAX_PUBLIC_CAPTURE_BYTES + 1 - len(buffer))
        if room:
            buffer.extend(payload[:room])
        if len(payload) > room or len(buffer) > MAX_PUBLIC_CAPTURE_BYTES:
            capture.capture_overflow.add(name)


def close_bounded_capture(capture: types.SimpleNamespace) -> None:
    if capture.closed:
        return
    drain_deadline = time.monotonic() + 1
    while capture.selector.get_map() and time.monotonic() < drain_deadline:
        drain_bounded_capture(capture, 0.02)
    capture.pipes_open = bool(capture.selector.get_map())
    for stream in capture.streams.values():
        try:
            capture.selector.unregister(stream)
        except KeyError:
            pass
        stream.close()
    capture.selector.close()
    capture.closed = True


def terminate_bounded_capture(
    capture: types.SimpleNamespace, marker: Path
) -> None:
    marker_record = stream_marker(marker)
    if marker_record is not None:
        process_group = marker_record["pgrp"]
        assert isinstance(process_group, int)
        signal_process_group(process_group, signal.SIGTERM)
    if capture.process.poll() is None:
        signal_process_group(capture.process.pid, signal.SIGTERM)
        try:
            capture.process.wait(timeout=1)
        except subprocess.TimeoutExpired:
            if marker_record is not None:
                process_group = marker_record["pgrp"]
                assert isinstance(process_group, int)
                signal_process_group(process_group, signal.SIGKILL)
            signal_process_group(capture.process.pid, signal.SIGKILL)
            capture.process.wait(timeout=2)
    close_bounded_capture(capture)


def bounded_capture_result(capture: types.SimpleNamespace) -> types.SimpleNamespace:
    assert capture.process.poll() is not None
    capture.process.wait()
    close_bounded_capture(capture)
    return types.SimpleNamespace(
        returncode=capture.process.returncode,
        stdout=bytes(capture.buffers["stdout"]),
        stderr=bytes(capture.buffers["stderr"]),
        capture_overflow=tuple(sorted(capture.capture_overflow)),
        pipes_open=capture.pipes_open,
    )


def marker_is_at_limit(
    value: dict[str, object] | None, profile: str, phase: str
) -> bool:
    return bool(
        value is not None
        and value.get("profile") == profile
        and value.get("phase") == phase
        and value.get("attempted_bytes") == MAX_CHILD_STREAM_BYTES
        and value.get("emitted_bytes") == MAX_CHILD_STREAM_BYTES
        and value.get("state") == "AT_LIMIT"
    )


def wait_for_at_limit(
    capture: types.SimpleNamespace,
    marker: Path,
    profile: str,
    phase: str,
    *peers: types.SimpleNamespace,
) -> dict[str, object]:
    deadline = time.monotonic() + MAX_OVERFLOW_SECONDS
    latest = None
    while time.monotonic() < deadline:
        drain_bounded_capture(capture, 0.01)
        for peer in peers:
            drain_bounded_capture(peer, 0.0)
        latest = stream_marker(marker)
        if marker_is_at_limit(latest, profile, phase):
            assert capture.process.poll() is None
            pid = latest["pid"]
            process_group = latest["pgrp"]
            assert isinstance(pid, int) and isinstance(process_group, int)
            assert marked_process_alive(pid, process_group)
            return latest
        if capture.process.poll() is not None:
            break
    raise AssertionError(
        f"{profile} did not remain alive at the exact child-stream boundary: "
        f"state={latest!r} returncode={capture.process.poll()!r}"
    )


def assert_still_held(
    capture: types.SimpleNamespace,
    marker: Path,
    profile: str,
    phase: str,
) -> dict[str, object]:
    value = stream_marker(marker)
    assert marker_is_at_limit(value, profile, phase), value
    assert capture.process.poll() is None
    assert value is not None
    pid = value["pid"]
    process_group = value["pgrp"]
    assert isinstance(pid, int) and isinstance(process_group, int)
    assert marked_process_alive(pid, process_group)
    return value


def wait_for_exit_with_peer_held(
    selected: types.SimpleNamespace,
    peer: types.SimpleNamespace,
    peer_marker: Path,
    peer_profile: str,
    phase: str,
) -> bool:
    deadline = time.monotonic() + MAX_OVERFLOW_SECONDS
    while time.monotonic() < deadline:
        drain_bounded_capture(selected, 0.01)
        drain_bounded_capture(peer, 0.0)
        if selected.process.poll() is not None:
            return True
        if peer.process.poll() is not None:
            return False
        assert_still_held(peer, peer_marker, peer_profile, phase)
    return selected.process.poll() is not None


def child_stream_boundary_case(phase: str, stream_name: str) -> None:
    exact_profile = f"{phase}-{stream_name}-exact"
    overflow_profile = f"{phase}-{stream_name}-over"
    exact_case = setup_case(f"child-output-{exact_profile}")
    overflow_case = setup_case(f"child-output-{overflow_profile}")
    exact_wrapper, exact_marker, exact_release = write_blender_wrapper(
        exact_case, exact_profile
    )
    overflow_wrapper, overflow_marker, overflow_release = write_blender_wrapper(
        overflow_case, overflow_profile
    )
    exact_case.arguments[exact_case.arguments.index(str(fake_blender))] = str(
        exact_wrapper
    )
    overflow_case.arguments[overflow_case.arguments.index(str(fake_blender))] = str(
        overflow_wrapper
    )
    exact_case.blender = exact_wrapper
    overflow_case.blender = overflow_wrapper
    exact_capture = None
    overflow_capture = None
    max_attempted = 0
    max_emitted = 0
    try:
        exact_capture = start_bounded_capture(exact_case, mode="success")
        exact_state = wait_for_at_limit(
            exact_capture, exact_marker, exact_profile, phase
        )
        assert not exact_release.exists()

        overflow_capture = start_bounded_capture(overflow_case, mode="success")
        assert exact_capture.started < overflow_capture.started
        overflow_state = wait_for_at_limit(
            overflow_capture,
            overflow_marker,
            overflow_profile,
            phase,
            exact_capture,
        )
        assert_still_held(
            exact_capture, exact_marker, exact_profile, phase
        )
        assert not exact_release.exists() and not overflow_release.exists()
        max_attempted = int(overflow_state["attempted_bytes"])
        max_emitted = int(overflow_state["emitted_bytes"])

        overflow_release.write_text("release\n", encoding="utf-8")
        exited = wait_for_exit_with_peer_held(
            overflow_capture,
            exact_capture,
            exact_marker,
            exact_profile,
            phase,
        )
        latest_overflow = stream_marker(overflow_marker)
        if latest_overflow is not None:
            max_attempted = max(
                max_attempted, int(latest_overflow["attempted_bytes"])
            )
            max_emitted = max(max_emitted, int(latest_overflow["emitted_bytes"]))
        assert exited, (
            "overflow candidate did not terminate from the released boundary byte",
            latest_overflow,
        )
        assert_still_held(
            exact_capture, exact_marker, exact_profile, phase
        )
        assert not exact_release.exists()
        assert max_attempted == MAX_CHILD_STREAM_BYTES + 1
        assert max_emitted in {
            MAX_CHILD_STREAM_BYTES,
            MAX_CHILD_STREAM_BYTES + 1,
        }

        overflow_result = bounded_capture_result(overflow_capture)
        overflow_stdout = assert_public_records(overflow_result.stdout)
        overflow_stderr = assert_public_records(overflow_result.stderr)
        assert not overflow_result.capture_overflow
        assert not overflow_result.pipes_open
        assert overflow_result.returncode != 0
        assert "output_triangles=" not in overflow_stdout
        assert_one_diagnostic(overflow_stderr)
        diagnostic = overflow_stderr.casefold()
        assert "timeout" not in diagnostic and "timed out" not in diagnostic
        assert list(overflow_case.output.iterdir()) == []
        assert_source_unchanged(overflow_case)
        assert wait_for_marked_tree(overflow_state, 2), (
            "overflow child process group survived candidate failure"
        )

        exact_release.write_text("release\n", encoding="utf-8")
        deadline = time.monotonic() + MAX_OVERFLOW_SECONDS
        while exact_capture.process.poll() is None and time.monotonic() < deadline:
            drain_bounded_capture(exact_capture, 0.02)
        assert exact_capture.process.poll() is not None, (
            "exact-boundary control did not finish after test release"
        )
        exact_result = bounded_capture_result(exact_capture)
        exact_stdout = assert_public_records(exact_result.stdout)
        exact_stderr = assert_public_records(exact_result.stderr)
        assert not exact_result.capture_overflow
        assert not exact_result.pipes_open
        assert_success_pair(
            exact_case,
            exact_result.returncode,
            exact_stdout,
            exact_stderr,
            None,
        )
        assert_fake_reached(exact_case)
        assert_source_unchanged(exact_case)
        released_state = stream_marker(exact_marker)
        assert released_state is not None
        assert released_state.get("attempted_bytes") == MAX_CHILD_STREAM_BYTES
        assert released_state.get("emitted_bytes") == MAX_CHILD_STREAM_BYTES
        assert released_state.get("state") == "RELEASED"
        assert wait_for_marked_tree(exact_state, 2), (
            "exact-boundary child process group survived successful release"
        )
    finally:
        if overflow_capture is not None:
            terminate_bounded_capture(overflow_capture, overflow_marker)
        if exact_capture is not None:
            terminate_bounded_capture(exact_capture, exact_marker)


def leader_exit_descendant_pipe_case() -> None:
    case = setup_case("leader-exit-descendant-pipes")
    wrapper = case.root / "descendant-pipe-blender.py"
    marker = case.root / "descendant.json"
    descendant_source = '''
import json
import os
import signal
import sys
import time
from pathlib import Path

signal.signal(signal.SIGTERM, signal.SIG_IGN)
Path(sys.argv[1]).write_text(
    json.dumps({
        "pid": os.getpid(),
        "pgrp": os.getpgrp(),
        "attempted_bytes": 0,
        "emitted_bytes": 0,
        "state": "HOLDING",
    }),
    encoding="utf-8",
)
while True:
    time.sleep(1)
'''
    wrapper.write_text(
        f'''#!/usr/bin/env python3
import subprocess
import sys
import time
from pathlib import Path

marker = Path({str(marker)!r})
descendant_source = {descendant_source!r}
subprocess.Popen(
    [sys.executable, "-c", descendant_source, str(marker)],
    stdin=subprocess.DEVNULL,
)
deadline = time.monotonic() + 2
while not marker.exists() and time.monotonic() < deadline:
    time.sleep(0.01)
if not marker.exists():
    raise SystemExit(71)
print("Blender {module.BLENDER_VERSION}")
print("build hash: {module.BLENDER_BUILD_HASH}")
raise SystemExit(0)
''',
        encoding="utf-8",
    )
    wrapper.chmod(0o755)
    case.arguments[case.arguments.index(str(fake_blender))] = str(wrapper)
    case.blender = wrapper
    marker_record = None
    try:
        started = time.monotonic()
        with mock.patch.object(module, "VERSION_TIMEOUT_SECONDS", 1):
            result, stdout, stderr, caught = run_main(case)
        elapsed = time.monotonic() - started
        marker_record = stream_marker(marker)
        assert marker_record is not None, "descendant marker was not written"
        assert elapsed < 5, f"descendant-pipe timeout was unbounded: {elapsed:.3f}s"
        assert caught is None, caught
        assert result == 1, (result, stdout, stderr)
        assert "output_triangles=" not in stdout
        assert_one_diagnostic(stderr)
        assert list(case.output.iterdir()) == []
        assert_source_unchanged(case)
        assert wait_for_marked_tree(marker_record, 2), (
            "leader exited but its pipe-holding descendant survived timeout",
            marker_record,
        )
    finally:
        if marker_record is None:
            marker_record = stream_marker(marker)
        if marker_record is not None:
            process_group = marker_record["pgrp"]
            assert isinstance(process_group, int)
            if process_group_alive(process_group):
                signal_process_group(process_group, signal.SIGKILL)
                wait_for_marked_tree(marker_record, 2)


def successful_cleanup_owned_group_case() -> None:
    real_popen = subprocess.Popen
    real_killpg = os.killpg
    ownership = {
        "process": None,
        "released": False,
        "unsafe_signals": [],
    }

    class OwnershipTrackedProcess:
        def __init__(self, *arguments, **keywords):
            assert keywords.get("start_new_session") is True
            self._process = real_popen(*arguments, **keywords)
            assert os.getpgid(self._process.pid) == self._process.pid
            ownership["process"] = self._process

        def __getattr__(self, name):
            return getattr(self._process, name)

        def poll(self):
            result = self._process.poll()
            if result is not None:
                ownership["released"] = True
            return result

        def wait(self, *arguments, **keywords):
            result = self._process.wait(*arguments, **keywords)
            ownership["released"] = True
            return result

    def reject_reused_group_signal(process_group, selected_signal):
        if ownership["released"]:
            ownership["unsafe_signals"].append(
                (process_group, int(selected_signal))
            )
            raise AssertionError("cleanup signalled a reused process group")
        return real_killpg(process_group, selected_signal)

    result = None
    caught = None
    try:
        with mock.patch.object(module.subprocess, "Popen", OwnershipTrackedProcess), \
             mock.patch.object(module.os, "killpg", reject_reused_group_signal):
            try:
                result = module._run_child_bounded(
                    [sys.executable, "-c", "print('owned-group-control')"],
                    timeout=2,
                    child_env={"PATH": os.defpath},
                )
            except BaseException as exc:
                caught = exc
        assert ownership["unsafe_signals"] == [], (
            "cleanup targeted a numerically reused group after leader reap",
            ownership["unsafe_signals"],
        )
        assert caught is None, caught
        assert result == (0, b"owned-group-control\n", b"")
    finally:
        process = ownership["process"]
        if process is not None:
            try:
                process.wait(timeout=1)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=1)


def missing_waitid_compatibility_case() -> None:
    if sys.platform != "darwin" or not hasattr(select, "kqueue"):
        return

    real_popen = subprocess.Popen
    real_killpg = os.killpg
    real_selector = selectors.DefaultSelector
    real_kqueue = select.kqueue
    real_waitid = getattr(os, "waitid", None)
    tracked = {
        "process": None,
        "released": False,
        "unsafe_signals": [],
        "selectors": [],
        "kqueues": [],
    }

    class TrackedKqueue:
        def __init__(self):
            self._value = real_kqueue()
            self.closed = False
            tracked["kqueues"].append(self)

        def __getattr__(self, name):
            return getattr(self._value, name)

        def close(self):
            self.closed = True
            return self._value.close()

    class TrackedSelector:
        def __init__(self):
            self._value = real_selector()
            self.closed = False
            tracked["selectors"].append(self)

        def __getattr__(self, name):
            return getattr(self._value, name)

        def close(self):
            self.closed = True
            return self._value.close()

    class OwnershipTrackedProcess:
        def __init__(self, *arguments, **keywords):
            self._process = real_popen(*arguments, **keywords)
            tracked["process"] = self._process
            if real_waitid is not None:
                deadline = time.monotonic() + 2
                while time.monotonic() < deadline:
                    status = real_waitid(
                        os.P_PID,
                        self._process.pid,
                        os.WEXITED | os.WNOHANG | os.WNOWAIT,
                    )
                    if status is not None:
                        break
                    time.sleep(0.005)
                else:
                    raise AssertionError("fast-exit control did not exit unreaped")

        def __getattr__(self, name):
            return getattr(self._process, name)

        def poll(self):
            result = self._process.poll()
            if result is not None:
                tracked["released"] = True
            return result

        def wait(self, *arguments, **keywords):
            result = self._process.wait(*arguments, **keywords)
            tracked["released"] = True
            return result

    def reject_reused_group_signal(process_group, selected_signal):
        if tracked["released"]:
            tracked["unsafe_signals"].append(
                (process_group, int(selected_signal))
            )
            raise AssertionError("compat cleanup signalled a reused process group")
        return real_killpg(process_group, selected_signal)

    descendant_source = r'''
import json
import os
from pathlib import Path
import signal
import sys
import time
sink = os.open(os.devnull, os.O_WRONLY)
os.dup2(sink, 1)
os.dup2(sink, 2)
os.close(sink)
signal.signal(signal.SIGTERM, signal.SIG_IGN)
Path(sys.argv[1]).write_text(json.dumps({
    "pid": os.getpid(),
    "pgrp": os.getpgrp(),
    "attempted_bytes": 0,
    "emitted_bytes": 0,
    "state": "DETACHED",
}), encoding="utf-8")
while True:
    time.sleep(1)
'''
    leader_source = r'''
import os
from pathlib import Path
import subprocess
import sys
import time
subprocess.Popen(
    [sys.executable, "-c", sys.argv[2], sys.argv[1]],
    stdin=subprocess.DEVNULL,
)
deadline = time.monotonic() + 2
while not Path(sys.argv[1]).exists() and time.monotonic() < deadline:
    time.sleep(0.005)
if not Path(sys.argv[1]).exists():
    raise SystemExit(73)
print("missing-waitid-control")
'''
    marker = root / "missing-waitid-descendant.json"
    caught = None
    result = None
    reaped = False
    record = None
    if real_waitid is not None:
        delattr(os, "waitid")
        try:
            with mock.patch.object(
                module.subprocess, "Popen", OwnershipTrackedProcess
            ), mock.patch.object(
                module.selectors, "DefaultSelector", TrackedSelector
            ), mock.patch.object(
                select, "kqueue", TrackedKqueue
            ), mock.patch.object(
                module.os, "killpg", reject_reused_group_signal
            ):
                try:
                    result = module._run_child_bounded(
                        [
                            sys.executable,
                            "-c",
                            leader_source,
                            str(marker),
                            descendant_source,
                        ],
                        timeout=3,
                        child_env={"PATH": os.defpath},
                    )
                except BaseException as exc:
                    caught = exc
        finally:
            setattr(os, "waitid", real_waitid)

        process = tracked["process"]
        assert process is not None
        record = stream_marker(marker)
        try:
            real_waitid(
                os.P_PID,
                process.pid,
                os.WEXITED | os.WNOHANG | os.WNOWAIT,
            )
        except ChildProcessError:
            reaped = True

    legacy_python = None
    candidates = [
        shutil.which("python3.12"),
        "/opt/homebrew/bin/python3.12",
        "/usr/local/bin/python3.12",
        "/usr/bin/python3",
    ]
    for candidate in dict.fromkeys(candidates):
        if not candidate or not os.access(candidate, os.X_OK):
            continue
        probe = subprocess.run(
            [
                candidate,
                "-B",
                "-c",
                "import os,select,sys; print(int(sys.platform == 'darwin' "
                "and not hasattr(os, 'waitid') and hasattr(select, 'kqueue')))",
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=3,
            check=False,
        )
        if probe.returncode == 0 and probe.stdout == b"1\n":
            legacy_python = candidate
            break

    legacy_result = None
    if legacy_python is not None:
        legacy_source = r'''
import importlib.util
import os
from pathlib import Path
import sys
script = Path(sys.argv[1])
spec = importlib.util.spec_from_file_location("legacy_decimate_probe", script)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
result = module._run_child_bounded(
    [sys.executable, "-c", "print('legacy-control')"],
    timeout=2,
    child_env={"PATH": os.defpath},
)
assert result == (0, b"legacy-control\n", b""), result
print("legacy-compat-pass")
'''
        legacy_result = subprocess.run(
            [legacy_python, "-B", "-c", legacy_source, str(script)],
            cwd=repo,
            env={"PATH": os.defpath, "PYTHONDONTWRITEBYTECODE": "1"},
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=5,
            check=False,
        )

    violations = []
    if real_waitid is not None:
        process = tracked["process"]
        assert process is not None
        if caught is not None:
            violations.append(f"absence backend raised {type(caught).__name__}")
        if result != (0, b"missing-waitid-control\n", b""):
            violations.append("absence backend lost the successful child result")
        if tracked["unsafe_signals"]:
            violations.append("absence backend signalled after leader ownership release")
        if not reaped:
            violations.append("absence backend did not reap its leader")
        if not process.stdout.closed or not process.stderr.closed:
            violations.append("absence backend left a child pipe open")
        if not tracked["selectors"] or not all(
            value.closed for value in tracked["selectors"]
        ):
            violations.append("absence backend left its selector open")
        if not tracked["kqueues"] or not all(
            value.closed for value in tracked["kqueues"]
        ):
            violations.append("absence backend left its exit observer open")
        if record is None or not wait_for_marked_tree(record, 2):
            violations.append("absence backend left its descendant group alive")
    if legacy_python is not None and (
        legacy_result is None or legacy_result.returncode != 0
    ):
        violations.append("available legacy macOS Python could not collect a child")
    elif legacy_result is not None and b"legacy-compat-pass" not in (
        legacy_result.stdout.splitlines()
    ):
        violations.append("legacy macOS Python compatibility control was not exact")

    try:
        assert not violations, "; ".join(violations)
    finally:
        if record is not None and not wait_for_marked_tree(record, 0):
            process_group = record["pgrp"]
            assert isinstance(process_group, int)
            signal_process_group(process_group, signal.SIGKILL)
        process = tracked["process"]
        if process is not None:
            try:
                process.wait(timeout=1)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=1)
            for stream in (process.stdout, process.stderr):
                if stream is not None and not stream.closed:
                    stream.close()
        for selector_value in tracked["selectors"]:
            if not selector_value.closed:
                selector_value.close()
        for kqueue_value in tracked["kqueues"]:
            if not kqueue_value.closed:
                kqueue_value.close()


def legacy_public_cli_case() -> None:
    violations = []
    deterministic = setup_case("legacy-zip-keyword")
    real_zip = builtins.zip

    def python_39_zip(*iterables, **keywords):
        if keywords:
            raise TypeError("zip() takes no keyword arguments")
        return real_zip(*iterables)

    with mock.patch.object(module, "zip", python_39_zip, create=True):
        deterministic_result = run_main(deterministic)
    try:
        assert_fake_reached(deterministic)
        assert_source_unchanged(deterministic)
        assert_success_pair(deterministic, *deterministic_result)
    except AssertionError:
        violations.append("Python 3.9 zip-keyword mutation rejected the pipeline")

    legacy_python = "/usr/bin/python3"
    if os.access(legacy_python, os.X_OK):
        probe = subprocess.run(
            [
                legacy_python,
                "-B",
                "-c",
                "import sys; print(int(sys.version_info[:2] == (3, 9)))",
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=3,
            check=False,
        )
        if probe.returncode != 0 or probe.stdout != b"1\n":
            legacy_python = None
    else:
        legacy_python = None

    if legacy_python is not None:
        public_case = setup_case("legacy-public-cli")
        public_result = subprocess.run(
            [legacy_python, "-B", str(script), *public_case.arguments],
            check=False,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=15,
            env=child_environment(public_case),
        )
        try:
            public_stdout = assert_public_records(public_result.stdout)
            public_stderr = assert_public_records(public_result.stderr)
            assert_fake_reached(public_case)
            assert_source_unchanged(public_case)
            assert_success_pair(
                public_case,
                public_result.returncode,
                public_stdout,
                public_stderr,
                None,
            )
        except (AssertionError, UnicodeDecodeError):
            violations.append("actual Python 3.9 public CLI did not publish exactly")
    else:
        print(
            "glb-decimation Python 3.9 public CLI: skipped "
            "(/usr/bin/python3 is not Python 3.9)",
            file=sys.stderr,
        )

    mismatch = setup_case("prepared-length-mismatch")
    real_prepare_assets = module._prepare_assets

    def drop_prepared_member(*arguments, **keywords):
        prepared = real_prepare_assets(*arguments, **keywords)
        assert len(prepared) == 1
        return []

    with mock.patch.object(
        module,
        "_prepare_assets",
        new=drop_prepared_member,
    ):
        result, stdout, stderr, caught = run_main(mismatch)
    assert caught is None
    assert result == 1, (result, stdout, stderr)
    assert "output_triangles=" not in stdout
    assert_one_diagnostic(stderr)
    assert "asset" not in audit_lines(mismatch)
    assert fake_records(mismatch) == []
    assert_source_unchanged(mismatch)
    assert list(mismatch.output.iterdir()) == []
    assert not violations, "; ".join(violations)


def argument_parse_diagnostic_case() -> None:
    hostile_value = "not-a-valid-option\nNEUTRAL_PARSE_SENTINEL\t" + "x" * 4_096
    result = subprocess.run(
        [
            sys.executable,
            str(script),
            "--unsupported-option",
            hostile_value,
        ],
        check=False,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=3,
    )
    assert result.returncode == 2, result.returncode
    assert result.stdout == b"", result.stdout
    stderr = result.stderr.decode("utf-8")
    assert_one_diagnostic(stderr)
    assert "Traceback" not in stderr


def successful_leader_detached_descendant_case() -> None:
    case = setup_case("successful-leader-detached-descendant")
    wrapper = case.root / "detached-descendant-blender.py"
    marker = case.root / "detached-descendant.json"
    descendant_source = '''
import json
import os
import signal
import sys
import time
from pathlib import Path

sink = os.open(os.devnull, os.O_WRONLY)
try:
    os.dup2(sink, 1)
    os.dup2(sink, 2)
finally:
    os.close(sink)
signal.signal(signal.SIGTERM, signal.SIG_IGN)
Path(sys.argv[1]).write_text(
    json.dumps({
        "pid": os.getpid(),
        "pgrp": os.getpgrp(),
        "leader_pid": int(sys.argv[2]),
        "leader_pgrp": int(sys.argv[3]),
        "attempted_bytes": 0,
        "emitted_bytes": 0,
        "state": "DETACHED",
    }),
    encoding="utf-8",
)
while True:
    time.sleep(1)
'''
    wrapper.write_text(
        f'''#!/usr/bin/env python3
import os
import subprocess
import sys
import time
from pathlib import Path

if "--version" not in sys.argv[1:]:
    os.execv(
        sys.executable,
        [sys.executable, {str(fake_blender)!r}, *sys.argv[1:]],
    )
audit_path = os.environ.get("FAKE_BLENDER_AUDIT")
if audit_path:
    with Path(audit_path).open("a", encoding="utf-8") as handle:
        handle.write("version\\n")
marker = Path({str(marker)!r})
descendant_source = {descendant_source!r}
subprocess.Popen(
    [
        sys.executable,
        "-c",
        descendant_source,
        str(marker),
        str(os.getpid()),
        str(os.getpgrp()),
    ],
    stdin=subprocess.DEVNULL,
)
deadline = time.monotonic() + 2
while not marker.exists() and time.monotonic() < deadline:
    time.sleep(0.01)
if not marker.exists():
    raise SystemExit(72)
print("Blender {module.BLENDER_VERSION}")
print("build hash: {module.BLENDER_BUILD_HASH}")
raise SystemExit(0)
''',
        encoding="utf-8",
    )
    wrapper.chmod(0o755)
    case.arguments[case.arguments.index(str(fake_blender))] = str(wrapper)
    case.blender = wrapper
    marker_record = None
    try:
        started = time.monotonic()
        result = run_main(case)
        elapsed = time.monotonic() - started
        marker_record = stream_marker(marker)
        assert marker_record is not None, "detached descendant marker was not written"
        assert marker_record.get("leader_pid") != marker_record["pid"]
        assert marker_record.get("leader_pgrp") == marker_record["pgrp"]
        assert marker_record["pgrp"] != os.getpgrp()
        assert elapsed < 8, f"successful group cleanup was unbounded: {elapsed:.3f}s"
        assert_success_pair(case, *result)
        assert_fake_reached(case)
        assert_source_unchanged(case)
        assert wait_for_marked_tree(marker_record, 2), (
            "successful leader returned while detached descendant survived",
            marker_record,
        )
    finally:
        if marker_record is None:
            marker_record = stream_marker(marker)
        if marker_record is not None:
            process_group = marker_record["pgrp"]
            assert isinstance(process_group, int)
            if process_group_alive(process_group):
                signal_process_group(process_group, signal.SIGKILL)
                wait_for_marked_tree(marker_record, 2)

def child_output_case(profile: str) -> None:
    placeholder = setup_case(f"child-output-{profile}")
    wrapper, _marker, _release = write_blender_wrapper(placeholder, profile)
    placeholder.blender = wrapper
    placeholder.arguments[placeholder.arguments.index(str(fake_blender))] = str(wrapper)
    mode = "fail" if profile == "asset-fail" else "success"
    result = run_cli(placeholder, mode=mode)

    violations = []
    try:
        stdout = assert_public_records(result.stdout)
        stderr = assert_public_records(result.stderr)
    except (AssertionError, UnicodeDecodeError):
        stdout = ""
        stderr = ""
        violations.append("child output crossed the bounded public record surface")
    combined = stdout + stderr
    if "child-sensitive-value" in combined:
        violations.append("child value crossed the public record boundary")

    failure = profile == "asset-fail"
    if failure:
        assert result.returncode != 0, "failed child process was reported as success"
        assert "output_triangles=" not in stdout
        assert not placeholder.final_glb.exists()
        assert not placeholder.final_json.exists()
        assert not any(placeholder.output.iterdir())
    else:
        if result.returncode != 0:
            violations.append("accepted child stream did not complete successfully")
        elif not (
            placeholder.final_glb.is_file()
            and placeholder.final_glb.read_bytes()[:4] == b"glTF"
            and placeholder.final_json.is_file()
        ):
            violations.append("accepted child stream did not commit a final pair")
        else:
            try:
                assert_fake_reached(placeholder)
            except AssertionError:
                violations.append("accepted child stream did not reach both child phases")
    assert_source_unchanged(placeholder)
    assert not violations, "; ".join(violations)


def child_entry(result_queue, function, arguments) -> None:
    try:
        try:
            os.setsid()
        except OSError:
            pass
        function(*arguments)
    except BaseException:
        result_queue.put(("error", traceback.format_exc()))
    else:
        result_queue.put(("ok", ""))


def terminate_process_tree(process) -> None:
    if not process.is_alive():
        return
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except (ProcessLookupError, PermissionError):
        process.terminate()
    process.join(2)
    if process.is_alive():
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except (ProcessLookupError, PermissionError):
            process.kill()
        process.join(2)


def run_bounded(label: str, function, *arguments) -> None:
    result_queue = process_context.Queue()
    process = process_context.Process(
        target=child_entry,
        args=(result_queue, function, arguments),
        name=f"glb-review-m-{label}",
    )
    process.start()
    process.join(20)
    if process.is_alive():
        terminate_process_tree(process)
        errors.append(f"{label}: bounded probe hung")
    else:
        try:
            state, detail = result_queue.get(timeout=1)
        except queue_module.Empty:
            errors.append(f"{label}: child exited {process.exitcode} without a result")
        else:
            if state != "ok":
                errors.append(f"{label}:\n{detail}")
        if process.exitcode not in (0, None):
            errors.append(f"{label}: child exit was {process.exitcode}")
    result_queue.close()
    result_queue.join_thread()
    process.close()


run_bounded("lock-release-terminal", lock_release_terminal_case)
run_bounded("force-cleanup-terminal", force_cleanup_terminal_case)
for cleanup_force in (False, True):
    for cleanup_after_effect in (False, True):
        run_bounded(
            "temporary-cleanup-"
            f"{'force' if cleanup_force else 'absent'}-"
            f"{'after' if cleanup_after_effect else 'before'}-effect",
            temporary_cleanup_terminal_case,
            cleanup_force,
            cleanup_after_effect,
        )
for batch_force in (False, True):
    run_bounded(
        f"later-asset-failure-{'force' if batch_force else 'absent'}",
        later_asset_failure_rolls_back_batch_case,
        batch_force,
    )
    run_bounded(
        f"interruption-after-publication-"
        f"{'force' if batch_force else 'absent'}",
        interruption_after_first_publication_case,
        batch_force,
    )
run_bounded(
    "persistent-absent-rollback-retirement",
    persistent_absent_rollback_retires_candidate_case,
)
run_bounded(
    "asymmetric-retired-cleanup",
    asymmetric_retired_cleanup_restores_exact_pair_case,
)
run_bounded(
    "sequential-retirement-rename-failure",
    sequential_retirement_rename_failure_case,
)
run_bounded(
    "mixed-force-success",
    mixed_force_success_uses_actual_lineage_case,
)
run_bounded(
    "mixed-force-later-failure",
    mixed_force_later_failure_rolls_back_actual_lineage_case,
)
for retry_force in (False, True):
    run_bounded(
        "rollback-verification-retry-"
        f"{'force' if retry_force else 'absent'}",
        rollback_verification_retry_case,
        retry_force,
    )
run_bounded(
    "sequential-force-commit-failure",
    sequential_force_commit_failure_rolls_back_batch_case,
)
run_bounded(
    "preunlink-receipt-read-failure",
    preunlink_receipt_read_failure_restores_single_publication_case,
)
run_bounded(
    "publication-recovery-aggregate-boundary",
    publication_recovery_aggregate_boundary_case,
)
run_bounded(
    "publication-recovery-production-ceiling",
    publication_recovery_production_ceiling_case,
)
run_bounded(
    "diagnostic-whole-message-redaction",
    diagnostic_whole_message_redaction_case,
)
run_bounded("json-numeric-boundary", json_numeric_boundary_case)
run_bounded("provenance-finite-number", provenance_finite_number_case)
run_bounded("success-record-before-effect", success_record_write_case, False)
run_bounded("success-record-after-effect", success_record_write_case, True)
run_bounded("bounded-read-oracle", bounded_read_oracle_case)
run_bounded("derivative-delegated-open-race", derivative_delegated_open_race_case)
for hash_role in ("json", "glb"):
    run_bounded(f"hash-role-race-{hash_role}", hash_role_race_case, hash_role)
    run_bounded(
        f"remove-non-old-final-role-{hash_role}",
        remove_non_old_final_role_case,
        hash_role,
    )
run_bounded("provenance-exact-boundary", provenance_exact_boundary_case)
for late_member in (
    "metadata-snapshot",
    "provenance",
    "existing-derivative",
    "final-derivative",
    "backup-derivative",
):
    run_bounded(f"late-cap-{late_member}", late_class_cap_case, late_member)
run_bounded("transaction-name-boundary", transaction_name_boundary_case)
run_bounded("exact-child-environment", exact_child_environment_case)
for child_profile in (
    "version-small",
    "asset-small",
    "asset-fail",
):
    run_bounded(f"child-{child_profile}", child_output_case, child_profile)
run_bounded("leader-exit-descendant-pipes", leader_exit_descendant_pipe_case)
run_bounded("successful-cleanup-owned-group", successful_cleanup_owned_group_case)
run_bounded("missing-waitid-compatibility", missing_waitid_compatibility_case)
run_bounded("legacy-public-cli", legacy_public_cli_case)
run_bounded("argument-parse-diagnostic", argument_parse_diagnostic_case)
run_bounded(
    "successful-leader-detached-descendant",
    successful_leader_detached_descendant_case,
)
for child_phase in ("version", "asset"):
    for child_stream in ("stdout", "stderr"):
        run_bounded(
            f"child-{child_phase}-{child_stream}-boundary",
            child_stream_boundary_case,
            child_phase,
            child_stream,
        )

if errors:
    raise AssertionError(
        "transaction/cap/subprocess hardening regressions:\n- "
        + "\n- ".join(errors)
    )
PY
  assert_no_external_effects
fi

if [ "$review_section" = M ]; then
  printf 'glb-decimation review M: pass\n'
  exit 0
fi

# Review regression A: filesystem identity is stronger than path spelling.
# These cases exercise the real orchestration entry point, but require every
# alias to be rejected before even the fake Blender version probe.
if [ "$review_section" = all ] || [ "$review_section" = A ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-identity" "$repo" "$fake_blender" <<'PY'
import contextlib
import hashlib
import importlib.util
import io
import json
import os
import re
import sys
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from glb_fixture import write_glb

spec = importlib.util.spec_from_file_location("decimate_assets_identity_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

errors = []

def check(condition, message):
    if not condition:
        errors.append(message)

def digest_bytes(value):
    return hashlib.sha256(value).hexdigest()

def snapshot(root_path):
    records = []
    for path in sorted(root_path.rglob("*"), key=lambda item: item.as_posix()):
        relative = path.relative_to(root_path).as_posix()
        if path.is_symlink():
            records.append((relative, "symlink", os.readlink(path)))
        elif path.is_dir():
            records.append((relative, "directory"))
        else:
            data = path.read_bytes()
            records.append((relative, "file", digest_bytes(data), data[:4].hex()))
    return records

def write_sidecar(source, service, prompt):
    path = Path(f"{source}.json")
    path.write_text(json.dumps({
        "service": service,
        "task_id": f"fixture-{service}-task",
        "timestamp_utc": "2026-08-15T12:34:56Z",
        "plan_tier": "paid",
        "prompt": prompt,
        "note": "local paid fixture",
        "sha256": digest_bytes(source.read_bytes()),
    }, sort_keys=True) + "\n", encoding="utf-8")
    return path

def write_manifest(path, entries):
    path.write_text(
        json.dumps({"assets": entries}, sort_keys=True) + "\n",
        encoding="utf-8",
    )

def line_count(path):
    if not path.exists():
        return 0
    return len(path.read_text(encoding="utf-8").splitlines())

def run_case(case_root, input_dir, output_dir, manifest, *, force=False):
    fake_log = case_root / "fake.log"
    fake_audit = case_root / "fake.audit"
    environment = {
        "FAKE_BLENDER_MODE": "success",
        "FAKE_BLENDER_LOG": str(fake_log),
        "FAKE_BLENDER_AUDIT": str(fake_audit),
        "PIPELINE_SENTINEL_KEY": "identity-sentinel-1",
        "PIPELINE_SENTINEL_TOKEN": "identity-sentinel-2",
        "PIPELINE_SENTINEL_SECRET": "identity-sentinel-3",
        "PIPELINE_SENTINEL_AUTH": "identity-sentinel-4",
        "PIPELINE_SENTINEL_CREDENTIAL": "identity-sentinel-5",
        "PIPELINE_SENTINEL_BEARER": "identity-sentinel-6",
        "PYTHONDONTWRITEBYTECODE": "1",
    }
    arguments = [
        "--manifest", str(manifest),
        "--input-dir", str(input_dir),
        "--output-dir", str(output_dir),
        "--blender", str(fake_blender),
    ]
    if force:
        arguments.append("--force")
    stdout = io.StringIO()
    stderr = io.StringIO()
    with (
        mock.patch.dict(os.environ, environment, clear=False),
        contextlib.redirect_stdout(stdout),
        contextlib.redirect_stderr(stderr),
    ):
        result = module.main(arguments)
    return result, stdout.getvalue() + stderr.getvalue(), fake_log, fake_audit

def require_pre_fake_rejection(label, result, output, fake_log, fake_audit, pattern):
    check(result != 0, f"{label}: aliased filesystem identity was accepted")
    check(
        re.search(pattern, output, re.IGNORECASE) is not None,
        f"{label}: missing alias diagnostic; output={output!r}",
    )
    check(line_count(fake_log) == 0, f"{label}: fake asset invocation was reached")
    check(line_count(fake_audit) == 0, f"{label}: fake version/asset invocation was reached")

# A1: each forced final member aliases the corresponding source inode.
case_root = root / "force-destination-hardlinks"
input_dir = case_root / "input"
output_dir = case_root / "output"
input_dir.mkdir(parents=True)
output_dir.mkdir()
source = input_dir / "asset.glb"
write_glb(source, triangles=30000)
sidecar = write_sidecar(source, "meshy", "identity fixture cat")
final_glb = output_dir / source.name
final_json = output_dir / sidecar.name
os.link(source, final_glb)
os.link(sidecar, final_json)
assert os.path.samefile(source, final_glb)
assert os.path.samefile(sidecar, final_json)
manifest = case_root / "manifest.json"
write_manifest(manifest, [{
    "id": "identity-cat", "kind": "cat", "service": "meshy",
    "out": source.name, "prompt": "identity fixture cat",
}])
input_before = snapshot(input_dir)
output_before = snapshot(output_dir)
source_bytes = source.read_bytes()
sidecar_bytes = sidecar.read_bytes()
source_hash = digest_bytes(source_bytes)
sidecar_hash = digest_bytes(sidecar_bytes)
result, output, fake_log, fake_audit = run_case(
    case_root, input_dir, output_dir, manifest, force=True
)
require_pre_fake_rejection(
    "force destination hardlinks", result, output, fake_log, fake_audit,
    r"alias|hard.?link|same (?:file|identity|inode)|filesystem identity",
)
check(snapshot(input_dir) == input_before, "force destination hardlinks: source tree changed")
check(snapshot(output_dir) == output_before, "force destination hardlinks: initial output pair changed")
check(source.read_bytes() == source_bytes, "force destination hardlinks: source GLB bytes changed")
check(sidecar.read_bytes() == sidecar_bytes, "force destination hardlinks: source sidecar bytes changed")
check(digest_bytes(source.read_bytes()) == source_hash, "force destination hardlinks: source GLB hash changed")
check(digest_bytes(sidecar.read_bytes()) == sidecar_hash, "force destination hardlinks: source sidecar hash changed")
check(source.read_bytes()[:4] == b"glTF", "force destination hardlinks: source GLB magic changed")
check(
    final_glb.exists() and os.path.samefile(source, final_glb),
    "force destination hardlinks: GLB alias was detached or removed",
)
check(
    final_json.exists() and os.path.samefile(sidecar, final_json),
    "force destination hardlinks: JSON alias was detached or removed",
)

# A2: two different manifest leaves name the same source GLB inode.
case_root = root / "manifest-source-hardlinks"
input_dir = case_root / "input"
output_dir = case_root / "output"
input_dir.mkdir(parents=True)
output_dir.mkdir()
source_a = input_dir / "first.glb"
source_b = input_dir / "second.glb"
write_glb(source_a, triangles=30000)
os.link(source_a, source_b)
assert os.path.samefile(source_a, source_b)
sidecar_a = write_sidecar(source_a, "meshy", "shared identity fixture")
sidecar_b = write_sidecar(source_b, "meshy", "shared identity fixture")
assert not os.path.samefile(sidecar_a, sidecar_b)
manifest = case_root / "manifest.json"
write_manifest(manifest, [
    {"id": "identity-first", "kind": "cat", "service": "meshy", "out": source_a.name, "prompt": "shared identity fixture"},
    {"id": "identity-second", "kind": "cat", "service": "meshy", "out": source_b.name, "prompt": "shared identity fixture"},
])
input_before = snapshot(input_dir)
source_a_bytes = source_a.read_bytes()
source_b_bytes = source_b.read_bytes()
source_a_hash = digest_bytes(source_a_bytes)
source_b_hash = digest_bytes(source_b_bytes)
result, output, fake_log, fake_audit = run_case(case_root, input_dir, output_dir, manifest)
require_pre_fake_rejection(
    "manifest source hardlinks", result, output, fake_log, fake_audit,
    r"source paths alias|hard.?link|duplicate source|same (?:file|identity|inode)|filesystem identity",
)
check(snapshot(input_dir) == input_before, "manifest source hardlinks: source tree changed")
check(source_a.read_bytes() == source_a_bytes, "manifest source hardlinks: first source bytes changed")
check(source_b.read_bytes() == source_b_bytes, "manifest source hardlinks: second source bytes changed")
check(digest_bytes(source_a.read_bytes()) == source_a_hash, "manifest source hardlinks: first source hash changed")
check(digest_bytes(source_b.read_bytes()) == source_b_hash, "manifest source hardlinks: second source hash changed")
check(source_a.read_bytes()[:4] == b"glTF", "manifest source hardlinks: first source magic changed")
check(source_b.read_bytes()[:4] == b"glTF", "manifest source hardlinks: second source magic changed")
check(os.path.samefile(source_a, source_b), "manifest source hardlinks: source identity split")
check(list(output_dir.iterdir()) == [], "manifest source hardlinks: partial output was created")

# A3: output names that differ only by case must be duplicate/alias-invalid on
# both case-sensitive and case-insensitive filesystems.
case_root = root / "casefold-output-names"
input_dir = case_root / "input"
output_dir = case_root / "output"
input_dir.mkdir(parents=True)
output_dir.mkdir()
upper = input_dir / "Case.glb"
lower = input_dir / "case.glb"
write_glb(upper, triangles=30000)
write_sidecar(upper, "meshy", "casefold identity fixture")
case_insensitive_casefold = lower.exists()
if not case_insensitive_casefold:
    write_glb(lower, triangles=30000)
    write_sidecar(lower, "meshy", "casefold identity fixture")
else:
    assert os.path.samefile(upper, lower)
manifest = case_root / "manifest.json"
write_manifest(manifest, [
    {"id": "case-upper", "kind": "cat", "service": "meshy", "out": "Case.glb", "prompt": "casefold identity fixture"},
    {"id": "case-lower", "kind": "cat", "service": "meshy", "out": "case.glb", "prompt": "casefold identity fixture"},
])
input_before = snapshot(input_dir)
result, output, fake_log, fake_audit = run_case(case_root, input_dir, output_dir, manifest)
require_pre_fake_rejection(
    "case-fold duplicate outputs", result, output, fake_log, fake_audit,
    (
        r"duplicate[^\n]*out|case.?fold|output paths alias|source paths alias"
        if case_insensitive_casefold
        else r"duplicate[^\n]*out|case.?fold|output paths alias"
    ),
)
check(snapshot(input_dir) == input_before, "case-fold duplicate outputs: source tree changed")
check(list(output_dir.iterdir()) == [], "case-fold duplicate outputs: partial output was created")

# A4: on a case-insensitive volume, differently cased root spellings are one
# directory. The capability guard itself proves why a skip is safe elsewhere.
case_root = root / "case-variant-roots"
input_dir = case_root / "InputCase"
output_dir = case_root / "inputcase"
input_dir.mkdir(parents=True)
case_insensitive = output_dir.exists() and os.path.samefile(input_dir, output_dir)
if case_insensitive:
    source = input_dir / "asset.glb"
    write_glb(source, triangles=30000)
    sidecar = write_sidecar(source, "meshy", "case root identity fixture")
    manifest = case_root / "manifest.json"
    write_manifest(manifest, [{
        "id": "case-root-cat", "kind": "cat", "service": "meshy",
        "out": source.name, "prompt": "case root identity fixture",
    }])
    source_bytes = source.read_bytes()
    sidecar_bytes = sidecar.read_bytes()
    source_hash = digest_bytes(source_bytes)
    sidecar_hash = digest_bytes(sidecar_bytes)
    tree_before = snapshot(input_dir)
    result, output, fake_log, fake_audit = run_case(
        case_root, input_dir, output_dir, manifest, force=True
    )
    require_pre_fake_rejection(
        "case-variant samefile roots", result, output, fake_log, fake_audit,
        r"alias|overlap|input.*output|output.*input|same (?:file|directory|identity|inode)",
    )
    check(os.path.samefile(input_dir, output_dir), "case-variant roots stopped aliasing")
    check(snapshot(input_dir) == tree_before, "case-variant samefile roots: shared source tree changed")
    check(source.read_bytes() == source_bytes, "case-variant samefile roots: source GLB changed")
    check(sidecar.read_bytes() == sidecar_bytes, "case-variant samefile roots: source sidecar changed")
    check(digest_bytes(source.read_bytes()) == source_hash, "case-variant samefile roots: source GLB hash changed")
    check(digest_bytes(sidecar.read_bytes()) == sidecar_hash, "case-variant samefile roots: source sidecar hash changed")
    check(source.read_bytes()[:4] == b"glTF", "case-variant samefile roots: GLB magic changed")
else:
    assert not output_dir.exists()
    print("glb-decimation review A: case-variant root skipped; volume proved case-sensitive")

if errors:
    raise AssertionError("filesystem identity regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = A ]; then
  printf 'glb-decimation review A: pass\n'
  exit 0
fi

# Review regression B: every forced promotion phase and rollback branch must
# preserve pair lineage. Persistent restore faults may leave a recoverable old
# pair only when both finals are absent and both complete backups remain.
if [ "$review_section" = all ] || [ "$review_section" = B ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-rollback" <<'PY'
import hashlib
import importlib.util
import multiprocessing
import os
import sys
import threading
import traceback
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
root.mkdir()
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("decimate_assets_rollback_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

errors = []
forward_order = ["backup_glb", "backup_json", "promote_glb", "promote_json"]
process_context = multiprocessing.get_context("fork")

def check_parent(condition, message):
    if not condition:
        errors.append(message)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def terminal_errors(
    label,
    directory,
    staged_glb,
    staged_json,
    final_glb,
    final_json,
    old_hashes,
    captured_backups,
    *,
    allow_absent_recovery,
):
    findings = []

    def require(condition, message):
        if not condition:
            findings.append(message)

    actual_paths = set(directory.iterdir())
    staged_leftovers = {
        path for path in (staged_glb, staged_json) if path.exists()
    }
    finals_are_old = (
        final_glb.is_file()
        and final_json.is_file()
        and (digest(final_glb), digest(final_json)) == old_hashes
    )
    finals_absent = not final_glb.exists() and not final_json.exists()
    captured_glb = captured_backups.get("glb")
    captured_json = captured_backups.get("json")
    captured_paths = {
        path for path in (captured_glb, captured_json) if path is not None
    }
    all_captured_paths = set(captured_backups.values())
    extant_captured = {
        path
        for path in all_captured_paths
        if path.exists() or path.is_symlink()
    }

    if finals_are_old:
        expected_paths = staged_leftovers | {final_glb, final_json}
        require(
            not extant_captured,
            f"{label}: captured backup remains after exact final restoration: "
            f"{sorted(path.name for path in extant_captured)}",
        )
        require(
            actual_paths == expected_paths,
            f"{label}: unexpected restored-branch contents: "
            f"{sorted(path.name for path in actual_paths - expected_paths)}",
        )
        return findings

    if finals_absent and allow_absent_recovery:
        protected_paths = {staged_glb, staged_json, final_glb, final_json}
        captured_are_unique_siblings = (
            set(captured_backups) == {"glb", "json"}
            and captured_glb is not None
            and captured_json is not None
            and len(captured_paths) == 2
            and captured_paths.isdisjoint(protected_paths)
            and all(path.parent == directory for path in captured_paths)
        )
        complete_old_backups = (
            captured_are_unique_siblings
            and captured_glb.is_file()
            and captured_json.is_file()
            and digest(captured_glb) == old_hashes[0]
            and digest(captured_json) == old_hashes[1]
        )
        expected_paths = staged_leftovers | captured_paths
        require(
            complete_old_backups,
            f"{label}: final pair absent without exactly two captured, unique, "
            "sibling old backup files",
        )
        require(
            actual_paths == expected_paths,
            f"{label}: unexpected recoverable-backup contents: "
            f"{sorted(path.name for path in actual_paths - expected_paths)}",
        )
        return findings

    if not allow_absent_recovery:
        findings.append(f"{label}: old final pair was not restored exactly")
    else:
        findings.append(
            f"{label}: split, partial, or unrecoverable terminal state; "
            f"final_glb={final_glb.exists()} final_json={final_json.exists()}"
        )
    if final_glb.exists() != final_json.exists():
        findings.append(f"{label}: exactly one final member remains")
    return findings

def new_oracle_fixture(name):
    directory = root / f"oracle-{name}"
    directory.mkdir()
    staged_glb = directory / "staged.glb"
    staged_json = directory / "staged.json"
    final_glb = directory / "final.glb"
    final_json = directory / "final.glb.json"
    old_glb = f"old oracle GLB bytes for {name}".encode()
    old_json = f"old oracle JSON bytes for {name}".encode()
    staged_glb.write_bytes(f"staged oracle GLB bytes for {name}".encode())
    staged_json.write_bytes(f"staged oracle JSON bytes for {name}".encode())
    return {
        "label": f"oracle-{name}",
        "directory": directory,
        "staged_glb": staged_glb,
        "staged_json": staged_json,
        "final_glb": final_glb,
        "final_json": final_json,
        "old_glb": old_glb,
        "old_json": old_json,
        "old_hashes": (
            hashlib.sha256(old_glb).hexdigest(),
            hashlib.sha256(old_json).hexdigest(),
        ),
    }

def inspect_oracle_fixture(fixture, captured_backups):
    return terminal_errors(
        fixture["label"],
        fixture["directory"],
        fixture["staged_glb"],
        fixture["staged_json"],
        fixture["final_glb"],
        fixture["final_json"],
        fixture["old_hashes"],
        captured_backups,
        allow_absent_recovery=True,
    )

def prove_terminal_oracle_mutations():
    probe_errors = []

    def probe(condition, message):
        if not condition:
            probe_errors.append(message)

    restored_copy = new_oracle_fixture("copy-restore-residue")
    restored_copy["final_glb"].write_bytes(restored_copy["old_glb"])
    restored_copy["final_json"].write_bytes(restored_copy["old_json"])
    copy_captures = {
        "glb": restored_copy["directory"] / ".old-glb",
        "json": restored_copy["directory"] / ".old-json",
    }
    probe(
        not inspect_oracle_fixture(restored_copy, copy_captures),
        "terminal oracle rejected exact restored finals with absent captured backups",
    )
    copy_captures["glb"].write_bytes(restored_copy["old_glb"])
    probe(
        digest(copy_captures["glb"]) == restored_copy["old_hashes"][0]
        and (
            restored_copy["final_glb"].read_bytes(),
            restored_copy["final_json"].read_bytes(),
        )
        == (restored_copy["old_glb"], restored_copy["old_json"]),
        "copy-restore mutation precondition was not established",
    )
    copy_errors = inspect_oracle_fixture(restored_copy, copy_captures)
    probe(
        any("captured backup remains" in error for error in copy_errors),
        "terminal oracle accepted copy restoration with captured .old-glb residue",
    )
    probe(
        any("unexpected restored-branch contents" in error for error in copy_errors),
        "copy-restore mutation did not exercise exact restored-branch membership",
    )

    restored_extra = new_oracle_fixture("restored-extra-residue")
    restored_extra["final_glb"].write_bytes(restored_extra["old_glb"])
    restored_extra["final_json"].write_bytes(restored_extra["old_json"])
    restored_captures = {
        "glb": restored_extra["directory"] / ".old-glb",
        "json": restored_extra["directory"] / ".old-json",
    }
    probe(
        not inspect_oracle_fixture(restored_extra, restored_captures),
        "terminal oracle rejected clean exact-restored baseline",
    )
    (restored_extra["directory"] / "arbitrary-residue").write_text(
        "must be rejected", encoding="utf-8"
    )
    restored_extra_errors = inspect_oracle_fixture(
        restored_extra, restored_captures
    )
    probe(
        any(
            "unexpected restored-branch contents" in error
            for error in restored_extra_errors
        ),
        "terminal oracle accepted arbitrary residue beside restored finals",
    )

    absent_recovery = new_oracle_fixture("renamed-backup-recovery")
    renamed_captures = {
        "glb": absent_recovery["directory"] / ".rollback-hold-old-glb",
        "json": absent_recovery["directory"] / ".rollback-hold-old-json",
    }
    probe(
        {path.name for path in renamed_captures.values()}
        == {".rollback-hold-old-glb", ".rollback-hold-old-json"}
        and not absent_recovery["final_glb"].exists()
        and not absent_recovery["final_json"].exists(),
        "renamed recoverable-backup precondition was not established",
    )
    renamed_captures["glb"].write_bytes(absent_recovery["old_glb"])
    renamed_captures["json"].write_bytes(absent_recovery["old_json"])
    probe(
        (digest(renamed_captures["glb"]), digest(renamed_captures["json"]))
        == absent_recovery["old_hashes"],
        "renamed recoverable-backup hashes do not match the old pair",
    )
    renamed_errors = inspect_oracle_fixture(absent_recovery, renamed_captures)
    probe(
        not renamed_errors,
        "terminal oracle rejected valid arbitrarily named recovery pair: "
        f"{renamed_errors}",
    )
    (absent_recovery["directory"] / "arbitrary-residue").write_text(
        "must be rejected", encoding="utf-8"
    )
    renamed_extra_errors = inspect_oracle_fixture(
        absent_recovery, renamed_captures
    )
    probe(
        any(
            "unexpected recoverable-backup contents" in error
            for error in renamed_extra_errors
        ),
        "terminal oracle accepted arbitrary residue beside recovery backups",
    )
    if probe_errors:
        raise AssertionError(
            "rollback terminal oracle self-test regressions:\n- "
            + "\n- ".join(probe_errors)
        )

def exercise(
    name,
    primary_failure,
    restore_failure=None,
    *,
    hang_on_restore=False,
    restore_progress=None,
):
    case_errors = []

    def check(condition, message):
        if not condition:
            case_errors.append(message)

    directory = root / name
    directory.mkdir()
    staged_glb = directory / "staged.glb"
    staged_json = directory / "staged.json"
    final_glb = directory / "final.glb"
    final_json = directory / "final.glb.json"
    old_glb = f"old GLB bytes for {name}".encode()
    old_json = f"old JSON bytes for {name}".encode()
    staged_glb.write_bytes(f"new GLB bytes for {name}".encode())
    staged_json.write_bytes(f"new JSON bytes for {name}".encode())
    final_glb.write_bytes(old_glb)
    final_json.write_bytes(old_json)
    old_hashes = (digest(final_glb), digest(final_json))

    real_replace = os.replace
    captured_backups = {}
    calls = []
    call_count = 0
    unexpected_seen = None
    primary_reached = False
    restore_attempts = 0

    def classify(source, destination):
        source_path = Path(source)
        destination_path = Path(destination)
        if source_path == final_glb and destination_path != staged_glb:
            captured_backups["glb"] = destination_path
            return "backup_glb"
        if source_path == final_json and destination_path != staged_json:
            captured_backups["json"] = destination_path
            return "backup_json"
        if source_path == staged_glb and destination_path == final_glb:
            return "promote_glb"
        if source_path == staged_json and destination_path == final_json:
            return "promote_json"
        if source_path == captured_backups.get("glb") and destination_path == final_glb:
            return "restore_glb"
        if source_path == captured_backups.get("json") and destination_path == final_json:
            return "restore_json"
        return f"unexpected:{source_path.name}->{destination_path.name}"

    def replacing(source, destination):
        nonlocal call_count, primary_reached, restore_attempts, unexpected_seen
        phase = classify(source, destination)
        call_count += 1
        if phase.startswith("unexpected:") and unexpected_seen is None:
            unexpected_seen = phase
        if len(calls) < 128:
            calls.append(phase)
        if phase == primary_failure and not primary_reached:
            primary_reached = True
            raise OSError(f"injected primary {primary_failure} failure")
        if (
            restore_failure is not None
            and primary_reached
            and phase == restore_failure
        ):
            restore_attempts += 1
            if restore_attempts == 1 and restore_progress is not None:
                restore_progress.set()
            if hang_on_restore:
                threading.Event().wait()
            raise OSError(
                f"injected persistent {restore_failure} failure "
                f"attempt={restore_attempts}"
            )
        return real_replace(source, destination)

    caught = None
    with mock.patch.object(module.os, "replace", new=replacing):
        try:
            module.promote_pair(
                staged_glb, staged_json, final_glb, final_json, True
            )
        except BaseException as exc:
            caught = exc

    label = f"{primary_failure}+{restore_failure or 'single'}"
    check(caught is not None, f"{label}: promotion swallowed injected failure")
    check(primary_reached, f"{label}: primary injection was not reached; calls={calls}")
    expected_prefix = forward_order[: forward_order.index(primary_failure) + 1]
    check(
        calls[: len(expected_prefix)] == expected_prefix,
        f"{label}: forward phases were not reached in order; calls={calls}",
    )
    check(
        unexpected_seen is None,
        f"{label}: unclassified replace call {unexpected_seen}; calls={calls} count={call_count}",
    )
    if restore_failure is not None:
        check(restore_attempts >= 1, f"{label}: persistent restore injection was not reached; calls={calls}")
        check(restore_failure in calls, f"{label}: restore phase absent; calls={calls}")

    case_errors.extend(
        terminal_errors(
            label,
            directory,
            staged_glb,
            staged_json,
            final_glb,
            final_json,
            old_hashes,
            captured_backups,
            allow_absent_recovery=restore_failure is not None,
        )
    )

    return case_errors

def child_exercise(sender, restore_progress, arguments, hang_on_restore):
    try:
        case_errors = exercise(
            *arguments,
            hang_on_restore=hang_on_restore,
            restore_progress=restore_progress,
        )
        sender.send(("result", case_errors))
    except BaseException:
        sender.send(("crash", traceback.format_exc()))
    finally:
        sender.close()

def stop_child(process):
    process.terminate()
    process.join(2)
    if process.is_alive():
        process.kill()
        process.join(2)
    check_parent(not process.is_alive(), f"child pid {process.pid} could not be terminated")

def run_bounded(arguments, *, expect_hang=False):
    label = f"{arguments[1]}+{arguments[2] or 'single'}"
    receiver, sender = process_context.Pipe(duplex=False)
    restore_progress = process_context.Event()
    process = process_context.Process(
        target=child_exercise,
        args=(sender, restore_progress, arguments, expect_hang),
        name=f"rollback-{arguments[0]}",
        daemon=True,
    )
    process.start()
    sender.close()

    if expect_hang:
        reached = restore_progress.wait(4)
        check_parent(reached, f"{label}: hang mutation never reached restore")
        if reached:
            process.join(0.25)
            check_parent(process.is_alive(), f"{label}: hang mutation unexpectedly returned")
        if process.is_alive():
            stop_child(process)
    else:
        process.join(4)
        if process.is_alive():
            reached_restore = restore_progress.is_set()
            stop_child(process)
            if arguments[2] is not None and reached_restore:
                errors.append(
                    f"{label}: persistent restore retry loop exceeded 4 seconds "
                    "after first targeted restore reach"
                )
            else:
                errors.append(
                    f"{label}: fault exercise exceeded 4 seconds before its "
                    "targeted restore was proven"
                )

    payload = None
    if receiver.poll():
        try:
            payload = receiver.recv()
        except EOFError:
            payload = None
    receiver.close()
    if not expect_hang and payload is None and process.exitcode not in {0, -15, -9}:
        errors.append(f"{label}: child exited {process.exitcode} without evidence")
    if not expect_hang and payload is not None:
        kind, value = payload
        if kind == "result":
            errors.extend(value)
        else:
            errors.append(f"{label}: child crashed:\n{value}")
    process.close()

prove_terminal_oracle_mutations()

for phase in forward_order:
    run_bounded((f"single-{phase}", phase, None))

for primary, restore in [
    ("backup_json", "restore_glb"),
    ("promote_glb", "restore_glb"),
    ("promote_glb", "restore_json"),
    ("promote_json", "restore_glb"),
    ("promote_json", "restore_json"),
]:
    run_bounded((f"compound-{primary}-{restore}", primary, restore))

# Prove a production retry/hang after a persistent restore fault cannot retain
# this test process. The child reports first restore reach, then blocks forever;
# the parent must terminate it within the bounded probe.
run_bounded(
    (
        "mutation-hanging-restore",
        "promote_json",
        "restore_json",
        ),
    expect_hang=True,
)

if errors:
    raise AssertionError("promotion rollback regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = B ]; then
  printf 'glb-decimation review B: pass\n'
  exit 0
fi

# Review regression C: the default absent-destination promotion is one atomic
# custody decision. Freeze _promotion_guard(final_glb, final_json, *,
# on_attempt=None, on_acquired=None) as a private context-manager seam around
# the complete existence-check/promotion/rollback transaction. Its callbacks
# report a nonblocking contention decision; _path_exists then proves B rechecks
# both completed finals after the real same-output guard acquires.
if [ "$review_section" = all ] || [ "$review_section" = C ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-concurrency" <<'PY'
import contextlib
import hashlib
import importlib.util
import inspect
import multiprocessing
import os
import signal
import sys
import threading
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
root.mkdir()
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("decimate_assets_concurrency_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

# Mutation-probe the outer termination mechanism itself. The child reaches a
# guard wrapped around the real promote_pair call and then blocks forever; it
# must be observed and killed without retaining a worker or touching the repo.
def hanging_guard_mutation(reached, mutation_root):
    mutation_root.mkdir()
    staged_glb = mutation_root / "staged.glb"
    staged_json = mutation_root / "staged.json"
    staged_glb.write_bytes(b"hanging mutation GLB")
    staged_json.write_bytes(b"hanging mutation JSON")
    final_glb = mutation_root / "final.glb"
    final_json = mutation_root / "final.glb.json"
    real_promote_pair = module.promote_pair

    @contextlib.contextmanager
    def hanging_guard(_final_glb, _final_json):
        reached.set()
        threading.Event().wait()
        yield

    def mutated_promote_pair(*arguments):
        with hanging_guard(arguments[2], arguments[3]):
            return real_promote_pair(*arguments)

    with mock.patch.object(module, "promote_pair", new=mutated_promote_pair):
        module.promote_pair(
            staged_glb, staged_json, final_glb, final_json, False
        )

process_context = multiprocessing.get_context("fork")
hang_reached = process_context.Event()
hang_process = process_context.Process(
    target=hanging_guard_mutation,
    args=(hang_reached, root / "mutation-hanging-guard"),
    name="concurrency-hanging-guard-mutation",
    daemon=True,
)
hang_process.start()
try:
    assert hang_reached.wait(4), "hanging guard mutation never reached the guard"
    hang_process.join(0.25)
    assert hang_process.is_alive(), "hanging guard mutation unexpectedly returned"
finally:
    if hang_process.is_alive():
        hang_process.terminate()
        hang_process.join(2)
    if hang_process.is_alive():
        hang_process.kill()
        hang_process.join(2)
    assert not hang_process.is_alive(), "hanging guard mutation could not be terminated"
    hang_process.close()

# Run all real callback and no-callback concurrency legs in a process that the
# outer harness can terminate even if a production lock blocks forever.
probe_pid = os.fork()
if probe_pid:
    class ProbeTimeout(Exception):
        pass

    def timeout_probe(_signum, _frame):
        raise ProbeTimeout

    previous_handler = signal.signal(signal.SIGALRM, timeout_probe)
    signal.alarm(20)
    try:
        try:
            _, probe_status = os.waitpid(probe_pid, 0)
        except ProbeTimeout:
            os.kill(probe_pid, signal.SIGKILL)
            os.waitpid(probe_pid, 0)
            raise AssertionError(
                "concurrency probe exceeded 20 seconds and was terminated"
            )
    finally:
        signal.alarm(0)
        signal.signal(signal.SIGALRM, previous_handler)
    raise SystemExit(os.waitstatus_to_exitcode(probe_status))

callback_root = root / "callback-protocol"
callback_root.mkdir()
final_glb = callback_root / "final.glb"
final_json = callback_root / "final.glb.json"
staged = {
    "A": (callback_root / "a-staged.glb", callback_root / "a-staged.json"),
    "B": (callback_root / "b-staged.glb", callback_root / "b-staged.json"),
}
payloads = {
    "A": (b"complete derivative from A", b"complete provenance from A"),
    "B": (b"complete derivative from B", b"complete provenance from B"),
}
for owner in ("A", "B"):
    staged[owner][0].write_bytes(payloads[owner][0])
    staged[owner][1].write_bytes(payloads[owner][1])

real_replace = os.replace
real_path_exists = module._path_exists
state_mutex = threading.Lock()
guard_state = threading.local()
a_at_first_replace = threading.Event()
b_at_first_replace = threading.Event()
b_progress = threading.Event()
b_guard_attempted = threading.Event()
a_guard_acquired = threading.Event()
b_guard_acquired = threading.Event()
a_pair_completed_in_guard = threading.Event()
allow_a_glb_replace = threading.Event()
a_glb_established = threading.Event()
allow_b_glb_replace = threading.Event()
b_json_failure_reached = threading.Event()
release_a_json = threading.Event()
results = {}
replace_calls = []
path_observations = []
guard_attempts = []
guard_acquisitions = []

@contextlib.contextmanager
def missing_promotion_guard(
    final_glb, final_json, *, on_attempt=None, on_acquired=None
):
    del final_glb, final_json
    if on_attempt is not None:
        on_attempt(False)
    if on_acquired is not None:
        on_acquired(False)
    yield

production_promotion_guard = getattr(
    module, "_promotion_guard", missing_promotion_guard
)
guard_parameters = list(
    inspect.signature(production_promotion_guard).parameters.values()
)
assert [parameter.name for parameter in guard_parameters] == [
    "final_glb", "final_json", "on_attempt", "on_acquired",
], "_promotion_guard must expose the exact frozen parameter names/order"
assert all(
    parameter.kind is inspect.Parameter.POSITIONAL_OR_KEYWORD
    for parameter in guard_parameters[:2]
), "_promotion_guard final paths must be positional parameters"
assert all(
    parameter.kind is inspect.Parameter.KEYWORD_ONLY
    and parameter.default is None
    for parameter in guard_parameters[2:]
), "_promotion_guard callbacks must be keyword-only and default exactly to None"
real_promotion_guard = production_promotion_guard
if os.environ.get("GLB_DECIMATION_TEST_GUARD_MUTATION") == "noop":
    real_promotion_guard = missing_promotion_guard
elif os.environ.get("GLB_DECIMATION_TEST_GUARD_MUTATION") not in {None, ""}:
    raise AssertionError("unsupported GLB_DECIMATION_TEST_GUARD_MUTATION")

class MissingPromotionLock:
    def acquire(self, blocking=True):
        del blocking
        return True

    def release(self):
        return None

def missing_promotion_lock_for(_final_glb, _final_json):
    return MissingPromotionLock()

real_promotion_lock_for = getattr(
    module, "_promotion_lock_for", missing_promotion_lock_for
)

def thread_owner():
    name = threading.current_thread().name
    if name == "promotion-A":
        return "A"
    if name == "promotion-B":
        return "B"
    raise AssertionError(f"unexpected promotion thread {name!r}")

@contextlib.contextmanager
def controlled_promotion_guard(guard_glb, guard_json):
    assert Path(guard_glb) == final_glb
    assert Path(guard_json) == final_json
    owner = thread_owner()

    def attempted(contended):
        assert isinstance(contended, bool)
        with state_mutex:
            guard_attempts.append((owner, contended))
        if owner == "B":
            b_guard_attempted.set()
            b_progress.set()

    def acquired(contended):
        assert isinstance(contended, bool)
        completed = a_pair_completed_in_guard.is_set()
        with state_mutex:
            guard_acquisitions.append((owner, contended, completed))
        if owner == "A":
            a_guard_acquired.set()
        else:
            b_guard_acquired.set()

    with real_promotion_guard(
        guard_glb,
        guard_json,
        on_attempt=attempted,
        on_acquired=acquired,
    ):
        guard_state.active = True
        try:
            yield
        finally:
            if (
                owner == "A"
                and final_glb.is_file()
                and final_json.is_file()
                and final_glb.read_bytes() == payloads["A"][0]
                and final_json.read_bytes() == payloads["A"][1]
            ):
                a_pair_completed_in_guard.set()
            guard_state.active = False

def observing_path_exists(path):
    candidate = Path(path)
    value = real_path_exists(candidate)
    if candidate in {final_glb, final_json}:
        owner = thread_owner()
        member = "glb" if candidate == final_glb else "json"
        inside_guard = bool(getattr(guard_state, "active", False))
        completed = a_pair_completed_in_guard.is_set()
        with state_mutex:
            path_observations.append(
                (owner, member, value, inside_guard, completed)
            )
    return value

def replacing(source, destination):
    source_path = Path(source)
    destination_path = Path(destination)
    if source_path in staged["A"]:
        owner = "A"
    elif source_path in staged["B"]:
        owner = "B"
    else:
        return real_replace(source_path, destination_path)
    if destination_path == final_glb:
        member = "glb"
    elif destination_path == final_json:
        member = "json"
    else:
        return real_replace(source_path, destination_path)
    with state_mutex:
        replace_calls.append((owner, member))
    if owner == "A" and member == "glb":
        a_at_first_replace.set()
        if not allow_a_glb_replace.wait(5):
            raise AssertionError("timed out releasing A GLB promotion")
        result = real_replace(source_path, destination_path)
        a_glb_established.set()
        if not release_a_json.wait(5):
            raise AssertionError("timed out releasing A JSON promotion")
        return result
    if owner == "B" and member == "glb":
        b_at_first_replace.set()
        b_progress.set()
        if not allow_b_glb_replace.wait(5):
            raise AssertionError("timed out releasing B GLB promotion")
        return real_replace(source_path, destination_path)
    if owner == "B" and member == "json":
        b_json_failure_reached.set()
        raise OSError("injected concurrent B JSON promotion failure")
    return real_replace(source_path, destination_path)

def promote(owner):
    try:
        module.promote_pair(
            staged[owner][0], staged[owner][1], final_glb, final_json, False
        )
    except BaseException as exc:
        result = ("failure", exc)
    else:
        result = ("success", None)
    with state_mutex:
        results[owner] = result

with (
    mock.patch.object(module, "_promotion_guard", new=controlled_promotion_guard, create=True),
    mock.patch.object(module, "_path_exists", new=observing_path_exists),
    mock.patch.object(module.os, "replace", new=replacing),
):
    thread_a = threading.Thread(
        target=promote, args=("A",), name="promotion-A", daemon=True
    )
    thread_b = threading.Thread(
        target=promote, args=("B",), name="promotion-B", daemon=True
    )
    try:
        thread_a.start()
        assert a_at_first_replace.wait(5), "A never reached first GLB promotion"
        thread_b.start()
        assert b_progress.wait(5), "B reached neither the guard-attempt seam nor first replace"
        b_acquired_before_a_release = b_guard_acquired.is_set()

        # Correct code signals the guard attempt and blocks on A's transaction.
        # Current unlocked code instead reaches B's first replace and blocks there.
        allow_a_glb_replace.set()
        assert a_glb_established.wait(5), "A never established the destination GLB"
        allow_b_glb_replace.set()
        if b_at_first_replace.is_set():
            assert b_json_failure_reached.wait(5), "B crossed but did not reach JSON injection"
        release_a_json.set()

        thread_a.join(5)
        thread_b.join(5)
        assert not thread_a.is_alive(), "A promotion thread did not terminate"
        assert not thread_b.is_alive(), "B promotion thread did not terminate"
    finally:
        allow_a_glb_replace.set()
        allow_b_glb_replace.set()
        release_a_json.set()
        if thread_a.is_alive():
            thread_a.join(0.5)
        if thread_b.is_alive():
            thread_b.join(0.5)

errors = []
def check(condition, message):
    if not condition:
        errors.append(message)

with state_mutex:
    result_snapshot = dict(results)
    replace_snapshot = list(replace_calls)
    observation_snapshot = list(path_observations)
    attempt_snapshot = list(guard_attempts)
    acquisition_snapshot = list(guard_acquisitions)

check(set(result_snapshot) == {"A", "B"}, f"missing thread result: {result_snapshot}")
successes = [owner for owner, result in result_snapshot.items() if result[0] == "success"]
failures = [owner for owner, result in result_snapshot.items() if result[0] == "failure"]
check(successes == ["A"], f"expected only A success; results={result_snapshot}")
check(failures == ["B"], f"expected only B refusal/failure; results={result_snapshot}")
b_exception = result_snapshot.get("B", (None, None))[1]
check(
    isinstance(b_exception, module.DecimationError)
    and str(b_exception) == "refusing existing derivative without --force",
    f"B lacked exact existing-destination DecimationError: {b_exception!r}",
)
check(a_guard_acquired.is_set(), "A did not acquire the _promotion_guard seam")
check(b_guard_attempted.is_set(), "B did not attempt _promotion_guard before A release")
check(b_guard_acquired.is_set(), "B did not acquire _promotion_guard after A completed")
check(
    not b_acquired_before_a_release,
    "B acquired _promotion_guard before A released its transaction",
)
check(
    attempt_snapshot == [("A", False), ("B", True)],
    f"guard attempts did not prove real B contention: {attempt_snapshot}",
)
check(
    acquisition_snapshot == [("A", False, False), ("B", True, True)],
    "guard acquisitions did not serialize B behind completed A: "
    f"{acquisition_snapshot}",
)
check(a_pair_completed_in_guard.is_set(), "A pair was not completed while its guard was held")
check(not b_at_first_replace.is_set(), f"B replace was reached: {replace_snapshot}")
b_postlock = [
    (member, value, completed)
    for owner, member, value, inside_guard, completed in observation_snapshot
    if owner == "B" and inside_guard
]
check(
    {member for member, _, _ in b_postlock} == {"glb", "json"}
    and all(value and completed for _, value, completed in b_postlock),
    f"B did not recheck both completed A finals inside the guard: {b_postlock}",
)
complete_pair = final_glb.is_file() and final_json.is_file()
check(complete_pair, "successful A final pair is incomplete")
if complete_pair:
    check(final_glb.read_bytes() == payloads["A"][0], "final GLB is not A's successful member")
    check(final_json.read_bytes() == payloads["A"][1], "final JSON is not A's successful member")
    check(
        hashlib.sha256(final_glb.read_bytes()).hexdigest()
        == hashlib.sha256(payloads["A"][0]).hexdigest(),
        "final GLB hash does not belong to successful A",
    )
    check(
        hashlib.sha256(final_json.read_bytes()).hexdigest()
        == hashlib.sha256(payloads["A"][1]).hexdigest(),
        "final JSON hash does not belong to successful A",
    )
expected_entries = {final_glb, final_json, staged["B"][0], staged["B"][1]}
actual_entries = set(callback_root.iterdir())
check(
    actual_entries == expected_entries,
    "concurrent promotion left missing/unexpected transaction entries: "
    f"{sorted(path.name for path in actual_entries)}",
)

# Independently exercise the real, unwrapped guard with its normal callback
# defaults. `_promotion_lock_for(final_glb, final_json)` must return the stable
# same-output LockLike used on every guard call. The proxy below delegates every
# acquire/release to that real lock and only records the low-level contention;
# it never supplies serialization.
def no_callback_probe(case_root, guard_override=None):
    case_errors = []

    def check_case(condition, message):
        if not condition:
            case_errors.append(message)

    case_root.mkdir()
    probe_final_glb = case_root / "final.glb"
    probe_final_json = case_root / "final.glb.json"
    probe_staged = {
        "A": (case_root / "a-staged.glb", case_root / "a-staged.json"),
        "B": (case_root / "b-staged.glb", case_root / "b-staged.json"),
    }
    probe_payloads = {
        "A": (b"no-callback derivative A", b"no-callback provenance A"),
        "B": (b"no-callback derivative B", b"no-callback provenance B"),
    }
    for probe_owner in ("A", "B"):
        probe_staged[probe_owner][0].write_bytes(probe_payloads[probe_owner][0])
        probe_staged[probe_owner][1].write_bytes(probe_payloads[probe_owner][1])

    probe_state_mutex = threading.Lock()
    lock_state = threading.local()
    probe_a_at_replace = threading.Event()
    probe_b_at_replace = threading.Event()
    probe_b_progress = threading.Event()
    probe_b_contended = threading.Event()
    probe_b_acquired = threading.Event()
    probe_allow_a_glb = threading.Event()
    probe_a_glb_established = threading.Event()
    probe_allow_b_glb = threading.Event()
    probe_b_json_failure = threading.Event()
    probe_release_a_json = threading.Event()
    probe_results = {}
    probe_replace_calls = []
    probe_path_observations = []
    probe_lock_operations = []
    underlying_locks = []

    def probe_owner():
        name = threading.current_thread().name
        if name == "no-callback-A":
            return "A"
        if name == "no-callback-B":
            return "B"
        raise AssertionError(f"unexpected no-callback thread {name!r}")

    def pair_is_complete_a():
        return (
            probe_final_glb.is_file()
            and probe_final_json.is_file()
            and probe_final_glb.read_bytes() == probe_payloads["A"][0]
            and probe_final_json.read_bytes() == probe_payloads["A"][1]
        )

    class ObservingLock:
        def __init__(self, underlying):
            self.underlying = underlying

        def acquire(self, blocking=True):
            owner = probe_owner()
            result = self.underlying.acquire(blocking)
            completed = pair_is_complete_a()
            with probe_state_mutex:
                probe_lock_operations.append(
                    (owner, "acquire", blocking, result, completed)
                )
            if owner == "B" and not blocking and not result:
                probe_b_contended.set()
                probe_b_progress.set()
            if result:
                lock_state.active = True
                if owner == "B":
                    probe_b_acquired.set()
            return result

        def release(self):
            owner = probe_owner()
            with probe_state_mutex:
                probe_lock_operations.append(
                    (owner, "release", None, None, pair_is_complete_a())
                )
            lock_state.active = False
            return self.underlying.release()

    def observing_lock_for(lock_glb, lock_json):
        assert Path(lock_glb) == probe_final_glb
        assert Path(lock_json) == probe_final_json
        underlying = real_promotion_lock_for(lock_glb, lock_json)
        with probe_state_mutex:
            underlying_locks.append((probe_owner(), underlying))
        return ObservingLock(underlying)

    def probe_path_exists(path):
        candidate = Path(path)
        value = real_path_exists(candidate)
        if candidate in {probe_final_glb, probe_final_json}:
            owner = probe_owner()
            member = "glb" if candidate == probe_final_glb else "json"
            active = bool(getattr(lock_state, "active", False))
            completed = pair_is_complete_a()
            with probe_state_mutex:
                probe_path_observations.append(
                    (owner, member, value, active, completed)
                )
        return value

    def probe_replacing(source, destination):
        source_path = Path(source)
        destination_path = Path(destination)
        if source_path in probe_staged["A"]:
            owner = "A"
        elif source_path in probe_staged["B"]:
            owner = "B"
        else:
            return real_replace(source_path, destination_path)
        if destination_path == probe_final_glb:
            member = "glb"
        elif destination_path == probe_final_json:
            member = "json"
        else:
            return real_replace(source_path, destination_path)
        with probe_state_mutex:
            probe_replace_calls.append((owner, member))
        if owner == "A" and member == "glb":
            probe_a_at_replace.set()
            if not probe_allow_a_glb.wait(5):
                raise AssertionError("timed out releasing no-callback A GLB")
            result = real_replace(source_path, destination_path)
            probe_a_glb_established.set()
            if not probe_release_a_json.wait(5):
                raise AssertionError("timed out releasing no-callback A JSON")
            return result
        if owner == "B" and member == "glb":
            probe_b_at_replace.set()
            probe_b_progress.set()
            if not probe_allow_b_glb.wait(5):
                raise AssertionError("timed out releasing no-callback B GLB")
            return real_replace(source_path, destination_path)
        if owner == "B" and member == "json":
            probe_b_json_failure.set()
            raise OSError("injected no-callback B JSON failure")
        return real_replace(source_path, destination_path)

    def probe_promote(owner):
        try:
            module.promote_pair(
                probe_staged[owner][0],
                probe_staged[owner][1],
                probe_final_glb,
                probe_final_json,
                False,
            )
        except BaseException as exc:
            result = ("failure", exc)
        else:
            result = ("success", None)
        with probe_state_mutex:
            probe_results[owner] = result

    with contextlib.ExitStack() as stack:
        stack.enter_context(
            mock.patch.object(
                module,
                "_promotion_lock_for",
                new=observing_lock_for,
                create=True,
            )
        )
        stack.enter_context(
            mock.patch.object(module, "_path_exists", new=probe_path_exists)
        )
        stack.enter_context(
            mock.patch.object(module.os, "replace", new=probe_replacing)
        )
        if guard_override is not None:
            stack.enter_context(
                mock.patch.object(
                    module,
                    "_promotion_guard",
                    new=guard_override,
                    create=True,
                )
            )

        probe_thread_a = threading.Thread(
            target=probe_promote,
            args=("A",),
            name="no-callback-A",
            daemon=True,
        )
        probe_thread_b = threading.Thread(
            target=probe_promote,
            args=("B",),
            name="no-callback-B",
            daemon=True,
        )
        try:
            probe_thread_a.start()
            assert probe_a_at_replace.wait(5), "no-callback A never reached GLB promotion"
            probe_thread_b.start()
            assert probe_b_progress.wait(5), (
                "no-callback B reached neither real low-level contention nor replace"
            )
            b_acquired_before_release = probe_b_acquired.is_set()
            probe_allow_a_glb.set()
            assert probe_a_glb_established.wait(5), (
                "no-callback A never established destination GLB"
            )
            probe_allow_b_glb.set()
            if probe_b_at_replace.is_set():
                assert probe_b_json_failure.wait(5), (
                    "no-callback B crossed without reaching JSON injection"
                )
            probe_release_a_json.set()
            probe_thread_a.join(5)
            probe_thread_b.join(5)
            assert not probe_thread_a.is_alive(), "no-callback A did not terminate"
            assert not probe_thread_b.is_alive(), "no-callback B did not terminate"
        finally:
            probe_allow_a_glb.set()
            probe_allow_b_glb.set()
            probe_release_a_json.set()
            if probe_thread_a.is_alive():
                probe_thread_a.join(0.5)
            if probe_thread_b.is_alive():
                probe_thread_b.join(0.5)

    with probe_state_mutex:
        result_snapshot = dict(probe_results)
        replace_snapshot = list(probe_replace_calls)
        path_snapshot = list(probe_path_observations)
        lock_snapshot = list(probe_lock_operations)
        lock_object_snapshot = list(underlying_locks)

    successes = [
        owner for owner, result in result_snapshot.items()
        if result[0] == "success"
    ]
    failures = [
        owner for owner, result in result_snapshot.items()
        if result[0] == "failure"
    ]
    check_case(set(result_snapshot) == {"A", "B"}, f"missing results: {result_snapshot}")
    check_case(successes == ["A"], f"expected only A success: {result_snapshot}")
    check_case(failures == ["B"], f"expected only B failure: {result_snapshot}")
    b_exception = result_snapshot.get("B", (None, None))[1]
    check_case(
        isinstance(b_exception, module.DecimationError)
        and str(b_exception) == "refusing existing derivative without --force",
        f"B lacked exact post-lock destination refusal: {b_exception!r}",
    )
    check_case(probe_b_contended.is_set(), "B did not observe real low-level contention")
    check_case(probe_b_acquired.is_set(), "B did not acquire the real low-level lock")
    check_case(
        not b_acquired_before_release,
        "B acquired the low-level lock before A released its transaction",
    )
    check_case(
        any(
            owner == "A" and operation == "acquire"
            and blocking is False and result is True
            for owner, operation, blocking, result, _ in lock_snapshot
        ),
        f"A lacked a successful nonblocking lock acquire: {lock_snapshot}",
    )
    check_case(
        any(
            owner == "B" and operation == "acquire"
            and blocking is False and result is False
            for owner, operation, blocking, result, _ in lock_snapshot
        ),
        f"B lacked a failed nonblocking contention probe: {lock_snapshot}",
    )
    check_case(
        any(
            owner == "B" and operation == "acquire"
            and blocking is True and result is True and completed
            for owner, operation, blocking, result, completed in lock_snapshot
        ),
        f"B did not acquire after A's complete pair: {lock_snapshot}",
    )
    check_case(
        any(item[0] == "A" and item[1] == "release" for item in lock_snapshot)
        and any(item[0] == "B" and item[1] == "release" for item in lock_snapshot),
        f"both low-level releases were not observed: {lock_snapshot}",
    )
    if lock_object_snapshot:
        first_underlying = lock_object_snapshot[0][1]
        check_case(
            all(underlying is first_underlying for _, underlying in lock_object_snapshot)
            and {owner for owner, _ in lock_object_snapshot} == {"A", "B"},
            "_promotion_lock_for did not return one stable same-output lock",
        )
    else:
        check_case(False, "_promotion_lock_for was never reached")
    check_case(
        not probe_b_at_replace.is_set(),
        f"B reached replace despite no-callback locking: {replace_snapshot}",
    )
    b_postlock = [
        (member, value, completed)
        for owner, member, value, active, completed in path_snapshot
        if owner == "B" and active
    ]
    check_case(
        {member for member, _, _ in b_postlock} == {"glb", "json"}
        and all(value and completed for _, value, completed in b_postlock),
        f"B did not recheck both completed finals after real acquire: {b_postlock}",
    )
    complete_pair = probe_final_glb.is_file() and probe_final_json.is_file()
    check_case(complete_pair, "no-callback successful A final pair is incomplete")
    if complete_pair:
        check_case(
            probe_final_glb.read_bytes() == probe_payloads["A"][0],
            "no-callback final GLB is not A's member",
        )
        check_case(
            probe_final_json.read_bytes() == probe_payloads["A"][1],
            "no-callback final JSON is not A's member",
        )
    expected_probe_entries = {
        probe_final_glb,
        probe_final_json,
        probe_staged["B"][0],
        probe_staged["B"][1],
    }
    actual_probe_entries = set(case_root.iterdir())
    check_case(
        actual_probe_entries == expected_probe_entries,
        "no-callback probe left missing/unexpected entries: "
        f"{sorted(path.name for path in actual_probe_entries)}",
    )
    return case_errors

errors.extend(
    f"no-callback: {message}"
    for message in no_callback_probe(root / "no-callback-real-guard")
)

@contextlib.contextmanager
def callback_only_lock_mutation(
    mutation_glb, mutation_json, *, on_attempt=None, on_acquired=None
):
    if on_attempt is None and on_acquired is None:
        yield
        return
    with production_promotion_guard(
        mutation_glb,
        mutation_json,
        on_attempt=on_attempt,
        on_acquired=on_acquired,
    ):
        yield

callback_only_errors = no_callback_probe(
    root / "mutation-callback-only-lock",
    callback_only_lock_mutation,
)
check(
    any("real low-level contention" in message for message in callback_only_errors)
    and any("reached replace" in message for message in callback_only_errors),
    "callback-only locking mutation was not killed by the no-callback leg: "
    f"{callback_only_errors}",
)
if errors:
    raise AssertionError("concurrent promotion regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = C ]; then
  printf 'glb-decimation review C: pass\n'
  exit 0
fi

# Review regression D: rollback decisions treat unreadable hashes as unknown,
# normalize both members after replace-after-effect failures, and clean both
# force backups even when either one-shot unlink reports an error.
if [ "$review_section" = all ] || [ "$review_section" = D ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-rollback-io" <<'PY'
import hashlib
import importlib.util
import multiprocessing
import os
import sys
import traceback
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
root.mkdir()
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("decimate_assets_rollback_io_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

errors = []
process_context = multiprocessing.get_context("fork")

def digest_bytes(value):
    return hashlib.sha256(value).hexdigest()


def digest(path):
    return digest_bytes(path.read_bytes())


def lexists(path):
    return os.path.lexists(path)


def make_pair(name, *, forced):
    directory = root / name
    directory.mkdir()
    pair = {
        "directory": directory,
        "staged_glb": directory / "staged.glb",
        "staged_json": directory / "staged.json",
        "final_glb": directory / "final.glb",
        "final_json": directory / "final.glb.json",
        "backup_glb": directory / ".captured-old-glb",
        "backup_json": directory / ".captured-old-json",
        "new_glb": f"new GLB bytes for {name}".encode(),
        "new_json": f"new JSON bytes for {name}".encode(),
        "old_glb": f"old GLB bytes for {name}".encode(),
        "old_json": f"old JSON bytes for {name}".encode(),
    }
    pair["staged_glb"].write_bytes(pair["new_glb"])
    pair["staged_json"].write_bytes(pair["new_json"])
    if forced:
        pair["final_glb"].write_bytes(pair["old_glb"])
        pair["final_json"].write_bytes(pair["old_json"])
    return pair


def exact_hash_recovery_errors(label, pair):
    findings = []
    expected = {
        pair["staged_glb"], pair["staged_json"],
        pair["final_glb"], pair["final_json"],
    }
    actual = set(pair["directory"].iterdir())
    if actual != expected:
        findings.append(
            f"{label}: early backup failure membership changed: "
            f"{sorted(path.name for path in actual)}"
        )
    for key, payload in (
        ("staged_glb", pair["new_glb"]),
        ("staged_json", pair["new_json"]),
        ("final_glb", pair["old_glb"]),
        ("final_json", pair["old_json"]),
    ):
        path = pair[key]
        if not path.is_file() or path.read_bytes() != payload:
            findings.append(f"{label}: {key} custody was not preserved exactly")
    if lexists(pair["backup_glb"]) or lexists(pair["backup_json"]):
        findings.append(f"{label}: backup exists despite pre-effect backup failure")
    return findings


def absent_terminal_errors(label, pair, allowed_memberships):
    findings = []
    if lexists(pair["final_glb"]) or lexists(pair["final_json"]):
        findings.append(f"{label}: absent-destination failure left a final member")
    actual = frozenset(pair["directory"].iterdir())
    if actual not in allowed_memberships:
        findings.append(
            f"{label}: unexpected failure residue: "
            f"{sorted(path.name for path in actual)}"
        )
    for key, payload in (
        ("staged_glb", pair["new_glb"]),
        ("staged_json", pair["new_json"]),
    ):
        path = pair[key]
        if path in actual and (not path.is_file() or path.read_bytes() != payload):
            findings.append(f"{label}: {key} residue has the wrong bytes")
    return findings


def force_cleanup_terminal_errors(label, pair):
    findings = []
    actual = set(pair["directory"].iterdir())
    final_pair = {pair["final_glb"], pair["final_json"]}
    backup_pair = {pair["backup_glb"], pair["backup_json"]}
    staged_pair = {pair["staged_glb"], pair["staged_json"]}
    if actual == final_pair and all(path.is_file() for path in final_pair):
        hashes = (digest(pair["final_glb"]), digest(pair["final_json"]))
        allowed_hashes = {
            (digest_bytes(pair["new_glb"]), digest_bytes(pair["new_json"])),
            (digest_bytes(pair["old_glb"]), digest_bytes(pair["old_json"])),
        }
        if hashes not in allowed_hashes:
            findings.append(f"{label}: complete finals have mixed or unknown custody")
        return findings

    finals_absent = not lexists(pair["final_glb"]) and not lexists(pair["final_json"])
    staged_leftovers = actual & staged_pair
    if finals_absent and actual == backup_pair | staged_leftovers:
        if not all(path.is_file() for path in backup_pair) or (
            digest(pair["backup_glb"]), digest(pair["backup_json"])
        ) != (
            digest_bytes(pair["old_glb"]), digest_bytes(pair["old_json"])
        ):
            findings.append(f"{label}: fail-closed backup pair is incomplete or wrong")
        for key, payload in (
            ("staged_glb", pair["new_glb"]),
            ("staged_json", pair["new_json"]),
        ):
            path = pair[key]
            if path in staged_leftovers and path.read_bytes() != payload:
                findings.append(f"{label}: staged recovery member has wrong bytes")
        return findings

    findings.append(
        f"{label}: terminal is neither residue-free finals nor a complete old "
        f"backup pair: {sorted(path.name for path in actual)}"
    )
    if lexists(pair["backup_glb"]) != lexists(pair["backup_json"]):
        findings.append(f"{label}: exactly one old backup remains")
    return findings


def prove_oracle_mutations():
    hash_pair = make_pair("oracle-hash", forced=True)
    assert not exact_hash_recovery_errors("oracle-hash", hash_pair)
    hash_pair["final_glb"].write_bytes(hash_pair["new_glb"])
    assert exact_hash_recovery_errors("oracle-hash-wrong-final", hash_pair)

    absent_pair = make_pair("oracle-absent", forced=False)
    absent_pair["staged_glb"].unlink()
    allowed = {frozenset({absent_pair["staged_json"]})}
    assert not absent_terminal_errors("oracle-absent", absent_pair, allowed)
    absent_pair["final_glb"].write_bytes(absent_pair["new_glb"])
    assert absent_terminal_errors("oracle-split-final", absent_pair, allowed)
    absent_pair["final_glb"].unlink()
    absent_pair["final_json"].write_bytes(absent_pair["new_json"])
    assert absent_terminal_errors("oracle-split-json-final", absent_pair, allowed)
    absent_pair["final_json"].unlink()
    (absent_pair["directory"] / "arbitrary-residue").write_bytes(b"residue")
    assert absent_terminal_errors("oracle-extra-residue", absent_pair, allowed)

    cleanup_pair = make_pair("oracle-cleanup", forced=True)
    os.replace(cleanup_pair["staged_glb"], cleanup_pair["final_glb"])
    os.replace(cleanup_pair["staged_json"], cleanup_pair["final_json"])
    assert not force_cleanup_terminal_errors("oracle-cleanup", cleanup_pair)
    cleanup_pair["backup_json"].write_bytes(cleanup_pair["old_json"])
    mutation_errors = force_cleanup_terminal_errors(
        "oracle-one-backup", cleanup_pair
    )
    assert mutation_errors and any("exactly one" in error for error in mutation_errors)


prove_oracle_mutations()

def exercise_hash_read_fault(name, target_key, persistent):
    pair = make_pair(name, forced=True)
    real_sha256 = module._sha256
    real_replace = os.replace
    primary_reached = False
    hash_faults = 0

    def unique_backup(path):
        candidate = Path(path)
        if candidate == pair["final_glb"]:
            return pair["backup_glb"]
        if candidate == pair["final_json"]:
            return pair["backup_json"]
        raise AssertionError(f"unexpected backup source {candidate}")

    def replacing(source, destination):
        nonlocal primary_reached
        source_path = Path(source)
        destination_path = Path(destination)
        if (
            not primary_reached
            and source_path == pair["final_glb"]
            and destination_path == pair["backup_glb"]
        ):
            primary_reached = True
            raise OSError("injected early backup failure before effect")
        return real_replace(source, destination)

    def faulting_sha256(path):
        nonlocal hash_faults
        candidate = Path(path)
        if (
            primary_reached
            and candidate == pair[target_key]
            and (persistent or hash_faults == 0)
        ):
            hash_faults += 1
            raise OSError("injected hash read failure")
        return real_sha256(candidate)

    caught = None
    with (
        mock.patch.object(module, "_unique_backup", new=unique_backup),
        mock.patch.object(module, "_sha256", new=faulting_sha256),
        mock.patch.object(module.os, "replace", new=replacing),
    ):
        try:
            module.promote_pair(
                pair["staged_glb"], pair["staged_json"],
                pair["final_glb"], pair["final_json"], True,
            )
        except BaseException as exc:
            caught = exc

    findings = []
    if caught is None:
        findings.append(f"{name}: early backup failure was swallowed")
    if not primary_reached:
        findings.append(f"{name}: early pre-effect backup fault was not reached")
    if hash_faults < 1:
        findings.append(f"{name}: hash-read fault was not reached")
    findings.extend(exact_hash_recovery_errors(name, pair))
    return findings


def exercise_unverified_candidate_hash(name):
    """Kill the opposite naive fix: treating a hash-read error as a match."""
    pair = make_pair(name, forced=True)
    real_sha256 = module._sha256
    real_replace = os.replace
    candidate_corrupted = False
    hash_faults = 0

    def unique_backup(path):
        candidate = Path(path)
        if candidate == pair["final_glb"]:
            return pair["backup_glb"]
        if candidate == pair["final_json"]:
            return pair["backup_json"]
        raise AssertionError(f"unexpected backup source {candidate}")

    def replacing(source, destination):
        nonlocal candidate_corrupted
        source_path = Path(source)
        destination_path = Path(destination)
        result = real_replace(source_path, destination_path)
        if (
            source_path == pair["staged_json"]
            and destination_path == pair["final_json"]
        ):
            pair["final_json"].write_bytes(b"corrupted unverified candidate")
            candidate_corrupted = True
        return result

    def faulting_sha256(path):
        nonlocal hash_faults
        candidate = Path(path)
        if candidate_corrupted and candidate == pair["final_json"] and hash_faults == 0:
            hash_faults += 1
            raise OSError("injected candidate hash read failure")
        return real_sha256(candidate)

    caught = None
    with (
        mock.patch.object(module, "_unique_backup", new=unique_backup),
        mock.patch.object(module, "_sha256", new=faulting_sha256),
        mock.patch.object(module.os, "replace", new=replacing),
    ):
        try:
            module.promote_pair(
                pair["staged_glb"], pair["staged_json"],
                pair["final_glb"], pair["final_json"], True,
            )
        except BaseException as exc:
            caught = exc

    findings = []
    if not candidate_corrupted:
        findings.append(f"{name}: corrupted-candidate after-effect was not reached")
    if hash_faults != 1:
        findings.append(f"{name}: candidate hash-read fault was not reached exactly once")
    findings.extend(force_cleanup_terminal_errors(name, pair))
    if caught is None:
        expected_new = (
            digest_bytes(pair["new_glb"]), digest_bytes(pair["new_json"])
        )
        if (
            not pair["final_glb"].is_file()
            or not pair["final_json"].is_file()
            or (digest(pair["final_glb"]), digest(pair["final_json"]))
            != expected_new
        ):
            findings.append(
                f"{name}: hash-read error was treated as successful candidate verification"
            )
    return findings


def exercise_bounded_reader_unknown_old(name):
    pair = make_pair(name, forced=True)
    expected_glb_sha = digest_bytes(pair["old_glb"])
    expected_json_sha = digest_bytes(pair["old_json"])
    old_glb_status = os.lstat(pair["final_glb"])
    old_glb_identity = (old_glb_status.st_dev, old_glb_status.st_ino)

    mismatch = pair["directory"] / "definite-mismatch"
    mismatch.write_bytes(pair["new_glb"])
    module._remove_non_old_final(
        mismatch,
        expected_glb_sha,
        module.MAX_DERIVATIVE_GLB_BYTES,
    )

    os.replace(pair["final_json"], pair["backup_json"])
    pair["final_json"].write_bytes(pair["new_json"])
    real_sha256 = module._sha256
    real_unlink = Path.unlink
    real_write_private_file = module._write_private_file
    bounded_faults = 0
    old_identity_unlinks = 0
    write_faults = 0
    old_missing_at_write = 0

    def bounded_reader_fault(path):
        nonlocal bounded_faults
        candidate = Path(path)
        if candidate == pair["final_glb"] and bounded_faults < 4:
            bounded_faults += 1
            raise module.DecimationError("injected bounded read failure")
        return real_sha256(candidate)

    def observe_unlink(path, *args, **kwargs):
        nonlocal old_identity_unlinks
        candidate = Path(path)
        if candidate == pair["final_glb"] and lexists(candidate):
            status = os.lstat(candidate)
            if (status.st_dev, status.st_ino) == old_glb_identity:
                old_identity_unlinks += 1
        return real_unlink(candidate, *args, **kwargs)

    def fail_old_glb_materialization(path, payload, mode=0o400):
        nonlocal write_faults, old_missing_at_write
        if payload == pair["old_glb"] and write_faults == 0:
            write_faults += 1
            if not lexists(pair["final_glb"]):
                old_missing_at_write += 1
            else:
                status = os.lstat(pair["final_glb"])
                if (status.st_dev, status.st_ino) != old_glb_identity:
                    old_missing_at_write += 1
            raise OSError("injected old-member materialization failure")
        return real_write_private_file(path, payload, mode)

    caught = None
    with (
        mock.patch.object(module, "_sha256", new=bounded_reader_fault),
        mock.patch.object(Path, "unlink", new=observe_unlink),
        mock.patch.object(
            module,
            "_write_private_file",
            new=fail_old_glb_materialization,
        ),
    ):
        try:
            module._restore_old_pair(
                pair["final_glb"],
                pair["final_json"],
                pair["backup_glb"],
                pair["backup_json"],
                expected_glb_sha,
                expected_json_sha,
                pair["old_glb"],
                pair["old_json"],
            )
        except BaseException as exc:
            caught = exc

    findings = []
    if lexists(mismatch):
        findings.append(f"{name}: definitive mismatch was retained")
    if bounded_faults != 4:
        findings.append(
            f"{name}: bounded-reader unknown was observed {bounded_faults} times"
        )
    if write_faults != 1:
        findings.append(
            f"{name}: old-member write fault was observed {write_faults} times"
        )
    if old_missing_at_write:
        findings.append(
            f"{name}: exact old GLB identity was absent before materialization"
        )
    if old_identity_unlinks:
        findings.append(
            f"{name}: exact old GLB identity was unlinked on unknown"
        )
    if caught is not None:
        findings.append(f"{name}: recovery raised {type(caught).__name__}")
    if pair["final_glb"].is_file():
        status = os.lstat(pair["final_glb"])
        if (status.st_dev, status.st_ino) != old_glb_identity:
            findings.append(f"{name}: exact old GLB identity changed")
    findings.extend(exact_hash_recovery_errors(name, pair))
    return findings


def exercise_lstat_unknown_old(name):
    pair = make_pair(name, forced=True)
    expected_glb_sha = digest_bytes(pair["old_glb"])
    expected_json_sha = digest_bytes(pair["old_json"])
    old_glb_status = os.lstat(pair["final_glb"])
    old_glb_identity = (old_glb_status.st_dev, old_glb_status.st_ino)

    os.replace(pair["final_json"], pair["backup_json"])
    pair["final_json"].write_bytes(pair["new_json"])
    real_lstat = os.lstat
    real_unlink = Path.unlink
    real_write_private_file = module._write_private_file
    lstat_faults = 0
    old_identity_unlinks = 0
    write_faults = 0
    old_missing_at_write = 0

    class FaultingOs:
        def __getattr__(self, attribute):
            return getattr(os, attribute)

        def lstat(self, path):
            nonlocal lstat_faults
            candidate = Path(path)
            if candidate == pair["final_glb"] and lstat_faults < 3:
                lstat_faults += 1
                raise OSError("injected bounded identity observation failure")
            return real_lstat(candidate)

    def observe_unlink(path, *args, **kwargs):
        nonlocal old_identity_unlinks
        candidate = Path(path)
        if candidate == pair["final_glb"] and lexists(candidate):
            status = real_lstat(candidate)
            if (status.st_dev, status.st_ino) == old_glb_identity:
                old_identity_unlinks += 1
        return real_unlink(candidate, *args, **kwargs)

    def fail_old_glb_materialization(path, payload, mode=0o400):
        nonlocal write_faults, old_missing_at_write
        if payload == pair["old_glb"] and write_faults == 0:
            write_faults += 1
            if not lexists(pair["final_glb"]):
                old_missing_at_write += 1
            else:
                status = real_lstat(pair["final_glb"])
                if (status.st_dev, status.st_ino) != old_glb_identity:
                    old_missing_at_write += 1
            raise OSError("injected old-member materialization failure")
        return real_write_private_file(path, payload, mode)

    caught = None
    with (
        mock.patch.object(module, "os", new=FaultingOs()),
        mock.patch.object(Path, "unlink", new=observe_unlink),
        mock.patch.object(
            module,
            "_write_private_file",
            new=fail_old_glb_materialization,
        ),
    ):
        try:
            module._restore_old_pair(
                pair["final_glb"],
                pair["final_json"],
                pair["backup_glb"],
                pair["backup_json"],
                expected_glb_sha,
                expected_json_sha,
                pair["old_glb"],
                pair["old_json"],
            )
        except BaseException as exc:
            caught = exc

    findings = []
    if lstat_faults != 3:
        findings.append(
            f"{name}: identity observation fault was reached {lstat_faults} times"
        )
    if write_faults > 1:
        findings.append(
            f"{name}: old-member write fault was reached {write_faults} times"
        )
    if old_missing_at_write:
        findings.append(
            f"{name}: exact old GLB identity was absent before materialization"
        )
    if old_identity_unlinks:
        findings.append(
            f"{name}: exact old GLB identity was unlinked on unknown"
        )
    if caught is not None:
        findings.append(f"{name}: recovery raised {type(caught).__name__}")
    if pair["final_glb"].is_file():
        status = real_lstat(pair["final_glb"])
        if (status.st_dev, status.st_ino) != old_glb_identity:
            findings.append(f"{name}: exact old GLB identity changed")
    findings.extend(exact_hash_recovery_errors(name, pair))
    return findings


def exercise_persistent_lstat_unknown_final(name, backup_present):
    pair = make_pair(name, forced=True)
    expected_glb_sha = digest_bytes(pair["old_glb"])
    expected_json_sha = digest_bytes(pair["old_json"])
    unknown_glb = b"unclassified public GLB bytes"
    pair["final_glb"].write_bytes(unknown_glb)
    unknown_status = os.lstat(pair["final_glb"])
    unknown_identity = (unknown_status.st_dev, unknown_status.st_ino)
    backup_identity = None
    if backup_present:
        pair["backup_glb"].write_bytes(pair["old_glb"])
        backup_status = os.lstat(pair["backup_glb"])
        backup_identity = (backup_status.st_dev, backup_status.st_ino)
    os.replace(pair["final_json"], pair["backup_json"])
    pair["final_json"].write_bytes(pair["new_json"])

    real_lstat = os.lstat
    real_replace = os.replace
    real_unlink = Path.unlink
    real_write_private_file = module._write_private_file
    lstat_faults = 0
    final_touches = []

    class FaultingOs:
        def __getattr__(self, attribute):
            return getattr(os, attribute)

        def lstat(self, path):
            nonlocal lstat_faults
            candidate = Path(path)
            if candidate == pair["final_glb"]:
                lstat_faults += 1
                if lstat_faults > 16:
                    raise AssertionError("unbounded identity observation retry")
                raise OSError("injected persistent identity observation failure")
            return real_lstat(candidate)

        def replace(self, source, destination):
            source_path = Path(source)
            destination_path = Path(destination)
            if (
                source_path == pair["final_glb"]
                or destination_path == pair["final_glb"]
            ):
                final_touches.append("replace")
            return real_replace(source_path, destination_path)

    def observe_unlink(path, *args, **kwargs):
        candidate = Path(path)
        if candidate == pair["final_glb"]:
            final_touches.append("unlink")
        return real_unlink(candidate, *args, **kwargs)

    def observe_private_write(path, payload, mode=0o400):
        candidate = Path(path)
        if candidate == pair["final_glb"]:
            final_touches.append("write")
        return real_write_private_file(candidate, payload, mode)

    caught = None
    with (
        mock.patch.object(module, "os", new=FaultingOs()),
        mock.patch.object(Path, "unlink", new=observe_unlink),
        mock.patch.object(
            module,
            "_write_private_file",
            new=observe_private_write,
        ),
    ):
        try:
            module._restore_old_pair(
                pair["final_glb"],
                pair["final_json"],
                pair["backup_glb"],
                pair["backup_json"],
                expected_glb_sha,
                expected_json_sha,
                pair["old_glb"],
                pair["old_json"],
            )
        except BaseException as exc:
            caught = exc

    findings = []
    if not isinstance(caught, module.DecimationError):
        findings.append(
            f"{name}: recovery raised "
            f"{type(caught).__name__ if caught is not None else 'nothing'}"
        )
    if not lstat_faults or lstat_faults > 16:
        findings.append(
            f"{name}: persistent identity fault count was {lstat_faults}"
        )
    if final_touches:
        findings.append(
            f"{name}: unknown public GLB was touched by recovery"
        )
    if not pair["final_glb"].is_file():
        findings.append(f"{name}: unknown public GLB is absent")
    else:
        final_status = real_lstat(pair["final_glb"])
        if (final_status.st_dev, final_status.st_ino) != unknown_identity:
            findings.append(f"{name}: unknown public GLB identity changed")
        if pair["final_glb"].read_bytes() != unknown_glb:
            findings.append(f"{name}: unknown public GLB bytes changed")
    if backup_present:
        if not pair["backup_glb"].is_file():
            findings.append(f"{name}: exact GLB backup is absent")
        else:
            backup_status = real_lstat(pair["backup_glb"])
            if (backup_status.st_dev, backup_status.st_ino) != backup_identity:
                findings.append(f"{name}: exact GLB backup identity changed")
            if pair["backup_glb"].read_bytes() != pair["old_glb"]:
                findings.append(f"{name}: exact GLB backup bytes changed")
    elif lexists(pair["backup_glb"]):
        findings.append(f"{name}: absent GLB backup was materialized")
    expected_members = {
        pair["staged_glb"],
        pair["staged_json"],
        pair["final_glb"],
        pair["final_json"],
    }
    if backup_present:
        expected_members.add(pair["backup_glb"])
    if set(pair["directory"].iterdir()) != expected_members:
        findings.append(f"{name}: terminal membership changed")
    if pair["final_json"].read_bytes() != pair["old_json"]:
        findings.append(f"{name}: old JSON was not restored")
    return findings


def exercise_absent_replace_fault(
    name, phase, after_effect, cleanup_member=None
):
    pair = make_pair(name, forced=False)
    real_replace = os.replace
    real_unlink = Path.unlink
    injected = False
    unlink_faults = 0

    phase_source, phase_destination = {
        "promote_glb": (pair["staged_glb"], pair["final_glb"]),
        "promote_json": (pair["staged_json"], pair["final_json"]),
    }[phase]

    def replacing(source, destination):
        nonlocal injected
        source_path = Path(source)
        destination_path = Path(destination)
        if (
            not injected
            and source_path == phase_source
            and destination_path == phase_destination
        ):
            injected = True
            if after_effect:
                real_replace(source_path, destination_path)
            raise OSError(
                f"injected {phase} {'after' if after_effect else 'before'}-effect failure"
            )
        return real_replace(source_path, destination_path)

    if cleanup_member not in {None, "glb", "json"}:
        raise AssertionError(f"unsupported cleanup member {cleanup_member!r}")
    cleanup_path = (
        pair[f"final_{cleanup_member}"] if cleanup_member is not None else None
    )

    def unlinking(path, *args, **kwargs):
        nonlocal unlink_faults
        candidate = Path(path)
        if (
            cleanup_path is not None
            and candidate == cleanup_path
            and unlink_faults == 0
        ):
            unlink_faults += 1
            raise OSError(
                f"injected one-shot final {cleanup_member.upper()} cleanup failure"
            )
        return real_unlink(path, *args, **kwargs)

    caught = None
    with (
        mock.patch.object(module.os, "replace", new=replacing),
        mock.patch.object(Path, "unlink", new=unlinking),
    ):
        try:
            module.promote_pair(
                pair["staged_glb"], pair["staged_json"],
                pair["final_glb"], pair["final_json"], False,
            )
        except BaseException as exc:
            caught = exc

    findings = []
    if caught is None:
        findings.append(f"{name}: injected replace failure was swallowed")
    if not injected:
        findings.append(f"{name}: targeted replace phase was not reached")
    if cleanup_member is not None and unlink_faults != 1:
        findings.append(
            f"{name}: one-shot {cleanup_member.upper()} cleanup unlink was not injected"
        )

    both_staged = frozenset({pair["staged_glb"], pair["staged_json"]})
    only_glb = frozenset({pair["staged_glb"]})
    only_json = frozenset({pair["staged_json"]})
    empty = frozenset()
    if phase == "promote_glb" and not after_effect:
        allowed = {both_staged}
    elif phase == "promote_glb":
        allowed = {only_json, both_staged}
    elif not after_effect:
        allowed = {only_json, both_staged}
    else:
        allowed = {empty, only_glb, only_json, both_staged}
    findings.extend(absent_terminal_errors(name, pair, allowed))
    return findings


def exercise_force_cleanup_fault(name, failed_member):
    pair = make_pair(name, forced=True)
    real_unlink = Path.unlink
    unlink_faults = 0

    def unique_backup(path):
        candidate = Path(path)
        if candidate == pair["final_glb"]:
            return pair["backup_glb"]
        if candidate == pair["final_json"]:
            return pair["backup_json"]
        raise AssertionError(f"unexpected backup source {candidate}")

    failed_path = pair[f"backup_{failed_member}"]

    def unlinking(path, *args, **kwargs):
        nonlocal unlink_faults
        candidate = Path(path)
        if candidate == failed_path and unlink_faults == 0:
            unlink_faults += 1
            raise OSError(f"injected one-shot {failed_member} backup cleanup failure")
        return real_unlink(path, *args, **kwargs)

    caught = None
    with (
        mock.patch.object(module, "_unique_backup", new=unique_backup),
        mock.patch.object(Path, "unlink", new=unlinking),
    ):
        try:
            module.promote_pair(
                pair["staged_glb"], pair["staged_json"],
                pair["final_glb"], pair["final_json"], True,
            )
        except BaseException as exc:
            caught = exc

    findings = []
    if unlink_faults != 1:
        findings.append(f"{name}: targeted one-shot backup unlink was not reached")
    findings.extend(force_cleanup_terminal_errors(name, pair))
    if caught is None:
        successful_entries = {pair["final_glb"], pair["final_json"]}
        successful_hashes = (
            digest_bytes(pair["new_glb"]), digest_bytes(pair["new_json"])
        )
        if (
            set(pair["directory"].iterdir()) != successful_entries
            or not all(path.is_file() for path in successful_entries)
            or (digest(pair["final_glb"]), digest(pair["final_json"]))
            != successful_hashes
        ):
            findings.append(
                f"{name}: promotion returned success without residue-free new finals"
            )
    return findings


def child_scenario(sender, kind, arguments):
    try:
        if kind == "hash":
            findings = exercise_hash_read_fault(*arguments)
        elif kind == "candidate_hash":
            findings = exercise_unverified_candidate_hash(*arguments)
        elif kind == "bounded_unknown":
            findings = exercise_bounded_reader_unknown_old(*arguments)
        elif kind == "lstat_unknown":
            findings = exercise_lstat_unknown_old(*arguments)
        elif kind == "persistent_lstat_unknown":
            findings = exercise_persistent_lstat_unknown_final(*arguments)
        elif kind == "absent":
            findings = exercise_absent_replace_fault(*arguments)
        elif kind == "cleanup":
            findings = exercise_force_cleanup_fault(*arguments)
        else:
            raise AssertionError(f"unknown scenario kind {kind}")
        sender.send(("result", findings))
    except BaseException:
        sender.send(("crash", traceback.format_exc()))
    finally:
        sender.close()


def run_bounded(kind, arguments):
    label = arguments[0]
    receiver, sender = process_context.Pipe(duplex=False)
    process = process_context.Process(
        target=child_scenario,
        args=(sender, kind, arguments),
        name=f"rollback-io-{label}",
        daemon=True,
    )
    process.start()
    sender.close()
    process.join(4)
    if process.is_alive():
        process.terminate()
        process.join(2)
        if process.is_alive():
            process.kill()
            process.join(2)
        errors.append(f"{label}: fault handling exceeded the four-second bound")

    payload = None
    if receiver.poll():
        try:
            payload = receiver.recv()
        except EOFError:
            payload = None
    receiver.close()
    if payload is None and not errors[-1:] == [
        f"{label}: fault handling exceeded the four-second bound"
    ]:
        errors.append(f"{label}: child exited {process.exitcode} without evidence")
    elif payload is not None:
        result_kind, value = payload
        if result_kind == "result":
            errors.extend(value)
        else:
            errors.append(f"{label}: child crashed:\n{value}")
    process.close()


run_bounded("hash", ("hash-transient-old-glb", "final_glb", False))
run_bounded("hash", ("hash-persistent-old-json", "final_json", True))
run_bounded("candidate_hash", ("hash-unknown-candidate-json",))
run_bounded("bounded_unknown", ("bounded-reader-unknown-old-glb",))
run_bounded("lstat_unknown", ("lstat-unknown-old-glb",))
run_bounded(
    "persistent_lstat_unknown",
    ("persistent-lstat-unknown-public-glb-absent-backup", False),
)
run_bounded(
    "persistent_lstat_unknown",
    ("persistent-lstat-unknown-public-glb-exact-backup", True),
)

run_bounded(
    "absent",
    ("absent-first-before-effect", "promote_glb", False),
)
run_bounded(
    "absent",
    ("absent-first-after-effect", "promote_glb", True),
)
run_bounded(
    "absent",
    ("absent-second-before-effect", "promote_json", False),
)
run_bounded(
    "absent",
    ("absent-second-after-effect", "promote_json", True),
)
run_bounded(
    "absent",
    ("absent-second-after-effect-glb-unlink", "promote_json", True, "glb"),
)
run_bounded(
    "absent",
    ("absent-second-after-effect-json-unlink", "promote_json", True, "json"),
)

run_bounded("cleanup", ("force-cleanup-glb-unlink", "glb"))
run_bounded("cleanup", ("force-cleanup-json-unlink", "json"))

if errors:
    raise AssertionError("rollback I/O regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = D ]; then
  printf 'glb-decimation review D: pass\n'
  exit 0
fi

# Happy path: both selected roots deliberately contain spaces and shell
# metacharacters. If production turns the argument vector into a shell string,
# the input path attempts to create $marker in the repository root.
input_dir="$tmp/input space;\$(touch $marker_name);#"
output_dir="$tmp/output space&[]{}"
manifest="$tmp/manifest happy.json"
mkdir -p "$input_dir" "$output_dir"
write_fixture "$input_dir/cat-source.glb" --triangles 30000
write_fixture "$input_dir/prop-source.glb" --triangles 20000
write_sidecar "$input_dir/cat-source.glb" meshy "round fixture cat" paid
write_sidecar "$input_dir/prop-source.glb" tripo "rounded fixture prop" paid
write_happy_manifest "$manifest"

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo/scripts" "$manifest" "$input_dir" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import inspect_glb

manifest = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
input_dir = Path(sys.argv[3])
assert [(entry["kind"], entry["out"]) for entry in manifest["assets"]] == [
    ("cat", "cat-source.glb"), ("prop", "prop-source.glb")
]
for entry, expected_triangles in zip(manifest["assets"], (30000, 20000)):
    source = input_dir / entry["out"]
    metrics = inspect_glb(source)
    assert metrics["triangles"] == expected_triangles
    assert metrics["uv_primitives"] == metrics["material_primitives"] == metrics["primitives"]
    assert metrics["materials"] == metrics["embedded_images"] == 1
    sidecar = json.loads(Path(f"{source}.json").read_text(encoding="utf-8"))
    assert sidecar["service"] == entry["service"]
    assert sidecar["prompt"] == entry["prompt"]
    assert sidecar["plan_tier"] == "paid"
    assert sidecar["sha256"] == hashlib.sha256(source.read_bytes()).hexdigest()
PY

happy_input_before=$(fingerprint_tree "$input_dir")
happy_log="$tmp/happy-fake.log"
happy_stdout="$tmp/happy.stdout"
happy_stderr="$tmp/happy.stderr"
if ! run_decimator success "$happy_log" "$happy_stdout" "$happy_stderr"; then
  sed -n '1,160p' "$happy_stdout" >&2
  sed -n '1,160p' "$happy_stderr" >&2
  die "two-entry happy path failed"
fi
assert_no_external_effects
test "$happy_input_before" = "$(fingerprint_tree "$input_dir")" || \
  die "happy path modified a source or source sidecar"

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo/scripts" "$happy_log" "$input_dir" "$output_dir" \
  "$fake_blender" "$expected_driver" "$happy_log.audit" <<'PY'
import json
import os
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import compare_preservation, inspect_glb

log_path, input_dir, output_dir, fake_path, driver_path, audit_path = map(Path, sys.argv[2:])
records = [json.loads(line) for line in log_path.read_text(encoding="utf-8").splitlines()]
assert len(records) == 2
assert [record["target"] for record in records] == [15000, 10000]
assert audit_path.read_text(encoding="utf-8").splitlines() == ["version", "asset", "asset"]
fixed_prefix = [
    "--background", "--factory-startup", "--offline-mode", "--disable-autoexec",
    "--threads", "1", "--python-exit-code", "97", "--python",
]
expected_sources = [input_dir / "cat-source.glb", input_dir / "prop-source.glb"]
expected_source_triangles = ["30000", "20000"]
expected_targets = ["15000", "10000"]
expected_minima = ["13500", "9000"]
expected_maxima = ["15000", "10000"]
staged_outputs = []
for index, record in enumerate(records):
    argv = record["argv"]
    assert Path(argv[0]).resolve() == fake_path.resolve()
    assert argv[1:1 + len(fixed_prefix)] == fixed_prefix
    driver_index = 1 + len(fixed_prefix)
    assert Path(argv[driver_index]).resolve() == driver_path.resolve()
    assert argv[driver_index + 1] == "--"
    post = argv[driver_index + 2:]
    assert post[0::2] == [
        "--source", "--output", "--source-triangles", "--target-triangles",
        "--minimum-triangles", "--maximum-triangles",
    ]
    values = dict(zip(post[0::2], post[1::2]))
    observed_source = Path(record["source"])
    assert Path(values["--source"]) == observed_source
    assert observed_source != expected_sources[index]
    assert record["source_sha256"] == inspect_glb(expected_sources[index])["sha256"]
    assert record["source_lstat_regular"] is True
    assert record["source_lstat_symlink"] is False
    assert record["source_nlink"] == 1
    assert record["source_mode"] & 0o222 == 0
    assert record["source_uid"] == os.getuid()
    assert record["source_parent_mode"] == 0o700
    assert record["source_parent_uid"] == os.getuid()
    assert not os.path.lexists(observed_source)
    assert not os.path.lexists(observed_source.parent)
    staged_output = Path(values["--output"])
    staged_outputs.append(staged_output)
    assert staged_output.resolve().is_relative_to(output_dir.resolve())
    assert staged_output.suffix == ".glb"
    assert values["--source-triangles"] == expected_source_triangles[index]
    assert values["--target-triangles"] == expected_targets[index]
    assert values["--minimum-triangles"] == expected_minima[index]
    assert values["--maximum-triangles"] == expected_maxima[index]
assert len(set(staged_outputs)) == len(staged_outputs) == 2

for filename, target, minimum in (
    ("cat-source.glb", 15000, 13500),
    ("prop-source.glb", 10000, 9000),
):
    source = input_dir / filename
    final = output_dir / filename
    proof_path = Path(f"{final}.json")
    assert final.is_file() and proof_path.is_file()
    source_metrics = inspect_glb(source)
    output_metrics = inspect_glb(final)
    assert output_metrics["triangles"] == target
    assert minimum <= output_metrics["triangles"] <= target
    assert 5000 <= output_metrics["triangles"] <= 20000
    assert compare_preservation(source_metrics, output_metrics) == []

forbidden = re.compile(
    r"api[_-]?key|token|secret|authorization|credential|bearer|https?://",
    re.IGNORECASE,
)
def scan(value):
    if isinstance(value, dict):
        for key, child in value.items():
            assert not forbidden.search(str(key)), key
            scan(child)
    elif isinstance(value, list):
        for child in value:
            scan(child)
    elif isinstance(value, str):
        assert not forbidden.search(value), value

for proof_path in output_dir.glob("*.glb.json"):
    scan(json.loads(proof_path.read_text(encoding="utf-8")))
for record in records:
    scan(record)

actual_entries = sorted(
    path.relative_to(output_dir).as_posix()
    for path in output_dir.rglob("*")
)
assert actual_entries == [
    "cat-source.glb", "cat-source.glb.json",
    "prop-source.glb", "prop-source.glb.json",
]
PY

assert_exact_provenance \
  "$input_dir/cat-source.glb" "$output_dir/cat-source.glb" \
  "$output_dir/cat-source.glb.json" cat 15000 13500 meshy "round fixture cat"
assert_exact_provenance \
  "$input_dir/prop-source.glb" "$output_dir/prop-source.glb" \
  "$output_dir/prop-source.glb.json" prop 10000 9000 tripo "rounded fixture prop"

# Each table row receives a fresh input/output tree. The shared runner proves
# the named diagnostic, source custody, final-pair behavior, fake reachability,
# curl abstinence, and absence of shell evaluation.
case_root=
input_dir=
output_dir=
manifest=
final_glb=
final_json=
case_external_referent=

prepare_valid_case() {
  local name=$1
  local triangles=${2:-30000}
  case_root="$tmp/cases/$name"
  input_dir="$case_root/input"
  output_dir="$case_root/output"
  manifest="$case_root/manifest.json"
  final_glb="$output_dir/asset.glb"
  final_json="$output_dir/asset.glb.json"
  case_external_referent=
  mkdir -p "$input_dir" "$output_dir"
  write_fixture "$input_dir/asset.glb" --triangles "$triangles"
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid
  write_single_manifest "$manifest" fixture-cat cat meshy asset.glb "fixture cat"
}

install_existing_pair() {
  write_fixture "$final_glb" --triangles 14000
  local derivative_sha
  derivative_sha=$(sha256_file "$final_glb")
  PYTHONDONTWRITEBYTECODE=1 python3 - "$final_json" "$derivative_sha" <<'PY'
import json
import sys
from pathlib import Path

record = {
    "schema_version": 1,
    "derivative": {"filename": "asset.glb", "sha256": sys.argv[2]},
    "sentinel": "old pair",
}
Path(sys.argv[1]).write_text(json.dumps(record, sort_keys=True) + "\n", encoding="utf-8")
PY
}

run_failure_case() {
  local name=$1
  local mode=$2
  local expected_pattern=$3
  local fake_reached=$4
  local existing=${5:-absent}
  local stdout="$case_root/run.stdout"
  local stderr="$case_root/run.stderr"
  local log="$case_root/fake.log"
  local before_input before_output='' before_lines after_lines
  local old_glb_sha='' old_json_sha=''
  local referent_snapshot='' referent_sha='' referent_magic=''

  before_input=$(fingerprint_tree "$input_dir")
  if [ "$existing" = preserve_tree ]; then
    before_output=$(fingerprint_tree "$output_dir")
  fi
  if [ -n "$case_external_referent" ]; then
    referent_snapshot="$case_root/external-referent.snapshot"
    cp -- "$case_external_referent" "$referent_snapshot"
    referent_sha=$(sha256_file "$case_external_referent")
    referent_magic=$(magic_hex "$case_external_referent")
    test "$referent_magic" = 676c5446 || \
      die "$name external referent was not independently valid GLB input"
  fi
  before_lines=$(line_count "$log")
  if [ "$existing" = preserve ]; then
    old_glb_sha=$(sha256_file "$final_glb")
    old_json_sha=$(sha256_file "$final_json")
  fi

  set +e
  run_decimator "$mode" "$log" "$stdout" "$stderr"
  local rc=$?
  set -e
  test "$rc" -ne 0 || die "$name unexpectedly succeeded"
  if ! rg -q "$expected_pattern" "$stderr"; then
    sed -n '1,120p' "$stderr" >&2
    die "$name lacked diagnostic $expected_pattern"
  fi
  test "$before_input" = "$(fingerprint_tree "$input_dir")" || \
    die "$name modified its source custody tree"
  if [ -n "$case_external_referent" ]; then
    test "$referent_sha" = "$(sha256_file "$case_external_referent")" || \
      die "$name changed its external referent hash"
    test "$referent_magic" = "$(magic_hex "$case_external_referent")" || \
      die "$name changed its external referent magic"
    cmp -s "$referent_snapshot" "$case_external_referent" || \
      die "$name changed its external referent bytes"
  fi

  after_lines=$(line_count "$log")
  if [ "$fake_reached" = yes ]; then
    test "$after_lines" -eq $((before_lines + 1)) || \
      die "$name did not reach fake Blender exactly once"
  else
    test "$after_lines" -eq "$before_lines" || \
      die "$name reached fake Blender's asset surface"
  fi

  if [ "$existing" = preserve ]; then
    test "$old_glb_sha" = "$(sha256_file "$final_glb")" || \
      die "$name changed the existing derivative"
    test "$old_json_sha" = "$(sha256_file "$final_json")" || \
      die "$name changed the existing provenance"
  elif [ "$existing" = preserve_tree ]; then
    test "$before_output" = "$(fingerprint_tree "$output_dir")" || \
      die "$name changed its pre-existing output tree or symlink"
  elif find "$output_dir" -mindepth 1 -print -quit | grep -q .; then
    find "$output_dir" -mindepth 1 -print >&2
    die "$name left a final or staged output"
  fi
  assert_no_external_effects
}

setup_malformed_json() {
  prepare_valid_case malformed-json
  printf '{"assets":[' >"$manifest"
}
setup_malformed_root() {
  prepare_valid_case malformed-root
  printf '[]\n' >"$manifest"
}
setup_malformed_assets() {
  prepare_valid_case malformed-assets
  printf '{"assets":{}}\n' >"$manifest"
}
setup_malformed_entry() {
  prepare_valid_case malformed-entry
  printf '{"assets":["not-an-object"]}\n' >"$manifest"
}
setup_duplicate_id() {
  prepare_valid_case duplicate-id
  PYTHONDONTWRITEBYTECODE=1 python3 - "$manifest" <<'PY'
import json
import sys
from pathlib import Path

entry = {"id": "same", "kind": "cat", "service": "meshy", "out": "asset.glb", "prompt": "fixture cat"}
Path(sys.argv[1]).write_text(json.dumps({"assets": [entry, {**entry, "out": "other.glb"}]}) + "\n", encoding="utf-8")
PY
}
setup_duplicate_out() {
  prepare_valid_case duplicate-out
  PYTHONDONTWRITEBYTECODE=1 python3 - "$manifest" <<'PY'
import json
import sys
from pathlib import Path

entry = {"id": "one", "kind": "cat", "service": "meshy", "out": "asset.glb", "prompt": "fixture cat"}
Path(sys.argv[1]).write_text(json.dumps({"assets": [entry, {**entry, "id": "two"}]}) + "\n", encoding="utf-8")
PY
}
setup_unsupported_kind() {
  prepare_valid_case unsupported-kind
  write_single_manifest "$manifest" fixture-station station meshy asset.glb "fixture cat"
}
setup_missing_source() {
  prepare_valid_case missing-source
  rm -f -- "$input_dir/asset.glb" "$input_dir/asset.glb.json"
}
setup_missing_sidecar() {
  prepare_valid_case missing-sidecar
  rm -f -- "$input_dir/asset.glb.json"
}
setup_bad_magic() {
  prepare_valid_case bad-magic
  printf 'NOTGLTF' >"$input_dir/asset.glb"
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid
}
setup_bad_sha() {
  prepare_valid_case bad-sha
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid \
    0000000000000000000000000000000000000000000000000000000000000000
}
setup_unpaid() {
  prepare_valid_case unpaid
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" unknown
}
setup_path_escape() {
  prepare_valid_case path-escape
  write_single_manifest "$manifest" fixture-cat cat meshy ../escape.glb "fixture cat"
}
setup_input_symlink_escape() {
  prepare_valid_case input-symlink-escape
  case_external_referent="$case_root/outside-input.glb"
  mv "$input_dir/asset.glb" "$case_external_referent"
  rm -f -- "$input_dir/asset.glb.json"
  ln -s "../outside-input.glb" "$input_dir/asset.glb"
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid
}
setup_output_symlink_escape() {
  prepare_valid_case output-symlink-escape
  case_external_referent="$case_root/outside-output.glb"
  write_fixture "$case_external_referent" --triangles 14000
  ln -s "../outside-output.glb" "$final_glb"
}
setup_wrong_version() {
  prepare_valid_case wrong-version
  CASE_BLENDER_VERSION=5.2.0
}
setup_wrong_build() {
  prepare_valid_case wrong-build
  CASE_BLENDER_BUILD_HASH=wrong-build
}
setup_small_source() {
  prepare_valid_case small-source 15000
}
setup_preexisting() {
  prepare_valid_case preexisting
  install_existing_pair
}
setup_fake_mode() {
  prepare_valid_case "$1"
}

setup_malformed_json
run_failure_case "malformed JSON" success 'invalid manifest' no
setup_malformed_root
run_failure_case "manifest root type" success 'invalid manifest' no
setup_malformed_assets
run_failure_case "manifest assets type" success 'invalid manifest' no
setup_malformed_entry
run_failure_case "manifest entry type" success 'invalid manifest' no
setup_duplicate_id
run_failure_case "duplicate manifest id" success 'invalid manifest' no
setup_duplicate_out
run_failure_case "duplicate manifest out" success 'invalid manifest' no
setup_unsupported_kind
run_failure_case "unsupported kind" success 'unsupported kind' no
setup_missing_source
run_failure_case "missing source" success 'missing source' no
setup_missing_sidecar
run_failure_case "missing source sidecar" success 'missing source sidecar' no
setup_bad_magic
run_failure_case "bad source magic" success 'invalid GLB header' no
setup_bad_sha
run_failure_case "bad source SHA" success 'source SHA-256 mismatch' no
setup_unpaid
run_failure_case "unpaid source" success 'plan_tier must be paid' no
setup_path_escape
run_failure_case "manifest path escape" success 'bare \.glb filename' no
test ! -e "$case_root/escape.glb" && test ! -e "$case_root/escape.glb.json" || \
  die "manifest path escape created an escaped output"
setup_input_symlink_escape
run_failure_case "input-leaf symlink escape" success 'path escapes' no
setup_output_symlink_escape
run_failure_case "output-leaf symlink escape" success 'path escapes' no preserve_tree
setup_wrong_version
run_failure_case "wrong Blender version" success 'requires Blender 5\.1\.2' no
unset CASE_BLENDER_VERSION
setup_wrong_build
run_failure_case "wrong Blender build" success 'ec6e62d40fa9' no
unset CASE_BLENDER_BUILD_HASH
setup_small_source
run_failure_case "source already within target" success 'already within budget' no
setup_preexisting
run_failure_case "pre-existing destination" success 'refusing existing derivative' no preserve

setup_fake_mode blender-failure
run_failure_case "Blender failure" fail 'Blender failed' yes
setup_fake_mode malformed-derivative
run_failure_case "malformed derivative" malformed_output 'invalid GLB header' yes
setup_fake_mode above-band
run_failure_case "above category band" over_budget 'triangle band' yes
setup_fake_mode below-band
run_failure_case "below category band" under_budget 'triangle band' yes
setup_fake_mode missing-uv
run_failure_case "missing UV" missing_uv 'lost UV' yes
setup_fake_mode missing-material
run_failure_case "missing material" missing_material 'material count changed' yes
setup_fake_mode missing-image
run_failure_case "missing embedded image" missing_image 'embedded-image count changed' yes
setup_fake_mode bounds-drift
run_failure_case "bounds drift" bounds_drift 'center drift' yes
setup_fake_mode external-image
run_failure_case "external image" external_image 'external URI' yes
setup_fake_mode unsupported-extension
run_failure_case "arbitrary extension" unsupported_extension 'unsupported extension' yes
setup_fake_mode active-scene
run_failure_case "active scene payload" unexpected_scene_content 'animation|camera|light' yes

# Force is pair-safe: default refusal and a rejected candidate retain both old
# hashes, while a fully accepted candidate replaces both and records its hash.
prepare_valid_case force-pair
install_existing_pair
force_old_glb_sha=$(sha256_file "$final_glb")
force_old_json_sha=$(sha256_file "$final_json")
force_input_before=$(fingerprint_tree "$input_dir")
force_log="$case_root/fake.log"

set +e
run_decimator success "$force_log" "$case_root/force-default.stdout" "$case_root/force-default.stderr"
force_default_rc=$?
set -e
test "$force_default_rc" -ne 0 || die "default run replaced an existing pair"
rg -q 'refusing existing derivative' "$case_root/force-default.stderr" || \
  die "default pair refusal lacked its diagnostic"
test "$force_old_glb_sha" = "$(sha256_file "$final_glb")" || \
  die "default refusal changed the old GLB"
test "$force_old_json_sha" = "$(sha256_file "$final_json")" || \
  die "default refusal changed the old JSON"
test "$(line_count "$force_log")" -eq 0 || \
  die "default refusal reached fake Blender's asset surface"

set +e
run_decimator over_budget "$force_log" "$case_root/force-bad.stdout" "$case_root/force-bad.stderr" --force
force_bad_rc=$?
set -e
test "$force_bad_rc" -ne 0 || die "--force promoted an over-budget candidate"
rg -q 'triangle band' "$case_root/force-bad.stderr" || \
  die "--force over-budget failure lacked triangle-band diagnostic"
test "$force_old_glb_sha" = "$(sha256_file "$final_glb")" || \
  die "failed --force changed the old GLB"
test "$force_old_json_sha" = "$(sha256_file "$final_json")" || \
  die "failed --force changed the old JSON"
test "$(line_count "$force_log")" -eq 1 || \
  die "failed --force did not reach fake Blender exactly once"
PYTHONDONTWRITEBYTECODE=1 python3 - "$output_dir" <<'PY'
import sys
from pathlib import Path

root = Path(sys.argv[1])
assert sorted(path.relative_to(root).as_posix() for path in root.rglob("*")) == [
    "asset.glb", "asset.glb.json"
]
PY

run_decimator success "$force_log" "$case_root/force-good.stdout" "$case_root/force-good.stderr" --force || {
  sed -n '1,120p' "$case_root/force-good.stderr" >&2
  die "valid --force replacement failed"
}
test "$(line_count "$force_log")" -eq 2 || \
  die "successful --force did not reach fake Blender exactly once"
test "$force_old_glb_sha" != "$(sha256_file "$final_glb")" || \
  die "successful --force did not replace the old GLB"
test "$force_old_json_sha" != "$(sha256_file "$final_json")" || \
  die "successful --force did not replace the old JSON"
test "$force_input_before" = "$(fingerprint_tree "$input_dir")" || \
  die "force path modified source custody"
PYTHONDONTWRITEBYTECODE=1 python3 - "$repo/scripts" "$final_glb" "$final_json" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import inspect_glb

glb = Path(sys.argv[2])
proof = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8"))
assert inspect_glb(glb)["triangles"] == 15000
assert proof["derivative"]["sha256"] == hashlib.sha256(glb.read_bytes()).hexdigest()
assert sorted(path.name for path in glb.parent.iterdir()) == [
    "asset.glb", "asset.glb.json"
]
PY
assert_exact_provenance \
  "$input_dir/asset.glb" "$final_glb" "$final_json" \
  cat 15000 13500 meshy "fixture cat"
assert_no_external_effects

# Static network boundary: the inspector, Blender driver, and orchestrator may
# not import a networking stack. Parse real syntax rather than grepping prose.
PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo/scripts/glb_metrics.py" "$expected_driver" "$decimate_script" <<'PY'
import ast
import sys
from pathlib import Path

for filename in sys.argv[1:]:
    source = Path(filename).read_bytes()
    tree = ast.parse(source, filename=filename)
    forbidden = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            forbidden.extend(alias.name for alias in node.names if alias.name.split(".")[0] in {"socket", "urllib", "http", "requests"})
        elif isinstance(node, ast.ImportFrom) and (node.module or "").split(".")[0] in {"socket", "urllib", "http", "requests"}:
            forbidden.append(node.module or "")
    assert not forbidden, f"{filename} imports network modules: {forbidden}"
PY

# Fault injection at the public promotion boundary. The injected exception is
# tied to the staged-JSON -> final-JSON replace, so both rollback legs prove
# the first promotion really occurred before the second one failed.
PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$decimate_script" "$tmp/helper-faults" "$repo" "$fake_blender" <<'PY'
import contextlib
import hashlib
import importlib.util
import io
import json
import os
import sys
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from glb_fixture import write_glb

spec = importlib.util.spec_from_file_location("decimate_assets_under_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def inject_second_promotion(directory, force):
    directory.mkdir()
    staged_glb = directory / "staged.glb"
    staged_json = directory / "staged.json"
    final_glb = directory / "final.glb"
    final_json = directory / "final.glb.json"
    staged_glb.write_bytes(b"new glb")
    staged_json.write_bytes(b"new json")
    old_hashes = None
    if force:
        final_glb.write_bytes(b"old glb")
        final_json.write_bytes(b"old json")
        old_hashes = (digest(final_glb), digest(final_json))

    real_replace = os.replace
    calls = []
    reached_first_promotion = False
    reached_second_promotion = False

    def failing_replace(source, destination):
        nonlocal reached_first_promotion, reached_second_promotion
        source_path = Path(source)
        destination_path = Path(destination)
        calls.append((source_path, destination_path))
        if source_path == staged_glb and destination_path == final_glb:
            reached_first_promotion = True
        if source_path == staged_json and destination_path == final_json:
            reached_second_promotion = True
            raise OSError("injected second promotion failure")
        return real_replace(source, destination)

    with mock.patch.object(module.os, "replace", side_effect=failing_replace):
        try:
            module.promote_pair(
                staged_glb, staged_json, final_glb, final_json, force
            )
        except OSError as exc:
            assert "injected second promotion failure" in str(exc)
        else:
            raise AssertionError("promote_pair swallowed the injected failure")

    assert reached_first_promotion and reached_second_promotion, calls
    if force:
        assert final_glb.is_file() and final_json.is_file()
        assert (digest(final_glb), digest(final_json)) == old_hashes
        assert set(directory.iterdir()) <= {final_glb, final_json, staged_json}
    else:
        assert not final_glb.exists() and not final_json.exists()
        assert set(directory.iterdir()) <= {staged_json}

inject_second_promotion(root / "new-destination", False)
inject_second_promotion(root / "forced-destination", True)

# Freeze main(argv: list[str]) as the import-safe orchestration interface. This
# fault reaches the real one-asset path after version check, fake execution,
# and candidate validation, then fails the staged provenance Path.open before
# promotion can begin.
orchestration = root / "provenance-orchestration"
input_dir = orchestration / "input"
output_dir = orchestration / "output"
input_dir.mkdir(parents=True)
output_dir.mkdir()
source = input_dir / "asset.glb"
source_sidecar = Path(f"{source}.json")
manifest = orchestration / "manifest.json"
final_glb = output_dir / "asset.glb"
final_json = output_dir / "asset.glb.json"
fake_log = orchestration / "fake.log"
fake_audit = orchestration / "fake.audit"
write_glb(source, triangles=30000)
source_sha = digest(source)
source_sidecar.write_text(json.dumps({
    "service": "meshy",
    "task_id": "fixture-meshy-task",
    "timestamp_utc": "2026-08-15T12:34:56Z",
    "plan_tier": "paid",
    "prompt": "fixture cat",
    "note": "local paid fixture",
    "sha256": source_sha,
}, sort_keys=True) + "\n", encoding="utf-8")
manifest.write_text(json.dumps({"assets": [{
    "id": "fixture-cat",
    "kind": "cat",
    "service": "meshy",
    "out": "asset.glb",
    "prompt": "fixture cat",
}]}, sort_keys=True) + "\n", encoding="utf-8")
source_before = source.read_bytes()
sidecar_before = source_sidecar.read_bytes()

real_open = Path.open
promote = mock.Mock()
opened_paths = []

def failing_open(path, *args, **kwargs):
    mode = args[0] if args else kwargs.get("mode", "r")
    candidate = Path(path)
    resolved = candidate.resolve(strict=False)
    if (
        any(flag in mode for flag in "wax")
        and candidate.suffix == ".json"
        and resolved.is_relative_to(output_dir.resolve())
        and candidate != final_json
    ):
        opened_paths.append(candidate)
        raise OSError("injected staged provenance failure")
    return real_open(path, *args, **kwargs)

sentinel_environment = {
    "FAKE_BLENDER_MODE": "success",
    "FAKE_BLENDER_LOG": str(fake_log),
    "FAKE_BLENDER_AUDIT": str(fake_audit),
    "PIPELINE_SENTINEL_KEY": "orchestration-sentinel-1",
    "PIPELINE_SENTINEL_TOKEN": "orchestration-sentinel-2",
    "PIPELINE_SENTINEL_SECRET": "orchestration-sentinel-3",
    "PIPELINE_SENTINEL_AUTH": "orchestration-sentinel-4",
    "PIPELINE_SENTINEL_CREDENTIAL": "orchestration-sentinel-5",
    "PIPELINE_SENTINEL_BEARER": "orchestration-sentinel-6",
    "PYTHONDONTWRITEBYTECODE": "1",
}
arguments = [
    "--manifest", str(manifest),
    "--input-dir", str(input_dir),
    "--output-dir", str(output_dir),
    "--blender", str(fake_blender),
]
stdout = io.StringIO()
stderr = io.StringIO()
assert callable(module.main), "decimate-assets.py must expose main(argv: list[str]) -> int"
with (
    mock.patch.dict(os.environ, sentinel_environment, clear=False),
    mock.patch.object(Path, "open", failing_open),
    mock.patch.object(module, "promote_pair", promote),
    contextlib.redirect_stdout(stdout),
    contextlib.redirect_stderr(stderr),
):
    try:
        main_result = module.main(arguments)
    except OSError as exc:
        assert "injected staged provenance failure" in str(exc)
    except SystemExit as exc:
        assert isinstance(exc.code, int) and exc.code != 0
    else:
        assert isinstance(main_result, int) and main_result != 0

assert len(opened_paths) == 1
assert opened_paths[0] != final_json
assert opened_paths[0].resolve(strict=False).is_relative_to(output_dir.resolve())
promote.assert_not_called()
assert not final_glb.exists() and not final_json.exists()
assert source.read_bytes() == source_before
assert source_sidecar.read_bytes() == sidecar_before
records = [json.loads(line) for line in fake_log.read_text(encoding="utf-8").splitlines()]
assert len(records) == 1 and records[0]["target"] == 15000
assert fake_audit.read_text(encoding="utf-8").splitlines() == ["version", "asset"]
combined_output = stdout.getvalue() + stderr.getvalue()
for value in sentinel_environment.values():
    if value.startswith("orchestration-sentinel-"):
        assert value not in combined_output
PY

assert_no_external_effects
test -z "$(find "$repo/tests/assets" "$repo/scripts" -type d -name __pycache__ -print -quit)" || \
  die "pipeline test left Python bytecode cache residue"

printf 'glb-decimation pipeline test: pass\n'
