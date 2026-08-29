using System;
using System.Collections.Generic;
using NUnit.Framework;
using CatMetro.Application.Save;

namespace CatMetro.Tests.Save
{
    public sealed class SaveRuntimeTests
    {
        [SetUp]
        public void SetUp() => SaveRuntime.ResetForTests();

        [TearDown]
        public void TearDown() => SaveRuntime.ResetForTests();

        // Catches a runtime that notifies before assigning Current, swaps in a wrapper, or
        // notifies repeatedly for the same store reference.
        [Test]
        public void Install_PublishesExactInstanceSynchronously_OncePerReference()
        {
            using var firstRoot = new SFixtures.TempRoot();
            using var secondRoot = new SFixtures.TempRoot();
            var first = SFixtures.Store(firstRoot);
            var second = SFixtures.Store(secondRoot);
            var notifications = new List<SaveStore>();
            bool currentWasPublishedDuringNotification = false;
            SaveRuntime.Installed += store =>
            {
                notifications.Add(store);
                currentWasPublishedDuringNotification = ReferenceEquals(SaveRuntime.Current, store);
            };

            Assert.That(SaveRuntime.Current, Is.Null);
            Assert.That(SaveRuntime.IsInstalled, Is.False);

            SaveRuntime.Install(null);
            Assert.That(notifications, Is.Empty, "null must not publish a store");

            SaveRuntime.Install(first);
            Assert.That(SaveRuntime.Current, Is.SameAs(first));
            Assert.That(SaveRuntime.IsInstalled, Is.True);
            Assert.That(notifications, Is.EqualTo(new[] { first }));
            Assert.That(currentWasPublishedDuringNotification, Is.True,
                "subscribers must see the instance synchronously after it becomes Current");

            SaveRuntime.Install(first);
            Assert.That(notifications, Is.EqualTo(new[] { first }),
                "reinstalling the same instance must not notify again");

            SaveRuntime.Install(second);
            Assert.That(SaveRuntime.Current, Is.SameAs(second));
            Assert.That(notifications, Is.EqualTo(new[] { first, second }),
                "a genuinely different store instance must publish exactly once");
        }

        // Catches static runtime state leaking between EditMode/PlayMode tests or Unity domain
        // reloads, including subscribers retained after reset.
        [Test]
        public void ResetForTests_ClearsCurrentStateAndSubscribers()
        {
            using var firstRoot = new SFixtures.TempRoot();
            using var secondRoot = new SFixtures.TempRoot();
            int notifications = 0;
            SaveRuntime.Installed += _ => notifications++;

            SaveRuntime.Install(SFixtures.Store(firstRoot));
            Assert.That(notifications, Is.EqualTo(1));

            SaveRuntime.ResetForTests();
            Assert.That(SaveRuntime.Current, Is.Null);
            Assert.That(SaveRuntime.IsInstalled, Is.False);

            SaveRuntime.Install(SFixtures.Store(secondRoot));
            Assert.That(notifications, Is.EqualTo(1),
                "ResetForTests must release installed subscribers");
        }
    }
}
