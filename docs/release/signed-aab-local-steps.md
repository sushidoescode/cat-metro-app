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

1. **The campaign-receipt gate checks a path Unity never emits.** `scripts/build-aab.sh:607`
   requires `base/assets/bin/Data/StreamingAssets/content/levels/L001.json`. The real layout, read
   out of `build/CatMetro-1.0.0-2.aab`, is `base/assets/content/levels/L001.json` — Unity maps
   `Assets/StreamingAssets/X` to `assets/X`. The wrapper test never caught it because
   `tests/unity/build-aab-wrapper.test.sh` builds a synthetic zip at the `bin/Data/…` path, so it
   passes against a shape Unity does not produce. Even with signing solved the script would abort
   at `Campaign receipt: FAIL`.
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

### Do not

- Never `git commit -a`, `git add -A`, or `git add unity/ProjectSettings/`.
- After the build, `git diff -- unity/ProjectSettings/ProjectSettings.asset`. The
  `AndroidKeystoreName` / `AndroidKeyaliasName` / `androidUseCustomKeystore` hunk **must never be
  committed**. If you bump the version code, commit that single line by explicit path.
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

1. **The highest versionCode that exists on this app across ALL tracks** — internal, closed, open,
   production — including superseded, halted, draft and rejected releases. *Play refuses any upload
   at or below the highest code ever seen, drafts included.*
2. **Which tracks currently hold a release, and each release's exact status** (Draft / In review /
   Rejected / Live / Halted / Superseded). *Decides new release vs edit draft.*
3. **Is Play App Signing enrolled, and by which method?** *If it is, the fingerprint below must
   match the UPLOAD certificate, not the app-signing one. If it is not, this keystore becomes the
   permanent signing key and losing it is unrecoverable.*
4. **The upload-certificate SHA-256 fingerprint** Console shows under Release ▸ Setup ▸ App
   integrity. *The only value that proves the bundle will be accepted.*
5. **The app's current review state** — has anything passed review, is anything in review now, are
   there open policy or pre-launch-report issues?
6. **Is this account post-2023-11-13?** If yes, what does Console currently show for opted-in
   testers and production-access eligibility? *Decides closed track vs straight to production.*
7. **Were Data safety, content rating, target audience, ads declaration and privacy policy already
   completed — and were they answered for a binary containing OneSignal and LevelPlay?** *If so they
   are now false for this binary; see `data-safety-assessment.md`.*
