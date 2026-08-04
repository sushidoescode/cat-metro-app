// Negative fixture for CM-C6 criterion 2 (never compiled; grep bait):
// `bash scripts/check.sh --root tests/fixtures/daily-bad` must fail on the clock-seam token
// below. IClock is the chosen token because ONLY the Daily clock-ban block catches it — the
// Domain full-ban list never included it — so this fixture proves the NEW block fires, and it
// doubles as criterion 11's scope-guard negative fixture (the wrapper greps it expecting a hit).
class DailyBanned
{
    private CatMetro.Services.IClock _clock; // the clock seam may never reach the Daily root
}
