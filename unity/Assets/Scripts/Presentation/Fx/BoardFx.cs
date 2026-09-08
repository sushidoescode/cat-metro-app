using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Fx
{
    public enum BoardFxSprite { Puff, Heart, Star, Confetti }

    public sealed class BoardFx : MonoBehaviour
    {
        private struct TweenState
        {
            public UnityEngine.Object Owner;
            public Action<float> Sample;
            public float Elapsed, Seconds, Delay;
            public int Channel, StartFrame, FrameLimit;
            public long Id;
        }

        private const int ScaleChannel = 1;
        private const int RotationChannel = 2;
        private const int PaintChannel = 3;
        private readonly List<TweenState> _tweens = new List<TweenState>(32);
        private readonly List<TweenState> _tick = new List<TweenState>(32);
        private readonly List<ParticleSystem> _particles = new List<ParticleSystem>(16);
        private readonly ParticleSystem[] _bursts = new ParticleSystem[4];
        private readonly Material[] _materials = new Material[4];
        private readonly Texture2D[] _textures = new Texture2D[4];
        private ParticleSystem _confetti;
        private static readonly Color[] ConfettiColours = { Palette.CreamCard,
            Palette.TicketOrange, Palette.MetroTeal, Palette.SignalRed,
            Palette.HarborBlue, Palette.TabbyYellow };
        private int _nextBurst;
        private long _nextTween;
        private bool _advancing;
        public Func<bool> MotionOffSource;
        public bool MotionOff => MotionOffSource != null && MotionOffSource();
        public int ActiveTweenCount => _tweens.Count;

        public static BoardFx GetOrCreate(Transform owner, Func<bool> motionOff = null)
        {
            var fx = owner.GetComponent<BoardFx>() ?? owner.gameObject.AddComponent<BoardFx>();
            if (motionOff != null) fx.MotionOffSource = motionOff;
            return fx;
        }

        // One unscaled clock for board and chrome. Callers own the sampled property; channels
        // replace an in-flight effect on that property, settling it before taking a new baseline.
        public void Tween(UnityEngine.Object owner, float seconds, Action<float> sample,
            float delay = 0f, int channel = 0) =>
            StartTween(owner, seconds, sample, delay, channel, 0);

        private void StartTween(UnityEngine.Object owner, float seconds, Action<float> sample,
            float delay, int channel, int frameLimit)
        {
            Finish(owner, channel);
            if (owner == null || sample == null) return;
            // Finishing a callback may start newer work for this property. That latest
            // request owns the channel; the interrupted outer request must not duplicate it.
            for (int i = 0; i < _tweens.Count; i++)
                if (_tweens[i].Owner == owner && _tweens[i].Channel == channel) return;
            if (MotionOff || !isActiveAndEnabled || seconds <= 0f) { sample(1f); return; }
            _tweens.Add(new TweenState { Owner = owner, Sample = sample,
                Seconds = seconds, Delay = Mathf.Max(0f, delay), Channel = channel,
                FrameLimit = frameLimit, StartFrame = Time.frameCount, Id = ++_nextTween });
            sample(0f);
        }

        public void Finish(UnityEngine.Object owner, int channel = 0)
        {
            for (int i = _tweens.Count - 1; i >= 0; i--)
                if (_tweens[i].Owner == owner && _tweens[i].Channel == channel)
                {
                    var tween = _tweens[i];
                    _tweens.RemoveAt(i);
                    if (tween.Owner != null) tween.Sample(1f);
                    return;
                }
        }

        public static float EaseOutBack(float progress)
        {
            float t = Mathf.Clamp01(progress) - 1f;
            return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
        }

        public void Punch(Transform target, float peak = 1.12f, float seconds = 0.14f,
            float delay = 0f)
        {
            if (target == null) return;
            Finish(target, ScaleChannel);
            Vector3 neutral = target.localScale;
            Tween(target, seconds, p => target.localScale = neutral *
                (p >= 1f ? 1f : 1f + (peak - 1f) * Mathf.Sin(p * Mathf.PI)),
                delay, ScaleChannel);
        }

        public void Shake(Transform target, float degrees = 4f, float seconds = 0.2f)
        {
            if (target == null) return;
            Finish(target, RotationChannel);
            Quaternion neutral = target.localRotation;
            Tween(target, seconds, p => target.localRotation = neutral * Quaternion.Euler(
                0f, 0f, p >= 1f ? 0f : Mathf.Sin(p * Mathf.PI * 6f) * (1f - p) * degrees),
                channel: RotationChannel);
        }

        public void Press(RectTransform target, Graphic face = null)
        {
            if (target == null) return;
            Finish(target, ScaleChannel);
            Vector3 neutral = target.localScale;
            Tween(target, 0.14f, p =>
            {
                float scale = p < 0.3f ? Mathf.Lerp(1f, 0.95f, p / 0.3f)
                    : p < 0.7f ? Mathf.Lerp(0.95f, 1.02f, (p - 0.3f) / 0.4f)
                    : Mathf.Lerp(1.02f, 1f, (p - 0.7f) / 0.3f);
                target.localScale = neutral * scale;
            }, channel: ScaleChannel);
            if (face == null) return;
            Finish(face, PaintChannel);
            Color paint = face.color;
            Flash(face, new Color(paint.r * 0.9f, paint.g * 0.9f, paint.b * 0.9f, paint.a), 2);
        }

        public void Flash(Graphic face, Color color, int frames = 2)
        {
            if (face == null) return;
            Finish(face, PaintChannel);
            Color neutral = face.color;
            StartTween(face, 1f, p => face.color = p >= 1f ? neutral : color,
                0f, PaintChannel, Mathf.Max(1, frames));
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        // Also used by capture sequences: never advances GameSession or consumes its RNG.
        public void Advance(float unscaledDeltaTime, int frame = -1)
        {
            if (_advancing) return;
            if (frame < 0) frame = Time.frameCount;
            bool stop = MotionOff;
            _advancing = true;
            _tick.Clear();
            _tick.AddRange(_tweens);
            try
            {
                // A callback may replace another tween, disable this helper, or destroy it.
                // Iterate a reused snapshot; only sample entries still owned by the live list.
                for (int t = _tick.Count - 1; t >= 0; t--)
                {
                    var tween = _tick[t];
                    int i = -1;
                    for (int j = _tweens.Count - 1; j >= 0; j--)
                        if (_tweens[j].Id == tween.Id) { i = j; break; }
                    if (i < 0) continue;
                    if (tween.Owner == null) { _tweens.RemoveAt(i); continue; }
                    tween.Elapsed += Mathf.Max(0f, unscaledDeltaTime);
                    float p = stop ? 1f : tween.FrameLimit > 0
                        ? Mathf.Clamp01((float)(frame - tween.StartFrame) / tween.FrameLimit)
                        : Mathf.Clamp01((tween.Elapsed - tween.Delay) / tween.Seconds);
                    if (p >= 1f) _tweens.RemoveAt(i);
                    else _tweens[i] = tween;
                    tween.Sample(p);
                }
            }
            finally { _tick.Clear(); _advancing = false; }
            if (stop) StopParticles();
        }

        public void Emit(BoardFxSprite sprite, Vector3 worldPosition, Color color, int count = 6)
        {
            if (MotionOff || !isActiveAndEnabled || count <= 0) return;
            if (_bursts[0] == null)
                for (int i = 0; i < _bursts.Length; i++)
                    _bursts[i] = CreateParticles(transform, "Board burst " + i, BoardFxSprite.Puff);
            var ps = _bursts[_nextBurst];
            _nextBurst = (_nextBurst + 1) % _bursts.Length;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.position = worldPosition;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial(sprite);
            var main = ps.main;
            main.startColor = color;
            main.startLifetime = sprite == BoardFxSprite.Puff ? 0.4f : 0.7f;
            main.startSize = sprite == BoardFxSprite.Puff ? 0.10f : 0.15f;
            main.startSpeed = 0.35f;
            main.gravityModifier = sprite == BoardFxSprite.Puff ? 0f : -0.3f;
            ps.Play();
            ps.Emit(Mathf.Min(count, 24));
        }

        // One short shower in camera space, owned by the board so it cannot survive a load.
        // Its local seed never consumes the simulation or Unity's shared random stream.
        public void Confetti(Camera camera)
        {
            if (camera == null || MotionOff || !isActiveAndEnabled) return;
            if (_confetti == null)
            {
                _confetti = CreateParticles(transform, "Win confetti", BoardFxSprite.Confetti);
                _confetti.useAutoRandomSeed = false;
                _confetti.randomSeed = 27;
                var main = _confetti.main;
                main.maxParticles = 100;
                main.startLifetime = 2f;
                main.startSpeed = 0f;
                main.startSize3D = true;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var shape = _confetti.shape;
                shape.enabled = false;
                var size = _confetti.sizeOverLifetime;
                size.enabled = false;
                var spin = _confetti.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
                var fade = _confetti.colorOverLifetime;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, .7f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
            }
            StopConfetti();
            float depth = camera.nearClipPlane + 1f;
            Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 right = camera.ViewportToWorldPoint(new Vector3(1f, 0f, depth)) - bottomLeft;
            Vector3 up = camera.ViewportToWorldPoint(new Vector3(0f, 1f, depth)) - bottomLeft;
            var random = new System.Random(27);
            _confetti.Play();
            for (int i = 0; i < 100; i++)
            {
                float x = .03f + .94f * (float)random.NextDouble();
                float y = 1.01f + .18f * (float)random.NextDouble();
                float width = right.magnitude * Mathf.Lerp(.012f, .022f, (float)random.NextDouble());
                _confetti.Emit(new ParticleSystem.EmitParams
                {
                    position = bottomLeft + right * x + up * y,
                    velocity = up * -Mathf.Lerp(.5f, .75f, (float)random.NextDouble())
                        + right * Mathf.Lerp(-.06f, .06f, (float)random.NextDouble()),
                    startColor = ConfettiColours[i % ConfettiColours.Length],
                    startSize3D = new Vector3(width, width * 1.4f, 1f),
                }, 1);
            }
        }

        public void StopConfetti()
        {
            if (_confetti != null)
                _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public ParticleSystem CreateParticles(Transform parent, string name, BoardFxSprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.useUnscaledTime = true;
            main.duration = 1f;
            main.startLifetime = 0.9f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.025f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.35f, 1f, 1f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial(sprite);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            for (int i = _particles.Count - 1; i >= 0; i--)
                if (_particles[i] == null) _particles.RemoveAt(i);
            _particles.Add(ps);
            return ps;
        }

        private Material ParticleMaterial(BoardFxSprite sprite)
        {
            int index = (int)sprite;
            if (_materials[index] != null) return _materials[index];
            // A serialized Resources material retains this shader AND its transparent variant
            // in players; Shader.Find alone silently loses them to Android shader stripping.
            var retained = Resources.Load<Material>("Materials/Particle");
            if (retained == null) throw new InvalidOperationException("Materials/Particle is missing.");
            _textures[index] = MakeTexture(sprite);
            _materials[index] = new Material(retained) { name = "BoardFx " + sprite,
                mainTexture = _textures[index], hideFlags = HideFlags.DontSave };
            return _materials[index];
        }

        private static Texture2D MakeTexture(BoardFxSprite sprite)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "BoardFx " + sprite, wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float alpha;
                    if (sprite == BoardFxSprite.Confetti)
                        alpha = 1f;
                    else if (sprite == BoardFxSprite.Puff)
                        alpha = Mathf.SmoothStep(0f, 1f, (0.95f - radius) / 0.35f);
                    else if (sprite == BoardFxSprite.Heart)
                    {
                        float a = u * 1.2f, b = v * 1.2f;
                        float q = a * a + b * b - 0.65f;
                        alpha = Mathf.Clamp01((a * a * b * b * b - q * q * q) * 100f);
                    }
                    else
                    {
                        float step = Mathf.PI / 5f;
                        float angle = Mathf.Repeat(Mathf.Atan2(u, v), Mathf.PI * 2f);
                        int sector = (int)(angle / step);
                        float theta = angle - sector * step;
                        float a = sector % 2 == 0 ? 0.9f : 0.4f;
                        float b = sector % 2 == 0 ? 0.4f : 0.9f;
                        float boundary = a * b * Mathf.Sin(step)
                            / (b * Mathf.Sin(step - theta) + a * Mathf.Sin(theta));
                        alpha = Mathf.Clamp01((boundary - radius) * 32f);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void StopParticles()
        {
            foreach (var ps in _particles)
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            while (_tweens.Count > 0)
            {
                var tween = _tweens[_tweens.Count - 1];
                _tweens.RemoveAt(_tweens.Count - 1);
                if (tween.Owner != null) tween.Sample(1f);
            }
            StopParticles();
        }

        private void OnDestroy()
        {
            foreach (var ps in _particles)
                if (ps != null) DestroyImmediate(ps.gameObject);
            _particles.Clear();
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] != null) DestroyImmediate(_materials[i]);
                if (_textures[i] != null) DestroyImmediate(_textures[i]);
            }
        }
    }
}
