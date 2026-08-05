using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Content;
using UnityEngine;

namespace CatMetro.Presentation.Hud.WavePreview
{
    // CM-C3 criterion 11 (CM-R17.1): the next TWO waves' colour and count, top 0-15% band,
    // ZERO interactive elements (no collider, no handler — the strip is information only),
    // updating as waves are consumed. A wave is consumed once its final emission tick passes.
    public sealed class WavePreviewStrip : MonoBehaviour
    {
        private GameSession _session;
        private UnityEngine.Camera _camera;
        private readonly List<GameObject> _chips = new List<GameObject>();
        private readonly List<TextMesh> _counts = new List<TextMesh>();

        public int VisibleChipCount { get; private set; }
        public string ChipSummary { get; private set; } = ""; // "red x2|blue x2" for asserts

        public static WavePreviewStrip Create(Transform parent, GameSession session,
            UnityEngine.Camera cam)
        {
            var go = new GameObject("WavePreview");
            go.transform.SetParent(parent, false);
            var strip = go.AddComponent<WavePreviewStrip>();
            strip._session = session;
            strip._camera = cam;
            for (int i = 0; i < 2; i++)
            {
                var chip = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(chip.GetComponent<Collider>()); // ZERO interactive elements
                chip.name = "chip" + i;
                chip.transform.SetParent(go.transform, false);
                chip.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
                var text = new GameObject("count").AddComponent<TextMesh>();
                text.transform.SetParent(chip.transform, false);
                text.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                text.characterSize = 0.2f;
                text.anchor = TextAnchor.MiddleCenter;
                strip._chips.Add(chip);
                strip._counts.Add(text);
            }
            strip.Refresh();
            return strip;
        }

        // In the top 0-15% band of the viewport, whatever the camera frames.
        public bool InTopBand(int chipIndex)
        {
            var vp = _camera.WorldToViewportPoint(_chips[chipIndex].transform.position);
            return vp.y >= 0.85f && vp.y <= 1.0f;
        }

        public void Refresh()
        {
            var waves = _session.Level.Dto.Waves.ToArray();
            int tick = _session.State.Tick;
            var pending = new List<WaveDto>();
            foreach (var w in waves)
            {
                int lastEmission = w.Tick + (w.Count - 1) * w.SpacingTicks;
                if (lastEmission >= tick) pending.Add(w);
            }
            pending.Sort((a, b) => a.Tick.CompareTo(b.Tick));

            VisibleChipCount = 0;
            var summary = new System.Text.StringBuilder();
            for (int i = 0; i < _chips.Count; i++)
            {
                if (i < pending.Count)
                {
                    _chips[i].SetActive(true);
                    VisibleChipCount++;
                    var band = _camera.ViewportToWorldPoint(new Vector3(0.35f + 0.3f * i, 0.93f,
                        -_camera.transform.position.z));
                    _chips[i].transform.position = new Vector3(band.x, band.y, -1.5f);
                    _chips[i].GetComponent<Renderer>().material.color = ColorFor(pending[i].Color);
                    _counts[i].text = "x" + pending[i].Count;
                    if (summary.Length > 0) summary.Append('|');
                    summary.Append(pending[i].Color).Append(" x").Append(pending[i].Count);
                }
                else
                {
                    _chips[i].SetActive(false);
                }
            }
            ChipSummary = summary.ToString();
        }

        private void LateUpdate() => Refresh();

        private static Color ColorFor(string name)
        {
            switch (name)
            {
                case "red": return new Color(0.85f, 0.2f, 0.2f);
                case "blue": return new Color(0.2f, 0.4f, 0.9f);
                case "yellow": return new Color(0.9f, 0.8f, 0.2f);
                case "green": return new Color(0.2f, 0.75f, 0.3f);
                default: return Color.magenta;
            }
        }
    }
}
