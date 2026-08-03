# setup-rulesets.sh show_error: trailing blank line in api.err exits 1 under `set -e` — still present in v3.6.0

**Kit version:** v3.6.0 (`ac973be`) · **Component:** `template/scripts/setup-rulesets.sh`
**Follow-up to:** the original report filed 2026-08-02 (gh startup failure dying mid-diagnosis).

## Symptom (as originally observed live)

When `gh` fails at startup (e.g. `failed to load config: open ~/.config/gh/config.yml`), its
stderr ends with a blank line. `show_error` then returns 1, and under `set -euo pipefail`
(line 31) the script dies **between `show_error` and `unverified()`** — a bare exit 1 that
forge-doctor misreads as DRIFT instead of the honest UNVERIFIED exit 2.

## Root cause, still shipping in v3.6.0

```sh
show_error() {
  while IFS= read -r line; do
    [ -n "$line" ] && echo "  $line" >&2
  done <"$1"
}
```

When the last line read is empty, `[ -n "$line" ]` (status 1) is the loop body's final
command, so the loop — and the function — return 1. All 13+ call sites are unguarded
(`show_error "$work/api.err"` bare), so `set -e` kills the script.

## Fix (the cat-metro-app local version)

```sh
show_error() {
  # A trailing blank line in the error file must not become the loop's (and thus the
  # function's) exit status: under set -e that kills the script between show_error and
  # unverified(), turning "cannot verify" into a bare exit 1 that doctor misreads as
  # DRIFT. Observed live with gh's config-load failure output.
  while IFS= read -r line; do
    if [ -n "$line" ]; then
      echo "  $line" >&2
    fi
  done <"$1"
  return 0
}
```

## Upgrade-friction note

Because the template still ships the buggy form, forge-upgrade 3.6.0 classifies the locally
fixed file as `both-changed | conflict` — every dogfooding project that applied this fix must
re-decline the template on every upgrade until it merges upstream.
