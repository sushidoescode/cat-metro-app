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

## Verify the artifact before upload

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

**Needed before upload, not before the build:**

2. **The upload-certificate SHA-256 fingerprint** under Release ▸ Setup ▸ App integrity, and
   whether Play App Signing is enrolled. *If enrolled, my verification must compare against the
   UPLOAD certificate, not the app-signing one; if not, this keystore becomes the permanent signing
   key.*
3. **Which tracks hold a release and each one's status**, plus the app's current review state.
   *Decides new release vs edit draft, and whether a release is blocked.*
4. **Whether Data safety, content rating, target audience and the ads declaration were already
   completed — and whether they were answered for a binary containing OneSignal and LevelPlay.**
   *If so they are now false for this binary; see `data-safety-assessment.md`.*
