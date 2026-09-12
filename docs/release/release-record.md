# Release record — the exact settings each bundle was cut with

The Android signing and version fields live in `unity/ProjectSettings/ProjectSettings.asset`, which
is one of the nine permanently-dirty protected files and is **never committed** — it carries the
human's keystore path and alias. This file is the tracked record of what the shipped bundle
actually used, so the values survive in git without the credentials doing so.

Fill a row in immediately after each build, from the Unity window and the verification commands in
`signed-aab-local-steps.md`. Never paste a password here or anywhere else.

## Current committed defaults

| Field | Committed MAIN | Note |
|---|---|---|
| `bundleVersion` | `1.0.0` | `ProjectSettings.asset:149` |
| `AndroidBundleVersionCode` | `1` | `ProjectSettings.asset:180` — **almost certainly burned**; confirm the Console maximum before building |
| `androidUseCustomKeystore` | `0` | `1` in the working tree; never commit that hunk |
| `AndroidKeystoreName` / `AndroidKeyaliasName` | empty | filled in the working tree only |
| `UnityConnectSettings.m_Enabled` | `0` | `1` in the working tree — decide which ships |
| Target / min SDK | 36 / 25 | |
| Architectures | ARM64 only | |
| Scripting backend | IL2CPP | |

## Builds

| Date | File | versionCode | versionName | Signing | AAB SHA-256 | Uploaded? | Notes |
|---|---|---|---|---|---|---|---|
| 2026-08-29 | `build/CatMetro-1.0.0-1.aab` | 1 | 1.0.0 | custom (`CN=Sushant Srikrish`) | not recorded | unknown | GUI build. **Predates the SDK-export transform — contains OneSignal/Firebase. Do not upload.** |
| 2026-08-30 | `build/CatMetro-1.0.0-2.aab` | 2 | 1.0.0 | custom (`CN=Sushant Srikrish`, SHA256withRSA, 2048-bit, valid to 2054-01-14) | `a9be8b9d46c37c3e09a8fd92eae150421821cda778f1e7ef7a44f531c07e5dbc` | unknown — **ask Console** | GUI build. **Verified 2026-09-12: FAILS six checks** — 22 extra permissions, `jarsigner` exit 4 "with signer errors", 19 of 60 levels, altered level bytes, 4 OneSignal resources, 1430 OneSignal + 467 Firebase dex classes. Receipt `build/CatMetro-1.0.0-2.aab.verify.json`. **Do not upload.** |
| 2026-09-11 | `build/CatMetro-main-88ae1ddc-20260911-run02.apk` | 1 | 1.0.0 | debug | `b2a61858fecfb655ff9360242746f3ac6a2746e4bfb7b0f50ed5877162f375b5` | no — APK, sideload only | Verification APK, 23/23 checks. Excludes OneSignal/LevelPlay; carries RevenueCat + Billing. |
| _next_ | `build/CatMetro-1.0.0-<N>.aab` | _from Console max + 1_ | 1.0.0 | custom | | **human-only upload** | Fill from the verification commands. |

## Verifying a bundle

`python3 scripts/verify-android-artifact.py <bundle> --expect-version-code <N> --expect-cert-sha256 <fingerprint>`
runs every check below in one pass and writes `<bundle>.verify.json` beside it. Attach that receipt
to the row. Exit 0 = upload-ready; 1 = a real defect; **3 = sound and correctly signed, but the
Console fingerprint was not supplied so upload readiness is unconfirmed**.

Signing is checked as three separate things — signature integrity, certificate health, certificate
identity — with chain trust reported as a note only, because an Android upload certificate is
self-signed and can never chain to a public CA. Prove the verifier is live before trusting it:

```sh
python3 scripts/verify-android-artifact.py build/CatMetro-1.0.0-2.aab   # must FAIL
bash tests/unity/aab-signature-controls.test.sh                         # must print OK
```

## What to record for the release bundle

- versionCode and versionName as typed into the Unity window
- the AAB SHA-256 from `shasum -a 256`
- the signer `Owner:` line and certificate SHA-256 from `keytool -printcert -jarfile`
- the permission list from `bundletool dump manifest`
- the level count from `unzip -l "$AAB" | grep -c 'base/assets/content/levels/L0'` (must be 60)
- whether `UnityConnectSettings.m_Enabled` was 0 or 1 in the tree at build time
- the git HEAD at build time, and `git status --porcelain | grep -c '^ M'` (must be 9)
- the `<bundle>.verify.json` receipt from `scripts/verify-android-artifact.py`, run WITH the
  Console fingerprint so `certificate_identity_confirmed` is true

## Two independent gates, both required

The **version code** is a Console fact. The **keystore passwords** are yours and live only in
Unity's session memory, so they must be typed into the Unity window at build time and re-typed
after every editor relaunch. Knowing the version code does not make the build possible; no agent
session can supply the passwords. See `signed-aab-local-steps.md`.
