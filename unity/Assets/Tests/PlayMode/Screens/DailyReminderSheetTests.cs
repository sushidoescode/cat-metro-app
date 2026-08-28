using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;
using CatMetro.Services;

namespace CatMetro.Tests.PlayMode
{
    public sealed class DailyReminderSheetTests
    {
        private GameObject _canvasGo;
        private HomeScreenView _home;
        private ChromeRegions _regions;
        private GameObject _cameraGo;
        private RenderTexture _captureTexture;

        private HomeScreenView CreateConfiguredHome()
        {
            _canvasGo = new GameObject("TestCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
            _home = HomeScreenView.Create(canvas.transform, dailyEntryUnlocked: true,
                lifetimeDailyCompletions: 1);
            _home.Attach(_regions, () => true);
            _home.ConfigureReminder(true, false, DailyReminderSlot.Morning,
                MessagingPermission.Unknown, true, true);
            _home.Show();
            return _home;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
            if (_cameraGo != null) Object.Destroy(_cameraGo);
            if (_captureTexture != null) Object.Destroy(_captureTexture);
            _canvasGo = null;
            _cameraGo = null;
            _captureTexture = null;
            _home = null;
            _regions = null;
        }

        [UnityTest]
        public IEnumerator Prompt_PaintsExactCsvCopy_MorningSelection_AndRoutesActions()
        {
            CreateConfiguredHome();
            int accepted = 0;
            int dismissed = 0;
            _home.ReminderAccepted = () => accepted++;
            _home.ReminderDismissed = () => dismissed++;
            _home.ShowReminderPrompt();
            yield return null;

            var sheet = _home.ReminderSheet;
            sheet.LayoutForViewport(new Rect(0f, 0f, 360f, 640f), 160f);
            Canvas.ForceUpdateCanvases();
            Assert.That(sheet.IsVisible, Is.True);
            Assert.That(sheet.TitleText,
                Is.EqualTo("Would you like tomorrow’s Daily Line delivered?"));
            Assert.That(sheet.BodyText,
                Is.EqualTo("One gentle reminder around the time you choose. Nothing expires."));
            Assert.That(sheet.MorningText, Is.EqualTo("Morning · around 10:00"));
            Assert.That(sheet.AfternoonText, Is.EqualTo("Afternoon · around 15:00"));
            Assert.That(sheet.EveningText, Is.EqualTo("Evening · around 18:00"));
            Assert.That(sheet.AcceptText, Is.EqualTo("Remind me"));
            Assert.That(sheet.DismissText, Is.EqualTo("Not now"));
            Assert.That(sheet.SelectedSlot, Is.EqualTo(DailyReminderSlot.Morning));
            Assert.That(Find(sheet.gameObject, "ReminderMorning").GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal), "Morning is visibly preselected");
            AssertPaintedText(sheet.gameObject);

            Assert.That(_regions.TryResolve(sheet.AcceptRectPx.center, out var accept), Is.True);
            accept();
            Assert.That(accepted, Is.EqualTo(1));

            _home.ShowReminderPrompt();
            Assert.That(_regions.TryResolve(sheet.DismissRectPx.center, out var dismiss), Is.True);
            dismiss();
            Assert.That(dismissed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Prompt_PreservesConfiguredAfternoonSelection()
        {
            CreateConfiguredHome();
            _home.ConfigureReminder(true, false, DailyReminderSlot.Afternoon,
                MessagingPermission.Unknown, true, true);
            _home.ShowReminderPrompt();
            yield return null;

            var sheet = _home.ReminderSheet;
            Assert.That(sheet.SelectedSlot, Is.EqualTo(DailyReminderSlot.Afternoon));
            Assert.That(sheet.AfternoonText, Is.EqualTo("Afternoon · around 15:00"));
            Assert.That(Find(sheet.gameObject, "ReminderAfternoon").GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal), "configured Afternoon stays visibly selected");
            Assert.That(Find(sheet.gameObject, "ReminderMorning").GetComponent<Image>().color,
                Is.EqualTo(Palette.CreamCard), "ShowPrompt must not silently reset the slot");
        }

        [UnityTest]
        public IEnumerator Settings_PaintsProviderNeutralState_AndRoutesEveryChoice()
        {
            CreateConfiguredHome();
            _home.ConfigureReminder(true, false, DailyReminderSlot.Afternoon,
                MessagingPermission.Denied, false, true);
            bool? enabled = null;
            DailyReminderSlot slot = null;
            _home.ReminderEnabledChanged = value => enabled = value;
            _home.ReminderSlotChanged = value => slot = value;
            _home.ShowReminderSettings();
            yield return null;

            var sheet = _home.ReminderSheet;
            sheet.LayoutForViewport(new Rect(0f, 0f, 360f, 640f), 160f);
            Canvas.ForceUpdateCanvases();
            Assert.That(sheet.TitleText, Is.EqualTo("Daily reminder"));
            Assert.That(sheet.OnText, Is.EqualTo("On"));
            Assert.That(sheet.OffText, Is.EqualTo("Off"));
            Assert.That(sheet.AfternoonText, Is.EqualTo("Afternoon · around 15:00"));
            Assert.That(sheet.EveningText, Is.EqualTo("Evening · around 18:00"));
            Assert.That(sheet.StatusText,
                Is.EqualTo("Notifications are off in device settings."));
            Assert.That(sheet.OpenSettingsText, Is.EqualTo("Open notification settings"));
            Assert.That(sheet.OpenSettingsVisible, Is.True,
                "native prompting exhausted exposes the explicit settings fallback");
            Assert.That(sheet.SelectedSlot, Is.EqualTo(DailyReminderSlot.Afternoon));
            Assert.That(Find(sheet.gameObject, "ReminderOff").GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal), "configured Off state is painted");
            Assert.That(Find(sheet.gameObject, "ReminderOn").GetComponent<Image>().color,
                Is.EqualTo(Palette.CreamCard));
            Assert.That(Find(sheet.gameObject, "ReminderAfternoon").GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal), "configured slot state is painted");
            AssertPaintedText(sheet.gameObject);

            Resolve(WorldRect(Find(sheet.gameObject,
                "ReminderOpenSettings") as RectTransform).center)();
            Assert.That(enabled, Is.True,
                "Task 5 chooses native prompt versus settings fallback from configured state");
            enabled = null;
            Resolve(sheet.OnRectPx.center)();
            Assert.That(enabled, Is.True);
            Resolve(sheet.MorningRectPx.center)();
            Assert.That(slot, Is.EqualTo(DailyReminderSlot.Morning));
            Resolve(sheet.AfternoonRectPx.center)();
            Assert.That(slot, Is.EqualTo(DailyReminderSlot.Afternoon));
            Resolve(sheet.EveningRectPx.center)();
            Assert.That(slot, Is.EqualTo(DailyReminderSlot.Evening));
            Resolve(sheet.OffRectPx.center)();
            Assert.That(enabled, Is.False);
            Resolve(sheet.CloseRectPx.center)();
            Assert.That(sheet.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator ModalBlocker_OutranksHome_ControlsOutrankBlocker()
        {
            CreateConfiguredHome();
            int campaign = 0;
            int daily = 0;
            int accepted = 0;
            _home.LevelSelected = () => campaign++;
            _home.DailySelected = () => daily++;
            _home.ReminderAccepted = () => accepted++;
            _home.ShowReminderPrompt();
            yield return null;

            var sheet = _home.ReminderSheet;
            var expectedBlocker = DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Prompt, false).Blocker;
            Assert.That(expectedBlocker.Contains(_home.PinPaintedRectPx.center), Is.True);
            Assert.That(expectedBlocker.Contains(_home.DailyPinPaintedRectPx.center), Is.True,
                "the injected safe-area blocker covers the real Daily target center");
            Resolve(_home.PinPaintedRectPx.center)();
            _home.ShowReminderPrompt();
            Resolve(_home.DailyPinPaintedRectPx.center)();
            Assert.That(campaign, Is.Zero, "the modal blocker absorbs Campaign background taps");
            Assert.That(daily, Is.Zero, "the modal blocker absorbs Daily background taps");
            _home.ShowReminderPrompt();
            Assert.That(sheet.IsVisible, Is.True);

            int acceptedBeforeControlTap = accepted;
            Resolve(sheet.AcceptRectPx.center)();
            Assert.That(accepted, Is.EqualTo(acceptedBeforeControlTap + 1),
                "a real control has strictly greater priority than the blocker");
        }

        [UnityTest]
        public IEnumerator Layout_RealWorldCornersKeepPaintedControlsInsideTheCard()
        {
            CreateConfiguredHome();
            _home.ShowReminderPrompt();
            var sheet = _home.ReminderSheet;
            sheet.LayoutForViewport(new Rect(0f, 0f, 360f, 640f), 160f);
            Canvas.ForceUpdateCanvases();

            var card = WorldRect(sheet.CardTransform);
            foreach (string name in new[]
            {
                "ReminderTitle", "ReminderBody", "ReminderMorning", "ReminderAfternoon",
                "ReminderEvening", "ReminderAccept", "ReminderDismiss",
            })
            {
                var transform = Find(sheet.gameObject, name) as RectTransform;
                Assert.That(transform, Is.Not.Null, name);
                AssertContained(WorldRect(transform), card, name);
            }

            var decoy = new GameObject("outside-decoy");
            try
            {
                var rect = decoy.AddComponent<RectTransform>();
                rect.position = new Vector3(card.xMax + 100f, card.yMax + 100f, 0f);
                rect.sizeDelta = new Vector2(10f, 10f);
                Assert.Throws<AssertionException>(() =>
                    AssertContained(WorldRect(rect), card, "positive-control"));
            }
            finally { Object.Destroy(decoy); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisibleSettings_ConfigureRefreshesFallbackPaintAndRegion()
        {
            CreateConfiguredHome();
            _home.ShowReminderSettings();
            yield return null;
            var sheet = _home.ReminderSheet;
            int withoutFallback = _regions.Count;
            Assert.That(sheet.OpenSettingsVisible, Is.False);

            _home.ConfigureReminder(true, false, DailyReminderSlot.Morning,
                MessagingPermission.Denied, false, true);
            Assert.That(sheet.OpenSettingsVisible, Is.True);
            Assert.That(_regions.Count, Is.EqualTo(withoutFallback + 1));

            _home.ConfigureReminder(true, false, DailyReminderSlot.Morning,
                MessagingPermission.Authorized, false, true);
            Assert.That(sheet.OpenSettingsVisible, Is.False);
            Assert.That(_regions.Count, Is.EqualTo(withoutFallback));
        }

        [UnityTest]
        public IEnumerator ConfigureBeforeAttach_SettingsRegistersControlsAndModalBlocker()
        {
            _canvasGo = new GameObject("AttachOrderCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
            _home = HomeScreenView.Create(canvas.transform, dailyEntryUnlocked: true,
                lifetimeDailyCompletions: 1);

            _home.ConfigureReminder(true, false, DailyReminderSlot.Morning,
                MessagingPermission.Unknown, true, true);
            _home.Attach(_regions, () => true);
            int campaign = 0;
            _home.LevelSelected = () => campaign++;
            _home.Show();
            _home.ShowReminderSettings();
            yield return null;

            var sheet = _home.ReminderSheet;
            Assert.That(sheet.IsVisible, Is.True);
            Assert.That(_regions.Count, Is.GreaterThan(3),
                "the pre-existing sheet receives the later ChromeRegions attachment");
            Assert.That(_regions.TryResolve(_home.PinPaintedRectPx.center, out var blocked),
                Is.True);
            blocked();
            Assert.That(campaign, Is.Zero,
                "the sheet blocker wins over Home even when Configure preceded Attach");

            Assert.That(_regions.TryResolve(sheet.CloseRectPx.center, out var close), Is.True);
            close();
            Assert.That(sheet.IsVisible, Is.False,
                "a real settings control also resolves through the late-attached registry");
        }

        [UnityTest]
        public IEnumerator HideDisableDestroy_AndHomeHide_UnregisterEverySheetRegion()
        {
            CreateConfiguredHome();
            _home.ShowReminderPrompt();
            yield return null;
            var sheet = _home.ReminderSheet;
            int homeOnlyCount = 3;
            Assert.That(_regions.Count, Is.GreaterThan(homeOnlyCount));

            sheet.Hide();
            Assert.That(_regions.Count, Is.EqualTo(homeOnlyCount));

            _home.ShowReminderPrompt();
            sheet.gameObject.SetActive(false);
            Assert.That(_regions.Count, Is.EqualTo(homeOnlyCount), "disable unregisters all modal regions");
            sheet.gameObject.SetActive(true);
            Assert.That(_regions.Count, Is.GreaterThan(homeOnlyCount),
                "direct re-enable honors the shown-intent lifecycle law");

            _home.Hide();
            Assert.That(_regions.Count, Is.Zero, "Home hide unregisters gear, entries, and sheet");

            _home.Show();
            _home.ShowReminderPrompt();
            Object.Destroy(sheet.gameObject);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(homeOnlyCount),
                "destroy unregisters every modal region while Home remains available");
        }

        [UnityTest]
        public IEnumerator SheetTree_IsRenderOnly_WithNoSecondInputConsumer()
        {
            CreateConfiguredHome();
            _home.ShowReminderSettings();
            yield return null;
            var sheet = _home.ReminderSheet;

            Assert.That(sheet.GetComponentsInChildren<Selectable>(true).Length, Is.Zero);
            Assert.That(sheet.GetComponentsInChildren<GraphicRaycaster>(true).Length, Is.Zero);
            Assert.That(sheet.GetComponentsInChildren<Animator>(true).Length
                + sheet.GetComponentsInChildren<Animation>(true).Length, Is.Zero);
            Assert.That(sheet.GetComponentsInChildren<Image>(true).Length, Is.GreaterThan(0));
            Assert.That(sheet.GetComponentsInChildren<TMP_Text>(true).Length, Is.GreaterThan(0));

            var decoy = new GameObject("decoy");
            try
            {
                decoy.AddComponent<Button>();
                Assert.That(decoy.GetComponentsInChildren<Selectable>(true).Length, Is.EqualTo(1));
            }
            finally { Object.Destroy(decoy); }
        }

        [UnityTest]
        public IEnumerator CapturePrompt_917x2048_AfterBoundFrame_EmitsPng()
        {
            const int width = 917;
            const int height = 2048;
            const float dpi = 408f;
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            const string path = "/private/tmp/cat-metro-daily-reminder-prompt.png";

            _captureTexture = new RenderTexture(width, height, 24);
            _cameraGo = new GameObject("ReminderCaptureCamera");
            var camera = _cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.WarmPaper;
            camera.targetTexture = _captureTexture;

            _canvasGo = new GameObject("ReminderCaptureCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            _regions = new ChromeRegions();
            _home = HomeScreenView.Create(canvas.transform, dailyEntryUnlocked: true,
                lifetimeDailyCompletions: 4);
            _home.Attach(_regions, () => true);
            _home.ConfigureReminder(true, false, DailyReminderSlot.Morning,
                MessagingPermission.Unknown, true, true);
            _home.Show();
            _home.ShowReminderPrompt();

            yield return null;
            _home.LayoutForViewport(safeArea, dpi);
            Canvas.ForceUpdateCanvases();
            yield return null;
            camera.Render();

            RenderTexture.active = _captureTexture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.Destroy(image);

            Assert.That(File.Exists(path), Is.True);
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(10000),
                "the evidence is a substantial rendered frame, not an empty placeholder");
        }

        private System.Action Resolve(Vector2 point)
        {
            Assert.That(_regions.TryResolve(point, out var action), Is.True,
                "positive control: the point resolves through the real ChromeRegions registry");
            return action;
        }

        private static void AssertPaintedText(GameObject root)
        {
            var texts = root.GetComponentsInChildren<TMP_Text>(false);
            Assert.That(texts.Length, Is.GreaterThan(0));
            foreach (var text in texts)
            {
                text.ForceMeshUpdate();
                Assert.That(text.text, Does.Not.Contain("??"), text.gameObject.name);
                Assert.That(text.textInfo.characterCount, Is.GreaterThan(0), text.gameObject.name);
                Assert.That(text.isTextOverflowing, Is.False, text.gameObject.name);
            }
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform;
            return null;
        }

        private static Rect WorldRect(RectTransform transform)
        {
            var corners = new Vector3[4];
            transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertContained(Rect inner, Rect outer, string name)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 0.01f), name + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 0.01f), name + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 0.01f), name + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 0.01f), name + " top");
        }
    }
}
