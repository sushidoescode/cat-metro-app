#!/usr/bin/env bash
# The shipped wardrobe is tied to one named RevenueCat offering. A dashboard operator changing
# Current must never redirect the release build to an unrelated product set.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "revenuecat-offering-selection.test.sh: FAIL — $*" >&2; exit 1; }

accepts_named_only() {
  candidate="$1"
  code="$(sed 's://.*::' "$candidate")"
  grep -q 'RevenueCatNames.CosmeticsOffering' <<<"$code" \
    && grep -q 'TryGetValue' <<<"$code" \
    && ! grep -Eq 'offerings[.]Current|offering[[:space:]]*[?][?]=' <<<"$code"
}

backend="unity/Assets/Scripts/Integrations/RevenueCat/RevenueCatBackend.cs"
accepts_named_only "$backend" \
  || fail "backend can fall back from the required cosmetics offering to Current"

runbook="docs/runbooks/revenuecat-setup.md"
runbook_text="$(tr '\n' ' ' < "$runbook")"
grep -Eqi 'does not fall back to the[[:space:]]+current offering' <<<"$runbook_text" \
  || fail "human setup guide does not describe named-offering fail-closed behavior"

# Balanced mutation: it retains the required named lookup and then adds the unsafe fallback. The
# checker must reject that union rather than counting the good line and overlooking the bad one.
fixture="$(mktemp)"
trap 'rm -f -- "$fixture"' EXIT
cat > "$fixture" <<'EOF'
var offering = offerings.All.TryGetValue(RevenueCatNames.CosmeticsOffering, out var named)
    ? named
    : null;
offering ??= offerings.Current;
EOF
if accepts_named_only "$fixture"; then
  fail "checker accepted a balanced named-plus-Current fallback mutation"
fi

echo "revenuecat-offering-selection.test.sh: OK"
