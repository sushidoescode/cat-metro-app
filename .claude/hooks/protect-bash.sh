#!/usr/bin/env bash
# PreToolUse hook (matcher: Bash): close the shell lane around the immutable paths.
# Edit/Write denies don't cover `sed -i`, redirection, `git checkout <ref> <path>`, `ln -s`, etc.
# This hook denies a Bash command that pairs a write/restore-shaped operation with a protected path.
#
# Design (learned from a self-review regression): match the protected path as a boundary-anchored
# SUBSTRING of the (lowercased) command — this catches absolute, quoted, and dot-segmented spellings
# that a token-normalizer misses, and it fails CLOSED. It deliberately over-blocks reads performed
# THROUGH an interpreter/ln (use cat/grep/diff for reads of protected files). It does NOT catch shell
# obfuscation (variable indirection, `$(...)`, base64|bash) — belt 2 cannot, by construction; the git
# pre-commit hook (belt 3, sees real staged paths) and the fail-closed consumers (forge-trust.sh only
# counts git-COMMITTED attested evidence) are the actual wall for those. Reads stay allowed.
# The command flows in via STDIN (no env/argv, so no ARG_MAX ceiling on ~MB commands); the fixed
# script is the small -c argument.

command -v python3 >/dev/null 2>&1 || { echo "protect-bash.sh: python3 not found — blocking command (fail-closed)" >&2; exit 2; }

# NOTE: the Python below must contain NO single-quote character (it is wrapped in bash single quotes).
python3 -c '
import json, re, sys
try:
    data = json.load(sys.stdin)
except Exception:
    print("protect-bash.sh: unparseable input — blocking (fail-closed)", file=sys.stderr); sys.exit(2)
cmd = (data.get("tool_input", {}) if isinstance(data, dict) else {}).get("command", "")
if cmd == "":
    sys.exit(0)
if not isinstance(cmd, str):
    print("protect-bash.sh: non-string command — blocking (fail-closed)", file=sys.stderr); sys.exit(2)

writeish = re.compile(
    r">|>>|\btee\b|\brm\b|\bmv\b|\bcp\b|\bln\b|\bpatch\b|\brsync\b|\bed\b|\bchown\b"
    r"|sed[ \t]+-i|g?awk[ \t]+-i|\btruncate\b|\bchmod\b"
    r"|git[ \t]+(checkout|restore)\b|\bdd[ \t][^|]*\bof="
    r"|(^|[;&|][ \t]*)install[ \t]"
    r"|\b(python3?|perl|ruby|node|deno)[ \t]+-(c|e)\b|\bperl[ \t]+-[^ \t]*i")
if not writeish.search(cmd):
    sys.exit(0)

# Lowercase (case-insensitive FS) and drop quote/backslash chars that are shell-transparent inside a
# path (docs/con"stitution".md, docs/constitution\.md) so they cannot split a protected substring.
# (Glob/brace/variable/base64 expansion is still shell-transparent and out of scope for belt 2.)
low = re.sub(r"[\"\\]", "", cmd.lower())
low = low.replace(chr(39), "")  # strip the single-quote char too (cannot appear literally above)

def deny(msg):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
          "permissionDecision": "deny", "permissionDecisionReason": msg}})); sys.exit(0)

REASON = ("Command pairs a write/restore operation with an immutable path (tests/contract/, "
          "docs/constitution.md, .claude/hooks/, scripts/git-hooks/, state/mode, state/trust.json, "
          "evals/ non-results, evals/results/attested/); changes require a human-authored PR")

# Boundary so a legit sibling like evals/results/attested-notes/ is NOT caught (the '-' after
# "attested" fails the boundary; a real attested file is always evals/results/attested/<f>).
if re.search(r"evals/results/attested(?:/|$|[^a-z0-9_.-])", low):
    deny(REASON)
low = re.sub(r"evals/results/[a-z0-9_.~/-]*",
             lambda m: " " if ".." not in m.group(0) else m.group(0), low)

BL = r"(?:^|[^\w.-])"
for pat in (BL + r"tests/contract/",
            BL + r"\.claude/hooks/",
            BL + r"scripts/git-hooks/",
            BL + r"\.github/workflows/",
            BL + r"evals/",
            BL + r"docs/constitution\.md",
            BL + r"state/mode(?:$|[^\w.-])",
            BL + r"state/trust\.json",
            BL + r"(?:\.github/|docs/)?codeowners(?:$|[^\w.-])"):
    if re.search(pat, low):
        deny(REASON)
sys.exit(0)
'
