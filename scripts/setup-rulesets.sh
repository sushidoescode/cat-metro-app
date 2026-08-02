#!/usr/bin/env bash
# Server-side enforcement as code: GitHub rulesets for the current repo.
# This is the wall that client hooks cannot be (pre-commit is bypassable with --no-verify).
# Requires: gh CLI authenticated with repository admin plus organization ruleset administration.
# HUMAN runs/approves this — agents stage it only. Personal repositories and plans without required
# ruleset workflows can install the defense-in-depth repository rules, but remain UNVERIFIED because
# GitHub cannot run the policy definition from a separately trusted default-branch source there.
#   ./scripts/setup-rulesets.sh          # create missing repo + trusted org rulesets, then verify
#   ./scripts/setup-rulesets.sh --solo   # same, but install the solo-maintainer review floor
#   ./scripts/setup-rulesets.sh --check  # read-only: exact desired-v-actual policy comparison
#
# Postures (the review floor is explicit, never inferred):
#   dual (default) — forge-main: PR + 1 approving review + code-owner review. Requires a second
#                    human CODEOWNER; a solo maintainer cannot approve their own PRs.
#   solo (--solo)  — forge-main-solo: identical walls minus required approvals (0) and code-owner
#                    review. The PR flow and green required checks still gate every merge;
#                    the independent-review leg is process-level (forge-review), not the server.
#                    Graduate to dual by deleting forge-main-solo in GitHub settings and
#                    re-running without --solo.
# The posture is DECLARED by a human in state/mode (`posture=dual|solo`, default dual; the file
# is hook-protected and human-authored) and verified against what is actually installed — the
# server-side state never vouches for itself. --check fails (drift) when detected != declared;
# forge-main AND forge-main-solo together is drift. `apply` refuses --solo without the
# declaration, and refuses the downgrade direction (installing solo while forge-main exists).
#
# Exit codes are stable for forge-doctor (--check, and apply's final verification):
#   0 exact match, dual posture · 1 confirmed missing/drifted · 2 remote authority could not
#   be verified · 3 solo-posture match — DECLARED RESIDUAL: no server-side control requires a
#   human for default-branch merges (personal repos additionally lack the trusted
#   required-workflow leg)
set -euo pipefail

mode="apply"
solo=0
for arg in "$@"; do
  case "$arg" in
    apply) mode="apply" ;;
    --check) mode="--check" ;;
    --solo) solo=1 ;;
    *)
      echo "usage: $0 [--solo] [--check]" >&2
      exit 2
      ;;
  esac
done
if [ "$mode" = "--check" ] && [ "$solo" -eq 1 ]; then
  echo "--solo selects the posture to INSTALL; --check auto-detects the installed posture" >&2
  exit 2
fi

unverified() {
  echo "RULESETS_UNVERIFIED $1" >&2
  exit 2
}

command -v gh >/dev/null 2>&1 || unverified "gh CLI is not installed"
command -v python3 >/dev/null 2>&1 || unverified "python3 is not installed"

work=$(mktemp -d "${TMPDIR:-/tmp}/forge-rulesets.XXXXXX")
trap 'rm -rf "$work"' EXIT
policy_root=$(cd "$(dirname "$0")/.." && pwd)

# The review floor is a human declaration, not an inference from the artifact under
# verification. state/mode is in the immutable set (pre-commit + harness hooks); its
# committed/clean provenance is checked by forge-doctor, not here.
# The declaration is read from the WORKTREE; committed/clean provenance of state/mode is
# forge-doctor's check — run doctor, not just --check, when trust matters. Parsing is strict:
# the whole line must be exactly posture=dual or posture=solo, and exactly one such line.
declared_posture=dual
if [ -f "$policy_root/state/mode" ]; then
  posture_line_count=$(grep -cE '^posture=' "$policy_root/state/mode" || true)
  if [ "${posture_line_count:-0}" -gt 1 ]; then
    unverified "state/mode contains multiple posture= lines; keep exactly one"
  fi
  posture_line=$(grep -E '^posture=' "$policy_root/state/mode" || true)
  case "$posture_line" in
    "") declared_posture=dual ;;
    posture=dual) declared_posture=dual ;;
    posture=solo) declared_posture=solo ;;
    *)
      unverified "state/mode declares an unrecognized posture line: $posture_line"
      ;;
  esac
fi
if [ "$mode" = "apply" ]; then
  if [ "$solo" -eq 1 ] && [ "$declared_posture" != "solo" ]; then
    echo "FAIL --solo requires a human-declared posture: add 'posture=solo' to state/mode (a FORGE_HUMAN_OVERRIDE=1 human commit), then re-run" >&2
    exit 1
  fi
  if [ "$solo" -eq 0 ] && [ "$declared_posture" = "solo" ]; then
    echo "FAIL state/mode declares posture=solo but this is a dual-posture install; flip the declaration first (human commit) or pass --solo" >&2
    exit 1
  fi
fi

# These request bodies are the source of truth. API response metadata is removed before comparison;
# the complete conditions/rules values are then compared, so drift in parameters cannot hide behind
# a matching ruleset name. Bypass actors are separately checked: branch and tag-history rules
# permit none; the isolated tag-creation ruleset permits only named human User/Team actors.
# forge-main-solo is the solo-maintainer main-branch floor: identical walls minus required
# approvals (0) and code-owner review — the PR flow and green required checks still gate merges.
cat >"$work/forge-main.desired.json" <<'JSON'
{
  "name": "forge-main",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["~DEFAULT_BRANCH"],
      "exclude": []
    }
  },
  "rules": [
    {
      "type": "deletion"
    },
    {
      "type": "non_fast_forward"
    },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 1,
        "dismiss_stale_reviews_on_push": true,
        "require_code_owner_review": true,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["squash"]
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "do_not_enforce_on_create": false,
        "strict_required_status_checks_policy": true,
        "required_status_checks": [
          {
            "context": "ci"
          },
          {
            "context": "forge-policy"
          }
        ]
      }
    }
  ]
}
JSON

cat >"$work/forge-main-solo.desired.json" <<'JSON'
{
  "name": "forge-main-solo",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["~DEFAULT_BRANCH"],
      "exclude": []
    }
  },
  "rules": [
    {
      "type": "deletion"
    },
    {
      "type": "non_fast_forward"
    },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": true,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["squash"]
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "do_not_enforce_on_create": false,
        "strict_required_status_checks_policy": true,
        "required_status_checks": [
          {
            "context": "ci"
          },
          {
            "context": "forge-policy"
          }
        ]
      }
    }
  ]
}
JSON

cat >"$work/forge-tags.desired.json" <<'JSON'
{
  "name": "forge-tags",
  "target": "tag",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/tags/v*"],
      "exclude": []
    }
  },
  "rules": [
    {
      "type": "deletion"
    },
    {
      "type": "non_fast_forward"
    }
  ]
}
JSON

cat >"$work/forge-tag-creators.desired.json" <<'JSON'
{
  "name": "forge-tag-creators",
  "target": "tag",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/tags/v*"],
      "exclude": []
    }
  },
  "rules": [
    {
      "type": "creation"
    }
  ]
}
JSON

show_error() {
  while IFS= read -r line; do
    [ -n "$line" ] && echo "  $line" >&2
  done <"$1"
}

hint_plan_limit() {
  if grep -qi 'upgrade to github pro' "$1" 2>/dev/null; then
    echo "  HINT: GitHub Free cannot apply rulesets to PRIVATE repositories — upgrade the owner to GitHub Pro, or make the repository public, then re-run" >&2
  fi
}

if ! gh repo view --json nameWithOwner,defaultBranchRef >"$work/repo.json" 2>"$work/api.err"; then
  show_error "$work/api.err"
  unverified "cannot resolve a GitHub repository from this checkout"
fi
if ! repo_info=$(python3 - "$work/repo.json" <<'PY'
import json
import sys
import urllib.parse

try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
    repository = data["nameWithOwner"]
    default_branch = data["defaultBranchRef"]["name"]
except (OSError, KeyError, TypeError, json.JSONDecodeError):
    raise SystemExit(1)
if not isinstance(repository, str) or repository.count("/") != 1:
    raise SystemExit(1)
if not isinstance(default_branch, str) or not default_branch:
    raise SystemExit(1)
print(repository)
print(default_branch)
print(urllib.parse.quote(default_branch, safe=""))
PY
); then
  unverified "gh returned an invalid repository identity/default branch"
fi
repo=$(printf '%s\n' "$repo_info" | sed -n '1p')
default_branch=$(printf '%s\n' "$repo_info" | sed -n '2p')
default_branch_path=$(printf '%s\n' "$repo_info" | sed -n '3p')

if ! gh api \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "repos/$repo" \
  >"$work/repository.json" 2>"$work/api.err"; then
  show_error "$work/api.err"
  unverified "cannot read repository authority metadata"
fi
if ! authority_info=$(python3 - "$work/repository.json" "$default_branch" <<'PY'
import json
import sys

try:
    response = json.load(open(sys.argv[1], encoding="utf-8"))
    repository_id = response["id"]
    owner = response["owner"]["login"]
    owner_type = response["owner"]["type"]
    api_default = response["default_branch"]
except (OSError, KeyError, TypeError, json.JSONDecodeError):
    raise SystemExit(1)
if not isinstance(repository_id, int) or isinstance(repository_id, bool):
    raise SystemExit(1)
if not isinstance(owner, str) or not owner:
    raise SystemExit(1)
if owner_type not in {"Organization", "User"} or api_default != sys.argv[2]:
    raise SystemExit(1)
print(repository_id)
print(owner)
print(owner_type)
PY
); then
  unverified "GitHub returned invalid or inconsistent repository authority metadata"
fi
repository_id=$(printf '%s\n' "$authority_info" | sed -n '1p')
owner=$(printf '%s\n' "$authority_info" | sed -n '2p')
owner_type=$(printf '%s\n' "$authority_info" | sed -n '3p')
organization=
if [ "$owner_type" = "Organization" ]; then
  organization=$owner
fi
authority_name="forge-policy-$repository_id"

if [ -n "$organization" ]; then
  if ! python3 - \
    "$work/forge-authority.desired.json" \
    "$authority_name" \
    "$repository_id" \
    "$default_branch" <<'PY'
import json
import sys

path, name, repository_id_raw, default_branch = sys.argv[1:]
repository_id = int(repository_id_raw)
document = {
    "name": name,
    "target": "branch",
    "enforcement": "active",
    "conditions": {
        "repository_id": {"repository_ids": [repository_id]},
        "ref_name": {"include": ["~DEFAULT_BRANCH"], "exclude": []},
    },
    "rules": [
        {
            "type": "workflows",
            "parameters": {
                "do_not_enforce_on_create": False,
                "workflows": [
                    {
                        "repository_id": repository_id,
                        "path": ".github/workflows/forge-policy.yml",
                        "ref": f"refs/heads/{default_branch}",
                    }
                ],
            },
        }
    ],
}
with open(path, "w", encoding="utf-8") as handle:
    json.dump(document, handle, indent=2)
    handle.write("\n")
PY
  then
    unverified "cannot construct the trusted required-workflow policy"
  fi
fi

# Pin required contexts to the GitHub Actions app. Without an expected source, any repository
# writer or integration can publish a successful status with the same context name.
if ! gh api \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "apps/github-actions" \
  >"$work/actions-app.json" 2>"$work/api.err"; then
  show_error "$work/api.err"
  unverified "cannot resolve the GitHub Actions app identity"
fi
if ! actions_app_id=$(python3 - "$work/actions-app.json" <<'PY'
import json
import sys

try:
    response = json.load(open(sys.argv[1], encoding="utf-8"))
    app_id = response["id"]
    slug = response["slug"]
except (OSError, KeyError, TypeError, json.JSONDecodeError):
    raise SystemExit(1)
if slug != "github-actions" or not isinstance(app_id, int) or isinstance(app_id, bool):
    raise SystemExit(1)
print(app_id)
PY
); then
  unverified "GitHub returned an invalid Actions app identity"
fi
for bind_target in "$work/forge-main.desired.json" "$work/forge-main-solo.desired.json"; do
if ! python3 - "$bind_target" "$actions_app_id" <<'PY'
import json
import sys

path = sys.argv[1]
app_id = int(sys.argv[2])
document = json.load(open(path, encoding="utf-8"))
for rule in document["rules"]:
    if rule.get("type") != "required_status_checks":
        continue
    for check in rule["parameters"]["required_status_checks"]:
        check["integration_id"] = app_id
with open(path, "w", encoding="utf-8") as handle:
    json.dump(document, handle, indent=2)
    handle.write("\n")
PY
then
  unverified "cannot bind desired checks to the GitHub Actions app"
fi
done

fetch_ruleset_list() {
  if ! gh api \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "repos/$repo/rulesets?includes_parents=false&per_page=100" \
    >"$work/list.json" 2>"$work/api.err"; then
    show_error "$work/api.err"
    hint_plan_limit "$work/api.err"
    return 2
  fi
  if ! python3 - "$work/list.json" <<'PY'
import json
import sys

try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
except Exception:
    sys.exit(0)  # invalid responses are reported precisely by lookup_ruleset_id
sys.exit(3 if isinstance(data, list) and len(data) >= 100 else 0)
PY
  then
    echo "RULESETS_DRIFT repository ruleset list has 100+ entries for $repo — cannot verify completely (pagination unsupported); treating as drift"
    return 1
  fi
}

# Prints one ruleset id. Exit 10 = missing, 11 = duplicate, 12 = bad response.
lookup_ruleset_id() {
  python3 - "$1" "$2" "$3" <<'PY'
import json
import sys

try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
except (OSError, json.JSONDecodeError):
    raise SystemExit(12)
if not isinstance(data, list):
    raise SystemExit(12)
matches = []
for item in data:
    if not isinstance(item, dict) or item.get("name") != sys.argv[2]:
        continue
    # includes_parents=false should already exclude inherited policy. Keep this filter for GHES/API
    # versions that ignore the query parameter.
    if item.get("source_type") not in (None, sys.argv[3]):
        continue
    if not isinstance(item.get("id"), int) or isinstance(item.get("id"), bool):
        raise SystemExit(12)
    matches.append(item["id"])
if not matches:
    raise SystemExit(10)
if len(matches) != 1:
    raise SystemExit(11)
print(matches[0])
PY
}

fetch_ruleset() {
  local name="$1"
  local id="$2"
  if ! gh api \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "repos/$repo/rulesets/$id?includes_parents=false" \
    >"$work/$name.actual.json" 2>"$work/api.err"; then
    show_error "$work/api.err"
    return 2
  fi
}

fetch_authority_list() {
  if [ -z "$organization" ]; then
    echo "RULESETS_UNVERIFIED required workflows need an organization/enterprise ruleset; $repo is a personal repository" >&2
    return 2
  fi
  if ! gh api \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "orgs/$organization/rulesets?targets=branch&per_page=100" \
    >"$work/authority-list.json" 2>"$work/api.err"; then
    show_error "$work/api.err"
    hint_plan_limit "$work/api.err"
    return 2
  fi
  if ! python3 - "$work/authority-list.json" <<'PY'
import json
import sys

try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
except Exception:
    sys.exit(0)
sys.exit(3 if isinstance(data, list) and len(data) >= 100 else 0)
PY
  then
    echo "RULESETS_DRIFT organization ruleset list has 100+ entries for $organization — cannot verify completely (pagination unsupported); treating as drift"
    return 1
  fi
}

fetch_authority_ruleset() {
  local id="$1"
  if ! gh api \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "orgs/$organization/rulesets/$id" \
    >"$work/forge-authority.actual.json" 2>"$work/api.err"; then
    show_error "$work/api.err"
    return 2
  fi
}

compare_rulesets() {
  # Arguments: required-names-csv, then name desired.json actual.json per ruleset.
  # The comparator refuses (exit 2) unless the triple names equal the required set exactly,
  # so a future call-site edit cannot silently drop a ruleset from verification.
  python3 - "$@" <<'PY'
import copy
import difflib
import json
import sys

MANAGED_KEYS = ("name", "target", "enforcement", "conditions", "rules")


def load(path):
    try:
        data = json.load(open(path, encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        print(f"RULESETS_UNVERIFIED invalid JSON from {path}: {error}", file=sys.stderr)
        raise SystemExit(2)
    if not isinstance(data, dict):
        print(f"RULESETS_UNVERIFIED expected a JSON object from {path}", file=sys.stderr)
        raise SystemExit(2)
    return data


def normalize(document):
    # Strip only response-only top-level metadata. Conditions and rules remain complete values.
    result = {key: copy.deepcopy(document.get(key)) for key in MANAGED_KEYS}
    conditions = result.get("conditions")
    if isinstance(conditions, dict):
        for condition in conditions.values():
            if isinstance(condition, dict):
                for key in ("include", "exclude", "repository_ids"):
                    if isinstance(condition.get(key), list):
                        condition[key] = sorted(condition[key])

    rules = result.get("rules")
    if not isinstance(rules, list):
        return result
    for rule in rules:
        if not isinstance(rule, dict) or not isinstance(rule.get("parameters"), dict):
            continue
        parameters = rule["parameters"]
        if rule.get("type") == "pull_request":
            if isinstance(parameters.get("allowed_merge_methods"), list):
                parameters["allowed_merge_methods"] = sorted(
                    parameters["allowed_merge_methods"]
                )
            # GitHub may materialize these unset optional fields as empty response defaults.
            if parameters.get("required_reviewers") == []:
                parameters.pop("required_reviewers")
            restriction = parameters.get("dismissal_restriction")
            if (
                isinstance(restriction, dict)
                and not restriction.get("enabled", False)
                and not restriction.get("allowed_actors")
            ):
                parameters.pop("dismissal_restriction")
        if rule.get("type") == "required_status_checks":
            parameters.setdefault("do_not_enforce_on_create", False)
            checks = parameters.get("required_status_checks")
            if isinstance(checks, list):
                for check in checks:
                    if isinstance(check, dict) and check.get("integration_id") is None:
                        check.pop("integration_id", None)
                checks.sort(
                    key=lambda item: (
                        str(item.get("context", "")) if isinstance(item, dict) else "",
                        json.dumps(item, sort_keys=True),
                    )
                )
        if rule.get("type") == "workflows":
            parameters.setdefault("do_not_enforce_on_create", False)
            workflows = parameters.get("workflows")
            if isinstance(workflows, list):
                for workflow in workflows:
                    if isinstance(workflow, dict) and workflow.get("sha") is None:
                        workflow.pop("sha", None)
                workflows.sort(
                    key=lambda item: (
                        int(item.get("repository_id", -1))
                        if isinstance(item, dict)
                        and isinstance(item.get("repository_id"), int)
                        else -1,
                        str(item.get("path", "")) if isinstance(item, dict) else "",
                        str(item.get("ref", "")) if isinstance(item, dict) else "",
                        json.dumps(item, sort_keys=True),
                    )
                )
    rules.sort(
        key=lambda item: (
            str(item.get("type", "")) if isinstance(item, dict) else "",
            json.dumps(item, sort_keys=True),
        )
    )
    return result


def pretty(value):
    return json.dumps(value, indent=2, sort_keys=True).splitlines(keepends=True)


argv = sys.argv[1:]
if len(argv) < 4 or (len(argv) - 1) % 3:
    print("RULESETS_UNVERIFIED compare_rulesets received a malformed triple list", file=sys.stderr)
    raise SystemExit(2)
required_names = {name for name in argv[0].split(",") if name}
triples = argv[1:]
pairs = [
    (triples[index], load(triples[index + 1]), load(triples[index + 2]))
    for index in range(0, len(triples), 3)
]
supplied_names = {name for name, _, _ in pairs}
if not required_names or supplied_names != required_names:
    print(
        "RULESETS_UNVERIFIED compare_rulesets ruleset set mismatch: required "
        + ",".join(sorted(required_names))
        + " got "
        + ",".join(sorted(supplied_names)),
        file=sys.stderr,
    )
    raise SystemExit(2)
drift = False
for name, desired_raw, actual_raw in pairs:
    desired = normalize(desired_raw)
    actual = normalize(actual_raw)
    if desired != actual:
        drift = True
        print(f"RULESETS_DRIFT {name}")
        print(
            "".join(
                difflib.unified_diff(
                    pretty(desired),
                    pretty(actual),
                    fromfile=f"DESIRED/{name}.json",
                    tofile=f"ACTUAL/{name}.json",
                )
            ),
            end="",
        )

    if "bypass_actors" not in actual_raw:
        print(
            f"RULESETS_UNVERIFIED {name} bypass actors are hidden; use an account with "
            "ruleset write access",
            file=sys.stderr,
        )
        raise SystemExit(2)
    bypass = actual_raw["bypass_actors"]
    if not isinstance(bypass, list):
        print(f"RULESETS_UNVERIFIED {name} returned invalid bypass_actors", file=sys.stderr)
        raise SystemExit(2)
    if bypass:
        # Branch and tag-history bypasses defeat their walls and are always drift. The isolated
        # creation ruleset needs named humans because its rule otherwise blocks all release tags.
        expected_mode = "always"
        invalid = [
            actor
            for actor in bypass
            if name in {"forge-main", "forge-main-solo", "forge-tags", "forge-authority"}
            or not isinstance(actor, dict)
            or actor.get("actor_type") not in {"User", "Team"}
            or not isinstance(actor.get("actor_id"), int)
            or actor.get("bypass_mode", "always") != expected_mode
        ]
        if invalid:
            drift = True
            if name in {"forge-main", "forge-main-solo", "forge-tags", "forge-authority"}:
                print(f"RULESETS_DRIFT {name} must not have bypass actors")
            else:
                print(
                    "RULESETS_DRIFT forge-tag-creators bypass must contain only named "
                    "User/Team actors with mode=always"
                )

if drift:
    raise SystemExit(1)
PY
}

check_remote_policy_files() {
  local path key tree_sha

  # GitHub searches .github/, then the repository root, then docs/ and uses only the first
  # CODEOWNERS file it finds. Prove that the root policy below is the active one; otherwise an
  # exact match at CODEOWNERS would be irrelevant.
  if ! gh api \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "repos/$repo/commits/$default_branch_path" \
    >"$work/default-commit.json" 2>"$work/api.err"; then
    show_error "$work/api.err"
    echo "RULESETS_UNVERIFIED cannot resolve the remote default-branch tree" >&2
    return 2
  fi
  if ! tree_sha=$(python3 - "$work/default-commit.json" <<'PY'
import json
import re
import sys

try:
    value = json.load(open(sys.argv[1], encoding="utf-8"))["commit"]["tree"]["sha"]
except (OSError, KeyError, TypeError, json.JSONDecodeError):
    raise SystemExit(1)
if not isinstance(value, str) or not re.fullmatch(r"[0-9a-fA-F]{40,64}", value):
    raise SystemExit(1)
print(value)
PY
  ); then
    echo "RULESETS_UNVERIFIED invalid default-branch commit response" >&2
    return 2
  fi
  if ! gh api \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "repos/$repo/git/trees/$tree_sha?recursive=1" \
    >"$work/default-tree.json" 2>"$work/api.err"; then
    show_error "$work/api.err"
    echo "RULESETS_UNVERIFIED cannot inspect CODEOWNERS precedence" >&2
    return 2
  fi
  if python3 - "$work/default-tree.json" <<'PY'
import json
import sys

try:
    response = json.load(open(sys.argv[1], encoding="utf-8"))
    entries = response["tree"]
except (OSError, KeyError, TypeError, json.JSONDecodeError) as error:
    print(f"RULESETS_UNVERIFIED invalid default-branch tree response: {error}", file=sys.stderr)
    raise SystemExit(2)
if response.get("truncated") is True:
    print("RULESETS_UNVERIFIED default-branch tree response was truncated", file=sys.stderr)
    raise SystemExit(2)
if not isinstance(entries, list):
    print("RULESETS_UNVERIFIED default-branch tree is not an array", file=sys.stderr)
    raise SystemExit(2)
files = {
    entry.get("path")
    for entry in entries
    if isinstance(entry, dict) and entry.get("type") == "blob"
}
shadowing = sorted(files & {".github/CODEOWNERS"})
if shadowing:
    print(
        "RULESETS_DRIFT higher-precedence CODEOWNERS shadows the required root policy: "
        + ", ".join(shadowing)
    )
    raise SystemExit(1)
if "CODEOWNERS" not in files:
    print("RULESETS_DRIFT root CODEOWNERS is absent from the remote default branch")
    raise SystemExit(1)
if ".github/workflows/forge-policy.yml" not in files:
    print("RULESETS_DRIFT forge-policy workflow is absent from the remote default branch")
    raise SystemExit(1)
PY
  then
    :
  else
    return $?
  fi

  for path in .github/workflows/forge-policy.yml CODEOWNERS; do
    key=$(printf '%s' "$path" | tr '/.' '__')
    if ! gh api \
      -H "Accept: application/vnd.github+json" \
      -H "X-GitHub-Api-Version: 2022-11-28" \
      "repos/$repo/contents/$path" \
      >"$work/$key.remote.json" 2>"$work/api.err"; then
      show_error "$work/api.err"
      echo "RULESETS_UNVERIFIED cannot read $path from the remote default branch" >&2
      return 2
    fi
    if python3 - "$work/$key.remote.json" "$policy_root/$path" "$path" <<'PY'
import base64
import difflib
import json
import sys

try:
    response = json.load(open(sys.argv[1], encoding="utf-8"))
    remote = base64.b64decode(response["content"], validate=False)
    local = open(sys.argv[2], "rb").read()
except (OSError, KeyError, TypeError, ValueError, json.JSONDecodeError) as error:
    print(
        f"RULESETS_UNVERIFIED invalid remote content response for {sys.argv[3]}: {error}",
        file=sys.stderr,
    )
    raise SystemExit(2)
if response.get("type") != "file" or response.get("encoding") != "base64":
    print(
        f"RULESETS_UNVERIFIED remote {sys.argv[3]} is not a base64-encoded regular file",
        file=sys.stderr,
    )
    raise SystemExit(2)
if remote != local:
    print(f"RULESETS_DRIFT remote default-branch {sys.argv[3]} differs from DESIRED")
    try:
        desired_lines = local.decode("utf-8").splitlines(keepends=True)
        actual_lines = remote.decode("utf-8").splitlines(keepends=True)
    except UnicodeDecodeError:
        print("  (binary content mismatch)")
    else:
        print(
            "".join(
                difflib.unified_diff(
                    desired_lines,
                    actual_lines,
                    fromfile=f"DESIRED/{sys.argv[3]}",
                    tofile=f"ACTUAL/{sys.argv[3]}",
                )
            ),
            end="",
        )
    raise SystemExit(1)
PY
    then
      :
    else
      return $?
    fi
  done

  if ! gh api \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "repos/$repo/codeowners/errors" \
    >"$work/codeowners-errors.json" 2>"$work/api.err"; then
    show_error "$work/api.err"
    echo "RULESETS_UNVERIFIED cannot validate remote CODEOWNERS" >&2
    return 2
  fi
  python3 - "$work/codeowners-errors.json" <<'PY'
import json
import sys

try:
    response = json.load(open(sys.argv[1], encoding="utf-8"))
    errors = response["errors"]
except (OSError, KeyError, TypeError, json.JSONDecodeError) as error:
    print(f"RULESETS_UNVERIFIED invalid CODEOWNERS error response: {error}", file=sys.stderr)
    raise SystemExit(2)
if not isinstance(errors, list):
    print("RULESETS_UNVERIFIED CODEOWNERS errors is not an array", file=sys.stderr)
    raise SystemExit(2)
if errors:
    print("RULESETS_DRIFT remote CODEOWNERS contains GitHub validation errors")
    for error in errors:
        if isinstance(error, dict):
            line = error.get("line", "?")
            message = error.get("message") or error.get("kind") or "unknown error"
            print(f"  line {line}: {str(message).splitlines()[0]}")
        else:
            print(f"  {error}")
    raise SystemExit(1)
PY
}

check_rulesets() {
  local name id lookup_rc main_name main_id expected_main solo_personal_residual compare_rc fetch_list_rc
  expected_main=forge-main
  if [ "$declared_posture" = "solo" ]; then
    expected_main=forge-main-solo
  fi
  fetch_list_rc=0
  fetch_ruleset_list || fetch_list_rc=$?
  if [ "$fetch_list_rc" -eq 1 ]; then
    return 1
  elif [ "$fetch_list_rc" -ne 0 ]; then
    echo "RULESETS_UNVERIFIED cannot read repository rulesets for $repo" >&2
    return 2
  fi

  # Review-floor posture is auto-detected: exactly one of forge-main (dual) / forge-main-solo.
  main_name=""
  main_id=""
  for name in forge-main forge-main-solo; do
    if id=$(lookup_ruleset_id "$work/list.json" "$name" "Repository"); then
      if [ -n "$main_name" ]; then
        if [ "$declared_posture" = "dual" ]; then
          echo "RULESETS_DRIFT both forge-main and forge-main-solo exist on $repo — finish graduation: forge-main is active; delete forge-main-solo in GitHub settings"
        else
          echo "RULESETS_DRIFT both forge-main and forge-main-solo exist on $repo — ambiguous review floor; remove one in GitHub settings"
        fi
        return 1
      fi
      main_name=$name
      main_id=$id
    else
      lookup_rc=$?
      case "$lookup_rc" in
        10) ;;
        11)
          echo "RULESETS_DRIFT multiple repository rulesets named $name on $repo"
          return 1
          ;;
        *)
          echo "RULESETS_UNVERIFIED invalid ruleset list response for $repo" >&2
          return 2
          ;;
      esac
    fi
  done
  if [ -z "$main_name" ]; then
    echo "RULESETS_DRIFT missing $expected_main on $repo (state/mode declares posture=$declared_posture)"
    return 1
  fi
  if [ "$main_name" != "$expected_main" ]; then
    echo "RULESETS_DRIFT installed review floor is $main_name but state/mode declares posture=$declared_posture (expected $expected_main) — posture changes are a human-authored state/mode commit, then re-run this script"
    return 1
  fi
  if ! fetch_ruleset "$main_name" "$main_id"; then
    echo "RULESETS_UNVERIFIED cannot read $main_name details on $repo" >&2
    return 2
  fi

  for name in forge-tags forge-tag-creators; do
    if id=$(lookup_ruleset_id "$work/list.json" "$name" "Repository"); then
      :
    else
      lookup_rc=$?
      case "$lookup_rc" in
        10)
          echo "RULESETS_DRIFT missing $name on $repo"
          return 1
          ;;
        11)
          echo "RULESETS_DRIFT multiple repository rulesets named $name on $repo"
          return 1
          ;;
        *)
          echo "RULESETS_UNVERIFIED invalid ruleset list response for $repo" >&2
          return 2
          ;;
      esac
    fi
    if ! fetch_ruleset "$name" "$id"; then
      echo "RULESETS_UNVERIFIED cannot read $name details on $repo" >&2
      return 2
    fi
  done

  solo_personal_residual=0
  if [ -z "$organization" ] && [ "$main_name" = "forge-main-solo" ]; then
    # Solo posture on a personal repository: GitHub offers no trusted required-workflow ruleset
    # outside organizations/enterprises. The residual is declared in the verdict line instead of
    # failing verification forever; dual posture keeps the strict behavior below.
    solo_personal_residual=1
  else
    fetch_list_rc=0
    fetch_authority_list || fetch_list_rc=$?
    if [ "$fetch_list_rc" -eq 1 ]; then
      return 1
    elif [ "$fetch_list_rc" -ne 0 ]; then
      echo "RULESETS_UNVERIFIED cannot confirm a trusted default-branch required workflow for $repo" >&2
      return 2
    fi
    if id=$(lookup_ruleset_id "$work/authority-list.json" "$authority_name" "Organization"); then
      :
    else
      lookup_rc=$?
      case "$lookup_rc" in
        10)
          echo "RULESETS_DRIFT missing trusted required-workflow ruleset $authority_name on $organization"
          return 1
          ;;
        11)
          echo "RULESETS_DRIFT multiple organization rulesets named $authority_name on $organization"
          return 1
          ;;
        *)
          echo "RULESETS_UNVERIFIED invalid organization ruleset list response for $organization" >&2
          return 2
          ;;
      esac
    fi
    if ! fetch_authority_ruleset "$id"; then
      echo "RULESETS_UNVERIFIED cannot read trusted required-workflow details on $organization" >&2
      return 2
    fi
  fi

  if [ "$solo_personal_residual" -eq 1 ]; then
    compare_rulesets "$main_name,forge-tags,forge-tag-creators" \
      "$main_name" "$work/$main_name.desired.json" "$work/$main_name.actual.json" \
      forge-tags "$work/forge-tags.desired.json" "$work/forge-tags.actual.json" \
      forge-tag-creators "$work/forge-tag-creators.desired.json" "$work/forge-tag-creators.actual.json"
  else
    compare_rulesets "$main_name,forge-tags,forge-tag-creators,forge-authority" \
      "$main_name" "$work/$main_name.desired.json" "$work/$main_name.actual.json" \
      forge-tags "$work/forge-tags.desired.json" "$work/forge-tags.actual.json" \
      forge-tag-creators "$work/forge-tag-creators.desired.json" "$work/forge-tag-creators.actual.json" \
      forge-authority "$work/forge-authority.desired.json" "$work/forge-authority.actual.json"
  fi
  compare_rc=$?
  if [ "$compare_rc" -ne 0 ]; then
    return "$compare_rc"
  fi

  if check_remote_policy_files; then
    :
  else
    return $?
  fi

  if [ "$solo_personal_residual" -eq 1 ]; then
    echo "RULESETS_MATCH $repo: repo rulesets exact (solo posture) + policy-file content verified — RESIDUAL: no server-side control requires a human for default-branch merges; any write-capable principal (including an agent token) can change ci.yml/forge-policy.yml — and any hook-protected path, state/mode included — via a self-merged PR (protection there is the client belts + this content comparison), and personal repos have no trusted required-workflow leg"
    return 3
  fi
  if [ "$main_name" = "forge-main-solo" ]; then
    echo "RULESETS_MATCH $repo: exact repo/org rulesets (solo posture) + trusted workflow + CODEOWNERS confirmed — RESIDUAL: no server-side human gate on default-branch merges (0 required approvals; self-merge after green checks)"
    return 3
  fi
  echo "RULESETS_MATCH $repo: exact repo/org rulesets + trusted workflow + CODEOWNERS confirmed"
  return 0
}

if [ "$mode" = "--check" ]; then
  set +e
  check_rulesets
  rc=$?
  set -e
  exit "$rc"
fi

if [ "$solo" -eq 1 ]; then
  posture=solo
  main_ruleset=forge-main-solo
  conflict_ruleset=forge-main
else
  posture=dual
  main_ruleset=forge-main
  conflict_ruleset=forge-main-solo
fi
echo "Installing missing Forge rulesets on $repo (posture: $posture; existing rulesets are never overwritten)"
apply_list_rc=0
fetch_ruleset_list || apply_list_rc=$?
if [ "$apply_list_rc" -eq 1 ]; then
  exit 1
elif [ "$apply_list_rc" -ne 0 ]; then
  unverified "cannot read repository rulesets for $repo"
fi

conflict_probe_rc=0
lookup_ruleset_id "$work/list.json" "$conflict_ruleset" "Repository" >/dev/null 2>&1 || conflict_probe_rc=$?
case "$conflict_probe_rc" in
  0|11)
    if [ "$solo" -eq 1 ]; then
      # Downgrade direction: never leave main momentarily unprotected, and never delete.
      echo "  FAIL posture downgrade refused: forge-main already exists on $repo — this script never deletes rulesets; removing the stricter floor is a deliberate human act in GitHub settings" >&2
      exit 1
    fi
    # Graduation (upgrade) direction: overlap is safe — rulesets aggregate to the most
    # restrictive result — so the stricter set is created BEFORE the human deletes the weaker.
    echo "  graduation overlap: forge-main-solo still exists; after forge-main is active, delete forge-main-solo in GitHub settings (--check reports the overlap as drift until then)"
    ;;
  10) ;;
  *)
    unverified "invalid ruleset list response for $repo"
    ;;
esac

for name in "$main_ruleset" forge-tags forge-tag-creators; do
  if id=$(lookup_ruleset_id "$work/list.json" "$name" "Repository"); then
    echo "  exists: $name ($id) — verifying exact contents"
    continue
  else
    lookup_rc=$?
    case "$lookup_rc" in
      10) ;;
      11)
        echo "  FAIL duplicate rulesets named $name; remove the ambiguity in GitHub settings" >&2
        exit 1
        ;;
      *)
        unverified "invalid ruleset list response for $repo"
        ;;
    esac
  fi

  if gh api \
    -X POST \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "repos/$repo/rulesets" \
    --input "$work/$name.desired.json" \
    >"$work/$name.created.json" 2>"$work/api.err"; then
    echo "  created: $name"
  else
    echo "  FAIL could not create $name" >&2
    show_error "$work/api.err"
    hint_plan_limit "$work/api.err"
    exit 1
  fi
done

skip_authority=0
if [ -z "$organization" ] && [ "$solo" -eq 1 ]; then
  skip_authority=1
  echo "  solo posture on a personal repository: skipping the trusted required-workflow leg (organization/enterprise only)"
fi
if [ "$skip_authority" -eq 0 ]; then
apply_list_rc=0
fetch_authority_list || apply_list_rc=$?
if [ "$apply_list_rc" -eq 1 ]; then
  exit 1
elif [ "$apply_list_rc" -ne 0 ]; then
  unverified "repository rules are installed, but a trusted required workflow could not be configured"
fi
if id=$(lookup_ruleset_id "$work/authority-list.json" "$authority_name" "Organization"); then
  echo "  exists: $authority_name ($id) — verifying exact contents"
else
  lookup_rc=$?
  case "$lookup_rc" in
    10) ;;
    11)
      echo "  FAIL duplicate organization rulesets named $authority_name" >&2
      exit 1
      ;;
    *)
      unverified "invalid organization ruleset list response for $organization"
      ;;
  esac

  if gh api \
    -X POST \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "orgs/$organization/rulesets" \
    --input "$work/forge-authority.desired.json" \
    >"$work/forge-authority.created.json" 2>"$work/api.err"; then
    echo "  created: $authority_name (trusted default-branch workflow)"
  else
    echo "  FAIL could not create trusted required-workflow ruleset $authority_name" >&2
    show_error "$work/api.err"
    hint_plan_limit "$work/api.err"
    exit 1
  fi
fi
fi

set +e
check_rulesets
rc=$?
set -e
case "$rc" in
  0|3) ;;
  *) exit "$rc" ;;
esac

echo
echo "NOTE: forge-tag-creators blocks ALL v* tag creation until a named human User/Team is added"
echo "to that ruleset's bypass list. Its creation authority is isolated: forge-tags has no bypass,"
echo "so release tags still cannot be deleted or force-moved. Broad roles/apps/keys are drift."
echo "forge-main / forge-main-solo bypasses are always drift: they bypass the policy/check wall."
if [ "$solo" -eq 1 ]; then
  echo "SOLO POSTURE: forge-main-solo allows self-merge once required checks pass — the"
  echo "independent-review leg lives in the Forge process (forge-review), not on the server."
  echo "Graduate before production stakes: delete forge-main-solo, re-run this script without --solo."
else
  echo "A solo maintainer therefore needs a second human CODEOWNER for protected changes."
fi
if [ "$skip_authority" -eq 0 ]; then
  echo "The organization required-workflow rule has no bypass. Changing ci.yml or forge-policy.yml"
  echo "therefore requires an organization ruleset administrator to make an explicit out-of-band change."
else
  echo "PERSONAL-REPO RESIDUAL: no trusted required-workflow rule exists here — ci.yml and"
  echo "forge-policy.yml can be changed by any write-capable principal via a self-merged PR;"
  echo "their protection is the client belts + the --check content comparison in this script."
fi
exit "$rc"
