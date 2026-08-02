#!/usr/bin/env bash
# Mechanical risk classifier (Ona pattern): objective criteria, no LLM judgment, zero token cost.
# Decides whether a diff needs agent review in sprint mode (and is a useful signal in any mode).
# Tunable policy lives in evals/mode-policy.json; enforcement floors are fixed below.
#
# Usage: scripts/forge-risk.sh [<base-ref>]              (default base: merge-base with main/master)
#        scripts/forge-risk.sh --vector [<base-ref>]     (deterministic JSON feature vector)
#        scripts/forge-risk.sh <base-ref> --vector       (equivalent flag order)
# Fails CLOSED (RISKY, exit 2) when it cannot classify safely — empty diff, no base, dirty tree.
# On an integrated sprint branch pass the session-start commit/tag explicitly:
#   scripts/forge-risk.sh <session-start-sha>
# Output: LOW-RISK or RISKY with reasons. Exit 0 = low-risk, 1 = risky, 2 = cannot classify (=risky).
# --vector trust boundary: use the protected script in an isolated checkout with a sanitized
# environment and trusted Git config; worktree checks can invoke configured clean filters.
set -uo pipefail

vector=0
arg_error=0
if [ "${1:-}" = "--vector" ]; then
  vector=1
  shift
elif [ "${2:-}" = "--vector" ]; then
  vector=1
  vector_base_arg="$1"
  shift 2
  set -- "$vector_base_arg" "$@"
else
  for argument in "$@"; do
    if [ "$argument" = "--vector" ]; then
      vector=1
      arg_error=1
    fi
  done
fi
if [ "$vector" -eq 1 ] && { [ "$#" -gt 1 ] || [ "${1:-}" = "--vector" ]; }; then
  arg_error=1
elif [ "$vector" -eq 0 ] && [ "$#" -gt 1 ]; then
  arg_error=1
fi

classification_error=''
base=''
head_oid=''
while :; do
  if [ "$arg_error" -eq 1 ]; then
    classification_error="usage: forge-risk.sh [--vector] [<base-ref>] or forge-risk.sh <base-ref> --vector"
    break
  fi

  export GIT_NO_LAZY_FETCH=1
  export GIT_NO_REPLACE_OBJECTS=1
  repo_root=$(git rev-parse --show-toplevel 2>/dev/null)
  if [ -z "$repo_root" ] || ! cd "$repo_root" 2>/dev/null; then
    classification_error="not a git repository"
    break
  fi
  if [ ! -f evals/mode-policy.json ]; then
    classification_error="missing evals/mode-policy.json"
    break
  fi

  # Dirty tree = the committed diff misses uncommitted work → cannot classify safely.
  if ! git -c core.fsmonitor=false diff --no-ext-diff --no-textconv --quiet -- 2>/dev/null ||
     ! git -c core.fsmonitor=false diff --cached --no-ext-diff --no-textconv --quiet -- 2>/dev/null; then
    classification_error="working tree has uncommitted changes to tracked files; commit them before classification"
    break
  fi
  git -c core.fsmonitor=false ls-files --others --exclude-standard 2>/dev/null |
    python3 -I -c 'import sys; sys.exit(1 if sys.stdin.buffer.read(1) else 0)' 2>/dev/null
  untracked_status=$?
  if [ "$untracked_status" -eq 1 ]; then
    classification_error="working tree has untracked files; commit or remove them before classification"
    break
  fi
  if [ "$untracked_status" -ne 0 ]; then
    classification_error="git could not check the working tree for untracked files"
    break
  fi

  base="${1:-}"
  if [ -z "$base" ]; then
    for b in main master; do
      git rev-parse -q --verify "$b" >/dev/null 2>&1 && { base=$(git merge-base HEAD "$b" 2>/dev/null); break; }
    done
  fi
  if [ -z "$base" ]; then
    classification_error="no base ref found; pass one explicitly (for example the session-start SHA)"
    break
  fi
  case "$base" in
    -*)
      classification_error="base ref must not begin with '-'"
      break
      ;;
  esac
  resolved_base=$(git rev-parse -q --verify "${base}^{commit}" 2>/dev/null)
  head_oid=$(git rev-parse -q --verify "HEAD^{commit}" 2>/dev/null)
  if [ -z "$resolved_base" ] || [ -z "$head_oid" ]; then
    classification_error="base and HEAD must resolve to commits"
    break
  fi
  if ! git merge-base --is-ancestor "$resolved_base" "$head_oid" 2>/dev/null; then
    classification_error="base must be an ancestor of HEAD"
    break
  fi
  base="$resolved_base"
  if [ "$base" = "$head_oid" ]; then
    classification_error="base equals HEAD; there is no committed diff to classify"
    base=''
    head_oid=''
    break
  fi
  break
done

BASE="$base" HEAD_OID="$head_oid" VECTOR="$vector" CLASSIFICATION_ERROR="$classification_error" python3 -I - <<'PY'
import fnmatch, json, os, re, stat, subprocess, sys, unicodedata

base = os.environ['BASE'] or None
vector_mode = os.environ['VECTOR'] == '1'
head = os.environ['HEAD_OID'] or None
precondition_error = os.environ['CLASSIFICATION_ERROR'] or None
if precondition_error is not None:
    base = None
    head = None
ledger_paths = []

GATE_LEDGER_FILENAME = 'decisions.jsonl'

WORKFLOW_FLOOR = (
    '.gitlab-ci.yml', '.gitlab-ci.yaml', '.gitlab/**', '.circleci/**', '.buildkite/**',
    'azure-pipelines*.yml', 'azure-pipelines*.yaml', 'bitbucket-pipelines.yml', 'Jenkinsfile*',
    '.claude/**', '.codex/**', '.cursor/**', 'template/.claude/settings.json', 'template/.github/**',
    'scripts/git-hooks/**', 'scripts/forge-risk.sh', 'scripts/forge-policy.sh', 'scripts/setup-rulesets.sh', 'template/scripts/git-hooks/**',
    'template/scripts/setup-rulesets.sh', 'template/scripts/forge-risk.sh', 'template/scripts/forge-trust.sh', 'template/scripts/forge-doctor.sh',
    'template/scripts/install-git-hooks.sh', 'template/scripts/forge-upgrade.sh', 'template/scripts/forge-metrics.sh',
    'template/scripts/forge-gate-view.sh', 'template/.claude/hooks/**',
    'evals/**', 'template/evals/mode-policy.json', 'template/evals/trust-policy.json', 'template/evals/release-rubric.md',
    'docs/constitution.md', 'docs/architecture/overview.md', 'docs/adr/**', 'docs/contracts/**', 'template/docs/constitution.md', 'template/docs/architecture/overview.md', 'template/docs/adr/**',
    'state/mode', 'template/state/mode', 'template/state/gate-prefs', '.gitattributes',
    'AGENTS.md', 'CLAUDE.md', 'template/AGENTS.md', 'template/CLAUDE.md', 'CODEOWNERS', '**/CODEOWNERS',
)
DATA_FLOOR = (
    '**/identity/**',
    '**/users/**', '**/accounts/**', '**/profiles/**', '**/*pii*',
)
PROD_FLOOR = (
    'src/**', 'source/**', 'app/**', 'lib/**', 'pkg/**', 'internal/**',
    'services/**', 'server/**', 'api/**', 'cmd/**', 'web/**', 'frontend/**', 'backend/**',
    'packages/**', 'modules/**', 'Sources/**', 'public/**', 'config/**',
    '**/*.c', '**/*.cc', '**/*.cpp', '**/*.cs', '**/*.dart', '**/*.ex', '**/*.exs',
    '**/*.go', '**/*.h', '**/*.hpp', '**/*.java', '**/*.js', '**/*.jsx', '**/*.kt', '**/*.kts',
    '**/*.lua', '**/*.mjs', '**/*.cjs', '**/*.php', '**/*.pl', '**/*.py', '**/*.pyi',
    '**/*.rb', '**/*.rs', '**/*.scala', '**/*.sh', '**/*.swift', '**/*.ts', '**/*.tsx',
    '**/*.sql', '**/*.graphql', '**/*.proto',
)

HARD_MAX_POLICY_BYTES = 262144
HARD_MAX_CHANGED_FILES = 200
HARD_MAX_PATH_LIST_BYTES = 1048576
HARD_MAX_INSPECTED_FILE_BYTES = 1048576
HARD_MAX_TOTAL_INSPECTED_BYTES = 16777216

class VectorFailure(Exception):
    def __init__(self, rule, explanation):
        super().__init__(explanation)
        self.rule = rule
        self.explanation = explanation

def unknown_vector(rule, explanation):
    return {
        'base_oid': base, 'exit_code': 2, 'head_oid': head,
        'rule': {'explanation': explanation, 'id': rule},
        'vector': {
            'blast_radius': 'unknown',
            'change_class': 'unclassifiable',
            'data_or_identity': None,
            'diff_size': {'binary_bytes': None, 'binary_files': None, 'bucket': 'unknown',
                          'changed_files': None, 'changed_lines': None},
            'prod_reach': None,
            'reversibility': 'unknown',
            'security_review_required': True,
        },
        'verdict': 'RISKY',
    }

def run_git_bytes(*args):
    result = subprocess.run(['git', *args], capture_output=True)
    if result.returncode:
        raise VectorFailure('fail-closed.git-diff', 'git could not produce a trustworthy committed diff')
    return result.stdout

def bounded_changed_paths():
    try:
        process = subprocess.Popen(
            [
                'git', 'diff', '--no-renames', '--no-ext-diff', '--no-textconv',
                '--name-only', '-z', base, head,
            ],
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
        )
    except OSError as exc:
        raise VectorFailure('fail-closed.git-diff', 'git could not start the changed-path preflight') from exc
    output = bytearray()
    records = 0
    overflow = None
    try:
        while True:
            chunk = process.stdout.read(65536)
            if not chunk:
                break
            output.extend(chunk)
            records += chunk.count(b'\0')
            if len(output) > HARD_MAX_PATH_LIST_BYTES:
                overflow = (
                    'fail-closed.inspection-limit',
                    f'the changed-path list exceeds the hard {HARD_MAX_PATH_LIST_BYTES}-byte cap',
                )
                process.kill()
                break
            if records > HARD_MAX_CHANGED_FILES:
                overflow = (
                    'fail-closed.inspection-limit',
                    f'the diff exceeds the hard {HARD_MAX_CHANGED_FILES}-file inspection cap',
                )
                process.kill()
                break
    except OSError as exc:
        process.kill()
        process.wait()
        raise VectorFailure('fail-closed.git-diff', 'git changed-path preflight could not be read safely') from exc
    finally:
        process.stdout.close()
    returncode = process.wait()
    if overflow is not None:
        raise VectorFailure(*overflow)
    if returncode:
        raise VectorFailure('fail-closed.git-diff', 'git could not produce a trustworthy changed-path preflight')
    raw = bytes(output)
    if not raw:
        return []
    if not raw.endswith(b'\0'):
        raise VectorFailure('fail-closed.diff-format', 'git returned an unterminated changed-path preflight')
    records = raw[:-1].split(b'\0')
    if any(not record for record in records):
        raise VectorFailure('fail-closed.diff-format', 'git returned an empty changed path')
    try:
        paths = [record.decode('utf-8') for record in records]
    except UnicodeError as exc:
        raise VectorFailure('fail-closed.path-encoding', 'a changed path is not valid UTF-8') from exc
    if len(paths) != len(set(paths)):
        raise VectorFailure('fail-closed.diff-format', 'git returned duplicate changed paths')
    return paths

def parse_numstat(raw):
    rows = []
    for record in raw.split(b'\0'):
        if not record:
            continue
        fields = record.split(b'\t', 2)
        if len(fields) != 3:
            raise VectorFailure('fail-closed.diff-format', 'git returned an unrecognized numstat record')
        try:
            added = fields[0].decode('ascii')
            deleted = fields[1].decode('ascii')
            path = fields[2].decode('utf-8')
        except UnicodeError as exc:
            raise VectorFailure('fail-closed.path-encoding', 'a changed path is not valid UTF-8') from exc
        if not ((added.isdigit() and deleted.isdigit()) or (added == deleted == '-')):
            raise VectorFailure('fail-closed.diff-format', 'git returned an unrecognized numstat count')
        rows.append((added, deleted, path))
    return rows

def parse_raw_diff(raw):
    records = raw.split(b'\0')
    if records and records[-1] == b'':
        records.pop()
    if len(records) % 2:
        raise VectorFailure('fail-closed.diff-format', 'git returned an unrecognized raw diff')
    entries = {}
    for index in range(0, len(records), 2):
        try:
            metadata = records[index].decode('ascii')
            path = records[index + 1].decode('utf-8')
        except UnicodeError as exc:
            raise VectorFailure('fail-closed.path-encoding', 'a changed path is not valid UTF-8') from exc
        fields = metadata.split()
        if len(fields) != 5 or not fields[0].startswith(':'):
            raise VectorFailure('fail-closed.diff-format', 'git returned unrecognized raw diff metadata')
        old_mode = fields[0][1:]
        new_mode, status = fields[1], fields[4]
        if status not in {'A', 'D', 'M'} or path in entries:
            raise VectorFailure('fail-closed.change-type', 'a rename, copy, merge, or unknown change type cannot be classified safely')
        entries[path] = (status, old_mode, new_mode)
    return entries

def agents_md_globs():
    out = []
    agents_mode = blob_mode_at(base, 'AGENTS.md')
    if agents_mode is None:
        return out
    if agents_mode != '100644':
        raise VectorFailure('fail-closed.policy', 'base AGENTS.md must be a regular non-executable file')
    size = blob_size_at(base, 'AGENTS.md')
    if size is None:
        raise VectorFailure('fail-closed.git-object', 'base AGENTS.md size could not be read safely')
    if size > HARD_MAX_POLICY_BYTES:
        raise VectorFailure(
            'fail-closed.policy',
            f'base AGENTS.md exceeds the hard {HARD_MAX_POLICY_BYTES}-byte policy-input cap',
        )
    data = blob_at(base, 'AGENTS.md')
    if data is None:
        raise VectorFailure('fail-closed.git-object', 'base AGENTS.md content could not be read safely')
    try:
        lines = data.decode('utf-8').splitlines()
    except UnicodeError as exc:
        raise VectorFailure('fail-closed.content-encoding', 'base AGENTS.md is not valid UTF-8') from exc
    grab = False
    for ln in lines:
        s = ln.strip()
        if s.startswith('## '):
            grab = 'risky path' in s.lower()
            continue
        if grab and s and not s.startswith('<!--'):
            out += re.findall(r'[A-Za-z0-9_./*-]+/\*+[A-Za-z0-9_./*-]*|[A-Za-z0-9_./*-]*\*\*[A-Za-z0-9_./*-]*|`(?:[^`]+)`', s)
    return [g for g in (x.strip('`').strip() for x in out) if g and ('*' in g or '/' in g)]

def vector_matches(path, globs):
    for glob in globs:
        candidates = [glob] + ([glob[3:]] if glob.startswith('**/') else [])
        if any(fnmatch.fnmatch(path, candidate) or fnmatch.fnmatch(os.path.basename(path), candidate)
               for candidate in candidates):
            return True
    return False

def safe_matches(path, globs):
    for glob in globs:
        if '/' not in glob:
            if '/' not in path and fnmatch.fnmatch(path, glob):
                return True
        elif vector_matches(path, (glob,)):
            return True
    return False

def is_test(path):
    lowered = path.lower()
    basename = os.path.basename(path)
    return (
        any(part in {'test', 'tests'} for part in lowered.split('/')[:-1])
        or basename.lower() == 'conftest.py'
        or re.search(r'(^|[._-])(tests?|specs?)([._-]|$)', basename, re.IGNORECASE)
        or re.search(r'(Test|Tests|Spec|Specs)\.[^.]+$', basename)
        or re.match(r'(?:test|tests|spec|specs|Test|Tests|Spec|Specs)(?=[A-Z0-9_])|tests?.*\.py$', basename)
    )

blob_cache = {}
blob_size_cache = {}

def blob_at(revision, path):
    key = (revision, path)
    if key in blob_cache:
        return blob_cache[key]
    result = subprocess.run(['git', 'show', f'{revision}:{path}'], capture_output=True)
    if result.returncode:
        return None
    blob_cache[key] = result.stdout
    return blob_cache[key]

SOURCE_INTERPRETERS = re.compile(r'\A(?:python[0-9.]*|node|bash|sh|dash|zsh|ksh|ruby|perl)\Z')

def shebang_interpreter(blob):
    if blob is None or not blob.startswith(b'#!'):
        return None
    first = blob.split(b'\n', 1)[0][2:].strip()
    try:
        parts = first.decode('ascii').split()
    except UnicodeError:
        return None
    if not parts:
        return None
    command = parts[0].rsplit('/', 1)[-1]
    if command == 'env':
        arguments = [argument for argument in parts[1:] if not argument.startswith('-')]
        if not arguments:
            return None
        command = arguments[0].rsplit('/', 1)[-1]
    return command

def blob_size_at(revision, path):
    key = (revision, path)
    if key in blob_size_cache:
        return blob_size_cache[key]
    result = subprocess.run(['git', 'cat-file', '-s', f'{revision}:{path}'], capture_output=True, text=True)
    if result.returncode:
        return None
    try:
        blob_size_cache[key] = int(result.stdout.strip())
    except ValueError as exc:
        raise VectorFailure('fail-closed.git-object', 'git returned an invalid blob size') from exc
    return blob_size_cache[key]

def blob_mode_at(revision, path):
    result = subprocess.run(['git', 'ls-tree', '-z', revision, '--', path], capture_output=True)
    if result.returncode:
        raise VectorFailure('fail-closed.git-object', 'git could not inspect a required tree entry safely')
    if not result.stdout:
        return None
    records = [record for record in result.stdout.split(b'\0') if record]
    if len(records) != 1:
        raise VectorFailure('fail-closed.git-object', 'git returned an invalid tree entry')
    try:
        metadata, recorded_path = records[0].split(b'\t', 1)
        mode = metadata.split(b' ', 1)[0].decode('ascii')
        decoded_path = recorded_path.decode('utf-8')
    except (UnicodeError, ValueError) as exc:
        raise VectorFailure('fail-closed.git-object', 'git returned an invalid tree entry') from exc
    if decoded_path != path:
        raise VectorFailure('fail-closed.git-object', 'git returned an unexpected tree entry')
    return mode

def is_lfs_pointer(data):
    if len(data) > 4096:
        return False
    lines = data.splitlines()
    return (
        bool(lines)
        and lines[0] == b'version https://git-lfs.github.com/spec/v1'
        and any(re.fullmatch(br'oid sha256:[0-9a-f]{64}', line) for line in lines[1:])
        and any(re.fullmatch(br'size [0-9]+', line) for line in lines[1:])
    )

def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f'duplicate JSON object key: {key}')
        result[key] = value
    return result

def path_summary(paths):
    return ', '.join(paths[:5]) + (' …' if len(paths) > 5 else '')

def early_risk(rule_id, explanation, *, blast_radius, changed_files, changed_lines,
               change_class='unclassifiable', data_or_identity=None, prod_reach=None,
               reversibility='moderate'):
    return {
        'base_oid': base, 'exit_code': 1, 'head_oid': head,
        'rule': {'explanation': explanation, 'id': rule_id},
        'vector': {
            'blast_radius': blast_radius,
            'change_class': change_class,
            'data_or_identity': data_or_identity,
            'diff_size': {'binary_bytes': None, 'binary_files': None, 'bucket': 'not-evaluated',
                          'changed_files': changed_files, 'changed_lines': changed_lines},
            'prod_reach': prod_reach,
            'reversibility': reversibility,
            'security_review_required': True,
        },
        'verdict': 'RISKY',
    }

def classify_vector():
    global ledger_paths
    preflight_paths = bounded_changed_paths()
    if not preflight_paths:
        raise VectorFailure('fail-closed.empty-diff', 'empty diff between base and HEAD')
    ledger_paths = sorted(preflight_paths)
    raw_entries = parse_raw_diff(
        run_git_bytes('diff', '--no-renames', '--no-ext-diff', '--no-textconv', '--raw', '-z', base, head)
    )
    paths = ledger_paths
    if set(paths) != set(raw_entries):
        raise VectorFailure('fail-closed.diff-format', 'git path and raw diff views disagreed')
    if len(paths) > HARD_MAX_CHANGED_FILES:
        raise VectorFailure(
            'fail-closed.inspection-limit',
            f'{len(paths)} changed files exceeds the hard {HARD_MAX_CHANGED_FILES}-file inspection cap',
        )

    unsupported = []
    deleted_paths = []
    for path, (status, old_mode, new_mode) in raw_entries.items():
        live_modes = {mode for mode in (old_mode, new_mode) if mode != '000000'}
        if any(mode not in {'100644', '100755'} for mode in live_modes):
            unsupported.append(path)
        if status == 'M' and old_mode != new_mode:
            unsupported.append(path)
        if status == 'D':
            deleted_paths.append(path)
    if unsupported:
        raise VectorFailure(
            'fail-closed.change-type',
            'symlink, submodule, or file-mode changes cannot be classified safely: ' + path_summary(sorted(set(unsupported))),
        )
    hard_total_blob_bytes = 0
    for path, (_, old_mode, new_mode) in raw_entries.items():
        revisions = []
        if old_mode != '000000':
            revisions.append(base)
        if new_mode != '000000':
            revisions.append(head)
        for revision in revisions:
            size = blob_size_at(revision, path)
            if size is None:
                raise VectorFailure('fail-closed.git-object', 'git could not read a changed blob size safely')
            if size > HARD_MAX_INSPECTED_FILE_BYTES:
                raise VectorFailure(
                    'fail-closed.inspection-limit',
                    f'a changed blob exceeds the hard {HARD_MAX_INSPECTED_FILE_BYTES}-byte cap: {path}',
                )
            hard_total_blob_bytes += size
            if hard_total_blob_bytes > HARD_MAX_TOTAL_INSPECTED_BYTES:
                raise VectorFailure(
                    'fail-closed.inspection-limit',
                    f'changed blobs exceed the hard {HARD_MAX_TOTAL_INSPECTED_BYTES}-byte aggregate cap',
                )

    numstat = parse_numstat(
        run_git_bytes('diff', '--no-renames', '--no-ext-diff', '--no-textconv', '--numstat', '-z', base, head)
    )
    numstat_paths = {path for _, _, path in numstat}
    if numstat_paths != set(paths):
        raise VectorFailure('fail-closed.diff-format', 'git path, raw, and numstat diff views disagreed')
    executable_paths = sorted(
        path for path, (_, old_mode, new_mode) in raw_entries.items()
        if '100755' in {old_mode, new_mode}
    )
    loc = sum(int(added) + int(deleted) for added, deleted, _ in numstat if added.isdigit())
    declared_binary = {path for added, deleted, path in numstat if added == deleted == '-'}
    areas = {path.split('/', 1)[0] for path in paths}
    if len(paths) == 1:
        blast_radius = 'single-file'
    elif len(areas) == 1:
        blast_radius = 'localized'
    else:
        blast_radius = 'cross-cutting'

    try:
        policy_path = 'evals/mode-policy.json'
        if blob_mode_at(base, policy_path) != '100644':
            raise VectorFailure('fail-closed.policy', 'base evals/mode-policy.json must be a regular non-executable file')
        policy_size = blob_size_at(base, policy_path)
        if policy_size is None:
            raise VectorFailure('fail-closed.policy', 'base evals/mode-policy.json is unavailable')
        if policy_size > HARD_MAX_POLICY_BYTES:
            raise VectorFailure(
                'fail-closed.policy',
                f'evals/mode-policy.json exceeds the hard {HARD_MAX_POLICY_BYTES}-byte policy cap',
            )
        policy_bytes = blob_at(base, policy_path)
        if policy_bytes is None or len(policy_bytes) != policy_size:
            raise VectorFailure('fail-closed.policy', 'base evals/mode-policy.json could not be read safely')
        policy_document = json.loads(
            policy_bytes.decode('utf-8'),
            object_pairs_hook=unique_object,
            parse_constant=lambda value: (_ for _ in ()).throw(ValueError(f'non-standard JSON constant: {value}')),
        )
        policy = policy_document['sprint_risk_classifier']
        has_vector_policy = 'risk_vector_classifier' in policy_document
        vector_policy = policy_document.get('risk_vector_classifier')
        if not isinstance(policy, dict):
            raise TypeError('sprint_risk_classifier must be a JSON object')
    except VectorFailure:
        raise
    except (OSError, UnicodeError, json.JSONDecodeError, KeyError, TypeError, ValueError) as exc:
        raise VectorFailure('fail-closed.policy', 'evals/mode-policy.json is not a valid risk policy') from exc

    def globs(key):
        value = policy.get(key)
        if not isinstance(value, list) or not all(isinstance(item, str) and item for item in value):
            raise VectorFailure(
                'fail-closed.policy',
                f'sprint_risk_classifier.{key} must be a string array',
            )
        return value

    policy_risky_globs = globs('risky_path_globs')
    project_risky_globs = agents_md_globs()
    dependency_globs = globs('dependency_manifests')
    if not has_vector_policy:
        legacy_loc = policy.get('max_changed_loc'); legacy_binary = policy.get('max_added_binary_bytes')
        if type(legacy_loc) is not int or legacy_loc < 0 or type(legacy_binary) is not int or legacy_binary < 0:
            raise VectorFailure('fail-closed.policy', 'risk_vector_classifier is required')
        try: [re.compile(item) for item in globs('test_weakening_markers')]
        except re.error as exc: raise VectorFailure('fail-closed.policy', 'legacy test_weakening_markers must be valid regexes') from exc
        risky_hits = sorted(
            path for path in paths
            if vector_matches(path, [*policy_risky_globs, *project_risky_globs])
        )
        dependency_hits = sorted(path for path in paths if vector_matches(path, dependency_globs))
        if risky_hits or dependency_hits:
            hits = risky_hits or dependency_hits
            rule_id = 'risk.risky-path' if risky_hits else 'risk.dependency'
            label = 'risky paths' if risky_hits else 'dependency manifests'
            return early_risk(
                rule_id, f'{label} changed: ' + path_summary(hits),
                blast_radius=blast_radius, changed_files=len(paths), changed_lines=loc,
            )
        raise VectorFailure('fail-closed.policy', 'risk_vector_classifier is required')
    if not isinstance(vector_policy, dict):
        raise VectorFailure('fail-closed.policy', 'risk_vector_classifier must be a JSON object')

    try:
        categories = policy.get('risky_path_glob_categories')
        if (
            not isinstance(categories, list)
            or len(categories) != len(policy_risky_globs)
            or not all(item in {'data', 'workflow', 'prod', 'general'} for item in categories)
        ):
            raise VectorFailure(
                'fail-closed.policy',
                'sprint_risk_classifier.risky_path_glob_categories must classify every risky glob',
            )
        weakening_markers = globs('test_weakening_markers')
        weakening_patterns = [re.compile(item) for item in weakening_markers]
        max_loc = policy.get('max_changed_loc')
        max_changed_files = vector_policy.get('max_changed_files')
        max_inspected_bytes = vector_policy.get('max_inspected_file_bytes')
        max_total_inspected_bytes = vector_policy.get('max_total_inspected_bytes')
        if type(max_loc) is not int or max_loc < 0:
            raise VectorFailure('fail-closed.policy', 'sprint_risk_classifier.max_changed_loc must be a non-negative integer')
        if type(max_changed_files) is not int or not 1 <= max_changed_files <= HARD_MAX_CHANGED_FILES:
            raise VectorFailure(
                'fail-closed.policy',
                f'risk_vector_classifier.max_changed_files must be between 1 and {HARD_MAX_CHANGED_FILES}',
            )
        if type(max_inspected_bytes) is not int or not 1 <= max_inspected_bytes <= HARD_MAX_INSPECTED_FILE_BYTES:
            raise VectorFailure(
                'fail-closed.policy',
                f'risk_vector_classifier.max_inspected_file_bytes must be between 1 and {HARD_MAX_INSPECTED_FILE_BYTES}',
            )
        if (
            type(max_total_inspected_bytes) is not int
            or not 1 <= max_total_inspected_bytes <= HARD_MAX_TOTAL_INSPECTED_BYTES
        ):
            raise VectorFailure(
                'fail-closed.policy',
                'risk_vector_classifier.max_total_inspected_bytes must be between '
                f'1 and {HARD_MAX_TOTAL_INSPECTED_BYTES}',
            )
        safe = vector_policy.get('safe_class_allowlists')
        required_safe = {'benign-companions', 'docs-only', 'generated-only'}
        if not isinstance(safe, dict) or set(safe) != required_safe:
            raise VectorFailure(
                'fail-closed.policy',
                'safe_class_allowlists must define exactly benign-companions, docs-only, and generated-only',
            )
        for name in sorted(required_safe):
            if not isinstance(safe[name], list) or not safe[name] or not all(
                isinstance(item, str) and item for item in safe[name]
            ):
                raise VectorFailure('fail-closed.policy', f'safe_class_allowlists.{name} must be a non-empty string array')
    except re.error as exc:
        raise VectorFailure('fail-closed.policy', 'sprint_risk_classifier.test_weakening_markers must be valid regexes') from exc

    categorized = {
        category: [glob for glob, assigned in zip(policy_risky_globs, categories) if assigned == category]
        for category in ('data', 'workflow', 'prod')}
    workflow_globs = [*WORKFLOW_FLOOR, *categorized['workflow']]
    data_globs = [*DATA_FLOOR, *categorized['data']]
    prod_globs = [*PROD_FLOOR, *categorized['prod']]
    risky_globs = [*policy_risky_globs, *project_risky_globs]
    workflow_hits = sorted(path for path in paths if vector_matches(path.lower(), [g.lower() for g in workflow_globs]))
    data_hits = sorted(path for path in paths if vector_matches(path.lower(), [g.lower() for g in data_globs]))
    prod_hits = sorted(path for path in paths if vector_matches(path.lower(), [g.lower() for g in prod_globs]))
    risky_hits = sorted(path for path in paths if vector_matches(path, risky_globs))
    dependency_hits = sorted(path for path in paths if vector_matches(path, dependency_globs))
    if workflow_hits:
        return early_risk(
            'risk.workflow-touching',
            'workflow or policy-control paths changed: ' + path_summary(workflow_hits),
            blast_radius=blast_radius, changed_files=len(paths), changed_lines=loc,
            change_class='workflow', data_or_identity=True if data_hits else None,
            prod_reach=True, reversibility='difficult' if data_hits else 'moderate',
        )

    content_binary = set()
    blob_sizes = {}
    total_inspected_bytes = 0
    for path, (_, old_mode, new_mode) in raw_entries.items():
        versions = []
        if old_mode != '000000':
            versions.append(base)
        if new_mode != '000000':
            versions.append(head)
        path_sizes = []
        for revision in versions:
            size = blob_size_at(revision, path)
            if size is None:
                raise VectorFailure('fail-closed.git-object', 'git could not read a changed blob safely')
            path_sizes.append(size)
            if path not in declared_binary:
                total_inspected_bytes += size
                if total_inspected_bytes > max_total_inspected_bytes:
                    raise VectorFailure(
                        'fail-closed.inspection-limit',
                        f'text content exceeds the {max_total_inspected_bytes}-byte aggregate inspection cap',
                    )
                if size > max_inspected_bytes:
                    raise VectorFailure(
                        'fail-closed.inspection-limit',
                        f'a text-classified file exceeds the {max_inspected_bytes}-byte inspection cap: {path}',
                    )
                blob = blob_at(revision, path)
                if blob is None:
                    raise VectorFailure('fail-closed.git-object', 'git could not read a changed blob safely')
                if b'\0' in blob or is_lfs_pointer(blob):
                    content_binary.add(path)
                else:
                    try:
                        blob.decode('utf-8')
                    except UnicodeError as exc:
                        raise VectorFailure(
                            'fail-closed.content-encoding',
                            'text-classified content is not valid UTF-8: ' + path,
                        ) from exc
        blob_sizes[path] = max(path_sizes, default=0)
    binary_paths = sorted(declared_binary | content_binary)
    binary_bytes = sum(blob_sizes[path] for path in binary_paths)

    # Extensionless source recognition: a text file with NO extension whose first line
    # is a known interpreter shebang is source (e.g. an executable python script named
    # "forge-hybrid"), not an unclassifiable stranger. Extension-bearing paths keep
    # extension-based recognition only; binary content is never inspected; every blob
    # consulted here was already read (and capped) by the inspection loop above, so
    # this adds no I/O. Recognition feeds prod_hits only — it can widen ordinary-code
    # and prod_reach but cannot suppress any floor, risky-path, or executable rule,
    # all of which match independently of prod classification.
    for path, (_, old_mode, new_mode) in raw_entries.items():
        basename = os.path.basename(path)
        if basename.startswith('.') or '.' in basename[1:]:
            # Dotfiles are configuration by convention, never interpreter scripts —
            # a planted shebang must not make .env read as ordinary source.
            continue
        if path in declared_binary or path in content_binary or path in prod_hits:
            continue
        revision = head if new_mode != '000000' else base
        if SOURCE_INTERPRETERS.match(shebang_interpreter(blob_at(revision, path)) or ''):
            prod_hits.append(path)
    prod_hits = sorted(set(prod_hits))

    docs_only = all(safe_matches(path, safe['docs-only']) for path in paths); generated_only = all(safe_matches(path, safe['generated-only']) for path in paths)
    # Tests are RISKY only when deleted, net-shrunk, or given a skip/only/ignore marker.
    # Mere test presence is normal TDD and cannot force review-everything.
    test_paths = {path for path in paths if is_test(path)}
    added_status_paths = {path for path, (status, _, _) in raw_entries.items() if status == 'A'}
    deleted_test_paths = {path for path in deleted_paths if path in test_paths}
    weakened = set(deleted_test_paths)
    for added, deleted, path in numstat:
        if (
            path in test_paths
            and path not in added_status_paths
            and added.isdigit()
            and deleted.isdigit()
            and int(deleted) > int(added)
        ):
            weakened.add(path)
    for path in sorted(test_paths - deleted_test_paths - declared_binary):
        blob = run_git_bytes(
            'diff', '--no-renames', '--no-ext-diff', '--no-textconv', base, head, '--', path
        )
        try:
            text = blob.decode('utf-8')
        except UnicodeError:
            raise VectorFailure('fail-closed.diff-format', 'a test diff was not valid UTF-8')
        for line in text.splitlines():
            if line.startswith('+') and not line.startswith('+++'):
                if any(pattern.search(line[1:]) for pattern in weakening_patterns):
                    weakened.add(path)
                    break
    test_hits = sorted(weakened)

    if binary_paths:
        change_class = 'binary'
    elif test_hits:
        change_class = 'test-semantic'
    elif docs_only:
        change_class = 'docs-only'
    elif generated_only:
        change_class = 'generated-only'
    elif prod_hits and all(path in test_paths or path in prod_hits or safe_matches(path, safe['docs-only'])
                           or safe_matches(path, safe['generated-only'])
                           or safe_matches(path, safe['benign-companions']) for path in paths):
        change_class = 'ordinary-code'
    else:
        change_class = 'unclassifiable'

    if binary_paths:
        reversibility = 'unknown'
    elif data_hits:
        reversibility = 'difficult'
    elif (
        deleted_paths
        or dependency_hits
        or risky_hits
        or executable_paths
        or prod_hits
    ):
        reversibility = 'moderate'
    elif docs_only or generated_only:
        reversibility = 'easy'
    else:
        reversibility = 'unknown'

    files_over_limit = len(paths) > max_changed_files
    if binary_paths:
        size_bucket = 'binary-present'
    elif loc > max_loc or files_over_limit:
        size_bucket = 'over-limit'
    else:
        size_bucket = 'within-limit'
    vector = {
        'blast_radius': blast_radius,
        'change_class': change_class,
        'data_or_identity': bool(data_hits),
        'diff_size': {
            'binary_bytes': binary_bytes,
            'binary_files': len(binary_paths),
            'bucket': size_bucket,
            'changed_files': len(paths),
            'changed_lines': loc,
        },
        'prod_reach': bool(prod_hits or executable_paths),
        'reversibility': reversibility,
        'security_review_required': bool(data_hits or risky_hits or dependency_hits),
    }

    if binary_paths:
        rule = ('risk.binary', 'binary content cannot be semantically classified: ' + path_summary(binary_paths))
        exit_code = 1
    elif test_hits:
        rule = ('risk.test-semantic', 'test semantics changed: ' + path_summary(test_hits))
        exit_code = 1
    elif data_hits:
        rule = ('risk.data-or-identity', 'data or identity paths changed: ' + path_summary(data_hits))
        exit_code = 1
    elif executable_paths:
        rule = ('risk.executable-artifact', 'executable artifacts changed: ' + path_summary(executable_paths))
        exit_code = 1
    elif risky_hits:
        rule = ('risk.risky-path', 'risky paths changed: ' + path_summary(risky_hits))
        exit_code = 1
    elif dependency_hits:
        rule = ('risk.dependency', 'dependency manifests changed: ' + path_summary(dependency_hits))
        exit_code = 1
    elif files_over_limit:
        rule = ('risk.diff-size', f'{len(paths)} changed files exceeds the {max_changed_files}-file policy cap')
        exit_code = 1
    elif loc > max_loc:
        rule = ('risk.diff-size', f'{loc} changed lines exceeds the {max_loc}-line policy cap')
        exit_code = 1
    else:
        safe_classes = [
            name for name, matched in (
                ('docs-only', docs_only),
                ('generated-only', generated_only),
            )
            if matched
        ]
        if len(safe_classes) == 1:
            safe_class = safe_classes[0]
            rule = (f'safe.{safe_class}', f'all changed files satisfy the {safe_class} positive allowlist')
            exit_code = 0
        elif safe_classes:
            rule = ('fail-closed.unclassified', 'the diff matched more than one positive safe class')
            exit_code = 2
        elif change_class == 'ordinary-code':
            # Recognized code inside caps is safe when no verdict-bearing signal fired;
            # prod_reach remains a routing dimension rather than a verdict.
            rule = ('safe.within-limits', 'recognized source within policy caps; no risk signal fired')
            exit_code = 0
        else:
            rule = ('fail-closed.unclassified', 'the diff did not match exactly one positive safe class')
            exit_code = 2

    if exit_code == 2:
        vector['security_review_required'] = True
    return {
        'base_oid': base,
        'exit_code': exit_code,
        'head_oid': head,
        'rule': {'explanation': rule[1], 'id': rule[0]},
        'vector': vector,
        'verdict': 'LOW-RISK' if exit_code == 0 else 'RISKY',
    }

def valid_absolute_ledger_path(path):
    if not isinstance(path, str) or not path or not os.path.isabs(path):
        return False
    if path != unicodedata.normalize('NFC', path) or '\x00' in path or '\\' in path:
        return False
    if any(ord(character) < 32 or ord(character) == 127 for character in path):
        return False
    parts = path.split('/')
    if parts[0] != '' or any(part in {'', '.', '..'} for part in parts[1:]):
        return False
    if any(part.casefold() == '.git' or ':' in part for part in parts[1:]):
        return False
    return True

def enters_evals_results(path):
    # Broadened from evals/results/attested/ only: forge-trust.sh's claims tier
    # globs the whole evals/results/ tree, so no ledger byte may land anywhere
    # under an evals/results segment pair — the ledger must stay unreachable by
    # trust in every tier, not just the attested one.
    parts = [part.casefold() for part in os.path.realpath(path).split(os.sep) if part]
    return any(parts[index:index + 2] == ['evals', 'results']
               for index in range(max(0, len(parts) - 1)))

def ledger_git_environment():
    environment = dict(os.environ)
    for name in (
        'GIT_ALTERNATE_OBJECT_DIRECTORIES', 'GIT_CEILING_DIRECTORIES', 'GIT_COMMON_DIR',
        'GIT_CONFIG', 'GIT_CONFIG_COUNT', 'GIT_CONFIG_PARAMETERS', 'GIT_DIR',
        'GIT_DISCOVERY_ACROSS_FILESYSTEM', 'GIT_INDEX_FILE', 'GIT_NAMESPACE',
        'GIT_OBJECT_DIRECTORY', 'GIT_PREFIX', 'GIT_WORK_TREE',
    ):
        environment.pop(name, None)
    for name in list(environment):
        if name.startswith('GIT_CONFIG_KEY_') or name.startswith('GIT_CONFIG_VALUE_'):
            environment.pop(name, None)
    environment.pop('GIT_LITERAL_PATHSPECS', None)
    return environment

def git_repository_environment_is_routed():
    # A routed repository can have no on-disk .git marker at its worktree. The
    # safety checks deliberately do not trust caller-supplied repository routing,
    # so telemetry must fail open rather than erase that context and mistake the
    # worktree for an ordinary directory.
    return any(name in os.environ for name in (
        'GIT_ALTERNATE_OBJECT_DIRECTORIES', 'GIT_CEILING_DIRECTORIES', 'GIT_COMMON_DIR',
        'GIT_DIR', 'GIT_DISCOVERY_ACROSS_FILESYSTEM', 'GIT_INDEX_FILE', 'GIT_NAMESPACE',
        'GIT_OBJECT_DIRECTORY', 'GIT_PREFIX', 'GIT_WORK_TREE',
    ))

def containing_git_roots(path):
    roots = []
    seen = set()
    current = os.path.dirname(path)
    environment = ledger_git_environment()
    while True:
        marker_present = os.path.lexists(os.path.join(current, '.git'))
        repository = subprocess.run(
            ['git', '-C', current, 'rev-parse', '--show-toplevel'],
            capture_output=True,
            text=True,
            env=environment,
        )
        if repository.returncode == 0:
            root = os.path.realpath(repository.stdout.strip())
            if not os.path.isabs(root) or not os.path.isdir(root):
                return None
            if root not in seen:
                seen.add(root)
                roots.append(root)
            next_current = os.path.dirname(root)
        else:
            if marker_present:
                return None
            next_current = os.path.dirname(current)
        if next_current == current:
            break
        current = next_current
    return roots

MAX_GIT_INDEX_BYTES = 8 * 1024 * 1024

def git_index_contains_casefold_path(root, normalized_relative):
    process = None
    try:
        process = subprocess.Popen(
            ['git', '-C', root, 'ls-files', '-z'],
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            env=ledger_git_environment(),
        )
        buffered = b''
        consumed = 0
        while True:
            chunk = process.stdout.read(65536)
            if not chunk:
                break
            consumed += len(chunk)
            if consumed > MAX_GIT_INDEX_BYTES:
                return None
            fields = (buffered + chunk).split(b'\0')
            buffered = fields.pop()
            for tracked_path in fields:
                decoded_path = os.fsdecode(tracked_path)
                if unicodedata.normalize('NFC', decoded_path).casefold() == normalized_relative:
                    return True
        if buffered or process.wait() != 0:
            return None
        return False
    except (OSError, UnicodeError):
        return None
    finally:
        if process is not None:
            if process.stdout is not None:
                process.stdout.close()
            if process.poll() is None:
                process.kill()
                process.wait()

def ledger_path_is_safe(path):
    if git_repository_environment_is_routed():
        return False
    roots = containing_git_roots(path)
    if roots is None:
        return False
    for root in roots:
        try:
            relative = os.path.relpath(path, root)
        except ValueError:
            return False
        if relative == '..' or relative.startswith('../') or os.path.isabs(relative):
            return False
        for pathspec in (
            f':(top,literal){relative}',
            f':(top,literal,icase){relative}',
        ):
            tracked = subprocess.run(
                ['git', '-C', root, 'ls-files', '--error-unmatch', '--', pathspec],
                capture_output=True,
                env=ledger_git_environment(),
            )
            if tracked.returncode != 1:
                return False
        # Git's icase pathspec matching does not cover every Unicode case alias
        # on a case-insensitive filesystem. Compare the complete index using the
        # same NFC + casefold rule used for destination validation. Stream it
        # under a hard cap; an oversized index means telemetry is refused.
        normalized_relative = unicodedata.normalize('NFC', relative).casefold()
        casefold_match = git_index_contains_casefold_path(root, normalized_relative)
        if casefold_match is not False:
            return False
        ignored = subprocess.run(
            ['git', '-C', root, 'check-ignore', '-q', '--no-index', '-z', '--stdin'],
            capture_output=True,
            input=(relative + '\x00').encode('utf-8'),
            env=ledger_git_environment(),
        )
        if ignored.returncode != 0:
            return False
    return True

def resolve_ledger_destination():
    configured = os.environ.get('FORGE_GATE_LEDGER')
    if configured is not None:
        return configured if valid_absolute_ledger_path(configured) else None
    directory = os.path.join(os.getcwd(), 'state', 'gate-ledger')
    try:
        directory_status = os.lstat(directory)
    except OSError:
        return None
    if stat.S_ISLNK(directory_status.st_mode) or not stat.S_ISDIR(directory_status.st_mode):
        return None
    return os.path.join(directory, GATE_LEDGER_FILENAME)

def template_kit_version():
    path = '.forge-template.json'
    descriptor = None
    try:
        nofollow = getattr(os, 'O_NOFOLLOW', None)
        nonblock = getattr(os, 'O_NONBLOCK', None)
        if nofollow is None or nonblock is None:
            return None
        descriptor = os.open(path, os.O_RDONLY | nofollow | nonblock)
        path_status = os.fstat(descriptor)
        if not stat.S_ISREG(path_status.st_mode) or path_status.st_size > 262144:
            return None
        raw = os.read(descriptor, 262145)
        if len(raw) != path_status.st_size:
            return None
        value = json.loads(raw.decode('utf-8')).get('kit_version')
    except (OSError, UnicodeError, json.JSONDecodeError, AttributeError):
        return None
    finally:
        if descriptor is not None:
            os.close(descriptor)
    return value if isinstance(value, str) and value else None

def open_resolved_destination(resolved, flags, nofollow):
    directory = getattr(os, 'O_DIRECTORY', None)
    if directory is None or os.open not in os.supports_dir_fd:
        return os.open(resolved, flags, 0o600)
    # Walk the already-realpath'd components with O_NOFOLLOW at every step so a
    # directory swapped for a symlink between validation and open cannot
    # redirect the write; only the final open may create the file.
    components = [part for part in resolved.split('/') if part]
    parent = os.open('/', os.O_RDONLY | directory)
    try:
        for part in components[:-1]:
            step = os.open(part, os.O_RDONLY | directory | nofollow, dir_fd=parent)
            os.close(parent)
            parent = step
        return os.open(components[-1], flags, 0o600, dir_fd=parent)
    finally:
        os.close(parent)

def write_gate_record(payload):
    try:
        destination = resolve_ledger_destination()
        if destination is None:
            return
        resolved = os.path.realpath(destination)
        if enters_evals_results(resolved) or not ledger_path_is_safe(resolved):
            return
        try:
            destination_status = os.lstat(destination)
        except FileNotFoundError:
            destination_status = None
        except OSError:
            return
        if destination_status is not None and (
            stat.S_ISLNK(destination_status.st_mode)
            or not stat.S_ISREG(destination_status.st_mode)
        ):
            return
        vector = payload['vector']
        record = {
            'schema': 'forge-gate-v1',
            'base_oid': payload['base_oid'],
            'head_oid': payload['head_oid'],
            'verdict': payload['verdict'],
            'exit_code': payload['exit_code'],
            'rule_id': payload['rule']['id'],
            'change_class': vector['change_class'],
            'data_or_identity': vector['data_or_identity'],
            'prod_reach': vector['prod_reach'],
            'reversibility': vector['reversibility'],
            'security_review_required': vector['security_review_required'],
            'diff_size': vector['diff_size'],
            'changed_paths': ledger_paths,
            'kit_template_version': template_kit_version(),
            'renderer': 'vector' if vector_mode else 'text',
        }
        line = (json.dumps(record, sort_keys=True, separators=(',', ':')) + '\n').encode('utf-8')
        nofollow = getattr(os, 'O_NOFOLLOW', None)
        nonblock = getattr(os, 'O_NONBLOCK', None)
        exclusive = getattr(os, 'O_EXCL', None)
        if nofollow is None or nonblock is None or exclusive is None:
            return
        flags = os.O_WRONLY | os.O_APPEND | os.O_CREAT | nofollow | nonblock
        if destination_status is None:
            flags |= exclusive
        descriptor = open_resolved_destination(resolved, flags, nofollow)
        try:
            opened_status = os.fstat(descriptor)
            if (
                not stat.S_ISREG(opened_status.st_mode)
                or stat.S_IMODE(opened_status.st_mode) != 0o600
                or opened_status.st_nlink != 1
            ):
                return
            os.write(descriptor, line)
        finally:
            os.close(descriptor)
    except BaseException:
        return

if precondition_error is not None:
    payload = unknown_vector('fail-closed.precondition', precondition_error)
else:
    try:
        payload = classify_vector()
    except VectorFailure as exc:
        payload = unknown_vector(exc.rule, exc.explanation)
    except Exception:
        payload = unknown_vector('fail-closed.internal', 'an unexpected classifier error prevented a trustworthy result')

write_gate_record(payload)

if vector_mode:
    print(json.dumps(payload, sort_keys=True, separators=(',', ':')))
elif precondition_error is not None:
    print('RISKY:')
    print('  - forge-risk: ' + precondition_error)
elif payload['exit_code'] == 0:
    changed_lines = payload['vector']['diff_size']['changed_lines']
    print(f'LOW-RISK: {changed_lines} lines, no risky paths, no dependency changes, no weakened tests')
    print('sprint mode: agent review may be skipped — CI + demo check gate this diff.')
else:
    print('RISKY:')
    print('  - ' + payload['rule']['explanation'])
    if payload['exit_code'] == 1:
        print('sprint mode: this diff gets ONE agent review round (see evals/mode-policy.json).')
    else:
        print('sprint mode: classification failed closed; treat this diff as requiring review.')
sys.exit(payload['exit_code'])
PY
exit $?
