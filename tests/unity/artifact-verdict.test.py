#!/usr/bin/env python3
"""The final-verdict decision table of scripts/verify-android-artifact.py.

Separate from the signature controls on purpose. Those build real artifacts and prove the three
signing CHECKS behave; this proves the VERDICT built from those checks behaves. The distinction
matters because the bug this exists to prevent slipped through a green control suite: the identity
row was correctly reported as FAIL, and the verdict then exempted it unconditionally — turning a
known certificate mismatch into "unconfirmed" on a bundle and into an outright PASS on an apk.

Every case here is CONTENT-CLEAN unless it is deliberately testing a content failure, so no other
failing row can mask the outcome under test. Building a real release bundle is not required to
test a decision table, and waiting for one would leave it untested.
"""
import importlib.util
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
spec = importlib.util.spec_from_file_location(
    "verifier", ROOT / "scripts/verify-android-artifact.py")
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)

IDENTITY = "signer certificate matches the Console upload certificate"

failures = []


def row(name, status, detail=""):
    return {"check": name, "pass": status == "pass", "status": status, "detail": detail}


def clean(*extra):
    """The rows a sound artifact produces: manifest, permissions, signature, levels, SDKs."""
    base = [row(n, "pass") for n in (
        "bundletool validate", "package is com.catmetro.game", "versionName is 1.0.0",
        "minSdkVersion 25", "targetSdkVersion 36", "allowBackup is false", "not debuggable",
        "permission set is exactly the five allowed", "no advertising-ID permission",
        "signature integrity: every entry is covered by a valid signature",
        "signer certificate is healthy: not expired, not pre-dated, no disabled algorithm",
        "signer certificate extracted", "signed by a real key, not the Android debug key",
        "staged corpus is 60 levels",
        "bundle carries all 60 levels at the path Unity emits",
        "every packaged level is byte-identical to the staged source",
        "no ad or push SDK file in the bundle", "dex tables readable",
        "no ad or push SDK class in the dex",
        "RevenueCat and Play Billing classes are present", "ARM64 only")]
    return base + list(extra)


def expect(label, rows, is_bundle, want_result, want_exit):
    result, status = verifier.decide_result(rows, is_bundle)
    ok = (result, status) == (want_result, want_exit)
    print(("PASS   " if ok else "FAIL   ") + label
          + "  ->  " + result + " exit " + str(status)
          + ("" if ok else "   EXPECTED " + want_result + " exit " + str(want_exit)))
    if not ok:
        failures.append(label)


# --- the three outcomes the verdict must distinguish, on clean content -----------------
expect("bundle, fingerprint supplied and MATCHED",
       clean(row(IDENTITY, "pass")), True, "PASS", 0)

expect("bundle, fingerprint NOT supplied",
       clean(row(IDENTITY, "unconfirmed", "NOT SUPPLIED")), True, "UNCONFIRMED", 3)

expect("bundle, fingerprint supplied and MISMATCHED",
       clean(row(IDENTITY, "fail", "artifact=aaaa expected=bbbb")), True, "FAIL", 1)

# The apk half of the same rule. An apk is not the release upload, so identity is normally not
# applicable -- but if a fingerprint IS supplied and does not match, that is a defect, not a pass.
expect("apk, fingerprint supplied and MISMATCHED",
       clean(row(IDENTITY, "fail", "artifact=aaaa expected=bbbb")), False, "FAIL", 1)

expect("apk, fingerprint supplied and MATCHED",
       clean(row(IDENTITY, "pass")), False, "PASS", 0)

expect("apk, no fingerprint: identity not applicable, no row emitted",
       clean(), False, "PASS", 0)

# --- an unconfirmed row must never soften a real failure -------------------------------
expect("bundle, content broken AND fingerprint not supplied",
       clean(row("no ad or push SDK class in the dex", "fail", "Lcom/onesignal/=1430"),
             row(IDENTITY, "unconfirmed", "NOT SUPPLIED")), True, "FAIL", 1)

expect("bundle, content broken but identity confirmed",
       clean(row("every packaged level is byte-identical to the staged source", "fail"),
             row(IDENTITY, "pass")), True, "FAIL", 1)

# --- a signing failure decides the verdict on its own ----------------------------------
expect("bundle, signature integrity failed (tampered), identity confirmed",
       [r for r in clean() if "signature integrity" not in r["check"]]
       + [row("signature integrity: every entry is covered by a valid signature", "fail",
              "digest error"), row(IDENTITY, "pass")], True, "FAIL", 1)

expect("bundle, certificate expired, identity confirmed",
       [r for r in clean() if "is healthy" not in r["check"]]
       + [row("signer certificate is healthy: not expired, not pre-dated, no disabled algorithm",
              "fail", "has expired"), row(IDENTITY, "pass")], True, "FAIL", 1)

# --- degenerate shapes -----------------------------------------------------------------
expect("bundle, several unconfirmed rows and nothing failing",
       clean(row(IDENTITY, "unconfirmed"), row("something else", "unconfirmed")),
       True, "UNCONFIRMED", 3)

expect("no rows at all",
       [], True, "PASS", 0)

print()
if failures:
    print("artifact-verdict.test.py: " + str(len(failures)) + " FAILED — "
          + ", ".join(failures))
    sys.exit(1)
print("artifact-verdict.test.py: OK")
