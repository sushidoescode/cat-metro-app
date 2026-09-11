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
| 2026-08-30 | `build/CatMetro-1.0.0-2.aab` | 2 | 1.0.0 | custom (`CN=Sushant Srikrish`, SHA256withRSA, 2048-bit, valid to 2054-01-14) | not recorded | unknown — **ask Console** | GUI build. Same exclusion problem. |
| 2026-09-11 | `build/CatMetro-main-88ae1ddc-20260911-run02.apk` | 1 | 1.0.0 | debug | `b2a61858fecfb655ff9360242746f3ac6a2746e4bfb7b0f50ed5877162f375b5` | no — APK, sideload only | Verification APK, 23/23 checks. Excludes OneSignal/LevelPlay; carries RevenueCat + Billing. |
| _next_ | `build/CatMetro-1.0.0-<N>.aab` | _from Console max + 1_ | 1.0.0 | custom | | **human-only upload** | Fill from the verification commands. |

## What to record for the release bundle

- versionCode and versionName as typed into the Unity window
- the AAB SHA-256 from `shasum -a 256`
- the signer `Owner:` line and certificate SHA-256 from `keytool -printcert -jarfile`
- the permission list from `bundletool dump manifest`
- the level count from `unzip -l "$AAB" | grep -c 'base/assets/content/levels/L0'` (must be 60)
- whether `UnityConnectSettings.m_Enabled` was 0 or 1 in the tree at build time
- the git HEAD at build time, and `git status --porcelain | grep -c '^ M'` (must be 9)
