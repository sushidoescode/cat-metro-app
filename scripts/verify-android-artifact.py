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


def run(args):
    done = subprocess.run([str(a) for a in args], capture_output=True, text=True)
    return done.returncode, done.stdout, done.stderr


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

    # An App Bundle is jar-signed, so jarsigner is the right reader; -strict returns non-zero on
    # any disqualifying signer condition and prints "jar verified, with signer errors." while
    # still containing "jar verified", so require the clean phrase AND the clean exit code. An
    # APK may be v2/v3-only and then carries no META-INF manifest at all, which jarsigner reports
    # as "no manifest." -- apksigner is the reader for those.
    if is_bundle:
        code, signed, _ = run([JARSIGNER, "-verify", "-strict", aab])
        first = signed.strip().splitlines()[0] if signed.strip() else "no output"
        report.check("jarsigner reports the bundle verified, with no signer errors",
                     code == 0 and "jar verified." in signed,
                     first + " (exit " + str(code) + ")")
        code, cert, _ = run([KEYTOOL, "-printcert", "-jarfile", aab])
    else:
        apksigner = next(iter(sorted((ANDROID / "SDK/build-tools").glob("*/apksigner"),
                                     reverse=True)), None)
        if apksigner is None:
            print("no apksigner under the pinned Android SDK build-tools")
            return 2
        # apksigner is a shell wrapper that needs a JRE on PATH; point it at the pinned one
        # rather than whatever the login shell happens to have.
        code, cert, err = run_with_java([apksigner, "verify", "--print-certs", "--verbose", aab])
        report.check("apksigner reports the apk verified", code == 0,
                     (err or cert).strip().splitlines()[0] if code else
                     ", ".join(line.strip() for line in cert.splitlines()
                               if line.startswith("Verified using")))
    owner = re.search(r"(?:Owner|Signer #1 certificate DN):\s*(.+)", cert)
    cert_sha = re.search(r"(?:SHA256:\s*([0-9A-F:]+)"
                         r"|Signer #1 certificate SHA-256 digest:\s*([0-9a-f]+))", cert)
    cert_sha = (cert_sha[1] or cert_sha[2]) if cert_sha else None
    is_debug = bool(owner and "Android Debug" in owner[1])
    report.check("signed by a real key, not the Android debug key",
                 args.allow_debug_signature or not is_bundle or not is_debug,
                 (owner[1].strip() if owner else "no Owner line"))
    if is_debug and (args.allow_debug_signature or not is_bundle):
        print("      NOTE: debug-signed. This is a packaging rehearsal artifact and must never "
              "be uploaded.")

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
        "signer_owner": owner[1].strip() if owner else None,
        "signer_cert_sha256": cert_sha,
        "checks": report.rows,
        "passed": not report.failures,
    }
    out_path = aab.with_suffix(aab.suffix + ".verify.json")
    out_path.write_text(json.dumps(receipt, indent=2) + "\n")
    print()
    print("receipt   " + str(out_path))
    if report.failures:
        print("RESULT    FAIL — " + str(len(report.failures)) + " check(s): "
              + ", ".join(r["check"] for r in report.failures))
        print("          Do not upload this bundle.")
        return 1
    print("RESULT    PASS — every check above. A human still performs the upload.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
