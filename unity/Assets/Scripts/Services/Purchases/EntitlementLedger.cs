using System;
using System.Collections.Generic;

namespace CatMetro.Services.Purchases
{
    // Where an entitlement came from. This is recorded rather than discarded because the two
    // sources have genuinely different lifecycles — see EntitlementLedger's class comment.
    public enum GrantSource
    {
        // Paid, and confirmed by RevenueCat's CustomerInfo. RevenueCat is the sole authority for
        // these: a refund, a chargeback, or a lapsed subscription must be able to take one away.
        Store,

        // Lent by watching a rewarded ad served by a network we integrated ourselves, and
        // granted locally. Expires on a wall clock. The ad lane grants these; it does not get
        // its own entitlement concept.
        //
        // Note this is the SECOND of two ad paths. RevenueCat's own Ad Monetization feature
        // grants a time-limited entitlement server-side (AdMob server-side verification) and
        // delivers it through CustomerInfo like any other entitlement, so that path arrives as
        // GrantSource.Store with a non-zero expiry and needs nothing special here. The
        // convergence is therefore true at two levels: RevenueCat converges its own ad grants
        // with purchases before we ever see them, and this ledger converges anything else.
        RewardedAd,

        // Granted from the RevenueCat dashboard (promotional entitlement) or by a support
        // action. Arrives through the same CustomerInfo channel as Store, so it is treated
        // identically for lifecycle purposes and distinguished only for analytics.
        Promotional
    }

    public readonly struct EntitlementGrant
    {
        public readonly string EntitlementId;
        public readonly GrantSource Source;

        // 0 means permanent. Otherwise a Unix-seconds instant after which the grant is dead.
        public readonly long ExpiresAtUnixSeconds;

        public bool IsPermanent => ExpiresAtUnixSeconds == 0L;

        public EntitlementGrant(string entitlementId, GrantSource source, long expiresAtUnixSeconds = 0L)
        {
            EntitlementId = entitlementId;
            Source = source;
            ExpiresAtUnixSeconds = expiresAtUnixSeconds;
        }

        public bool IsActiveAt(long nowUnixSeconds)
            => IsPermanent || nowUnixSeconds < ExpiresAtUnixSeconds;
    }

    // THE CONVERGENCE POINT. A paid purchase and a rewarded ad both end here, and the rest of
    // the game only ever asks `IsActive(entitlementId, now)`. Nothing downstream — no wardrobe
    // screen, no cat renderer, no save code — is allowed to know whether the conductor's coat
    // was bought or watched for. That is the whole design requirement, and it is enforced by
    // there being exactly one query method and no way to ask "was this paid for?".
    //
    // The one place the sources DO differ is lifecycle, and conflating that would be a bug:
    //
    //   * Store grants are REPLACED wholesale every time CustomerInfo arrives
    //     (ReplaceStoreGrants). RevenueCat is the authority; if an entitlement is not in the
    //     new snapshot it is gone, which is what makes refunds and lapsed subscriptions work.
    //     Merging instead of replacing would make a refund permanently unenforceable.
    //
    //   * Ad leases are ADDED and expire on their own clock (GrantLease). A CustomerInfo
    //     refresh must never wipe a lease the player earned thirty seconds ago, because
    //     RevenueCat has never heard of it.
    //
    // Ownership beats a lease: if you own the coat outright, an ad lease on it is irrelevant and
    // extending it is meaningless. IsActive says yes either way; that is the point.
    //
    // Deliberately clock-free. Every method that needs "now" takes it as an argument, matching
    // GameRoot.DailyClockUnixSeconds — tests pin an exact instant instead of racing wall time.
    public sealed class EntitlementLedger
    {
        private readonly Dictionary<string, EntitlementGrant> _store =
            new Dictionary<string, EntitlementGrant>(StringComparer.Ordinal);

        private readonly Dictionary<string, EntitlementGrant> _leases =
            new Dictionary<string, EntitlementGrant>(StringComparer.Ordinal);

        // Raised whenever the active set could have changed, so a wardrobe screen can refresh
        // without polling. Not raised for a no-op replace: re-delivering an identical
        // CustomerInfo (which RevenueCat does routinely, on every foreground) must not make the
        // UI flicker.
        public event Action Changed;

        // ---- writes ---------------------------------------------------------------------

        // Called with the full set of entitlements RevenueCat currently considers active for
        // this customer. Wholesale replacement, on purpose — see the class comment.
        public void ReplaceStoreGrants(IReadOnlyList<EntitlementGrant> grants)
        {
            var incoming = new Dictionary<string, EntitlementGrant>(StringComparer.Ordinal);
            if (grants != null)
            {
                for (int i = 0; i < grants.Count; i++)
                {
                    var g = grants[i];
                    if (string.IsNullOrEmpty(g.EntitlementId)) continue;
                    if (g.Source == GrantSource.RewardedAd) continue; // not the store's to assert
                    incoming[g.EntitlementId] = g;
                }
            }

            if (SameKeys(_store, incoming)) return;

            _store.Clear();
            foreach (var kv in incoming) _store[kv.Key] = kv.Value;
            Changed?.Invoke();
        }

        // A rewarded ad (or any other temporary source) lending an entitlement until `expiresAt`.
        // Returns false — without touching anything — when the lease would be pointless or
        // invalid, so callers can tell "already yours" from "granted".
        public bool GrantLease(string entitlementId, long expiresAtUnixSeconds, long nowUnixSeconds)
        {
            if (!CanGrantLease(entitlementId, expiresAtUnixSeconds, nowUnixSeconds)) return false;

            _leases[entitlementId] = new EntitlementGrant(entitlementId, GrantSource.RewardedAd,
                expiresAtUnixSeconds);
            Changed?.Invoke();
            return true;
        }

        // PurchaseService uses the exact same decision before loading an ad. Keeping the dry-run
        // here prevents TASK 11 from offering thirty seconds of advertising for a lease this
        // ledger would reject, while still allowing a long reward to extend short timed access.
        internal bool CanGrantLease(string entitlementId, long expiresAtUnixSeconds,
            long nowUnixSeconds)
        {
            if (string.IsNullOrEmpty(entitlementId)) return false;

            // Already expired on arrival: a clock skew or a zero-length lease. Refuse rather
            // than store a grant that can never be active.
            if (expiresAtUnixSeconds <= nowUnixSeconds) return false;

            // Already covered by the store for at least as long — the lease adds nothing. Report
            // false so the ad lane can decline to offer the ad rather than selling the player
            // nothing. Note the expiry comparison rather than a plain "is it active": a
            // RevenueCat ad reward grants a SHORT entitlement (30 minutes minimum) through
            // CustomerInfo, and a longer lease on top of that is a real improvement, not a
            // no-op. Only a permanent grant, or a longer-running one, makes the lease pointless.
            if (_store.TryGetValue(entitlementId, out var owned) && owned.IsActiveAt(nowUnixSeconds) &&
                (owned.IsPermanent || owned.ExpiresAtUnixSeconds >= expiresAtUnixSeconds))
                return false;

            // Extending an existing lease only ever moves the expiry forward. A shorter lease
            // arriving late must not cut an existing one short.
            if (_leases.TryGetValue(entitlementId, out var existing) &&
                existing.ExpiresAtUnixSeconds >= expiresAtUnixSeconds)
                return false;

            return true;
        }

        // Drops any grant that has run out, from EITHER limb. Cheap to call every few seconds;
        // only fires Changed if something actually died.
        //
        // Nothing depends on this being called — IsActive already ignores expired grants — but
        // it matters for a specific reason RevenueCat calls out: when a time-limited entitlement
        // lapses, "RevenueCat doesn't notify your app the moment it expires… Check the
        // entitlement's active status at the point where you gate access, not once when the
        // reward is granted and never again." A player can sit on the wardrobe screen while a
        // 30-minute grant runs out. This is what makes the UI notice.
        public bool PruneExpired(long nowUnixSeconds)
        {
            bool changed = Prune(_leases, nowUnixSeconds) | Prune(_store, nowUnixSeconds);
            if (changed) Changed?.Invoke();
            return changed;
        }

        private static bool Prune(Dictionary<string, EntitlementGrant> map, long nowUnixSeconds)
        {
            List<string> dead = null;
            foreach (var kv in map)
            {
                if (!kv.Value.IsActiveAt(nowUnixSeconds))
                    (dead ??= new List<string>()).Add(kv.Key);
            }

            if (dead == null) return false;
            for (int i = 0; i < dead.Count; i++) map.Remove(dead[i]);
            return true;
        }

        // ---- reads ----------------------------------------------------------------------

        // The ONLY question the rest of the game asks. Note there is deliberately no
        // `IsOwned` / `WasPurchased` companion: giving callers a way to distinguish a paid
        // unlock from an ad-granted one is exactly how the two code paths diverge again.
        public bool IsActive(string entitlementId, long nowUnixSeconds)
        {
            if (string.IsNullOrEmpty(entitlementId)) return false;

            if (_store.TryGetValue(entitlementId, out var owned) && owned.IsActiveAt(nowUnixSeconds))
                return true;

            return _leases.TryGetValue(entitlementId, out var leased) && leased.IsActiveAt(nowUnixSeconds);
        }

        public IReadOnlyList<string> ActiveEntitlements(long nowUnixSeconds)
        {
            var result = new List<string>(_store.Count + _leases.Count);
            foreach (var kv in _store)
                if (kv.Value.IsActiveAt(nowUnixSeconds)) result.Add(kv.Key);
            foreach (var kv in _leases)
            {
                bool activeStoreGrant = _store.TryGetValue(kv.Key, out var owned) &&
                                        owned.IsActiveAt(nowUnixSeconds);
                if (kv.Value.IsActiveAt(nowUnixSeconds) && !activeStoreGrant)
                    result.Add(kv.Key);
            }
            result.Sort(StringComparer.Ordinal); // deterministic for tests and for UI ordering
            return result;
        }

        // Seconds until this entitlement lapses, or 0 if it never will (owned outright — an
        // owned thing has no countdown) or is not active at all. Presentation uses this for the
        // little "2h left" chip.
        //
        // Deliberately source-blind: a 30-minute entitlement granted by RevenueCat's own
        // server-verified ad reward arrives as an expiring STORE grant, and a locally granted
        // lease arrives as a RewardedAd grant. Both are countdowns and both report here, because
        // the UI's question is "how long have I got?", not "who gave me this?".
        public long SecondsUntilExpiry(string entitlementId, long nowUnixSeconds)
        {
            if (string.IsNullOrEmpty(entitlementId)) return 0L;

            long latestExpiry = 0L;
            if (_store.TryGetValue(entitlementId, out var owned) && owned.IsActiveAt(nowUnixSeconds))
            {
                if (owned.IsPermanent) return 0L;
                latestExpiry = owned.ExpiresAtUnixSeconds;
            }

            if (_leases.TryGetValue(entitlementId, out var leased) &&
                leased.IsActiveAt(nowUnixSeconds) && leased.ExpiresAtUnixSeconds > latestExpiry)
                latestExpiry = leased.ExpiresAtUnixSeconds;

            return latestExpiry == 0L ? 0L : latestExpiry - nowUnixSeconds;
        }

        // ---- persistence seam -----------------------------------------------------------

        // Ad leases must survive an app restart or the reward is worthless — a player who
        // watches a thirty-second ad and then backgrounds the game has been robbed. Store grants
        // are deliberately NOT exported: they come back from RevenueCat on every launch, and a
        // locally cached copy of "you own this" is precisely the thing an attacker edits.
        public IReadOnlyList<EntitlementGrant> ExportLeases()
        {
            var result = new List<EntitlementGrant>(_leases.Count);
            foreach (var kv in _leases) result.Add(kv.Value);
            result.Sort((a, b) => string.CompareOrdinal(a.EntitlementId, b.EntitlementId));
            return result;
        }

        public void ImportLeases(IReadOnlyList<EntitlementGrant> leases, long nowUnixSeconds)
        {
            if (leases == null) return;
            bool changed = false;
            for (int i = 0; i < leases.Count; i++)
            {
                var g = leases[i];
                if (string.IsNullOrEmpty(g.EntitlementId)) continue;
                if (!g.IsActiveAt(nowUnixSeconds)) continue; // do not resurrect a dead lease
                _leases[g.EntitlementId] =
                    new EntitlementGrant(g.EntitlementId, GrantSource.RewardedAd, g.ExpiresAtUnixSeconds);
                changed = true;
            }

            if (changed) Changed?.Invoke();
        }

        private static bool SameKeys(Dictionary<string, EntitlementGrant> a,
            Dictionary<string, EntitlementGrant> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var other)) return false;
                if (other.ExpiresAtUnixSeconds != kv.Value.ExpiresAtUnixSeconds) return false;
            }

            return true;
        }
    }
}
