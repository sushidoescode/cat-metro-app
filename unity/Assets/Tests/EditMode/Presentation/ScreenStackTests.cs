using System;
using NUnit.Framework;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-06 criteria 1+2, red-first (P-7): the ADR-0007 navigation law and the ADR-0006
    // breadcrumb SHAPE, by direct construction. Save I/O is deferred by contract — nothing
    // here touches a file, and the purity pin at the bottom is LABELED a pin.
    public sealed class ScreenStackTests
    {
        [Test]
        public void Push_SetsCurrentAndCount()
        {
            var stack = new ScreenStack();
            Assert.That(stack.Count, Is.EqualTo(0), "a fresh stack is empty");
            Assert.That(stack.Current, Is.Null, "empty stack has no current screen");

            stack.Push("Home");
            Assert.That(stack.Count, Is.EqualTo(1));
            Assert.That(stack.Current, Is.EqualTo("Home"));

            stack.Push("LevelIntro");
            Assert.That(stack.Count, Is.EqualTo(2));
            Assert.That(stack.Current, Is.EqualTo("LevelIntro"), "top of stack is current");
        }

        [Test]
        public void TryPop_IsLifo_AndEmptyPopReturnsFalse()
        {
            var stack = new ScreenStack();
            stack.Push("Home");
            stack.Push("LevelIntro");

            // positive control first (anti-vacuity): pops really pop, in LIFO order
            Assert.That(stack.TryPop(out var popped), Is.True);
            Assert.That(popped, Is.EqualTo("LevelIntro"));
            Assert.That(stack.Current, Is.EqualTo("Home"));

            Assert.That(stack.TryPop(out popped), Is.True);
            Assert.That(popped, Is.EqualTo("Home"));

            // PC-3: on the first Home there is nothing to pop — false, never a throw
            Assert.That(stack.TryPop(out popped), Is.False);
            Assert.That(popped, Is.Null);
            Assert.That(stack.Count, Is.EqualTo(0));
        }

        [Test]
        public void Push_RejectsNullOrEmpty_AsAWiringDefect()
        {
            var stack = new ScreenStack();
            Assert.Throws<ArgumentException>(() => stack.Push(null),
                "a null screen id is a wiring defect, never a silent no-op");
            Assert.Throws<ArgumentException>(() => stack.Push(""),
                "an empty screen id is a wiring defect too");
            Assert.That(stack.Count, Is.EqualTo(0), "rejected pushes leave the stack unchanged");
        }

        [Test]
        public void Breadcrumb_ShapeMatchesAdr0006_BottomToTop()
        {
            var stack = new ScreenStack();
            // ADR-0006: `breadcrumbs.screenStack: []` — the empty default is an EMPTY array
            Assert.That(stack.ToBreadcrumb(), Is.EqualTo(new string[0]),
                "empty stack serializes to the schema's [] default");

            stack.Push("Home");
            stack.Push("LevelIntro");
            Assert.That(stack.ToBreadcrumb(),
                Is.EqualTo(new[] { "Home", "LevelIntro" }),
                "ordered string array, bottom -> top — the ADR-0006 shape");

            // the breadcrumb is a SNAPSHOT: mutating it never mutates the stack
            var crumb = stack.ToBreadcrumb();
            crumb[0] = "Mutated";
            Assert.That(stack.ToBreadcrumb()[0], Is.EqualTo("Home"),
                "serialization hands out a copy, never internal state");
        }

        [Test]
        public void RestoreFrom_RoundTrips_AndReplacesContents()
        {
            var stack = new ScreenStack();
            stack.Push("Stale");

            stack.RestoreFrom(new[] { "Home", "LevelIntro" });
            Assert.That(stack.Count, Is.EqualTo(2), "restore REPLACES, never appends");
            Assert.That(stack.Current, Is.EqualTo("LevelIntro"),
                "the last breadcrumb entry is the top — where the player was");
            Assert.That(stack.ToBreadcrumb(), Is.EqualTo(new[] { "Home", "LevelIntro" }),
                "restore -> serialize round-trips exactly");
        }

        [Test]
        public void RestoreFrom_RejectsNullCollection_AndNullOrEmptyEntries()
        {
            var stack = new ScreenStack();
            stack.Push("Home");
            Assert.Throws<ArgumentException>(() => stack.RestoreFrom(null));
            Assert.Throws<ArgumentException>(
                () => stack.RestoreFrom(new[] { "Home", null }),
                "a null entry in a breadcrumb is corrupt data — reject, never half-restore");
            Assert.Throws<ArgumentException>(
                () => stack.RestoreFrom(new[] { "" }));
            // a rejected restore leaves the stack UNCHANGED (never half-restored)
            Assert.That(stack.ToBreadcrumb(), Is.EqualTo(new[] { "Home" }),
                "rejection is atomic — prior contents survive");
        }

        [Test]
        public void ScreenStack_IsPureCSharp_NotAnEngineObject()
        {
            // LABELED PIN (P-7, green on arrival by design): the CM-UX-01 criterion-8 pattern —
            // the pure types never derive from UnityEngine.Object, structurally.
            Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(typeof(ScreenStack)),
                Is.False, "ScreenStack is pure C#");
            Assert.That(typeof(HomeLayout).IsAbstract && typeof(HomeLayout).IsSealed,
                Is.True, "HomeLayout is a static pure-math class");
        }
    }
}
