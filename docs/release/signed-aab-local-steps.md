# Building the signed release AAB — local steps, and what I need from Console

Written 2026-09-11 against MAIN `ba603c76`. **Passwords are typed into the Unity window and
nowhere else** — never into a terminal, a script, a file, or this conversation.

## Why the GUI, and what I got wrong before

I previously wrote that `scripts/build-aab.sh` "cannot sign". That was wrong as stated: the
wrapper accepts `signing=custom` (receipt regex `:542`, the debug refusal at `:551-555` fires only
for `signing=debug`, and the success epilogue at `:728-735` is written for the custom case). What
it lacks is a **password seam**, and that is this repo's deliberate policy —
`tests/unity/cli-aab-build.test.sh:56-59` fails the build if `CatMetroCliAabBuild.cs` so much as
names a keystore identifier or writes any `PlayerSettings.X`.

Unity keeps the keystore **passwords in session memory only**. They are not in
`ProjectSettings.asset` (which has no password field at all), not in `unity/UserSettings/`, and not
in the prefs plist. That is the real reason batch mode cannot sign, and it is why you must redo the
Publishing Settings steps after any editor relaunch.

**PROVEN 2026-09-12, not merely argued.** A real batchmode invocation at MAIN `e1a2a288` —
`CM_ALLOW_DEBUG_SIGNING=1 bash scripts/build-aab.sh build/CatMetro-e1a2a288-20260912-debug-proof.aab`
— reached `Prepare For Build` and stopped with Unity's own message:

```
UnityException: Can not sign the application
Unable to sign the application; please provide passwords!
```

It emitted `CLI_AAB_RESULT Failed signing=custom campaignLevels=60 campaignIds=L001,…,L060`, which
also confirms the campaign-receipt fix enumerates all sixty levels in a live run. Two things follow.
First, the password constraint is now an artifact rather than an inference. Second,
`CM_ALLOW_DEBUG_SIGNING=1` does **not** make a CLI bundle possible: it only *permits* a debug
result, and the working tree has `androidUseCustomKeystore: 1`, so Unity attempts the custom key
and fails before compiling anything. Producing a CLI bundle would mean flipping that flag in
`ProjectSettings.asset`, which is one of the nine protected files, so it is not done.
Log: `build/CatMetro-e1a2a288-20260912-debug-proof-failed-release-build.log`.

**Also fixed in passing:** the wrapper staged its build in `build/.catmetro-aab.XXXXXX`, and
Unity's Android post-processor rejects a dot-prefixed directory name — it printed
`.catmetro-aab.6QsdqH is not a valid directory name` twice into the build log. The staging
directory is now `build/catmetro-aab-staging.XXXXXX`; `build/` is gitignored either way.
`tests/unity/build-aab-wrapper.test.sh` still exits 0 with all PASS lines.

**Three further reasons the script cannot cut today's release**, each verified against artifacts:

1. ~~**The campaign-receipt gate checks a path Unity never emits.**~~ **FIXED 2026-09-11.** The
   gate now requires `base/assets/content/levels/<id>.json`, the layout Unity actually emits, and
   names the real path in its failure message when a bundle is shaped differently. The fixture was
   rebuilt at that path and at the full 60-level campaign, with two new regression cases:
   `legacy-level-path` (levels present only at the old `bin/Data` path) and `missing-one-level`.
   Mutation-checked: restoring the old path makes the suite fail.
2. **The permission allowlist may be stale** — though probably not for *this* build. It permits
   only INTERNET, ACCESS_NETWORK_STATE, VIBRATE, BILLING and the signature-only AndroidX receiver
   permission. The 2026-08-30 AAB also declared POST_NOTIFICATIONS, WAKE_LOCK,
   RECEIVE_BOOT_COMPLETED and the C2DM pair, because it predates the SDK-export transform. Our
   current APK declares exactly the allowlisted five, so a fresh bundle should match — but check.
3. **The obvious output names are burned.** The immutability gate refuses any pre-existing output
   or sidecar, and `CatMetro-1.0.0-1.aab` / `-2.aab` both exist. Use `-3` or higher.

**Precedent:** the GUI path already worked. Both existing AABs have the
`_BackUpThisFolder_ButDontShipItWithYourGame/` sibling the script never leaves, neither has the
`-play-listing.md` sidecar the script always writes, and
`unity/Library/EditorUserBuildSettings.asset` still records the GUI build location. Both are
correctly signed by `CN=Sushant Srikrish`, SHA256withRSA, 2048-bit, valid to 2054-01-14.

## Before you open Unity

```sh
cd /Users/sushantsrikrish/cat-metro-app
git rev-parse HEAD && git status --short
grep m_EditorVersion unity/ProjectSettings/ProjectVersion.txt   # must read 6000.3.16f1
diff -rq content/levels unity/Assets/StreamingAssets/content/levels | grep -v '\.meta'
```

Decide the version code from the Console answers below. `ProjectSettings.asset:180` is
`AndroidBundleVersionCode: 1`, `:149` is `bundleVersion: 1.0.0`. **If Play holds versionCode 1 on
any track, including a draft, this build is dead on upload.**

Also: `unity/ProjectSettings/UnityConnectSettings.asset` root `m_Enabled` is `1` in your working
tree and `0` at committed MAIN. Build from a state you are willing to ship, or commit the `0`.

## The build

1. Unity Hub → open `/Users/sushantsrikrish/cat-metro-app/unity` with **6000.3.16f1**.
2. **File ▸ Build Profiles** (⇧⌘B; legacy editors show **Build Settings**) → **Android** →
   **Switch Platform** if it is not active.
3. **Edit ▸ Project Settings ▸ Player ▸ Android ▸ Other Settings** — confirm package
   `com.catmetro.game`, Version `1.0.0`, Min API 25, Target API 36, IL2CPP, **ARM64 only**.
4. **Bundle Version Code** — set the value you decided.
5. **Publishing Settings**, in this order:
   1. tick **Custom Keystore**
   2. **Project Keystore ▸ Select…** → `~/catmetro-keys/catmetro-upload.keystore`
   3. type the **keystore password** — *here, in the Unity window, and nowhere else*
   4. **Project Key ▸ Alias** → `catmetro-upload` (it only populates once the password is accepted;
      if it stays empty the password was wrong — retype it, do not proceed)
   5. type the **key password**
6. **Build Profiles ▸ Android**: **Build App Bundle (Google Play)** ticked; **Development Build**
   unticked; Autoconnect Profiler / Deep Profiling / Script Debugging off; scene list is
   `Assets/Scenes/Game.unity`.
7. **Build** (not Build And Run) → `build/CatMetro-1.0.0-3.aab` (next unused number).
8. Expect 25–45 minutes cold.

### The version code does NOT get committed

I previously wrote "commit that single line by explicit path". **That is wrong** — `git add
<path>` stages the whole file, keystore drift included. There is no way to stage one line, and
`ProjectSettings.asset` is one of the nine permanently-dirty protected files.

**The rule is absolute: none of the nine protected files is ever committed.** They are
`.claude/settings.json`, `unity/Assets/DefaultVolumeProfile.asset`,
`unity/Assets/Plugins/Android/{gradleTemplate.properties,mainTemplate.gradle}`,
`unity/Assets/Settings/CatMetro_URP.asset`,
`unity/Assets/UniversalRenderPipelineGlobalSettings.asset`, and
`unity/ProjectSettings/{PackageManagerSettings,ProjectSettings,UnityConnectSettings}.asset`.

So: **set the Bundle Version Code in the Unity window only.** It lives in your working tree, which
is where the build reads it, and it is recorded for the release in
`docs/release/release-record.md` — a tracked file that carries the exact settings the bundle was
cut with, without carrying your keystore path. Fill that record in after the build.

If a future change genuinely has to move the committed default, use the established preservation
protocol rather than staging the dirty file: `/private/tmp/catmetro-merge-owner.py` backs up your
version, writes the base version, performs the git operation, then restores yours —
`catmetro_merge_settings.preserve_disjoint_changes` merges disjoint edits byte-for-byte and
refuses anything ambiguous. That protocol exists precisely so a settings change never launders the
keystore path into a commit.

### Do not

- Never `git commit -a`, `git add -A`, or `git add unity/ProjectSettings/`.
- After the build, run `git status --porcelain | grep -c '^ M'` — it must print **9**, and
  `git diff -- unity/ProjectSettings/ProjectSettings.asset` must still show your keystore hunk.
- Do not paste a password anywhere outside the Unity window.
- Do not run a Play upload from an agent session.

## Signing readiness is a SEPARATE gate from the version code

Two independent things block the release build, and answering one does not unblock the other:

1. **The version code** — a Console fact, listed under "What I need from Console" below.
2. **The keystore passwords** — yours, typed into the Unity window at build time, step 5 below.
   Unity holds them in **session memory only**: they are not in `ProjectSettings.asset` (which has
   no password field), not in `unity/UserSettings/`, not in the prefs plist. **They must be
   re-entered after every editor relaunch**, and an agent session can never supply them. So even
   with the version code in hand, the build does not start until you are at the machine and
   willing to type them.

A build that produces an unsigned or debug-signed bundle is not a release candidate. The verifier
below refuses one unless you explicitly pass `--allow-debug-signature`, which exists only for a
packaging rehearsal that is never uploaded.

## Verify the artifact before upload

**One command does all of it:**

```sh
python3 scripts/verify-android-artifact.py build/CatMetro-1.0.0-3.aab \
    --expect-version-code <N> --expect-cert-sha256 <Console upload certificate SHA-256>
```

Exit **0** = every check passed and upload readiness is established. Exit **1** = a real defect,
do not upload. Exit **3** = the bundle is internally sound and correctly signed, but no Console
fingerprint was supplied, so *which* certificate signed it could not be corroborated.

### Signing: three separate questions, never conflated

An Android upload certificate is **self-signed by design**, so Java can never build a trusted CA
chain up from it. Measured on real controls (`tests/unity/aab-signature-controls.test.sh`):

| control | `jarsigner -verify -strict` | what it means |
|---|---|---|
| **valid self-signed release** | **rc=4**, invalid-chain + self-signed | the normal, correct shape |
| **expired signer** | **rc=4**, *plus* "signer certificate has expired" | must be refused |
| unsigned | rc=0, "jar is unsigned." | must be refused |
| entry added after signing | rc=20, "unsigned entries … not integrity-checked" | must be refused |
| one byte tampered | rc=1, `SecurityException: SHA-256 digest error` | must be refused |

So **the exit code is not the signal**. Requiring `rc == 0` rejects every genuine release;
accepting `rc == 4` admits an expired certificate. The verifier reads jarsigner's `Error:` block
and separates:

1. **Signature integrity** — is every entry covered by a valid signature? (unsigned, tampered and
   added-after-signing all fail here; an expired or foreign certificate does *not*.)
2. **Certificate health** — not expired, not pre-dated, no disabled or weak algorithm. Any error
   line other than the two benign self-signed-chain diagnostics fails this.
3. **Certificate identity** — the extracted SHA-256 compared against the fingerprint Console
   shows. **This is the only signing fact a local tool cannot establish by itself**, which is why
   it is a required input rather than an inference.

**Certificate-chain trust is reported as a NOTE and is never a release gate.**

**`scripts/build-aab.sh` already handled this correctly and was not changed.** It rejects strict
bit 16 (unsigned entries), accepts bit 4 only when the `Error:` block contains exactly the
invalid-chain and self-signed diagnostics, refuses any other strict error text, separately refuses
expiry/not-yet-valid/disabled/weak-algorithm warnings, and states that the fingerprint comparison
remains human-only. The controls above confirm each of those branches against a real artifact.

It prints a PASS/FAIL line per check, writes `<bundle>.verify.json` beside the bundle, and exits
non-zero if anything fails. It reads only — it never uploads, and it never touches a password.
Checks: bundletool validate; package / versionName / versionCode / minSdk / targetSdk /
`allowBackup=false` / not debuggable; the permission set is **exactly** the allowed five and
carries no `AD_ID`; `jarsigner -verify -strict` exits clean with no signer errors; the signer is
not the Android debug key; all **60** levels are present at `base/assets/content/levels/` and each
is **byte-identical** to the staged source; no OneSignal / ironSource / Unity-mediation /
Firebase file **or dex class**; RevenueCat and Play Billing classes **are** present; ARM64 only.

It is not vacuous, and you can prove that yourself:

```sh
python3 scripts/verify-android-artifact.py build/CatMetro-1.0.0-2.aab   # must FAIL
bash tests/unity/aab-signature-controls.test.sh                         # must print OK
```

The second builds unsigned, tampered, entry-added, expired-signer and wrong-certificate bundles by
re-signing a real one, and asserts each is caught by the right check while a **valid self-signed
bundle is accepted** — the direction that rejecting an obsolete bundle can never establish.

That 2026-08-30 bundle fails six checks — 22 extra permissions (the OneSignal push and
launcher-badge set), `jarsigner` exiting 4 with "jar verified, **with signer errors**", only
**19 of 60** levels, altered level bytes, four OneSignal resource files, and **1430 OneSignal +
467 Firebase** dex classes. Receipt: `build/CatMetro-1.0.0-2.aab.verify.json`.

### The same checks by hand, if you want to see them individually

Tools are the pinned ones under
`/Applications/Unity/Hub/Editor/6000.3.16f1/PlaybackEngines/AndroidPlayer`.

```sh
AAB=build/CatMetro-1.0.0-3.aab
AP=/Applications/Unity/Hub/Editor/6000.3.16f1/PlaybackEngines/AndroidPlayer
JAVA="$AP/OpenJDK/bin/java"; JARSIGNER="$AP/OpenJDK/bin/jarsigner"; KEYTOOL="$AP/OpenJDK/bin/keytool"
BT="$AP/Tools/bundletool-all-1.17.2.jar"

"$JAVA" -jar "$BT" validate --bundle "$AAB"                       # structure
"$JAVA" -jar "$BT" dump manifest --bundle "$AAB"                  # package, versions, SDKs, permissions
"$JARSIGNER" -verify -strict -verbose "$AAB" | head -40           # must print: jar verified
"$KEYTOOL" -printcert -jarfile "$AAB" | grep -Ei 'Owner|SHA256'   # your cert, not Android Debug
unzip -l "$AAB" | grep -c 'base/assets/content/levels/L0'         # must be 60
unzip -l "$AAB" | grep -Ei 'onesignal|ironsource|mediation'       # must be EMPTY
shasum -a 256 "$AAB"
```

Check the manifest dump shows package `com.catmetro.game`, your chosen versionCode, versionName
`1.0.0`, minSdk 25, targetSdk 36, `allowBackup="false"`, no `debuggable`, and a permission set that
is exactly INTERNET, ACCESS_NETWORK_STATE, VIBRATE, `com.android.vending.BILLING` and the
signature-protected `com.catmetro.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`. Anything extra —
especially `com.google.android.gms.permission.AD_ID`, POST_NOTIFICATIONS or the C2DM pair — means
the SDK-export transform did not run and the bundle must not be uploaded.

Send me the output of those commands (they contain no secrets) and I will reconcile them against
the APK receipt.

## What I need from Console

**Already known — do not re-ask:** the Play account **predates 2023-11-13**, so it is exempt from
the 12-tester / 14-day closed-testing gate and can go straight to production. Recorded 2026-09-09
and again 2026-09-11.

**Blocking, needed before the build:**

1. **The highest versionCode that exists on this app across ALL tracks** — internal, closed, open,
   production — including superseded, halted, draft and rejected releases. *Play refuses any upload
   at or below the highest code ever seen, drafts included, and `ProjectSettings.asset:180` is
   still `1`. This is the only answer needed to start the build.*

**Needed before the artifact can be called upload-ready (not before the build starts):**

2. **The upload-certificate SHA-256 fingerprint** under Release ▸ Setup ▸ App integrity, and
   whether Play App Signing is enrolled. *If enrolled, compare against the UPLOAD certificate, not
   the app-signing one; if not, this keystore becomes the permanent signing key.* Without it the
   verifier exits 3 and explicitly refuses to call the bundle upload-ready — it can say the bundle
   is correctly signed and by whom, but not that Play expects that certificate. For reference, the
   2026-08-30 bundle was signed by `CN=Sushant Srikrish` with SHA-256
   `548257b29e36012b06ca6577f422fd843d411110accbf14cdfeac9bb95354408`; if the new bundle reports a
   different fingerprint, the keystore changed and the upload will be rejected.
3. **Which tracks hold a release and each one's status**, plus the app's current review state.
   *Decides new release vs edit draft, and whether a release is blocked.*
4. **Whether Data safety, content rating, target audience and the ads declaration were already
   completed — and whether they were answered for a binary containing OneSignal and LevelPlay.**
   *If so they are now false for this binary; see `data-safety-assessment.md`.*
