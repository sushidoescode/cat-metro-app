#!/usr/bin/env python3
"""Verify an Android artifact before a human uploads it. Reads only; never uploads.

    python3 scripts/verify-android-artifact.py build/CatMetro-1.0.0-3.aab
    python3 scripts/verify-android-artifact.py build/CatMetro-<sha>-<date>.apk

Takes either an .aab (the release artifact) or an .apk (a local sideload build, which needs no
keystore and so can be produced without a human at the keyboard). The checks are the same; only
the zip layout differs -- an App Bundle nests everything under base/, an APK does not -- and an
APK is expected to carry the Android debug signature.

Prints one PASS/FAIL line per check and writes a JSON receipt next to the bundle. Exits
non-zero if anything fails. No secret is read, printed or written: the signer's public
certificate is public information, and the keystore passwords live only in Unity's session
memory (which is exactly why this script cannot build the bundle -- see
docs/release/signed-aab-local-steps.md).

Refuses to be vacuous: run it against build/CatMetro-1.0.0-2.aab and it must FAIL, because
that bundle predates the SDK-export transform and still carries OneSignal and Firebase.
"""
import argparse
import hashlib
import json
import re
import struct
import subprocess
import sys
import zipfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ANDROID = Path("/Applications/Unity/Hub/Editor/6000.3.16f1/PlaybackEngines/AndroidPlayer")
BUNDLETOOL = ANDROID / "Tools/bundletool-all-1.17.2.jar"
JAVA = ANDROID / "OpenJDK/bin/java"
JARSIGNER = ANDROID / "OpenJDK/bin/jarsigner"
KEYTOOL = ANDROID / "OpenJDK/bin/keytool"

PACKAGE = "com.catmetro.game"
VERSION_NAME = "1.0.0"
MIN_SDK, TARGET_SDK = 25, 36
LEVEL_DIR = REPO / "unity/Assets/StreamingAssets/content/levels"

# Unity maps Assets/StreamingAssets/X to assets/X, and an App Bundle nests the base split.
LAYOUT = {
    ".aab": {"levels": "base/assets/content/levels/", "dex": r"base/dex/classes\d*\.dex",
             "lib": "base/lib/"},
    ".apk": {"levels": "assets/content/levels/", "dex": r"classes\d*\.dex", "lib": "lib/"},
}

# Exactly these, and nothing else. AD_ID, POST_NOTIFICATIONS and the C2DM pair appearing here
# means the SDK-export transform did not run.
PERMISSIONS = {
    "android.permission.INTERNET",
    "android.permission.ACCESS_NETWORK_STATE",
    "android.permission.VIBRATE",
    "com.android.vending.BILLING",
    "com.catmetro.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION",
}

# Must not ship. Checked in the zip listing AND in the dex class table, because a zip name can
# be absent while the classes are merged into classesN.dex.
FORBIDDEN_ENTRY = re.compile(r"onesignal|ironsource|unity3d/mediation", re.I)
# Firebase is named by COMPONENT, not wholesale. com.google.firebase.encoders{,-json,-proto} is a
# standalone serialization utility that Play Services and Play Billing pull in transitively: it
# creates no FirebaseApp, generates no identifier and makes no request. The 2026-09-11 APK carries
# 41 such classes plus three exception types and ZERO of the components below, which is why the
# Data safety declaration does not list a Firebase identifier. Forbidding the whole namespace
# would fail a clean build on a library that collects nothing.
FORBIDDEN_CLASS = ("Lcom/onesignal/", "Lcom/ironsource/", "Lcom/unity3d/mediation/",
                   "Lcom/unity3d/ads/",
                   "Lcom/google/firebase/FirebaseApp;", "Lcom/google/firebase/installations/",
                   "Lcom/google/firebase/messaging/", "Lcom/google/firebase/analytics/",
                   "Lcom/google/firebase/crashlytics/", "Lcom/google/firebase/iid/",
                   "Lcom/google/android/gms/measurement/")
# Must ship: the monetisation path the Data safety declaration is written against.
REQUIRED_CLASS = {"revenuecat": "Lcom/revenuecat/purchases/",
                  "billing_client": "Lcom/android/billingclient/api/"}


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def normalise_xmltree(xmltree):
    """aapt2 xmltree -> the android:name="value" text shape the bundletool checks read.

    aapt2 prints one `E: <tag>` line then its attributes on following `A:` lines, so an
    attribute cannot be matched in the same line as its element and a regex spanning lines
    happily reads the NEXT element's attributes. Walk it instead.
    """
    lines, out, current, attrs = xmltree.splitlines(), [], None, {}

    def flush():
        if current is None:
            return
        rendered = " ".join('android:' + k + '="' + v + '"' for k, v in attrs.items())
        out.append("<" + current + " " + rendered + ">")

    for line in lines:
        element = re.match(r"\s*E: ([\w-]+)", line)
        if element:
            flush()
            current, attrs = element[1], {}
            continue
        attribute = re.match(r'\s*A: (?:http://schemas\.android\.com/apk/res/android:)?'
                             r'([\w:]+)(?:\([^)]*\))?=(.*)', line)
        if attribute and current is not None:
            value = attribute[2].strip()
            quoted = re.search(r'"([^"]*)"', value)
            number = re.search(r"\(type 0x[0-9a-f]+\)0x([0-9a-f]+)", value)
            attrs[attribute[1].split(":")[-1]] = (
                quoted[1] if quoted else str(int(number[1], 16)) if number else value)
    flush()
    # `package` is a bare attribute on <manifest>, and the checks read it unprefixed.
    return "\n".join(out).replace('<manifest android:package=', '<manifest package=')


def run(args, c_locale=False):
    import os
    env = None
    if c_locale:   # jarsigner's diagnostics are parsed by text; pin the language.
        env = dict(os.environ, LC_ALL="C", LANG="C")
    done = subprocess.run([str(a) for a in args], capture_output=True, text=True, env=env)
    return done.returncode, done.stdout, done.stderr


def normalise_fingerprint(value):
    """Console prints AA:BB:..., apksigner prints aabb...; compare them on one form."""
    return re.sub(r"[^0-9a-f]", "", (value or "").lower())


def jarsigner_errors(output):
    """The lines of jarsigner's `Error:` block, which is what actually distinguishes a benign
    self-signed chain from an expired certificate. Both produce strict status 4."""
    lines, collecting, found = output.splitlines(), False, []
    for line in lines:
        if re.match(r"^Error:\s*$", line):
            collecting = True
            continue
        if collecting and re.match(r"^Warning:\s*$", line):
            break
        if collecting and line.strip():
            found.append(line.strip())
    return found


def run_with_java(args):
    import os
    env = dict(os.environ)
    env["JAVA_HOME"] = str(ANDROID / "OpenJDK")
    env["PATH"] = str(ANDROID / "OpenJDK/bin") + ":" + env.get("PATH", "")
    done = subprocess.run([str(a) for a in args], capture_output=True, text=True, env=env)
    return done.returncode, done.stdout, done.stderr


def dex_classes(data):
    """Class names from a DEX class table. Same reader as the APK receipt tool."""
    assert data[:4] == b"dex\n" and data[7] == 0, "unsupported DEX format"
    assert struct.unpack_from("<I", data, 36)[0] == 112, "unsupported DEX header"
    strings_count, strings_at, types_count, types_at = struct.unpack_from("<4I", data, 56)
    classes_count, classes_at = struct.unpack_from("<2I", data, 96)
    assert classes_at + classes_count * 32 <= len(data)
    names = []
    for index in range(classes_count):
        type_index = struct.unpack_from("<I", data, classes_at + index * 32)[0]
        assert type_index < types_count
        string_index = struct.unpack_from("<I", data, types_at + type_index * 4)[0]
        assert string_index < strings_count
        offset = struct.unpack_from("<I", data, strings_at + string_index * 4)[0]
        for _ in range(5):
            byte = data[offset]
            offset += 1
            if not byte & 128:
                break
        else:
            raise AssertionError("invalid DEX string length")
        names.append(data[offset:data.index(0, offset)].decode("utf-8", errors="replace"))
    return names


class Report:
    def __init__(self):
        self.rows = []

    def check(self, name, ok, detail=""):
        self.rows.append({"check": name, "pass": bool(ok), "detail": detail})
        print(("PASS  " if ok else "FAIL  ") + name + (("  — " + detail) if detail else ""))
        return bool(ok)

    @property
    def failures(self):
        return [r for r in self.rows if not r["pass"]]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("bundle", type=Path)
    parser.add_argument("--expect-version-code", type=int, default=None,
                        help="the code you typed into Unity; omit to record whatever is there")
    parser.add_argument("--allow-debug-signature", action="store_true",
                        help="for a CLI packaging rehearsal that is NEVER uploaded")
    parser.add_argument("--expect-cert-sha256", default=None,
                        help="the upload certificate SHA-256 from Console (Release > Setup > App "
                             "integrity). Either AA:BB:.. or aabb.. Upload readiness cannot be "
                             "established without it.")
    args = parser.parse_args()
    aab = args.bundle.resolve()
    report = Report()
    layout = LAYOUT.get(aab.suffix.lower())
    if layout is None:
        print("expected a .aab or a .apk, got: " + aab.suffix)
        return 2
    is_bundle = aab.suffix.lower() == ".aab"
    level_prefix, dex_pattern, lib_prefix = layout["levels"], layout["dex"], layout["lib"]

    if not aab.is_file():
        print("no such artifact: " + str(aab))
        return 2
    for tool in (JAVA, JARSIGNER, KEYTOOL, BUNDLETOOL):
        if not tool.exists():
            print("missing pinned Android tool: " + str(tool))
            return 2

    digest = sha256(aab.read_bytes())
    print("bundle    " + str(aab))
    print("sha256    " + digest)
    print("bytes     " + str(aab.stat().st_size))
    print()

    if is_bundle:
        code, out, err = run([JAVA, "-jar", BUNDLETOOL, "validate", "--bundle", aab])
        report.check("bundletool validate", code == 0,
                     (err or out).strip().splitlines()[-1] if code else "")
        code, manifest, err = run([JAVA, "-jar", BUNDLETOOL, "dump", "manifest", "--bundle", aab])
        report.check("bundletool dump manifest", code == 0, err.strip()[:200] if code else "")
    else:
        aapt = next(iter(sorted((ANDROID / "SDK/build-tools").glob("*/aapt2"), reverse=True)), None)
        if aapt is None:
            print("no aapt2 under the pinned Android SDK build-tools")
            return 2
        code, xmltree, err = run([aapt, "dump", "xmltree", "--file", "AndroidManifest.xml", aab])
        report.check("aapt2 dump manifest", code == 0, err.strip()[:200] if code else "")
        manifest = normalise_xmltree(xmltree)

    def attr(name):
        found = re.search(r'android:' + name + r'="([^"]*)"', manifest)
        return found[1] if found else None

    package = re.search(r'package="([^"]*)"', manifest)
    report.check("package is " + PACKAGE, package and package[1] == PACKAGE,
                 package[1] if package else "absent")
    report.check("versionName is " + VERSION_NAME, attr("versionName") == VERSION_NAME,
                 str(attr("versionName")))
    version_code = attr("versionCode")
    if args.expect_version_code is None:
        report.check("versionCode recorded", version_code is not None, str(version_code))
        print("      NOTE: versionCode " + str(version_code) + " is only valid if it is strictly "
              "greater than the highest code Play has ever seen on any track, drafts included.")
    else:
        report.check("versionCode is " + str(args.expect_version_code),
                     str(version_code) == str(args.expect_version_code), str(version_code))
    min_sdk = re.search(r'minSdkVersion="?(?:0x)?([0-9a-f]+)"?', manifest)
    target_sdk = re.search(r'targetSdkVersion="?(?:0x)?([0-9a-f]+)"?', manifest)
    report.check("minSdkVersion " + str(MIN_SDK),
                 min_sdk and int(min_sdk[1], 16 if not min_sdk[1].isdigit() else 10) == MIN_SDK,
                 min_sdk[1] if min_sdk else "absent")
    report.check("targetSdkVersion " + str(TARGET_SDK),
                 target_sdk and int(target_sdk[1], 16 if not target_sdk[1].isdigit() else 10) == TARGET_SDK,
                 target_sdk[1] if target_sdk else "absent")
    report.check("allowBackup is false", attr("allowBackup") in ("false", "0x0"),
                 str(attr("allowBackup")))
    debuggable = 'android:debuggable="true"' in manifest
    if is_bundle:
        report.check("not debuggable", not debuggable)
    elif debuggable:
        print("      NOTE: debuggable — expected for a local sideload APK, disqualifying for a "
              "release bundle.")

    declared = set(re.findall(r'uses-permission[^>]*android:name="([^"]+)"', manifest))
    report.check("permission set is exactly the five allowed",
                 declared == PERMISSIONS,
                 "extra=" + ",".join(sorted(declared - PERMISSIONS))
                 + " missing=" + ",".join(sorted(PERMISSIONS - declared)))
    report.check("no advertising-ID permission",
                 "com.google.android.gms.permission.AD_ID" not in declared)

    # ---- signing: three SEPARATE questions, deliberately not conflated ----------------
    #
    #   integrity  — is every byte in the archive covered by a valid signature?
    #   identity   — WHICH certificate signed it? (the only thing Console can corroborate)
    #   chain trust— does Java trust a CA chain up from that certificate?
    #
    # An Android upload certificate is self-signed BY DESIGN, so chain trust always fails and
    # must never, on its own, fail a release. Measured on real controls built from a real
    # bundle (scripts/test-aab-signature-controls.sh):
    #
    #   valid self-signed  rc=4   "jar verified, with signer errors" + invalid-chain + self-signed
    #   EXPIRED cert       rc=4   the same two, PLUS "signer certificate has expired"
    #   unsigned           rc=0   "jar is unsigned."
    #   entry added later  rc=20  "unsigned entries which have not been integrity-checked"
    #   tampered byte      rc=1   SecurityException: SHA-256 digest error
    #
    # So requiring rc==0 rejects every real release, and allowing rc==4 admits an expired
    # certificate. The exit code is not the signal; the Error block is.
    CHAIN_ERRORS = ("This jar contains entries whose certificate chain is invalid",
                    "This jar contains entries whose signer certificate is self-signed")
    cert_sha = owner = None
    chain_note = ""
    if is_bundle:
        code, signed, _ = run([JARSIGNER, "-verify", "-strict", aab], c_locale=True)
        errors = jarsigner_errors(signed)
        verified = re.search(r"^jar verified(, with signer errors)?\.?$", signed, re.M) is not None
        unsigned = ("jar is unsigned." in signed or "no manifest." in signed
                    or any("unsigned entries" in e for e in errors) or bool(code & 16))
        digest_error = "SecurityException" in signed or "digest error" in signed
        report.check("signature integrity: every entry is covered by a valid signature",
                     verified and not unsigned and not digest_error,
                     ("unsigned" if unsigned else "digest error" if digest_error
                      else "not verified") if not (verified and not unsigned and not digest_error)
                     else "jarsigner strict status " + str(code))

        unexpected = [e for e in errors if not any(e.startswith(c) for c in CHAIN_ERRORS)]
        report.check("signer certificate is healthy: not expired, not pre-dated, "
                     "no disabled algorithm",
                     not unexpected, "; ".join(unexpected)[:240])
        chain_note = ("self-signed with no CA chain — normal and expected for an Android "
                      "upload key" if any(e.startswith(CHAIN_ERRORS[1]) for e in errors)
                      else "no self-signed diagnostic reported")

        code, cert, _ = run([KEYTOOL, "-printcert", "-jarfile", aab])
    else:
        apksigner = next(iter(sorted((ANDROID / "SDK/build-tools").glob("*/apksigner"),
                                     reverse=True)), None)
        if apksigner is None:
            print("no apksigner under the pinned Android SDK build-tools")
            return 2
        code, cert, err = run_with_java([apksigner, "verify", "--print-certs", "--verbose", aab])
        report.check("signature integrity: apksigner verifies the apk", code == 0,
                     (err or cert).strip().splitlines()[0] if code else
                     ", ".join(line.strip() for line in cert.splitlines()
                               if line.startswith("Verified using")))
        chain_note = "apk signature schemes are self-contained; no CA chain is involved"

    owner_match = re.search(r"(?:Owner|Signer #1 certificate DN):\s*(.+)", cert)
    sha_match = re.search(r"(?:SHA256:\s*([0-9A-Fa-f:]+)"
                          r"|Signer #1 certificate SHA-256 digest:\s*([0-9a-fA-F]+))", cert)
    owner = owner_match[1].strip() if owner_match else None
    cert_sha = normalise_fingerprint(sha_match[1] or sha_match[2]) if sha_match else None
    report.check("signer certificate extracted", cert_sha is not None,
                 (owner or "no Owner line") + ("  sha256=" + cert_sha if cert_sha else ""))
    print("      NOTE: certificate-chain trust — " + chain_note + ". Not a release gate.")

    is_debug = bool(owner and "Android Debug" in owner)
    report.check("signed by a real key, not the Android debug key",
                 args.allow_debug_signature or not is_bundle or not is_debug,
                 owner or "no Owner line")
    if is_debug and (args.allow_debug_signature or not is_bundle):
        print("      NOTE: debug-signed. This is a local sideload/rehearsal artifact and must "
              "never be uploaded.")

    # Identity is the ONLY signing fact a machine here cannot establish alone: the tool can say
    # which certificate signed the bundle, but only Console knows which certificate Play expects.
    identity_confirmed = False
    if args.expect_cert_sha256:
        expected = normalise_fingerprint(args.expect_cert_sha256)
        identity_confirmed = cert_sha is not None and cert_sha == expected
        report.check("signer certificate matches the Console upload certificate",
                     identity_confirmed,
                     "artifact=" + (cert_sha or "none") + " expected=" + expected)
    elif is_bundle:
        report.check("signer certificate matches the Console upload certificate", False,
                     "NOT SUPPLIED — pass --expect-cert-sha256 from Console ▸ Release ▸ Setup ▸ "
                     "App integrity. Upload readiness cannot be established without it.")

    with zipfile.ZipFile(aab) as bundle:
        names = bundle.namelist()

        staged = sorted(p.name for p in LEVEL_DIR.glob("L*.json"))
        report.check("staged corpus is 60 levels", len(staged) == 60, str(len(staged)))
        packaged = sorted(n[len(level_prefix):] for n in names
                          if n.startswith(level_prefix) and n.endswith(".json"))
        report.check("bundle carries all 60 levels at the path Unity emits",
                     packaged == staged,
                     "packaged=" + str(len(packaged)) + " missing="
                     + ",".join(sorted(set(staged) - set(packaged))[:5])
                     + " extra=" + ",".join(sorted(set(packaged) - set(staged))[:5]))
        altered = [name for name in packaged
                   if name in staged
                   and sha256(bundle.read(level_prefix + name)) != sha256((LEVEL_DIR / name).read_bytes())]
        report.check("every packaged level is byte-identical to the staged source",
                     not altered, ",".join(altered[:5]))

        offenders = sorted({n for n in names if FORBIDDEN_ENTRY.search(n)})
        report.check("no ad or push SDK file in the bundle", not offenders,
                     str(len(offenders)) + " entries e.g. " + "; ".join(offenders[:3]))

        classes = []
        dex_names = [n for n in names if re.fullmatch(dex_pattern, n)]
        for name in dex_names:
            classes.extend(dex_classes(bundle.read(name)))
        report.check("dex tables readable", bool(dex_names),
                     str(len(dex_names)) + " dex, " + str(len(classes)) + " classes")
        hits = {prefix: sum(name.startswith(prefix) for name in classes)
                for prefix in FORBIDDEN_CLASS}
        report.check("no ad or push SDK class in the dex", not any(hits.values()),
                     ", ".join(k + "=" + str(v) for k, v in hits.items() if v))
        present = {label: sum(name.startswith(prefix) for name in classes)
                   for label, prefix in REQUIRED_CLASS.items()}
        report.check("RevenueCat and Play Billing classes are present",
                     all(present.values()),
                     ", ".join(k + "=" + str(v) for k, v in present.items()))

        abis = sorted({re.search(re.escape(lib_prefix) + r"([^/]+)/", n)[1] for n in names
                       if n.startswith(lib_prefix)})
        report.check("ARM64 only", abis == ["arm64-v8a"], ",".join(abis) or "no native libs")

    receipt = {
        "artifact": str(aab), "kind": "aab" if is_bundle else "apk", "sha256": digest, "bytes": aab.stat().st_size,
        "package": package[1] if package else None, "versionName": attr("versionName"),
        "versionCode": version_code, "minSdk": MIN_SDK, "targetSdk": TARGET_SDK,
        "permissions": sorted(declared),
        "signer_owner": owner,
        "signer_cert_sha256": cert_sha,
        "certificate_identity_confirmed": identity_confirmed,
        "chain_trust_note": chain_note,
        "checks": report.rows,
        "passed": not report.failures,
    }
    out_path = aab.with_suffix(aab.suffix + ".verify.json")
    out_path.write_text(json.dumps(receipt, indent=2) + "\n")
    print()
    print("receipt   " + str(out_path))
    identity_row = "signer certificate matches the Console upload certificate"
    blocking = [r for r in report.failures if r["check"] != identity_row]
    if blocking:
        print("RESULT    FAIL — " + str(len(blocking)) + " check(s): "
              + ", ".join(r["check"] for r in blocking))
        print("          Do not upload this artifact.")
        return 1
    if is_bundle and not identity_confirmed:
        print("RESULT    CONTENT AND SIGNATURE OK, UPLOAD READINESS UNCONFIRMED.")
        print("          The bundle is internally sound and signed by "
              + (owner or "an unknown certificate") + " (sha256 " + (cert_sha or "?") + "),")
        print("          but nothing here can say that is the certificate Play expects. Re-run")
        print("          with --expect-cert-sha256 <Console fingerprint> before uploading.")
        return 3
    print("RESULT    PASS — every check above. A human still performs the upload.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
