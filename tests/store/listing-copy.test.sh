#!/usr/bin/env bash
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

python3 - <<'PY'
from pathlib import Path

path = Path("docs/store/assets/listing-copy.md")
text = path.read_text(encoding="utf-8")
blocks = []
for part in text.split("```text\n")[1:4]:
    blocks.append(part.split("\n```", 1)[0])
if len(blocks) != 3:
    raise SystemExit("listing-copy: expected exactly three paste-ready text blocks")

title, short, full = blocks
limits = {"title": (title, 30), "short": (short, 80), "full": (full, 4000)}
for name, (value, limit) in limits.items():
    if len(value) > limit:
        raise SystemExit(f"listing-copy: {name} is {len(value)} characters, limit={limit}")

expected_counts = {"title": 23, "short": 80, "full": 406}
for name, expected in expected_counts.items():
    observed = len(limits[name][0])
    if observed != expected:
        raise SystemExit(f"listing-copy: {name} count={observed}, expected={expected}")

phrases = {
    "train puzzle": (1, 1, 1),
    "cat puzzle": (0, 0, 1),
    "metro puzzle": (0, 0, 1),
    "route puzzle": (0, 0, 1),
    "no forced ads": (0, 0, 0),
}
for phrase, expected in phrases.items():
    observed = tuple(value.lower().count(phrase) for value in blocks)
    if observed != expected:
        raise SystemExit(f"listing-copy: {phrase!r} placement={observed}, expected={expected}")

for held in ("daily", "rewarded", "loot box", "energy timer", "purchase", "19", "60"):
    if any(held in value.lower() for value in blocks):
        raise SystemExit(f"listing-copy: held term {held!r} leaked into paste-ready copy")

print("listing-copy: OK")
PY
