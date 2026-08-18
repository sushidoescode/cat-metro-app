# Offline GLB decimation operations

This is the operator contract for converting the 15 gitignored generated
source GLBs into smaller local derivatives. The approved default derivative
destination is gitignored; an arbitrary explicit destination is not. The
governing decision is
[ADR-0012](../../adr/0012-blender-headless-glb-decimation.md), which remains
**Proposed** and requires human approval. This tooling decision is not a
generated-asset license approval.

The pipeline is local, offline, and derivative-only:

```text
docs/design/assets/CAT-MANIFEST.json
  + unity/Assets/Art/Generated/incoming/<manifest out>
  + unity/Assets/Art/Generated/incoming/<manifest out>.json
  -> unity/Assets/Art/Generated/incoming/decimated/<manifest out>
  +  unity/Assets/Art/Generated/incoming/decimated/<manifest out>.json
```

Sources and source sidecars are immutable inputs. The orchestrator preflights
and privately snapshots the complete queue, then processes assets in manifest
order. A catchable pre-commit failure is batch-terminal: unless an after-effect cleanup
report can be normalized to an exact committed run with no private-root
residue, the orchestrator rolls every completed publication back in reverse
manifest order and does not start another asset. An absent destination returns
to no public final pair, or to an exact privately retired pair if deletion is
unavailable; a forced destination returns to its exact old public pair. This
normalization is not one filesystem-atomic transaction. An operating-system
kill, power loss, filesystem fault, or uncatchable termination still requires
the recovery procedure below.

## Commands and paths

Run from the repository root. With Blender available as `blender` on `PATH`,
the exact default invocation is:

```bash
python3 scripts/decimate-assets.py
```

The defaults are resolved relative to the tracked orchestrator, not the
caller's working directory:

| Argument | Default |
|---|---|
| `--manifest` | `docs/design/assets/CAT-MANIFEST.json` |
| `--input-dir` | `unity/Assets/Art/Generated/incoming` |
| `--output-dir` | `unity/Assets/Art/Generated/incoming/decimated` |
| `--blender` | the executable returned by searching `PATH` for `blender` |

The approved/default `incoming/decimated` destination is covered by the
repository's `incoming/` ignore rule. Before any repo-local run, verify every
actual manifest GLB/JSON destination leaf. This standard-library check creates
no file and invokes Git with an argument vector:

```bash
output_dir=unity/Assets/Art/Generated/incoming/decimated
PYTHONDONTWRITEBYTECODE=1 python3 - \
  docs/design/assets/CAT-MANIFEST.json "$output_dir" <<'PY'
import json
import subprocess
import sys
from pathlib import Path

manifest = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
output_dir = Path(sys.argv[2])
for asset in manifest["assets"]:
    output = asset["out"]
    for leaf in (output, f"{output}.json"):
        selected_path = output_dir / leaf
        result = subprocess.run(
            ["git", "check-ignore", "-q", "--", str(selected_path)],
            check=False,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        if result.returncode != 0:
            print(
                "selected decimation output is not gitignored: "
                f"{selected_path.as_posix()!r}",
                file=sys.stderr,
            )
            raise SystemExit(1)
print("all manifest derivative GLB/JSON leaves are gitignored")
PY
```

`--output-dir` accepts any distinct writable directory; the orchestrator does
not require it to be beneath `incoming/`, inside a repository, or covered by a
Git ignore rule. It enforces the selected filesystem root, not Git custody.
Never select a tracked Unity directory, `docs/`, or another tracked path. For
an absolute repo-local destination in another checkout, run this same loop
against every manifest GLB and `<out>.json` leaf, using an argument vector of
`git -C <checkout> check-ignore -q -- <relative-output>/<leaf>`. Do not replace
the real leaf set with one marker filename.

Use explicit paths when the tracked scripts live in a clean worktree but the
large ignored source files live in the main checkout. This is the real-queue
command from the approved 2026-08-15 plan:

```bash
set +e
python3 scripts/decimate-assets.py \
  --manifest "$PWD/docs/design/assets/CAT-MANIFEST.json" \
  --input-dir /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming \
  --output-dir /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated \
  --blender /opt/homebrew/bin/blender \
  >/tmp/glb-decimation-real.txt 2>&1
real_rc=$?
set -e
sed -n '1,240p' /tmp/glb-decimation-real.txt
test "$real_rc" -eq 0
```

Do not add `--force` for a first run. It is only for deliberately replacing a
complete, previously accepted local derivative pair after inspecting the
existing pair and the recovery rules below. To repeat the explicit invocation
as a guarded replacement, append `--force` after the Blender path and before
the redirection.

## Fixed tool and triangle policy

The approved toolchain pin is:

- Blender **5.1.2**;
- Blender build **`ec6e62d40fa9`**; and
- the bundled official **`io_scene_gltf2` 5.1.20** importer/exporter from that
  exact Blender build.

The orchestrator explicitly checks the first version line and build hash with
`<blender> --background --version`; the Blender-side driver checks
`bpy.app.version` and `bpy.app.build_hash` again. `io_scene_gltf2` 5.1.20 is the
bundled, transitively pinned component verified for this exact build; the
current implementation does not independently query the add-on version. A
modified Blender bundle is unsupported even if its executable reports the
expected version and build.

| Manifest `kind` | Requested target | Accepted post-export band |
|---|---:|---:|
| `cat` | **15,000 triangles** | **13,500–15,000** |
| `prop` | **10,000 triangles** | **9,000–10,000** |

Every derivative must also be within the global **5,000–20,000 triangle**
range. Categories cannot borrow each other's policy. A source at or below its
category target fails with `source already within budget`; it requires a
curation decision and is not silently re-exported. Blender's collapse ratio is
`target triangles / measured seam-safe effective triangles`. The raw GLB count
remains the source-custody and provenance count, and the strict post-export GLB
inspection—not Blender's requested ratio—is the acceptance authority.

## Exact Blender process boundary

The version probe is an argument vector, never a shell command:

```text
<resolved-blender> --background --version
```

It has a 60-second timeout. Each asset then receives a fresh Blender process
with this exact ordered argument surface (paths and counts vary per asset):

```text
<resolved-blender>
  --background
  --factory-startup
  --offline-mode
  --disable-autoexec
  --threads 1
  --python-exit-code 97
  --python <repo>/scripts/blender_decimate.py
  --
  --source <input-dir>/<manifest-out>
  --output <output-dir>/.glb-decimation-<run>/asset-<asset>/<manifest-out>
  --source-triangles <measured-source-triangles>
  --target-triangles <policy-target>
  --minimum-triangles <policy-minimum>
  --maximum-triangles <policy-maximum>
```

Each asset process has a 1,800-second timeout, standard input disconnected,
and no shell. Inside that one process, the driver first imports the GLB with
`merge_vertices=False` and `import_shading="SMOOTH"`, requires one static mesh
and one material primitive, triangulates, and requires the audited count to
equal the outer inspector exactly. It clears those audit objects before the
second import, which uses the seam-safe `merge_vertices=True` and
`import_shading="SMOOTH"` settings. The measured seam-safe count must not
exceed the raw inspected/audited count and must remain strictly above the
category target. The driver applies Blender's `COLLAPSE` decimator using the
effective count, then exports an ordinary self-contained GLB. Exported
animations, skins, morphs, cameras, lights, Draco, gltfpack, and arbitrary
extensions are disabled.

## Validation gates

All manifest entries and source custody records are preflighted before Blender
runs. An expected validation failure exits nonzero and prints a concise
`glb-decimation: <diagnostic>` line on standard error.

Before the Blender version probe, the orchestrator performs a full-queue path
and filesystem-identity census over every source GLB, source sidecar, and
present final member. It rejects cross-entry path or identity reuse, then
bounded-reads each regular single-link source GLB and source sidecar pair into
a private mode-0700 snapshot. Blender and provenance construction use that
snapshot, not a later reopen of the mutable source path. The orchestrator
reverifies both the immutable originals and private snapshots immediately
before publication.

Manifest IDs are validated before even the Blender version probe. They must be
nonempty, have no leading/trailing whitespace, and be printable single-line
strings; ordinary printable Unicode, spaces, and mixed case remain valid. An
invalid ID receives the fixed, non-interpolating diagnostic
`glb-decimation: invalid manifest: id must be a printable single-line string`,
so an ID cannot forge another physical start or acceptance record.

Before Blender, the orchestrator rejects:

- a missing, non-file, malformed, empty, or structurally invalid manifest;
- duplicate asset IDs or output names, including names that collide after
  Unicode normalization and case folding;
- an output name that is not one bare, case-sensitive `.glb` filename;
- unsupported categories or generation services;
- missing inputs, sidecars, driver, or Blender executable;
- escaped, symlinked, hard-linked, aliased, or cross-entry paths that violate
  the selected input/output roots or reuse a filesystem identity;
- input and output directories that resolve to the same filesystem identity;
- an existing split destination where only the GLB or only its JSON exists;
- any complete existing destination pair without explicit `--force`;
- a malformed source GLB, a source whose inspected hash changes, or a source
  that is already at or below its category target;
- source geometry other than exactly one mesh, one primitive, one bound
  material, UVs on that primitive, and at least one wholly embedded image;
- any source external URI, glTF extension, animation, camera, light, skin, or
  morph target;
- a source sidecar missing any required string field (`service`, `task_id`,
  `timestamp_utc`, `plan_tier`, `prompt`, `note`, `sha256`), a non-lowercase
  64-character SHA-256, a timestamp that does not have the exact textual shape
  `YYYY-MM-DDTHH:MM:SSZ`, a tier other than `paid`, or service/prompt/hash
  facts that disagree with the manifest/source; the timestamp regex checks UTC
  second-precision shape only, not calendar-date validity; and
- a missing, non-executable, wrong-version, wrong-build, or timed-out Blender.

Inside Blender, the driver fails before decimation if the unmerged/smoothed
audit count differs at all from the inspected raw GLB count, if the seam-safe
effective count exceeds either exact source count, or if the effective count
is at or below the category target. There is no two-triangle exception or
percentage tolerance.

After Blender, the orchestrator rejects:

- Blender failure, timeout, or failure to create a non-empty staged GLB;
- a malformed or externally dependent GLB;
- a category-band or global-range miss;
- lost UV bindings, a material-count or embedded-image-count change, or any
  animation, camera, light, skin, or morph target;
- any `extensionsUsed` or `extensionsRequired` value—the derivative extension
  allowlist is exactly empty;
- bounds drift outside the formulas below;
- a source GLB or source-sidecar hash change during processing;
- a provenance record containing a secret-shaped key/value or an HTTP(S)
  URL-shaped string (including a signed download URL), or any failure to
  create, flush, fsync, re-read, and hash-check its staged JSON; the scan does
  not reject every non-HTTP URI scheme; and
- any promotion, rollback, or cleanup state that cannot be verified.

The GLB parser additionally rejects malformed headers/chunks, invalid JSON,
bad accessor/buffer relationships, invalid scene references or cycles,
non-finite geometry/transforms, and other unsupported glTF structure before
its metrics are trusted.

### Parser and aggregate work ceilings

Every ceiling in this subsection is inclusive: exact equality is accepted;
the first unit beyond it is rejected before the named expensive work begins.
An otherwise-invalid value still fails its semantic validation. In particular,
a non-integer token at its character ceiling must still decode to a finite
number.

- The shared strict decoder accepts JSON integer tokens through 4,300 digits
  excluding a leading minus, and non-integer tokens through 4,300 characters
  including sign, decimal point, and exponent syntax. NaN, Infinity, and
  duplicate object keys are rejected. Inspected GLB documents additionally
  have a 16 MiB GLB JSON chunk and 256 nesting levels; the depth limit describes
  GLB document/property inspection, not arbitrary manifest or sidecar nesting.
- Geometry inspection permits 8,000,000 combined POSITION-value and
  index-reference units across all meshes, including unselected meshes.
  Unindexed primitives charge their POSITION count twice. Repeated primitives
  and shared accessors are charged per primitive occurrence, before any
  POSITION decode.
- Sparse validation permits 8,000,000 sparse values from sparse.count across
  all accessors, including unreferenced accessors, before any sparse index is
  read.
- Image inspection permits 8 MiB per embedded image payload and 64 MiB
  aggregate embedded-image work. The aggregate charges bufferView byteLength,
  data-URI encoded characters, and every repeated image entry is charged again;
  rejection precedes image decoding or hashing.
- Selected-scene world bounds permit 8,000,000 world-bounds corner transforms,
  charged as eight per selected node/primitive instance, before any corner is
  transformed.
- World-position iteration permits 8,000,000 selected-instance POSITION
  values, precharged before an iterator is returned. Reusing one mesh through
  multiple selected nodes charges every instance.
- The silhouette renderer separately permits 8,000,000 combined selected
  index-reference and POSITION-value units across selected mesh instances
  before strict inspection. Its strict all-mesh geometry ceiling separately
  includes unselected meshes, closing the formerly deferred unselected-mesh
  path.

### Bounds formulas and tolerances

For source bounds `min_s`, `max_s` and output bounds `min_o`, `max_o`, each a
three-axis vector, define for axis `i`:

```text
c_s[i] = (min_s[i] + max_s[i]) / 2
c_o[i] = (min_o[i] + max_o[i]) / 2
e_s[i] = max_s[i] - min_s[i]
e_o[i] = max_o[i] - min_o[i]
L_s    = max(e_s)
L_o    = max(e_o)
```

Both longest extents must be positive and finite. Equality at a tolerance is
accepted; a value greater than the tolerance fails:

```text
center drift            = max_i(abs(c_o[i] - c_s[i])) / L_s       <= 0.005
longest-extent drift     = abs(L_o / L_s - 1)                      <= 0.01
normalized-extent drift = max_i(abs(e_o[i]/L_o - e_s[i]/L_s))     <= 0.02
```

Thus center drift is the largest per-axis displacement relative to the source
longest extent (0.5%), not Euclidean distance. Scale drift is limited to 1%,
and the largest normalized shape-extent change is limited to 2%. These numeric
checks do not replace the required human silhouette comparison.

## Derivative sidecar schema

The accepted sidecar is UTF-8 JSON with schema version 1, sorted keys, two-space
indentation, and a trailing newline. It is created exclusively in staging,
flushed and fsynced, then re-read before promotion. The derivative hash in the
sidecar must equal the staged GLB's SHA-256. `tool.timestamp_utc` is generated
from the current UTC time at provenance-record creation and formatted to whole
seconds; it is not copied from the source generation timestamp. This complete
example uses dummy, non-secret 64-character hashes:

```json
{
  "derivative": {
    "filename": "cat-conductor.glb",
    "sha256": "3333333333333333333333333333333333333333333333333333333333333333"
  },
  "geometry": {
    "accepted_maximum": 15000,
    "accepted_minimum": 13500,
    "category": "cat",
    "output": {
      "animations": 0,
      "cameras": 0,
      "embedded_images": 1,
      "extensions_required": [],
      "extensions_used": [],
      "images": 1,
      "lights": 0,
      "material_primitives": 1,
      "materials": 1,
      "morph_targets": 0,
      "primitives": 1,
      "skins": 0,
      "triangles": 15000,
      "uv_primitives": 1,
      "vertices": 9000,
      "world_bounds": {
        "max": [
          0.999,
          2.0,
          0.499
        ],
        "min": [
          -0.999,
          0.0,
          -0.499
        ]
      }
    },
    "source": {
      "animations": 0,
      "cameras": 0,
      "embedded_images": 1,
      "extensions_required": [],
      "extensions_used": [],
      "images": 1,
      "lights": 0,
      "material_primitives": 1,
      "materials": 1,
      "morph_targets": 0,
      "primitives": 1,
      "skins": 0,
      "triangles": 42000,
      "uv_primitives": 1,
      "vertices": 24000,
      "world_bounds": {
        "max": [
          1.0,
          2.0,
          0.5
        ],
        "min": [
          -1.0,
          0.0,
          -0.5
        ]
      }
    },
    "target_triangles": 15000
  },
  "schema_version": 1,
  "source": {
    "filename": "cat-conductor.glb",
    "provenance": {
      "note": "local paid candidate",
      "plan_tier": "paid",
      "prompt": "a friendly cat conductor",
      "service": "meshy",
      "task_id": "example-local-task",
      "timestamp_utc": "2026-08-15T12:34:56Z"
    },
    "sha256": "1111111111111111111111111111111111111111111111111111111111111111",
    "sidecar_sha256": "2222222222222222222222222222222222222222222222222222222222222222"
  },
  "tool": {
    "build_hash": "ec6e62d40fa9",
    "name": "Blender",
    "operation": "collapse-decimate",
    "timestamp_utc": "2026-08-15T13:00:00Z",
    "version": "5.1.2"
  }
}
```

The `source.provenance` object copies only the six non-hash generation fields;
the original source sidecar's byte hash is recorded separately. The metric
objects intentionally contain exactly the 16 fields shown above. Paths, byte
counts, mesh counts, external URIs, and SHA-256 values from the inspector are
not part of the geometry metric subset.

## Publication, overwrite, and concurrency semantics

Candidate files are created under a mode-0700
`.glb-decimation-<random>/asset-<random>/` directory inside the selected output
directory. The sidecar is created with exclusive-create mode and fsynced.
Publication uses `os.replace` on the same filesystem, so each individual GLB
or JSON rename is atomic. A two-file pair cannot be one filesystem-atomic
rename: managed writers are serialized and failures are normalized, but an
uncoordinated lock-free observer could see the interval between the two
renames. Only a verified terminal pair after the command returns successfully
is accepted.

Destination decisions and promotion are rechecked under a canonical
same-output in-process lock plus an advisory `flock` on the output directory.
There is no visible lock file. Candidate generation happens outside the lock,
so two concurrent first-time runs can both do Blender work; after acquiring
the lock, the default loser observes the winner's complete pair and refuses it.
Concurrent `--force` promotions are serialized and each one treats the pair it
finds under the lock as its rollback source. Programs that bypass the
orchestrator do not honor this advisory boundary.

### Default, absent destination

Both final members must be absent. The staged GLB is renamed first and the
staged JSON second. If either rename reports a failure—including a rename that
completed and then raised—the cleanup path independently attempts to remove
both final names, then makes one bounded retry. A transient one-shot unlink
fault on either member therefore cannot leave an accepted split pair. If a
candidate member remains at a public final name, the cleanup path moves the
complete candidate pair to unique private names of the form
`.<final-name>.retired-<random>`, verifies both payloads, and requires both
public final names to be absent. This privately retired candidate pair is a
rejected transaction, not an accepted derivative. If the pair cannot be
retired and verified exactly, the command fails closed and the whole directory
requires the recovery procedure below.

Retirement itself is a sequential two-member move. If the first retirement
rename succeeds but the second fails, the normalizer re-forms the exact
two-member private pair from bounded receipts before public cleanup, drawing
each member only from its verified staged, final, or retired holder. If that
later public/staged cleanup cannot be verified, the command fails closed for
the recovery inventory. Likewise, if one retired-member unlink succeeds and
its mate cannot be removed during rollback, the removed member is re-created
from its bounded receipt so the rejected transaction remains an exact private
pair rather than half-deleted evidence.

If both destination members already exist, the default path refuses them
before Blender. If exactly one exists, the lineage is inconsistent and is also
refused. It never guesses which member should win.

### Deliberate `--force` replacement

`--force` still requires a complete old GLB/JSON pair. The new GLB and sidecar
must pass every validation before promotion begins. While holding the lock, the
orchestrator:

1. hashes both old finals and both staged candidates;
2. renames the old pair to unique hidden names of the form
   `.<final-name>.backup-<random>`;
3. renames the staged GLB and JSON into their final names;
4. verifies both candidate hashes at the finals and both old hashes at the
   backups; and
5. retains the backups and exact old-byte receipts for batch-terminal commit.

The CLI applies a 128 MiB aggregate rollback-retention budget across all old
GLB/JSON pairs replaced by one managed `--force` run. The hard cap is checked
before either old member is read or either public name is renamed; both current
sizes must fit the remaining budget. Equality is accepted; a pair that would
exceed the remaining budget is neither read nor renamed, earlier completed
publications are rolled back, and later assets are not started. An absent
destination in a mixed force run consumes zero retention bytes.

Exact old bytes remain retained until every asset and private-root cleanup has
succeeded. Only then does the batch commit by verifying and removing each old
backup pair. Recovery bytes remain available until every backup cleanup has
succeeded, so a later backup-cleanup failure restores the whole batch,
including an earlier pair whose backup names were already removed.

A hash-read error before the first backup leaves the old finals in place. In
rollback, an unreadable identity is treated as unknown, never as proof that a
file is expendable. A backup, publication, or cleanup error attempts both
old-member restores independently. Bounded pre-transaction copies are the
recovery authority if a rename consumed or aliased a backup. A reported
failure is normalized only after the implementation proves both exact old
members at their public final names with no backup residue; otherwise it raises
`forced promotion could not recover the old pair`. The original source GLB and
source sidecar are never overwrite targets.

A successful `--force` return means both complete new finals exist and no old
backup remains. One reported backup-unlink fault is retried for each member. If
backup deletion stays persistently unavailable after a verified new pair was
installed, the failure path restores the exact old public pair, removes both
backups, and returns nonzero only after verifying that old-pair terminal with
no backup residue. Do not treat any nonzero exit as acceptance of the new
derivative.

Temporary staging is removed when the Python context unwinds normally,
including expected validation exceptions. A cleanup report that arrives after
publication is normalized to success only when every private root is absent
and every intended final pair is the exact committed candidate. Otherwise the
run reverses completed publications in reverse manifest order: an absent
destination returns to no public pair (or an exact privately retired candidate
pair when a member cannot be removed), while a forced destination restores its
exact old public pair with no backup residue. Rollback attempts every completed
pair independently even if an earlier rollback attempt fails, then reports the
aggregate failure. An operating-system kill, power loss, filesystem fault, or
uncatchable process termination can interrupt that normalization. There is no
automatic startup recovery scan.

### Recovery after a nonzero exit or interrupted process

1. Stop all decimation processes using the output directory. Do not rerun with
   `--force`, and do not modify the source GLB or its source sidecar.
2. Preserve and inventory the exact directory before changing it. Include
   normal finals, `.glb-decimation-*` staging directories, and
   `.*.backup-*` and `.*.retired-*` files; record SHA-256 values for every
   readable file.
3. Classify a complete pair by content, not filename alone. A JSON member must
   parse, name its GLB in `derivative.filename`, and its
   `derivative.sha256` must equal that GLB's hash. Preserve any unreadable or
   unverified member; an I/O error is not evidence that it is stale.
4. Exit 0 plus a complete final pair and no private transaction residue can be
   validated normally. After a catchable nonzero first publication, the
   expected terminal has no public finals; an exact retired candidate pair may
   be preserved for inspection and then removed only as a set. After a
   catchable nonzero forced replacement, the expected terminal is the exact old
   public pair with no backup residue. A no-final state with a complete,
   matching old backup pair can still result from an uncatchable interruption
   and is recoverable only by restoring both old members together during an
   explicit maintenance action. Never delete only one member of a candidate or
   recovery pair.
5. Quarantine and escalate any split final, split backup, hash disagreement,
   unknown identity, or mixture that cannot be classified. Retain the whole
   directory as evidence rather than attempting another force run.
6. Remove stale `.glb-decimation-*` staging or `.*.retired-*` candidates only
   after no process is running, the final/backup/candidate lineage is
   classified, and any needed bytes are preserved. Remove each exact pair as a
   set. Rerun only from an exact state of either no finals or one verified
   complete final pair, with no unexplained private residue.

Useful read-only inventory commands are:

```bash
output=/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated
find "$output" -mindepth 1 -maxdepth 3 -print | LC_ALL=C sort
find "$output" -type f -exec shasum -a 256 {} \;
```

## Logs, exit status, and offline posture

Start/progress records are attempted immediately before each Blender call and
are flushed as work begins, so they can remain in the transcript when a later
asset fails. No success record is attempted before whole-batch terminal commit:
every intended publication is exact, all private roots are absent, and all
old-backup commits have succeeded. A failure before terminal commit emits no
success record. Validated IDs cannot add a line or control character. Counts
are decimal integers without grouping:

```text
glb-decimation: asset=cat-red-tabby-sitting category=cat target=15000 source_triangles=1428306
glb-decimation: asset=cat-red-tabby-sitting output_triangles=15000 output_vertices=9460
```

For this reason, success-record writes are best effort after exact commit.
A stream failure before the write is suppressed only when that asset's final
pair still observes exact; this can leave exit 0 and exact final pairs with no
success line. A failure reported after a write can leave that line present.
Because records are emitted sequentially, a later record-write failure followed
by a failed exact-publication observation can return nonzero after earlier
success lines were already written. Inspect the final and recovery state:
neither nonzero nor record cardinality alone proves rollback or acceptance.

Every decimator public record and diagnostic is one line of at most 512 bytes.
HTTP(S) substrings become [redacted-uri]; remaining credential-shaped content
collapses to fixed text, nonprintable characters are escaped, and overlong
content is truncated without splitting UTF-8. This same boundary covers both
standard-output progress/success records and standard-error diagnostics.

The two inspection CLIs have matching safe public surfaces. Metrics failures
write one diagnostic-only line of at most 512 bytes and no success JSON. On
success, the one-line success JSON recursively redacts unsafe string values,
including risky path and extension values, and redacts unsafe external URIs
while preserving benign relative leaves. Each public string is limited. The
complete metrics success JSON is not subject to a 512-byte total limit.
Silhouette failures write one diagnostic-only line of at most 512 bytes. Its
silhouette success record is one printable line of at most 512 bytes and
redacts unsafe or overlong source and output paths; if the combined record is
still too long, both paths are redacted.

Each child's standard output and standard error are captured separately and
are not replayed. The version and asset calls each enforce an independent
ceiling of 1 MiB per stream; the first byte beyond either ceiling terminates
the owned child process group and fails the run. Child text, including the driver's
internal triangle audit, is never a public acceptance record or diagnostic.
The sample above therefore shows the complete normal public record formats,
not output interleaved with Blender chatter.

There is no batch success summary. Exit 0 together with the exact final pairs,
matching hashes, and accepted metrics is authoritative. Expected pipeline
failures exit 1 and emit a prefixed diagnostic on standard error, but buffering
means that diagnostic need not be the last line in a combined capture. Example
diagnostic format:

```text
glb-decimation: derivative triangle band miss for cat: 15001
```

Command-line parsing errors are argparse usage failures and exit 2. Never infer
success from the presence of one GLB or one log line; require exit 0 and the
post-run checks below.

No API key, account session, `.env`, credential file, vendor call, download, or
network access is needed or allowed. The tracked Python pipeline imports no
network stack. Blender receives `--offline-mode`, `--background`, factory
settings, and disabled auto-execution. Both the version and asset subprocesses
receive an explicit name allowlist: `PATH` and the supported locale variables,
plus private mode-0700 `HOME`, temporary, and XDG roots created for that run.
The parent environment is not copied and blacklist-filtered. Arguments are
passed as a vector with `shell=False`, standard input is `/dev/null`, and both
captured child streams are discarded after bounded processing.

Generated files and source metadata remain untrusted data. Secret-shaped keys
or values, bearer-shaped data, and HTTP(S) URL-shaped strings (including signed
download URLs) are rejected from the derivative provenance record; other URI
schemes are not rejected by that secret scan. Logs should contain only
validated asset IDs, categories, triangle/vertex counts, and bounded public
diagnostics. Raw Blender output is never replayed. If a log unexpectedly
contains sensitive or remote-service material, stop, preserve it outside the
repository, and treat the run as a custody incident.

## Real-run acceptance checklist

Complete every item; code-green and exit 0 are necessary but not sufficient.

### Before and after the queue

- [ ] Confirm the manifest still contains exactly 15 entries: 10 `cat` and 5
  `prop`, with one local source GLB and paid-tier source sidecar per entry.
- [ ] Record source and source-sidecar SHA-256 values before the run. Confirm
  the input and output directories are different filesystem identities and
  that every destination is either absent or a deliberately reviewed complete
  pair.
- [ ] For a repo-local output, require `git check-ignore` to accept a
  hypothetical leaf beneath the selected directory. Confirm the path is not a
  tracked Unity or documentation destination; the orchestrator does not make
  this Git-custody decision for the operator.
- [ ] Confirm the stock Blender toolchain is 5.1.2, build
  `ec6e62d40fa9`, with bundled `io_scene_gltf2` 5.1.20. Do not substitute a
  newer release, modified bundle, alternate exporter, or compression add-on.
- [ ] Run the explicit command above in an offline, no-key session. Confirm no
  GUI opens, save the complete output, and require `real_rc == 0`.
- [ ] Account for one start/progress record for every Blender asset call that
  began. Under a writable capture, expect one post-commit success record per
  manifest entry, but treat those records as best-effort reporting: a missing
  line after exit 0 does not invalidate exact finals. A failure before terminal
  commit must contain no success record; after reporting begins, a nonzero
  result can retain earlier success lines. Classify the final and recovery
  state rather than record cardinality. Require no raw Blender output in the
  public transcript, and never use a start record by itself as success authority.
- [ ] Confirm the output root contains exactly 15 GLBs and their 15 JSON
  sidecars, with no `.glb-decimation-*`, `.*.backup-*`, `.*.retired-*`, split
  pair, symlink, external file, or unexplained residue.
- [ ] Recompute every source, source-sidecar, derivative, and sidecar hash.
  Require source custody hashes to be unchanged and each sidecar's derivative
  hash to match its GLB.
- [ ] Reinspect all 15 derivatives with `scripts/glb_metrics.py`. Require cats
  at 13,500–15,000 triangles, props at 9,000–10,000, every output at
  5,000–20,000, the exact empty extension allowlist, complete UV/material/image
  preservation, and all three bounds tolerances.

This read-only local assertion covers the central post-run facts without
creating bytecode or contacting a service:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 - \
  scripts \
  docs/design/assets/CAT-MANIFEST.json \
  /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming \
  /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated <<'PY'
import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, sys.argv[1])
from glb_metrics import compare_preservation, inspect_glb

manifest = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
input_dir = Path(sys.argv[3])
output_dir = Path(sys.argv[4])
assets = manifest["assets"]
assert len(assets) == 15
assert sum(a["kind"] == "cat" for a in assets) == 10
assert sum(a["kind"] == "prop" for a in assets) == 5
expected_names = sorted(
    name
    for asset in assets
    for name in (asset["out"], f'{asset["out"]}.json')
)
assert sorted(path.name for path in output_dir.iterdir()) == expected_names

for asset in assets:
    source = input_dir / asset["out"]
    source_json = Path(f"{source}.json")
    derivative = output_dir / asset["out"]
    derivative_json = Path(f"{derivative}.json")
    source_record = json.loads(source_json.read_text(encoding="utf-8"))
    record = json.loads(derivative_json.read_text(encoding="utf-8"))
    source_sha = hashlib.sha256(source.read_bytes()).hexdigest()
    source_json_sha = hashlib.sha256(source_json.read_bytes()).hexdigest()
    derivative_sha = hashlib.sha256(derivative.read_bytes()).hexdigest()
    assert source_record["sha256"] == source_sha
    assert record["source"]["sha256"] == source_sha
    assert record["source"]["sidecar_sha256"] == source_json_sha
    assert record["derivative"] == {
        "filename": derivative.name,
        "sha256": derivative_sha,
    }
    source_metrics = inspect_glb(source)
    output_metrics = inspect_glb(derivative)
    low, high = (13500, 15000) if asset["kind"] == "cat" else (9000, 10000)
    assert low <= output_metrics["triangles"] <= high
    assert 5000 <= output_metrics["triangles"] <= 20000
    assert compare_preservation(source_metrics, output_metrics) == []
print("15/15 derivative pairs pass local structural checks")
PY
```

### Same-yaw visual review

Render every source and derivative from the exact tracked renderer at HEAD,
using the same default 520-pixel canvas and yaw 25. Each invocation must pass
the renderer's 1% minimum coverage gate. The planned `.catshots/` root is
inside the main repository checkout. It is untracked but **not gitignored**;
the same is true of any contact sheets created there. These depictions require
active operator custody rather than relying on an ignore rule.

```bash
render_root="/Users/sushantsrikrish/cat-metro-app/.catshots/glb-decimation-2026-08-15"
mkdir -p "$render_root/before" "$render_root/after"
while IFS=$'\t' read -r id out; do
  python3 scripts/glb-silhouette.py \
    "/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/$out" \
    "$render_root/before/$id.png" 25
  python3 scripts/glb-silhouette.py \
    "/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated/$out" \
    "$render_root/after/$id.png" 25
done < <(jq -r '.assets[] | [.id, .out] | @tsv' docs/design/assets/CAT-MANIFEST.json)

PYTHONDONTWRITEBYTECODE=1 python3 - \
  docs/design/assets/CAT-MANIFEST.json \
  /Users/sushantsrikrish/cat-metro-app <<'PY'
import json
import os
import subprocess
import sys
from pathlib import Path

manifest = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
checkout = Path(sys.argv[2])
tracked = subprocess.run(
    ["git", "-C", str(checkout), "ls-files", "-z", "--", ".catshots"],
    check=True,
    stdin=subprocess.DEVNULL,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
)
if tracked.stdout:
    print(".catshots contains a tracked or staged path", file=sys.stderr)
    raise SystemExit(1)

status = subprocess.run(
    [
        "git", "-C", str(checkout), "status", "--porcelain=v1", "-z",
        "--untracked-files=all", "--", ".catshots",
    ],
    check=True,
    stdin=subprocess.DEVNULL,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
)
states = {}
for entry in status.stdout.split(b"\0"):
    if not entry:
        continue
    if len(entry) < 4 or entry[2:3] != b" ":
        print("unexpected Git porcelain record for .catshots", file=sys.stderr)
        raise SystemExit(1)
    states[os.fsdecode(entry[3:])] = entry[:2].decode("ascii")

expected = {
    f".catshots/glb-decimation-2026-08-15/{phase}/{asset['id']}.png"
    for asset in manifest["assets"]
    for phase in ("before", "after")
}
if len(expected) != 30:
    print("manifest does not identify 15 unique render pairs", file=sys.stderr)
    raise SystemExit(1)
wrong = sorted(path for path in expected if states.get(path) != "??")
if wrong:
    for path in wrong:
        print(f"expected untracked render is missing or not ??: {path}", file=sys.stderr)
    raise SystemExit(1)
print("30/30 individual renders are present and untracked")
PY
```

Actually view all 30 individual PNGs at the same size. A contact sheet is only
a navigation aid; it never replaces individual inspection. The custody check
requires an empty `git ls-files -- .catshots` result and requires all 30
individual before/after paths to have `??` porcelain status. It intentionally
does not require contact sheets, which may not have been built yet. Never use
`git add -A` in a checkout containing these files. Record a separate PASS/FAIL
for every row:

| Asset | Required visual comparison |
|---|---|
| `cat-red-tabby` | body, face, ears, paws, tail, existing plinth |
| `cat-blue-siamese` | body, face, ears, paws, tail, existing plinth |
| `cat-yellow-longhair` | fur silhouette, face, ears, paws, tail, existing plinth |
| `cat-green-shorthair` | body, face, ears, paws, tail, existing plinth |
| `cat-wild-alley` | body, face, ears, paws, tail, existing plinth |
| `cat-red-tabby-sitting` | sitting pose, ears, paws, tail, existing plinth |
| `cat-blue-siamese-loaf` | loaf pose, ears, paws/tucked legs, tail, existing plinth |
| `cat-yellow-longhair-wave` | raised paw, fur silhouette, ears, tail, existing plinth |
| `cat-green-shorthair-sit` | sitting pose, ears, paws, tail, existing plinth |
| `cat-conductor` | ears, paws, tail, conductor hat and baton, existing plinth |
| `prop-depot-shed` | roof, walls, openings, trim, existing base/plinth |
| `prop-toy-engine` | locomotive silhouette, wheels, chimney and cab |
| `prop-station-kiosk` | kiosk silhouette, roof and sign |
| `prop-trees` | trunks, branches, canopy separation and existing base |
| `prop-desk-clutter` | desk silhouette and recognizable small clutter |

Open any ambiguous source/derivative pair individually at original detail.
Record the pre-existing plinth inconsistency as deferred to its separate
contract; do not mislabel it as a decimation regression. A broken ear, tail,
paw, accessory, branch, sign, roof, wheel, or other intentional silhouette is
a visual failure even when all numeric checks pass.

## Shipping and license boundary — human approval required

Derivative GLBs and sidecars are gitignored only when they stay under the
approved/default `incoming/decimated/` tree. An arbitrary explicit output may
not be ignored. Silhouette PNGs and contact sheets under repo-local
`.catshots/` are untracked but not gitignored. **None of these artifacts may be
copied, staged, committed, shipped, or otherwise promoted into tracked Unity
assets, and generated-art depictions must not enter tracked documentation,
until all three gates exist and pass:**

1. a separate generated-asset license ADR covering the Meshy/Tripo source
   rights and provenance;
2. the separate asset curation contract, including per-asset technical and
   visual acceptance; and
3. explicit human approval of that promotion.

ADR-0012 does not provide those approvals. Decimation creates a derivative; it
does not change, replace, cleanse, or enlarge the generation service, account
tier, prompt, task ID, timestamp, source hash, license record, or human custody
obligation. Keep every source and provenance sidecar intact so the later human
decision can evaluate the original lineage.
