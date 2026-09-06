using System;
using CatMetro.Presentation.Props;
using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Fx
{
    // Driven by BoardView's existing presentation update, never by the simulation clock.
    public sealed class BoardAmbientFx
    {
        private readonly BoardFx _fx;
        private readonly Camera _camera;
        private readonly Func<bool> _screensVisible, _homeVisible;
        private ParticleSystem _parkedSteam;
        private bool _breathing, _screensWereVisible, _searchedForEngine;
        private float _restSize, _lastSize, _nextPuff, _lastTime = float.NegativeInfinity;

        public BoardAmbientFx(BoardFx fx, Camera camera, Func<bool> screensVisible, Func<bool> homeVisible)
        {
            _fx = fx; _camera = camera;
            _screensVisible = screensVisible; _homeVisible = homeVisible;
        }

        public void Advance(float time)
        {
            if (_fx.MotionOff || !_screensVisible()) { Stop(); _lastTime = time; return; }
            if (!_screensWereVisible || time < _lastTime) _nextPuff = time + 4f;
            _screensWereVisible = true;
            _lastTime = time;
            if (!_searchedForEngine) { _searchedForEngine = true; FindParkedEngine(); }
            if (_parkedSteam != null && time >= _nextPuff)
            {
                _parkedSteam.Play();
                _parkedSteam.Emit(1);
                _nextPuff = time + 4f;
            }
            if (_camera == null) return;
            if (!_homeVisible()) { RestCamera(); return; }
            // A viewport refit owns its new size even while Home is breathing.
            if (!_breathing || !Mathf.Approximately(_camera.orthographicSize, _lastSize))
                _restSize = _camera.orthographicSize;
            _breathing = true;
            _lastSize = _restSize * (1f + 0.006f * Mathf.Sin(time * Mathf.PI / 3f));
            _camera.orthographicSize = _lastSize;
        }

        public void Stop()
        {
            RestCamera();
            _screensWereVisible = false;
            if (_parkedSteam != null)
                _parkedSteam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void RestCamera()
        {
            if (_breathing && _camera != null && Mathf.Approximately(_camera.orthographicSize, _lastSize))
                _camera.orthographicSize = _restSize;
            _breathing = false;
        }

        private void FindParkedEngine()
        {
            foreach (var prop in _fx.GetComponentsInChildren<BoardPropInstance>())
            {
                if (prop.Role != "parked-engine") continue;
                Transform funnel = null;
                foreach (var part in prop.GetComponentsInChildren<Transform>())
                    if (part.name.IndexOf("funnel", StringComparison.OrdinalIgnoreCase) >= 0)
                    { funnel = part; break; }
                _parkedSteam = _fx.CreateParticles(prop.transform, "Parked steam", BoardFxSprite.Puff);
                // Provider meshes may be a single part. In that case use their measured top,
                // with the emission point kept in presentation space and away from model bytes.
                if (funnel != null) _parkedSteam.transform.position = funnel.position;
                else
                {
                    bool found = false;
                    Bounds bounds = default;
                    foreach (var renderer in prop.GetComponentsInChildren<Renderer>())
                    {
                        if (renderer is ParticleSystemRenderer) continue;
                        var world = renderer.bounds;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Vector3 point = _fx.transform.InverseTransformPoint(world.center +
                                Vector3.Scale(world.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                            if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                            else bounds.Encapsulate(point);
                        }
                    }
                    _parkedSteam.transform.position = _fx.transform.TransformPoint(
                        new Vector3(bounds.center.x, bounds.center.y, bounds.min.z));
                }
                var main = _parkedSteam.main;
                main.startSize = 0.2f;
                main.startSpeed = 0.12f;
                main.startColor = Palette.WarmPaper;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                return;
            }
        }
    }
}
