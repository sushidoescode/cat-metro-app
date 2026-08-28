using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.PlayMode
{
    public sealed class DevelopmentConsoleGuardTests
    {
        private bool _consoleWasEnabled;
        private bool _forcedBaseline;

        [SetUp]
        public void SetUp()
        {
            _consoleWasEnabled = Debug.developerConsoleEnabled;
            Debug.developerConsoleEnabled = true;
            _forcedBaseline = Debug.developerConsoleEnabled;
        }

        [TearDown]
        public void TearDown()
        {
            Debug.developerConsoleEnabled = _consoleWasEnabled;
        }

        [Test]
        public void AndroidDevelopmentBuild_DisablesOverlay_ButErrorsStillReachTheLog()
        {
            string received = null;
            UnityEngine.Application.LogCallback capture = (message, _, type) =>
            {
                if (type == LogType.Error) received = message;
            };
            UnityEngine.Application.logMessageReceived += capture;
            try
            {
                Assert.That(DevelopmentConsoleGuard.Apply(RuntimePlatform.Android, isDebugBuild: true),
                    Is.True, "the Android development-build policy applies");

                Assert.That(Debug.developerConsoleEnabled, Is.False,
                    "the native Development Console stays disabled after later errors");
                LogAssert.Expect(LogType.Error, "console-guard-log-sentinel");
                Debug.LogError("console-guard-log-sentinel");
                Assert.That(received, Is.EqualTo("console-guard-log-sentinel"),
                    "the guard hides only Unity's overlay; diagnostics still reach logcat/log sinks");
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= capture;
            }
        }

        [TestCase(RuntimePlatform.Android, false)]
        [TestCase(RuntimePlatform.OSXPlayer, true)]
        public void OtherBuildKinds_LeaveTheConsolePolicyUntouched(
            RuntimePlatform platform, bool isDebugBuild)
        {
            Assert.That(DevelopmentConsoleGuard.Apply(platform, isDebugBuild), Is.False,
                "only Android development builds suppress the native overlay");

            Assert.That(Debug.developerConsoleEnabled, Is.EqualTo(_forcedBaseline),
                "a no-op policy leaves the observed pre-call baseline alone");
        }
    }
}
