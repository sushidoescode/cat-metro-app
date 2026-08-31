using UnityEngine;

namespace CatMetro.Presentation.Cats
{
    public readonly struct CatMicroPose
    {
        public CatMicroPose(float bob, float eyeYScale, float earTwitchDegrees,
            float arrivalHeadTurnDegrees)
        {
            Bob = bob;
            EyeYScale = eyeYScale;
            EarTwitchDegrees = earTwitchDegrees;
            ArrivalHeadTurnDegrees = arrivalHeadTurnDegrees;
        }

        public float Bob { get; }
        public float EyeYScale { get; }
        public float EarTwitchDegrees { get; }
        public float ArrivalHeadTurnDegrees { get; }

        public static CatMicroPose Neutral => new CatMicroPose(0f, 1f, 0f, 0f);
    }

    // This clock has its own deterministic seed expansion. It deliberately has no dependency
    // on Domain.Pcg32 or a session seed stream, because visual cadence must not perturb replay
    // state even when a cat is re-used or hidden.
    public sealed class CatMicroMotion
    {
        public const float BlinkIntervalMinimum = 2.4f;
        public const float BlinkIntervalMaximum = 4.8f;
        public const float BlinkMinimumEyeYScale = 0.08f;
        public const float BobAmplitudeNormalized = 1f;
        public const float EarTwitchMaximumDegrees = 14f;
        public const float ArrivalHeadTurnMaximumDegrees = 16f;

        private const float BlinkDuration = 0.12f;
        private readonly float _phase;

        public CatMicroMotion(uint stableSeed)
        {
            uint mixed = Mix(stableSeed);
            float unit = (mixed & 0x00ffffffu) / 16777215f;
            BlinkInterval = Mathf.Lerp(BlinkIntervalMinimum, BlinkIntervalMaximum, unit);
            _phase = ((mixed >> 24) & 0xffu) / 255f * Mathf.PI * 2f;
        }

        public float BlinkInterval { get; }

        public CatMicroPose Evaluate(float visualTime, bool motionOff, bool arrivalMotion)
        {
            if (motionOff) return CatMicroPose.Neutral;

            float time = float.IsNaN(visualTime) || float.IsInfinity(visualTime) ? 0f : visualTime;
            float blink = BlinkScale(time);
            float bob = Mathf.Sin(time * 4.1f + _phase) * BobAmplitudeNormalized;
            float ear = Mathf.Sin(time * 2.7f + _phase * 1.7f) * EarTwitchMaximumDegrees;
            float head = arrivalMotion
                ? Mathf.Sin(time * 3.2f + _phase * 0.5f) * ArrivalHeadTurnMaximumDegrees
                : 0f;
            return new CatMicroPose(bob, blink, ear, head);
        }

        private float BlinkScale(float time)
        {
            float phase = Mathf.Repeat(time + _phase, BlinkInterval);
            if (phase >= BlinkDuration) return 1f;
            float closing = 1f - Mathf.Abs(phase / BlinkDuration * 2f - 1f);
            return Mathf.Lerp(1f, BlinkMinimumEyeYScale, closing);
        }

        private static uint Mix(uint value)
        {
            value += 0x9e3779b9u;
            value = (value ^ (value >> 16)) * 0x85ebca6bu;
            value = (value ^ (value >> 13)) * 0xc2b2ae35u;
            return value ^ (value >> 16);
        }
    }
}
