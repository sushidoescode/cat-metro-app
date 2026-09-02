using System.Globalization;
using CatMetro.Application.EventTaxonomy;
using CatMetro.Services;

namespace CatMetro.Application.Analytics
{
    public sealed class GameplayAnalytics
    {
        public const string CampaignMode = "campaign";
        public const string DailyMode = "daily";

        private readonly IAnalytics _analytics;
        private string _levelId;
        private string _mode;
        private int _attempt;
        private bool _active;
        private bool _completed;

        public GameplayAnalytics(IAnalytics analytics)
        {
            _analytics = analytics ?? throw new System.ArgumentNullException(nameof(analytics));
        }

        public void BeginCampaignLevel(string levelId, double difficultyTarget, bool retry,
            string fromScreen) =>
            BeginLevel(levelId, CampaignMode, difficultyTarget, retry, fromScreen);

        public void BeginDailyLevel(string levelId, double difficultyTarget, long seed,
            string dateKey)
        {
            SafeLog(Events.DailyStarted(seed, dateKey));
            BeginLevel(levelId, DailyMode, difficultyTarget, retry: false, fromScreen: "home");
        }

        public void BeginLevel(string levelId, string mode, double difficultyTarget, bool retry,
            string fromScreen)
        {
            bool sameRun = _active && _levelId == levelId && _mode == mode;
            _attempt = retry && sameRun && _attempt < int.MaxValue ? _attempt + 1 : 1;
            _levelId = levelId ?? "";
            _mode = mode ?? CampaignMode;
            _active = true;
            _completed = false;
            string difficulty = difficultyTarget.ToString("0.#######", CultureInfo.InvariantCulture);
            SafeLog(Events.LevelStarted(_levelId, _mode, _attempt, difficulty, fromScreen));
        }

        public void CompleteLevel(string levelId, int tick, int switchesUsed, int rejections,
            int overloads, int score, int perfectMaxSwitches, int twoStarScore,
            int threeStarScore)
        {
            if (!_active || _completed || _levelId != levelId) return;
            _completed = true;
            int durationSeconds = System.Math.Max(0, tick) / CatMetro.Domain.SimConstants.TicksPerSecond;
            bool perfect = rejections == 0 && overloads == 0
                && switchesUsed <= perfectMaxSwitches;
            int stars = score >= threeStarScore ? 3 : score >= twoStarScore ? 2 : 1;
            SafeLog(Events.LevelCompleted(_levelId, _mode, _attempt, durationSeconds,
                switchesUsed, perfect, score, stars));
        }

        private void SafeLog(in AnalyticsEvent e)
        {
            if (string.IsNullOrEmpty(e.Name)) return;
            try { _analytics.Log(e); } catch { }
        }
    }
}
