#!/usr/bin/env bash
# Server-side enforcement as code: GitHub rulesets for the current repo.
# This is the wall that client hooks cannot be (pre-commit is bypassable with --no-verify).
# Requires: gh CLI authenticated with repository admin plus organization ruleset administration.
# HUMAN runs/approves this — agents stage it only. Personal repositories and plans without required
# ruleset workflows can install the defense-in-depth repository rules, but remain UNVERIFIED because
# GitHub cannot run the policy definition from a separately trusted default-branch source there.
#   ./scripts/setup-rulesets.sh          # create missing repo + trusted org rulesets, then verify
#   ./scripts/setup-rulesets.sh --check  # read-only: exact desired-v-actual policy comparison
#
# --check exit codes are stable for forge-doctor:
#   0 exact match · 1 confirmed missing/drifted · 2 remote authority could not be verified
set -euo pipefail

mode="${1:-apply}"
case "$mode" in
  apply|--check) ;;
  *)
    echo "usage: $0 [--check]" >&2
    exit 2
    ;;
esac

unverified() {
  echo "RULESETS_UNVERIFIED $1" >&2
  exit 2
}

command -v gh >/dev/null 2>&1 || unverified "gh CLI is not installed"
command -v python3 >/dev/null 2>&1 || unverified "python3 is not installed"

work=$(mktemp -d "${TMPDIR:-/tmp}/forge-rulesets.XXXXXX")
trap 'rm -rf "$work"' EXIT
policy_root=$(cd "$(dirname "$0")/.." && pwd)

# These request bodies are the source of truth. API response metadata is removed before comparison;
# the complete conditions/rules values are then compared, so drift in parameters cannot hide behind
# a matching ruleset name. Bypass actors are separately checked: branch and tag-history rules
# permit none; the isolated tag-creation ruleset permits only named human User/Team actors.
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
if ! python3 - "$work/forge-main.desired.json" "$actions_app_id" <<'PY'
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

fetch_ruleset_list() {
  if ! gh api \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "repos/$repo/rulesets?includes_parents=false&per_page=100" \
    >"$work/list.json" 2>"$work/api.err"; then
    show_error "$work/api.err"
    return 2
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
    return 2
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
  python3 - \
    "$work/forge-main.desired.json" "$work/forge-main.actual.json" \
    "$work/forge-tags.desired.json" "$work/forge-tags.actual.json" \
    "$work/forge-tag-creators.desired.json" "$work/forge-tag-creators.actual.json" \
    "$work/forge-authority.desired.json" "$work/forge-authority.actual.json" <<'PY'
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


pairs = [
    ("forge-main", load(sys.argv[1]), load(sys.argv[2])),
    ("forge-tags", load(sys.argv[3]), load(sys.argv[4])),
    ("forge-tag-creators", load(sys.argv[5]), load(sys.argv[6])),
    ("forge-authority", load(sys.argv[7]), load(sys.argv[8])),
]
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
            if name in {"forge-main", "forge-tags", "forge-authority"}
            or not isinstance(actor, dict)
            or actor.get("actor_type") not in {"User", "Team"}
            or not isinstance(actor.get("actor_id"), int)
            or actor.get("bypass_mode", "always") != expected_mode
        ]
        if invalid:
            drift = True
            if name in {"forge-main", "forge-tags", "forge-authority"}:
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
  local name id lookup_rc
  if ! fetch_ruleset_list; then
    echo "RULESETS_UNVERIFIED cannot read repository rulesets for $repo" >&2
    return 2
  fi

  for name in forge-main forge-tags forge-tag-creators; do
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

  if ! fetch_authority_list; then
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

  if compare_rulesets; then
    :
  else
    return $?
  fi

  if check_remote_policy_files; then
    :
  else
    return $?
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

echo "Installing missing Forge rulesets on $repo (existing rulesets are never overwritten)"
if ! fetch_ruleset_list; then
  unverified "cannot read repository rulesets for $repo"
fi

for name in forge-main forge-tags forge-tag-creators; do
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
    exit 1
  fi
done

if ! fetch_authority_list; then
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
    exit 1
  fi
fi

set +e
check_rulesets
rc=$?
set -e
[ "$rc" -eq 0 ] || exit "$rc"

echo
echo "NOTE: forge-tag-creators blocks ALL v* tag creation until a named human User/Team is added"
echo "to that ruleset's bypass list. Its creation authority is isolated: forge-tags has no bypass,"
echo "so release tags still cannot be deleted or force-moved. Broad roles/apps/keys are drift."
echo "forge-main bypasses are always drift because they bypass the required policy/review wall."
echo "A solo maintainer therefore needs a second human CODEOWNER for protected changes."
echo "The organization required-workflow rule has no bypass. Changing ci.yml or forge-policy.yml"
echo "therefore requires an organization ruleset administrator to make an explicit out-of-band change."
