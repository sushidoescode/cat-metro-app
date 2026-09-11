# Prepared correction to the published privacy policy

**Do not apply yet.** Apply when the release AAB is cut, and only if that exact bundle still
excludes OneSignal and LevelPlay. Verify with the commands in
`docs/release/signed-aab-local-steps.md` — `unzip -l "$AAB" | grep -Ei 'onesignal|ironsource|mediation'`
must be empty.

The policy is live at <https://sushidoescode.github.io/cat-metro-app/privacy/> and is served from
`docs/privacy/`. It must describe the binary that is actually published, because Play reconciles
the Data safety form against it.

## Why it needs correcting

`docs/privacy/privacy-policy.txt` names OneSignal and LevelPlay as processors, hedged by
"Depending on the features you use and which optional services are enabled". Neither SDK is in
the shipped binary: the export transform removes both, the build log records
`CATMETRO_ANDROID_SDK_PROFILE oneSignal=False levelPlay=False`, and the APK carries zero
`com.onesignal` / `com.ironsource` / `com.unity3d.mediation` classes, zip entries or manifest
components. The hedge is weaker than it looks — a policy that describes push and ad processing
while the form declares neither is the kind of inconsistency Play checks for.

## Change 1 — remove the two "When enabled" processor sections

Delete the `OneSignal — When enabled` block and the `LevelPlay — When enabled` block in their
entirety, along with the device-identifier and notification categories that exist only to support
them. Nothing else in the policy depends on those paragraphs.

## Change 2 — replace the hedged preamble

Replace:

> Depending on the features you use and which optional services are enabled, Cat Metro and its
> service providers may collect the following five categories published in the app stores.

with:

> Cat Metro collects the two categories below. It contains no advertising SDK and no push
> notification SDK, and it asks for no permission beyond internet access, network state, vibration
> and Google Play billing.

## Change 3 — say what the identifier actually is

The Purchase History section should name the identifier plainly, because it is the reason the Data
safety form declares "Device or other IDs":

> RevenueCat assigns each installation an anonymous identifier of its own and uses it, together
> with purchase records, to validate purchases, restore purchases, and keep purchased entitlements
> available. This identifier is created by RevenueCat for this app; it is not your device's
> advertising identifier, which Cat Metro neither requests nor uses.

## Change 4 — only if the RevenueCat dashboard has integrations

**Blocked on reading the dashboard.** RevenueCat's guidance is that integrations with third parties
that are not service providers may make the data "Shared" rather than merely collected, and that
integrations consuming `gpsAdId` / `androidId` bring device identifiers into scope on a different
basis. If any such integration is enabled, this policy must name the recipient and the purpose, and
`docs/release/data-safety-assessment.md`'s `Shared: No` answers must change with it.

## After applying

Bump the effective date, confirm the rendered page at the live URL, and re-read
`docs/release/data-safety-assessment.md` so the form and the policy agree line for line.
