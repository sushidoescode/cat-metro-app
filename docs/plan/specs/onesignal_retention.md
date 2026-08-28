# OneSignal Daily Line Retention Contract

Status: approved 2026-08-27. This replaces the July multi-campaign retention proposal.

Authoritative implementation design:
[`docs/superpowers/specs/2026-08-27-onesignal-daily-reminders-design.md`](../../superpowers/specs/2026-08-27-onesignal-daily-reminders-design.md).
Human platform and dashboard procedure:
[`docs/runbooks/onesignal-push-setup.md`](../../runbooks/onesignal-push-setup.md).

## Product promise

Cat Metro offers one gentle remote reminder for the Daily Line. It does not use notification
pressure as progression.

- Reminders default Off; Morning is only the preselected choice and is described as “around
  10:00”. Afternoon is around 15:00 and Evening is around 18:00.
- The sole automatic soft prompt is earned only after the first durable counted Daily win.
- `Not now` permanently consumes that automatic prompt. The player may later opt in from Settings.
- Native permission appears only after explicit **Remind me** or Settings enablement.
- The existing cumulative lifetime Daily tally remains the return-progress measure.
- There is exactly one remote notification campaign: the recurring Daily Line Journey below.

Streak, lapse or win-back, guilt, countdown, hard-level help, purchase, event-ending, local-backup,
and second-campaign notifications are forbidden. No Unity local-notification fallback ships.

## Runtime boundary

Application and presentation code depend only on provider-neutral `IMessaging`. OneSignal SDK types
remain inside `CatMetro.Integrations.OneSignal`.

The checked-in OneSignal App ID is public configuration and initially blank in
`unity/Assets/Resources/Config/onesignal.json`. Missing or malformed configuration disables
messaging without blocking play. A OneSignal REST key, FCM service-account file, APNs key, password,
or signing material must never enter the client or repository.

After durable preference state commits, the adapter writes only:

| Tag | Values | Use |
|---|---|---|
| `daily_opt_in` | `true`, `false` | Journey entry and immediate exit rule |
| `daily_reminder_slot` | `morning`, `afternoon`, `evening` | Selects one fixed local-time branch |

Authorized On opts the push subscription in and writes both tags. Off writes
`daily_opt_in=false`, removes the slot tag, and opts the subscription out because Daily is the only
push category. A slot change while On rewrites the slot; a slot change while Off changes only the
durable preselection.

The click adapter accepts only exact Additional Data `route=daily`. It rejects missing and unknown
routes. Accepted clicks are queued onto Unity's main loop and open the current Daily Line through
the existing Daily selection authority across cold and warm process states.

## Single recurring Journey

Create one Audience Segment Journey with this exact contract:

| Setting | Value |
|---|---|
| Audience / entry | `daily_opt_in == true` |
| Future additions only | **Off**, so already-eligible users can enter when activated |
| Branch key | exact `daily_reminder_slot` |
| Morning branch | recurring local Time Window `10:00–10:15` |
| Afternoon branch | recurring local Time Window `15:00–15:15` |
| Evening branch | recurring local Time Window `18:00–18:15` |
| Missing / invalid / else | exit without a message |
| Re-entry | after **12 hours** |
| Exit | as soon as the user no longer matches `daily_opt_in == true` |

Twelve hours is longer than a 15-minute Time Window and shorter than a day. The design intent is
one message per local day. OneSignal starts the re-entry timer when a user exits, and may randomize
a waiting user's release from the Time Window start through 15 minutes after it; real dashboard
delivery must prove the once-per-day intent. Never promise an exact send minute.

A slot edit may arrive after the current Journey instance already evaluated its branch. Promise
only that the next eligible send/re-entry uses the new slot, not an immediate rebucket or send.

Every branch sends the same message:

| Field | Exact value |
|---|---|
| Title | `Today's Line is ready` |
| Body | `A fresh little route is waiting when you feel like playing.` |
| Additional data | `{"route":"daily"}` |
| Launch URL | `catmetro://daily` |

## Permission and preference behavior

The automatic flow begins only after `DailyProgressTracker.RecordDailyCompletion` returns
`Counted == true` and Home is safely presented after the existing input lockout. Campaign wins,
practice Dailies, replayed completions, and first launch do not earn or show it.

The prompt is durably marked seen when presented:

- **Not now:** remains opted out; no schedule or permission request; no later automatic prompt.
- **Remind me:** requests native permission. Authorization enables the selected slot. Denial stays
  durably Off.
- **Settings enable:** explicit player action; if the OS prompt is exhausted, the settings fallback
  opens the app's native notification settings.
- **Settings Off:** commits locally before provider cancellation and tag cleanup.

Provider failure never pretends delivery is enabled. Durable intent remains authoritative so a
later launch can reconcile it after the provider recovers.

## Platform and evidence requirements

OneSignal Unity Android/iOS is pinned to `5.3.3`, with exactly one EDM4U `1.2.188`. The exact Unity
`6000.3.16f1` custom main Gradle and Gradle properties templates are committed. The unused
OneSignal location module is disabled.

Human work remains mandatory for:

- OneSignal app creation and public App ID placement;
- FCM v1 credential upload for Android `com.catmetro.game`;
- the five required Android notification-small-icon variants, which do not yet exist;
- final iOS bundle ID, Team ID, App Group, main/extension identifiers and profiles, APNs `.p8`,
  generated `.xcworkspace`, both-target signing, and TestFlight;
- the one Journey's segments, branches, windows, copy, data, exit, and re-entry settings;
- real foreground, background, killed/cold, resumed/warm, permission, tag cleanup, slot-change,
  Android-icon, APNs sandbox, and TestFlight evidence.

Dashboard screenshots and device evidence must show the single Journey, three local-time branches,
two tags, exact message data, delivery counts, and Daily routing. No delivery, click, retention, or
award claim may be published until that evidence exists.
