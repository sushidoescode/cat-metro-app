#if CATMETRO_LEVELPLAY
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CatMetro.Services.Ads;
using Unity.Services.LevelPlay;
using UnityEngine;
using LevelPlaySdk = Unity.Services.LevelPlay.LevelPlay;

[assembly: InternalsVisibleTo("CatMetro.Tests.LevelPlay.EditMode")]

namespace CatMetro.Integrations.LevelPlay
{
    internal readonly struct LevelPlayAdSnapshot
    {
        internal string AdId { get; }
        internal string AuctionId { get; }
        internal string AdUnitId { get; }
        internal string PlacementId { get; }
        internal string NetworkName { get; }

        internal LevelPlayAdSnapshot(string adId, string auctionId, string adUnitId,
            string placementId, string networkName)
        {
            AdId = adId;
            AuctionId = auctionId;
            AdUnitId = adUnitId;
            PlacementId = placementId;
            NetworkName = networkName;
        }
    }

    internal readonly struct LevelPlayErrorSnapshot
    {
        internal int ErrorCode { get; }
        internal string AdId { get; }
        internal string AdUnitId { get; }

        internal LevelPlayErrorSnapshot(int errorCode, string adId = null,
            string adUnitId = null)
        {
            ErrorCode = errorCode;
            AdId = adId;
            AdUnitId = adUnitId;
        }
    }

    internal readonly struct LevelPlayImpressionSnapshot
    {
        internal string AuctionId { get; }
        internal string AdUnitId { get; }
        internal string PlacementId { get; }
        internal string NetworkName { get; }
        internal double? RevenueUsd { get; }
        internal string Precision { get; }

        internal LevelPlayImpressionSnapshot(string auctionId, string adUnitId,
            string placementId, string networkName, double? revenueUsd, string precision)
        {
            AuctionId = auctionId;
            AdUnitId = adUnitId;
            PlacementId = placementId;
            NetworkName = networkName;
            RevenueUsd = revenueUsd;
            Precision = precision;
        }
    }

    internal interface ILevelPlaySdkBridge
    {
        event Action InitializationSucceeded;
        event Action<int> InitializationFailed;
        void Initialize(string appKey);
        ILevelPlayRewardedAdBridge CreateRewardedAd(string adUnitId);
    }

    internal interface ILevelPlayRewardedAdBridge : IDisposable
    {
        event Action<LevelPlayAdSnapshot> AdLoaded;
        event Action<LevelPlayErrorSnapshot> AdLoadFailed;
        event Action<LevelPlayAdSnapshot> AdDisplayed;
        event Action<LevelPlayAdSnapshot, LevelPlayErrorSnapshot> AdDisplayFailed;
        event Action<LevelPlayAdSnapshot> AdRewarded;
        event Action<LevelPlayAdSnapshot> AdClicked;
        event Action<LevelPlayAdSnapshot> AdClosed;
        event Action<LevelPlayAdSnapshot> AdInfoChanged;
        event Action<LevelPlayImpressionSnapshot> AdImpression;
        void Load();
        void Show(string placementId);
        bool IsReady();
        bool IsPlacementCapped(string placementId);
    }

    internal sealed class LevelPlayRewardedAdProvider : IRewardedAdProvider,
        IRewardedAdPlacementReadiness, IMainThreadRewardedAdEventDrain
    {
        private const int MaxContexts = 16;
        private const int MaxCompletedAuctions = 32;
        private const int MaxAdHistory = 32;
        private const int MaxTerminalRevenueContexts = 16;

        private readonly RewardedAdsConfig _config;
        private readonly ILevelPlaySdkBridge _sdk;
        private readonly Func<long> _monotonicTicks;
        private readonly long _contextLifetimeTicks;
        private readonly MainThreadAdEventQueue _mainThreadQueue =
            new MainThreadAdEventQueue();
        private readonly List<ShowContext> _contexts = new List<ShowContext>();
        private readonly Dictionary<string, ShowContext> _byAuction =
            new Dictionary<string, ShowContext>(StringComparer.Ordinal);
        private readonly Dictionary<string, ShowContext> _byAd =
            new Dictionary<string, ShowContext>(StringComparer.Ordinal);
        private readonly HashSet<string> _completedAuctions =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _completedAuctionOrder = new Queue<string>();
        private readonly HashSet<string> _retiredAdIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _retiredAdOrder = new Queue<string>();
        private readonly HashSet<string> _ambiguousAdIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _ambiguousAdOrder = new Queue<string>();
        private readonly Dictionary<string, TerminalRevenueContext> _terminalRevenueByAuction =
            new Dictionary<string, TerminalRevenueContext>(StringComparer.Ordinal);
        private readonly Queue<string> _terminalRevenueOrder = new Queue<string>();

        private Action<RewardedAdEvent> _eventReceived;
        private ILevelPlayRewardedAdBridge _ad;
        private ShowContext _activeContext;
        private ShowContext _unboundContext;
        private ShowContext _showInvocationContext;
        private LoadedIdentity _loadedIdentity;
        private long _loadGeneration;
        private long _identityQuarantineAfterGeneration;
        private bool _auctionHistorySaturated;
        private bool _adHistorySaturated;
        private bool _requiresNonRewardBinding;
        private bool _postQuarantineStableTerminalRequired;
        private bool _loadInFlight;
        private bool _loadedAvailable;
        private bool _initializeCalled;
        private bool _initializationTerminal;
        private bool _loadRequested;
        private bool _createAttempted;
        private bool _failed;
        private bool _disposed;
        private bool _initSuccessSubscription;
        private bool _initFailureSubscription;
        private bool _adLoadedSubscription;
        private bool _adLoadFailedSubscription;
        private bool _adDisplayedSubscription;
        private bool _adDisplayFailedSubscription;
        private bool _adRewardedSubscription;
        private bool _adClickedSubscription;
        private bool _adClosedSubscription;
        private bool _adInfoChangedSubscription;
        private bool _adImpressionSubscription;
        private bool _adDisposed;

        public event Action<RewardedAdEvent> EventReceived
        {
            add => _eventReceived += value;
            remove => _eventReceived -= value;
        }

        internal LevelPlayRewardedAdProvider(RewardedAdsConfig config,
            ILevelPlaySdkBridge sdk, Func<long> monotonicTicks, long contextLifetimeTicks)
        {
            _config = config;
            _sdk = sdk;
            _monotonicTicks = monotonicTicks;
            _contextLifetimeTicks = contextLifetimeTicks;
            if (config == null || !config.IsConfigured || sdk == null ||
                monotonicTicks == null || contextLifetimeTicks <= 0L)
                _failed = true;
        }

        public bool IsReady => IsReadyCore(null, checkPlacementCap: false);

        public bool IsReadyForPlacement(string placementId)
            => IsReadyCore(placementId, checkPlacementCap: true);

        public void Initialize()
        {
            if (_disposed || _failed || _initializeCalled) return;
            _initializeCalled = true;

            if (!AttachInitializationHandlers())
            {
                FailInitialization(null);
                return;
            }

            try
            {
                _sdk.Initialize(_config.AppKey);
            }
            catch
            {
                FailInitialization(null);
            }
        }

        public void Load()
        {
            if (_disposed || _failed) return;
            if (_ad == null)
            {
                _loadRequested = true;
                return;
            }
            SafeLoad();
        }

        public bool TryShow(long attemptId, string placementId)
        {
            if (attemptId <= 0L || string.IsNullOrWhiteSpace(placementId) ||
                !IsReadyForPlacement(placementId) || !TryReadClock(out long now) ||
                _contexts.Count >= MaxContexts || _unboundContext != null ||
                _activeContext != null)
                return false;

            var context = new ShowContext(attemptId, placementId, now, _loadGeneration);
            _contexts.Add(context);
            _activeContext = context;
            _unboundContext = context;
            bool identityBound = ConsumeLoadedIdentity(context, out long identityGeneration,
                out bool hasAuctionIdentity);
            bool wasIdentityQuarantined = _identityQuarantineAfterGeneration > 0L;
            if (wasIdentityQuarantined)
            {
                if (!identityBound || !hasAuctionIdentity ||
                    identityGeneration <= _identityQuarantineAfterGeneration)
                {
                    RemoveContext(context, tombstoneAuction: false);
                    return false;
                }
                _identityQuarantineAfterGeneration = 0L;
                _postQuarantineStableTerminalRequired = true;
            }
            _loadedAvailable = false;
            _showInvocationContext = context;
            try
            {
                _ad.Show(placementId);
                // Display failure is callback-capable and may arrive before Show returns. The
                // terminal callback removes this exact context; propagate that rejection so the
                // coordinator reports the synchronous outcome without issuing a second reload.
                return _contexts.Contains(context);
            }
            catch
            {
                if (_contexts.Contains(context))
                {
                    RemoveContext(context, tombstoneAuction: true);
                    Raise(LevelPlayPayloadMapper.CreateLifecycle(
                        RewardedAdEventKind.DisplayFailed, attemptId, placementId,
                        _config.RewardedAdUnitId));
                }
                return false;
            }
            finally
            {
                if (ReferenceEquals(_showInvocationContext, context))
                    _showInvocationContext = null;
            }
        }

        public void DrainMainThreadEvents()
        {
            if (_disposed) return;
            _mainThreadQueue.Drain();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _mainThreadQueue.Dispose();
            DetachInitializationHandlers();
            DetachRewardedHandlers();
            DisposeAdOnce();
            _activeContext = null;
            _unboundContext = null;
            _showInvocationContext = null;
            _contexts.Clear();
            _byAuction.Clear();
            _byAd.Clear();
            _completedAuctions.Clear();
            _completedAuctionOrder.Clear();
            _retiredAdIds.Clear();
            _retiredAdOrder.Clear();
            _ambiguousAdIds.Clear();
            _ambiguousAdOrder.Clear();
            _terminalRevenueByAuction.Clear();
            _terminalRevenueOrder.Clear();
            _loadedIdentity = default;
            _loadInFlight = false;
            _loadedAvailable = false;
            _eventReceived = null;
        }

        private bool IsReadyCore(string placementId, bool checkPlacementCap)
        {
            if (_disposed || _failed || _ad == null ||
                (checkPlacementCap && string.IsNullOrWhiteSpace(placementId)) ||
                !PurgeExpiredContexts() || _loadInFlight || _unboundContext != null ||
                _activeContext != null || _contexts.Count >= MaxContexts)
                return false;
            if (_identityQuarantineAfterGeneration > 0L &&
                (!_loadedIdentity.HasAuctionIdentity ||
                 _loadedIdentity.Generation <= _identityQuarantineAfterGeneration))
                return false;

            try
            {
                if (!_ad.IsReady()) return false;
                return !checkPlacementCap || !_ad.IsPlacementCapped(placementId);
            }
            catch
            {
                return false;
            }
        }

        private bool AttachInitializationHandlers()
        {
            _initSuccessSubscription = true;
            try { _sdk.InitializationSucceeded += OnInitializationSucceeded; }
            catch
            {
                DetachInitializationHandlers();
                return false;
            }

            _initFailureSubscription = true;
            try { _sdk.InitializationFailed += OnInitializationFailed; }
            catch
            {
                DetachInitializationHandlers();
                return false;
            }
            return true;
        }

        private void DetachInitializationHandlers()
        {
            if (_initSuccessSubscription)
            {
                _initSuccessSubscription = false;
                try { _sdk.InitializationSucceeded -= OnInitializationSucceeded; }
                catch { }
            }
            if (_initFailureSubscription)
            {
                _initFailureSubscription = false;
                try { _sdk.InitializationFailed -= OnInitializationFailed; }
                catch { }
            }
        }

        private void OnInitializationSucceeded()
        {
            if (_disposed || _failed || _initializationTerminal) return;
            _initializationTerminal = true;
            DetachInitializationHandlers();
            if (_createAttempted) return;
            _createAttempted = true;
            try { _ad = _sdk.CreateRewardedAd(_config.RewardedAdUnitId); }
            catch
            {
                FailInitialization(null);
                return;
            }
            if (_ad == null || !AttachRewardedHandlers())
            {
                FailInitialization(null);
                return;
            }

            if (_loadRequested)
            {
                _loadRequested = false;
                SafeLoad();
            }
        }

        private void OnInitializationFailed(int errorCode)
        {
            if (_disposed || _failed || _initializationTerminal) return;
            _initializationTerminal = true;
            FailInitialization(errorCode);
        }

        private void FailInitialization(int? errorCode)
        {
            if (_disposed || _failed) return;
            _failed = true;
            _loadRequested = false;
            _loadInFlight = false;
            _loadedAvailable = false;
            _loadedIdentity = default;
            DetachInitializationHandlers();
            DetachRewardedHandlers();
            DisposeAdOnce();
            Raise(LevelPlayPayloadMapper.CreateLifecycle(RewardedAdEventKind.LoadFailed,
                adUnitId: _config?.RewardedAdUnitId, errorCode: errorCode));
        }

        private bool AttachRewardedHandlers()
        {
            if (!TryAttach(ref _adLoadedSubscription,
                () => _ad.AdLoaded += OnAdLoaded)) return false;
            if (!TryAttach(ref _adLoadFailedSubscription,
                () => _ad.AdLoadFailed += OnAdLoadFailed)) return false;
            if (!TryAttach(ref _adDisplayedSubscription,
                () => _ad.AdDisplayed += OnAdDisplayed)) return false;
            if (!TryAttach(ref _adDisplayFailedSubscription,
                () => _ad.AdDisplayFailed += OnAdDisplayFailed)) return false;
            if (!TryAttach(ref _adRewardedSubscription,
                () => _ad.AdRewarded += OnAdRewarded)) return false;
            if (!TryAttach(ref _adClickedSubscription,
                () => _ad.AdClicked += OnAdClicked)) return false;
            if (!TryAttach(ref _adClosedSubscription,
                () => _ad.AdClosed += OnAdClosed)) return false;
            if (!TryAttach(ref _adInfoChangedSubscription,
                () => _ad.AdInfoChanged += OnAdInfoChanged)) return false;
            if (!TryAttach(ref _adImpressionSubscription,
                () => _ad.AdImpression += OnAdImpression)) return false;
            return true;
        }

        private bool TryAttach(ref bool subscription, Action attach)
        {
            subscription = true;
            try
            {
                attach();
                return true;
            }
            catch
            {
                DetachRewardedHandlers();
                return false;
            }
        }

        private void DetachRewardedHandlers()
        {
            SafeDetach(ref _adLoadedSubscription, () => _ad.AdLoaded -= OnAdLoaded);
            SafeDetach(ref _adLoadFailedSubscription, () => _ad.AdLoadFailed -= OnAdLoadFailed);
            SafeDetach(ref _adDisplayedSubscription, () => _ad.AdDisplayed -= OnAdDisplayed);
            SafeDetach(ref _adDisplayFailedSubscription,
                () => _ad.AdDisplayFailed -= OnAdDisplayFailed);
            SafeDetach(ref _adRewardedSubscription, () => _ad.AdRewarded -= OnAdRewarded);
            SafeDetach(ref _adClickedSubscription, () => _ad.AdClicked -= OnAdClicked);
            SafeDetach(ref _adClosedSubscription, () => _ad.AdClosed -= OnAdClosed);
            SafeDetach(ref _adInfoChangedSubscription,
                () => _ad.AdInfoChanged -= OnAdInfoChanged);
            SafeDetach(ref _adImpressionSubscription,
                () => _ad.AdImpression -= OnAdImpression);
        }

        private static void SafeDetach(ref bool subscription, Action detach)
        {
            if (!subscription) return;
            subscription = false;
            try { detach(); }
            catch { }
        }

        private void DisposeAdOnce()
        {
            var ad = _ad;
            _ad = null;
            if (ad == null || _adDisposed) return;
            _adDisposed = true;
            try { ad.Dispose(); }
            catch { }
        }

        private void SafeLoad()
        {
            // LevelPlay 9.5.1 supplies no request token with Loaded/LoadFailed. Never overlap
            // requests: a callback is current only because exactly one vendor Load is in flight.
            if (_disposed || _failed || _ad == null || _loadInFlight ||
                HasConsumableLoadedResult() || _activeContext != null) return;
            if (_loadGeneration == long.MaxValue)
            {
                _failed = true;
                Raise(LevelPlayPayloadMapper.CreateLifecycle(RewardedAdEventKind.LoadFailed,
                    adUnitId: _config.RewardedAdUnitId));
                return;
            }
            _loadGeneration++;
            _loadedAvailable = false;
            _loadedIdentity = default;
            _loadInFlight = true;
            try { _ad.Load(); }
            catch
            {
                _loadInFlight = false;
                _loadedAvailable = false;
                Raise(LevelPlayPayloadMapper.CreateLifecycle(RewardedAdEventKind.LoadFailed,
                    adUnitId: _config.RewardedAdUnitId));
            }
        }

        private bool HasConsumableLoadedResult()
        {
            if (!_loadedAvailable) return false;
            if (_identityQuarantineAfterGeneration <= 0L) return true;
            return _loadedIdentity.HasAuctionIdentity &&
                _loadedIdentity.Generation > _identityQuarantineAfterGeneration;
        }

        private void OnAdLoaded(LevelPlayAdSnapshot info)
        {
            if (_disposed || _failed || !_loadInFlight) return;
            _loadInFlight = false;
            _loadedAvailable = true;
            if (_loadGeneration > 0L && HasStableId(info))
                _loadedIdentity = new LoadedIdentity(_loadGeneration, info);
            Raise(Lifecycle(RewardedAdEventKind.Loaded, null, info));
        }

        private void OnAdLoadFailed(LevelPlayErrorSnapshot error)
        {
            if (_disposed || _failed || !_loadInFlight) return;
            _loadInFlight = false;
            _loadedAvailable = false;
            _loadedIdentity = default;
            Raise(LevelPlayPayloadMapper.CreateLifecycle(RewardedAdEventKind.LoadFailed,
                adUnitId: FirstNonBlank(error.AdUnitId, _config.RewardedAdUnitId),
                adId: error.AdId, errorCode: error.ErrorCode));
        }

        private void OnAdDisplayed(LevelPlayAdSnapshot info)
        {
            if (!TryResolve(info, allowNewBinding: true, allowNoStableId: false,
                confirmsCurrentShow: true, trustedLoadedIdentity: false,
                out var context)) return;
            Raise(Lifecycle(RewardedAdEventKind.Displayed, context, info));
        }

        private void OnAdDisplayFailed(LevelPlayAdSnapshot info, LevelPlayErrorSnapshot error)
        {
            var correlation = new LevelPlayAdSnapshot(FirstNonBlank(info.AdId, error.AdId),
                info.AuctionId, FirstNonBlank(info.AdUnitId, error.AdUnitId),
                info.PlacementId, info.NetworkName);
            ShowContext context;
            if (!HasStableId(correlation))
            {
                if (_postQuarantineStableTerminalRequired) return;
                context = _showInvocationContext;
                if (context == null || !ReferenceEquals(context, _activeContext)) return;
            }
            else if (!TryResolve(correlation, allowNewBinding: true,
                allowNoStableId: false, confirmsCurrentShow: true,
                trustedLoadedIdentity: false, out context)) return;
            RemoveContext(context, tombstoneAuction: true);
            Raise(LevelPlayPayloadMapper.CreateLifecycle(RewardedAdEventKind.DisplayFailed,
                context.AttemptId, context.PlacementId,
                FirstNonBlank(info.AdUnitId, error.AdUnitId, _config.RewardedAdUnitId),
                FirstNonBlank(info.AdId, error.AdId), info.AuctionId, info.NetworkName,
                error.ErrorCode));
        }

        private void OnAdRewarded(LevelPlayAdSnapshot info)
        {
            if (!HasStableId(info) || !TryResolve(info,
                allowNewBinding: false, allowNoStableId: false,
                confirmsCurrentShow: true, trustedLoadedIdentity: false,
                out var context) || context.RewardDelivered)
                return;

            context.RewardDelivered = true;
            Raise(Lifecycle(RewardedAdEventKind.Rewarded, context, info));
            if (_disposed) return;
            if (context.Closed) RemoveContext(context, tombstoneAuction: true);
        }

        private void OnAdClicked(LevelPlayAdSnapshot info)
        {
            if (!TryResolve(info, allowNewBinding: true, allowNoStableId: false,
                confirmsCurrentShow: true, trustedLoadedIdentity: false,
                out var context)) return;
            Raise(Lifecycle(RewardedAdEventKind.Opened, context, info));
        }

        private void OnAdClosed(LevelPlayAdSnapshot info)
        {
            if (!TryResolve(info, allowNewBinding: true,
                allowNoStableId: !_requiresNonRewardBinding, confirmsCurrentShow: true,
                trustedLoadedIdentity: false, out var context) || context.Closed)
                return;

            context.Closed = true;
            if (ReferenceEquals(_activeContext, context)) _activeContext = null;
            Raise(Lifecycle(RewardedAdEventKind.Closed, context, info));
            if (_disposed) return;
            if (context.RewardDelivered) RemoveContext(context, tombstoneAuction: true);
        }

        private void OnAdInfoChanged(LevelPlayAdSnapshot info)
        {
            TryResolve(info, allowNewBinding: true, allowNoStableId: false,
                confirmsCurrentShow: true, trustedLoadedIdentity: false, out _);
        }

        private void OnAdImpression(LevelPlayImpressionSnapshot impression)
        {
            if (_disposed || _failed) return;
            // The bridge has already copied all package values. Correlation and EventReceived
            // stay on the existing MonetizationPump's main-thread drain.
            _mainThreadQueue.Enqueue(() => DeliverImpression(impression));
        }

        private void DeliverImpression(LevelPlayImpressionSnapshot impression)
        {
            if (_disposed || _failed || !impression.RevenueUsd.HasValue) return;
            var info = new LevelPlayAdSnapshot(null, impression.AuctionId,
                impression.AdUnitId, impression.PlacementId, impression.NetworkName);
            if (TryResolve(info, allowNewBinding: true, allowNoStableId: false,
                confirmsCurrentShow: true, trustedLoadedIdentity: false,
                out var context))
            {
                if (context.RevenueDelivered) return;
                if (!LevelPlayPayloadMapper.TryCreateRevenue(context.AttemptId,
                    FirstNonBlank(impression.PlacementId, context.PlacementId),
                    FirstNonBlank(impression.AdUnitId, _config.RewardedAdUnitId), null,
                    impression.AuctionId, impression.NetworkName, impression.RevenueUsd.Value,
                    impression.Precision, out var activeEvent)) return;
                context.RevenueDelivered = true;
                Raise(activeEvent);
                return;
            }

            if (!TryGetTerminalRevenue(impression.AuctionId, out var terminal)) return;
            if (!LevelPlayPayloadMapper.TryCreateRevenue(terminal.AttemptId,
                FirstNonBlank(impression.PlacementId, terminal.PlacementId),
                FirstNonBlank(impression.AdUnitId, _config.RewardedAdUnitId), null,
                impression.AuctionId, impression.NetworkName, impression.RevenueUsd.Value,
                impression.Precision, out var terminalEvent)) return;
            RemoveTerminalRevenue(impression.AuctionId);
            Raise(terminalEvent);
        }

        private RewardedAdEvent Lifecycle(RewardedAdEventKind kind, ShowContext context,
            LevelPlayAdSnapshot info)
            => LevelPlayPayloadMapper.CreateLifecycle(kind, context?.AttemptId ?? 0L,
                FirstNonBlank(info.PlacementId, context?.PlacementId),
                FirstNonBlank(info.AdUnitId, _config.RewardedAdUnitId), info.AdId,
                info.AuctionId, info.NetworkName);

        private bool TryResolve(LevelPlayAdSnapshot info, bool allowNewBinding,
            bool allowNoStableId, bool confirmsCurrentShow,
            bool trustedLoadedIdentity, out ShowContext context)
        {
            context = null;
            if (_disposed || _failed || !PurgeExpiredContexts()) return false;
            bool hasAuction = !string.IsNullOrWhiteSpace(info.AuctionId);
            bool hasAd = !string.IsNullOrWhiteSpace(info.AdId);

            if (!hasAuction && !hasAd)
            {
                if (!allowNoStableId || _unboundContext == null) return false;
                context = _unboundContext;
                return context != null;
            }
            if (_adHistorySaturated && hasAd && !hasAuction) return false;

            if (hasAuction && _completedAuctions.Contains(info.AuctionId)) return false;
            _byAuction.TryGetValue(info.AuctionId ?? string.Empty, out var auctionContext);
            bool ambiguousAd = hasAd && _ambiguousAdIds.Contains(info.AdId);
            bool retiredAd = hasAd && _retiredAdIds.Contains(info.AdId);
            ShowContext adContext = null;
            if (hasAd && !ambiguousAd && !retiredAd)
                _byAd.TryGetValue(info.AdId, out adContext);

            // Resolve both indexes before changing either. A pair that already names two
            // contexts is malformed and must leave all state untouched.
            if (auctionContext != null && adContext != null &&
                !ReferenceEquals(auctionContext, adContext))
                return false;
            if (_auctionHistorySaturated && hasAuction && auctionContext == null &&
                !trustedLoadedIdentity)
            {
                if (adContext == null) return false;
                if (!string.IsNullOrEmpty(adContext.AuctionId) &&
                    !string.Equals(adContext.AuctionId, info.AuctionId,
                        StringComparison.Ordinal))
                    return false;
            }

            bool makeAdAmbiguous = false;
            if (auctionContext != null)
            {
                context = auctionContext;
            }
            else if (adContext != null)
            {
                // AdId may be supplied before AuctionId. If this context already owns a
                // different auction, a new auction can identify the sole unbound newer show,
                // but AdId becomes permanently ambiguous while retained in bounded history.
                if (hasAuction && !string.IsNullOrEmpty(adContext.AuctionId) &&
                    !string.Equals(adContext.AuctionId, info.AuctionId,
                        StringComparison.Ordinal))
                {
                    if (!allowNewBinding || _unboundContext == null ||
                        ReferenceEquals(_unboundContext, adContext))
                        return false;
                    context = _unboundContext;
                    makeAdAmbiguous = true;
                }
                else context = adContext;
            }
            else
            {
                if (!allowNewBinding || _unboundContext == null) return false;
                context = _unboundContext;
            }

            bool bindAuction = false;
            if (hasAuction)
            {
                if (string.IsNullOrEmpty(context.AuctionId)) bindAuction = true;
                else if (!string.Equals(context.AuctionId, info.AuctionId,
                    StringComparison.Ordinal)) return false;
            }

            bool bindAd = false;
            if (hasAd)
            {
                if (adContext != null && !ReferenceEquals(adContext, context))
                {
                    if (!makeAdAmbiguous || !bindAuction ||
                        !ReferenceEquals(context, _unboundContext)) return false;
                }
                if (string.IsNullOrEmpty(context.AdId)) bindAd = true;
                else if (!string.Equals(context.AdId, info.AdId,
                    StringComparison.Ordinal)) return false;

                if (ambiguousAd || retiredAd)
                {
                    bool knownAmbiguousContext = ambiguousAd &&
                        string.Equals(context.AdId, info.AdId, StringComparison.Ordinal) &&
                        hasAuction && !bindAuction;
                    bool distinctFreshAuction = hasAuction && bindAuction &&
                        ReferenceEquals(context, _unboundContext);
                    if (!knownAmbiguousContext && !distinctFreshAuction) return false;
                    makeAdAmbiguous = true;
                }
            }

            if (makeAdAmbiguous) MarkAdAmbiguous(info.AdId);
            if (bindAuction) BindAuction(context, info.AuctionId);
            if (bindAd) BindAd(context, info.AdId);
            if ((hasAuction || hasAd) && ReferenceEquals(_unboundContext, context))
                _unboundContext = null;
            if (confirmsCurrentShow && ReferenceEquals(_activeContext, context) &&
                (hasAuction || hasAd))
            {
                _requiresNonRewardBinding = false;
                _postQuarantineStableTerminalRequired = false;
            }
            return context != null;
        }

        private bool ConsumeLoadedIdentity(ShowContext context, out long generation,
            out bool hasAuctionIdentity)
        {
            var loaded = _loadedIdentity;
            _loadedIdentity = default;
            generation = loaded.Generation;
            hasAuctionIdentity = loaded.HasAuctionIdentity;
            if (!loaded.IsValid || loaded.Generation != _loadGeneration) return false;
            TryResolve(loaded.Info, allowNewBinding: true, allowNoStableId: false,
                confirmsCurrentShow: false, trustedLoadedIdentity: true,
                out var resolved);
            return ReferenceEquals(resolved, context);
        }

        private bool PurgeExpiredContexts()
        {
            if (!TryReadClock(out long now)) return false;
            for (int i = _contexts.Count - 1; i >= 0; i--)
            {
                var context = _contexts[i];
                if (now < context.CreatedAt || now - context.CreatedAt <= _contextLifetimeTicks)
                    continue;
                if (ReferenceEquals(context, _unboundContext))
                {
                    _requiresNonRewardBinding = true;
                    if (context.LoadGeneration > _identityQuarantineAfterGeneration)
                        _identityQuarantineAfterGeneration = context.LoadGeneration;
                }
                RemoveContext(context, tombstoneAuction: true);
            }
            return true;
        }

        private bool TryReadClock(out long now)
        {
            now = 0L;
            try
            {
                now = _monotonicTicks();
                return now >= 0L;
            }
            catch
            {
                return false;
            }
        }

        private void BindAuction(ShowContext context, string auctionId)
        {
            if (context == null || string.IsNullOrWhiteSpace(auctionId)) return;
            if (string.IsNullOrEmpty(context.AuctionId)) context.AuctionId = auctionId;
            _byAuction[auctionId] = context;
        }

        private void BindAd(ShowContext context, string adId)
        {
            if (context == null || string.IsNullOrWhiteSpace(adId)) return;
            if (string.IsNullOrEmpty(context.AdId)) context.AdId = adId;
            if (!_ambiguousAdIds.Contains(adId) && !_retiredAdIds.Contains(adId))
                _byAd[adId] = context;
        }

        private void RemoveContext(ShowContext context, bool tombstoneAuction)
        {
            if (context == null) return;
            _contexts.Remove(context);
            if (ReferenceEquals(_activeContext, context))
            {
                _activeContext = null;
                _postQuarantineStableTerminalRequired = false;
            }
            if (ReferenceEquals(_unboundContext, context)) _unboundContext = null;
            if (!string.IsNullOrEmpty(context.AuctionId))
            {
                if (context.Closed && context.RewardDelivered &&
                    !context.RevenueDelivered)
                    RememberTerminalRevenue(context);
                if (_byAuction.TryGetValue(context.AuctionId, out var indexed) &&
                    ReferenceEquals(indexed, context))
                    _byAuction.Remove(context.AuctionId);
                if (tombstoneAuction) RememberCompletedAuction(context.AuctionId);
            }
            if (!string.IsNullOrEmpty(context.AdId))
            {
                if (_byAd.TryGetValue(context.AdId, out var indexed) &&
                    ReferenceEquals(indexed, context))
                    _byAd.Remove(context.AdId);
                RememberRetiredAd(context.AdId);
            }
        }

        private void RememberCompletedAuction(string auctionId)
        {
            if (string.IsNullOrWhiteSpace(auctionId) || !_completedAuctions.Add(auctionId)) return;
            _completedAuctionOrder.Enqueue(auctionId);
            while (_completedAuctionOrder.Count > MaxCompletedAuctions)
            {
                _completedAuctions.Remove(_completedAuctionOrder.Dequeue());
                _auctionHistorySaturated = true;
            }
        }

        private void RememberTerminalRevenue(ShowContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.AuctionId) ||
                _terminalRevenueByAuction.ContainsKey(context.AuctionId) ||
                !TryReadClock(out long now)) return;
            PurgeExpiredTerminalRevenue(now);
            _terminalRevenueByAuction[context.AuctionId] = new TerminalRevenueContext(
                context.AttemptId, context.PlacementId, now);
            _terminalRevenueOrder.Enqueue(context.AuctionId);
            while (_terminalRevenueOrder.Count > MaxTerminalRevenueContexts)
                _terminalRevenueByAuction.Remove(_terminalRevenueOrder.Dequeue());
        }

        private bool TryGetTerminalRevenue(string auctionId,
            out TerminalRevenueContext terminal)
        {
            terminal = null;
            if (string.IsNullOrWhiteSpace(auctionId) || !TryReadClock(out long now))
                return false;
            PurgeExpiredTerminalRevenue(now);
            return _terminalRevenueByAuction.TryGetValue(auctionId, out terminal);
        }

        private void PurgeExpiredTerminalRevenue(long now)
        {
            int count = _terminalRevenueOrder.Count;
            for (int i = 0; i < count; i++)
            {
                string auctionId = _terminalRevenueOrder.Dequeue();
                if (!_terminalRevenueByAuction.TryGetValue(auctionId, out var terminal))
                    continue;
                if (now < terminal.CreatedAt ||
                    now - terminal.CreatedAt > _contextLifetimeTicks)
                {
                    _terminalRevenueByAuction.Remove(auctionId);
                    continue;
                }
                _terminalRevenueOrder.Enqueue(auctionId);
            }
        }

        private void RemoveTerminalRevenue(string auctionId)
        {
            if (string.IsNullOrWhiteSpace(auctionId) ||
                !_terminalRevenueByAuction.Remove(auctionId)) return;
            RemoveFromOrder(_terminalRevenueOrder, auctionId);
        }

        private void RememberRetiredAd(string adId)
        {
            if (string.IsNullOrWhiteSpace(adId) || _ambiguousAdIds.Contains(adId) ||
                !_retiredAdIds.Add(adId)) return;
            _retiredAdOrder.Enqueue(adId);
            if (TrimHistory(_retiredAdIds, _retiredAdOrder, MaxAdHistory))
                _adHistorySaturated = true;
        }

        private void MarkAdAmbiguous(string adId)
        {
            if (string.IsNullOrWhiteSpace(adId)) return;
            _byAd.Remove(adId);
            if (_retiredAdIds.Remove(adId)) RemoveFromOrder(_retiredAdOrder, adId);
            if (!_ambiguousAdIds.Add(adId)) return;
            _ambiguousAdOrder.Enqueue(adId);
            TrimAmbiguousAdHistory();
        }

        private static bool TrimHistory(HashSet<string> history, Queue<string> order,
            int maximum)
        {
            bool removed = false;
            while (order.Count > maximum)
            {
                history.Remove(order.Dequeue());
                removed = true;
            }
            return removed;
        }

        private static void RemoveFromOrder(Queue<string> order, string value)
        {
            int count = order.Count;
            for (int i = 0; i < count; i++)
            {
                string item = order.Dequeue();
                if (!string.Equals(item, value, StringComparison.Ordinal))
                    order.Enqueue(item);
            }
        }

        private void TrimAmbiguousAdHistory()
        {
            while (_ambiguousAdOrder.Count > MaxAdHistory)
            {
                int candidates = _ambiguousAdOrder.Count;
                bool removed = false;
                for (int i = 0; i < candidates; i++)
                {
                    string adId = _ambiguousAdOrder.Dequeue();
                    if (IsAdIdActive(adId))
                    {
                        _ambiguousAdOrder.Enqueue(adId);
                        continue;
                    }
                    _ambiguousAdIds.Remove(adId);
                    _adHistorySaturated = true;
                    removed = true;
                    break;
                }
                if (!removed) break;
            }
        }

        private bool IsAdIdActive(string adId)
        {
            for (int i = 0; i < _contexts.Count; i++)
                if (string.Equals(_contexts[i].AdId, adId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private void Raise(RewardedAdEvent adEvent)
        {
            if (_disposed) return;
            var handlers = _eventReceived;
            if (handlers == null) return;
            foreach (Action<RewardedAdEvent> handler in handlers.GetInvocationList())
            {
                if (_disposed) return;
                try { handler(adEvent); }
                catch { }
            }
        }

        private static bool HasStableId(LevelPlayAdSnapshot info)
            => !string.IsNullOrWhiteSpace(info.AuctionId) ||
               !string.IsNullOrWhiteSpace(info.AdId);

        private static string FirstNonBlank(params string[] values)
        {
            if (values == null) return null;
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(values[i])) return values[i];
            return null;
        }

        private sealed class ShowContext
        {
            internal long AttemptId { get; }
            internal string PlacementId { get; }
            internal long CreatedAt { get; }
            internal long LoadGeneration { get; }
            internal string AuctionId;
            internal string AdId;
            internal bool Closed;
            internal bool RewardDelivered;
            internal bool RevenueDelivered;

            internal ShowContext(long attemptId, string placementId, long createdAt,
                long loadGeneration)
            {
                AttemptId = attemptId;
                PlacementId = placementId;
                CreatedAt = createdAt;
                LoadGeneration = loadGeneration;
            }
        }

        private sealed class TerminalRevenueContext
        {
            internal long AttemptId { get; }
            internal string PlacementId { get; }
            internal long CreatedAt { get; }

            internal TerminalRevenueContext(long attemptId, string placementId, long createdAt)
            {
                AttemptId = attemptId;
                PlacementId = placementId;
                CreatedAt = createdAt;
            }
        }

        private readonly struct LoadedIdentity
        {
            internal long Generation { get; }
            internal LevelPlayAdSnapshot Info { get; }
            internal bool IsValid => Generation > 0L && HasStableId(Info);
            internal bool HasAuctionIdentity => Generation > 0L &&
                !string.IsNullOrWhiteSpace(Info.AuctionId);

            internal LoadedIdentity(long generation, LevelPlayAdSnapshot info)
            {
                Generation = generation;
                Info = info;
            }
        }
    }

    internal static class LevelPlayRewardedAdRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterFactory()
        {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            long lifetime = Stopwatch.Frequency > long.MaxValue / 300L
                ? long.MaxValue
                : Stopwatch.Frequency * 300L;
            RewardedAdProviderFactory.Register(config => new LevelPlayRewardedAdProvider(config,
                new RealLevelPlaySdkBridge(), Stopwatch.GetTimestamp, lifetime));
#endif
        }
    }

    internal sealed class RealLevelPlaySdkBridge : ILevelPlaySdkBridge
    {
        private Action _initializationSucceeded;
        private Action<int> _initializationFailed;
        private bool _successHooked;
        private bool _failureHooked;

        public event Action InitializationSucceeded
        {
            add
            {
                _initializationSucceeded += value;
                if (_successHooked) return;
                _successHooked = true;
                LevelPlaySdk.OnInitSuccess += OnVendorInitializationSucceeded;
            }
            remove
            {
                _initializationSucceeded -= value;
                if (_initializationSucceeded != null || !_successHooked) return;
                _successHooked = false;
                LevelPlaySdk.OnInitSuccess -= OnVendorInitializationSucceeded;
            }
        }

        public event Action<int> InitializationFailed
        {
            add
            {
                _initializationFailed += value;
                if (_failureHooked) return;
                _failureHooked = true;
                LevelPlaySdk.OnInitFailed += OnVendorInitializationFailed;
            }
            remove
            {
                _initializationFailed -= value;
                if (_initializationFailed != null || !_failureHooked) return;
                _failureHooked = false;
                LevelPlaySdk.OnInitFailed -= OnVendorInitializationFailed;
            }
        }

        public void Initialize(string appKey)
        {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            LevelPlaySdk.Init(appKey);
#endif
        }

        public ILevelPlayRewardedAdBridge CreateRewardedAd(string adUnitId)
        {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            return new RealLevelPlayRewardedAdBridge(new LevelPlayRewardedAd(adUnitId));
#else
            return null;
#endif
        }

        private void OnVendorInitializationSucceeded(LevelPlayConfiguration configuration)
        {
            try { _initializationSucceeded?.Invoke(); }
            catch { }
        }

        private void OnVendorInitializationFailed(LevelPlayInitError error)
        {
            try { _initializationFailed?.Invoke(error?.ErrorCode ?? 0); }
            catch { }
        }
    }

    internal sealed class RealLevelPlayRewardedAdBridge : ILevelPlayRewardedAdBridge
    {
        private readonly LevelPlayRewardedAd _ad;
        private bool _disposed;

        public event Action<LevelPlayAdSnapshot> AdLoaded;
        public event Action<LevelPlayErrorSnapshot> AdLoadFailed;
        public event Action<LevelPlayAdSnapshot> AdDisplayed;
        public event Action<LevelPlayAdSnapshot, LevelPlayErrorSnapshot> AdDisplayFailed;
        public event Action<LevelPlayAdSnapshot> AdRewarded;
        public event Action<LevelPlayAdSnapshot> AdClicked;
        public event Action<LevelPlayAdSnapshot> AdClosed;
        public event Action<LevelPlayAdSnapshot> AdInfoChanged;
        public event Action<LevelPlayImpressionSnapshot> AdImpression;

        internal RealLevelPlayRewardedAdBridge(LevelPlayRewardedAd ad)
        {
            _ad = ad ?? throw new ArgumentNullException(nameof(ad));
            try
            {
                _ad.OnAdLoaded += OnVendorLoaded;
                _ad.OnAdLoadFailed += OnVendorLoadFailed;
                _ad.OnAdDisplayed += OnVendorDisplayed;
                _ad.OnAdDisplayFailed += OnVendorDisplayFailed;
                _ad.OnAdRewarded += OnVendorRewarded;
                _ad.OnAdClicked += OnVendorClicked;
                _ad.OnAdClosed += OnVendorClosed;
                _ad.OnAdInfoChanged += OnVendorInfoChanged;
                _ad.OnAdImpressionDataReady += OnVendorImpression;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Load()
        {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            _ad.LoadAd();
#endif
        }

        public void Show(string placementId)
        {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            _ad.ShowAd(placementId);
#endif
        }

        public bool IsReady()
        {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            return _ad.IsAdReady();
#else
            return false;
#endif
        }

        public bool IsPlacementCapped(string placementId)
        {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            return LevelPlayRewardedAd.IsPlacementCapped(placementId);
#else
            return true;
#endif
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _ad.OnAdLoaded -= OnVendorLoaded; } catch { }
            try { _ad.OnAdLoadFailed -= OnVendorLoadFailed; } catch { }
            try { _ad.OnAdDisplayed -= OnVendorDisplayed; } catch { }
            try { _ad.OnAdDisplayFailed -= OnVendorDisplayFailed; } catch { }
            try { _ad.OnAdRewarded -= OnVendorRewarded; } catch { }
            try { _ad.OnAdClicked -= OnVendorClicked; } catch { }
            try { _ad.OnAdClosed -= OnVendorClosed; } catch { }
            try { _ad.OnAdInfoChanged -= OnVendorInfoChanged; } catch { }
            try { _ad.OnAdImpressionDataReady -= OnVendorImpression; } catch { }
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            try { _ad.Dispose(); } catch { }
#endif
        }

        private void OnVendorLoaded(LevelPlayAdInfo info) => Emit(AdLoaded, info);
        private void OnVendorDisplayed(LevelPlayAdInfo info) => Emit(AdDisplayed, info);
        private void OnVendorClicked(LevelPlayAdInfo info) => Emit(AdClicked, info);
        private void OnVendorClosed(LevelPlayAdInfo info) => Emit(AdClosed, info);
        private void OnVendorInfoChanged(LevelPlayAdInfo info) => Emit(AdInfoChanged, info);

        private void OnVendorLoadFailed(LevelPlayAdError error)
        {
            try { AdLoadFailed?.Invoke(Copy(error)); }
            catch { }
        }

        private void OnVendorDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            try { AdDisplayFailed?.Invoke(Copy(info), Copy(error)); }
            catch { }
        }

        private void OnVendorRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
            => Emit(AdRewarded, info);

        private void OnVendorImpression(LevelPlayImpressionData impression)
        {
            // The package documents this callback as a worker thread. Copy every used property
            // before invoking the provider bridge; CreativeId is deliberately not AdId.
            try
            {
                AdImpression?.Invoke(new LevelPlayImpressionSnapshot(impression?.AuctionId,
                    impression?.MediationAdUnitId, impression?.Placement,
                    impression?.AdNetwork, impression?.Revenue, impression?.Precision));
            }
            catch { }
        }

        private static void Emit(Action<LevelPlayAdSnapshot> consumer, LevelPlayAdInfo info)
        {
            try { consumer?.Invoke(Copy(info)); }
            catch { }
        }

        private static LevelPlayAdSnapshot Copy(LevelPlayAdInfo info)
            => new LevelPlayAdSnapshot(info?.AdId, info?.AuctionId, info?.AdUnitId,
                info?.PlacementName, info?.AdNetwork);

        private static LevelPlayErrorSnapshot Copy(LevelPlayAdError error)
            => new LevelPlayErrorSnapshot(error?.ErrorCode ?? 0, error?.AdId,
                error?.AdUnitId);

        // Never invoked. Method-group and constructor-lambda binding make the Editor compiler
        // check exact 9.5.1 operation members even though all actual native calls above are
        // independently player-platform guarded.
        private static void CompileOnlyApiSurface(LevelPlayRewardedAd ad,
            LevelPlayAdInfo info, LevelPlayAdError error, LevelPlayImpressionData impression)
        {
            Func<string, LevelPlayRewardedAd.Config, LevelPlayRewardedAd> constructor =
                (id, config) => new LevelPlayRewardedAd(id, config);
            Action<string, string> initialize = LevelPlaySdk.Init;
            Action load = ad.LoadAd;
            Action<string> show = ad.ShowAd;
            Func<bool> ready = ad.IsAdReady;
            Func<string, bool> capped = LevelPlayRewardedAd.IsPlacementCapped;
            Action dispose = ad.Dispose;
            Action destroy = ad.DestroyAd;
            _ = constructor;
            _ = initialize;
            _ = load;
            _ = show;
            _ = ready;
            _ = capped;
            _ = dispose;
            _ = destroy;
            _ = ad.AdUnitId;
            _ = ad.GetAdId();
            _ = info.AdId;
            _ = info.AuctionId;
            _ = info.AdUnitId;
            _ = info.PlacementName;
            _ = info.AdNetwork;
            _ = error.ErrorCode;
            _ = error.AdId;
            _ = error.AdUnitId;
            _ = impression.AuctionId;
            _ = impression.MediationAdUnitId;
            _ = impression.Placement;
            _ = impression.AdNetwork;
            _ = impression.Revenue;
            _ = impression.Precision;
        }
    }
}
#endif
