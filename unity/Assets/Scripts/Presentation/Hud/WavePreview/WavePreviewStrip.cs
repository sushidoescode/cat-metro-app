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
        private readonly List<TextMesh> _symbols = new List<TextMesh>();
        private readonly List<Renderer> _renderers = new List<Renderer>(); // review S6: cached
        private TextMesh _flipBudget;
        private int _lastRefreshTick = -1;

        public int VisibleChipCount { get; private set; }
        public string ChipSummary { get; private set; } = ""; // "red x2|blue x2" for asserts
        public string FlipSummary => _flipBudget != null && _flipBudget.gameObject.activeSelf
            ? _flipBudget.text : "";

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
                var renderer = chip.GetComponent<Renderer>();
                renderer.sharedMaterial = Board.GreyboxMaterial.Shared;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Object.Destroy(chip.GetComponent<Collider>()); // ZERO interactive elements
                chip.name = "chip" + i;
                chip.transform.SetParent(go.transform, false);
                chip.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
                var text = new GameObject("count").AddComponent<TextMesh>();
                text.transform.SetParent(chip.transform, false);
                text.transform.localPosition = new Vector3(0.22f, 0f, -0.01f);
                text.characterSize = 0.2f;
                text.anchor = TextAnchor.MiddleCenter;
                var textRenderer = text.GetComponent<Renderer>();
                textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
                var symbol = new GameObject("cat-token").AddComponent<TextMesh>();
                symbol.transform.SetParent(chip.transform, false);
                symbol.transform.localPosition = new Vector3(-0.24f, 0f, -0.01f);
                symbol.characterSize = 0.2f;
                symbol.anchor = TextAnchor.MiddleCenter;
                symbol.alignment = TextAlignment.Center;
                symbol.color = Theme.Palette.InkNavy;
                var symbolRenderer = symbol.GetComponent<Renderer>();
                symbolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                symbolRenderer.receiveShadows = false;
                strip._chips.Add(chip);
                strip._counts.Add(text);
                strip._symbols.Add(symbol);
                strip._renderers.Add(renderer);
            }

            strip._flipBudget = new GameObject("flip-budget").AddComponent<TextMesh>();
            strip._flipBudget.transform.SetParent(go.transform, false);
            strip._flipBudget.characterSize = 0.15f;
            strip._flipBudget.anchor = TextAnchor.MiddleCenter;
            strip._flipBudget.alignment = TextAlignment.Center;
            strip._flipBudget.color = Theme.Palette.InkNavy;
            var budgetRenderer = strip._flipBudget.GetComponent<Renderer>();
            budgetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            budgetRenderer.receiveShadows = false;
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
            _lastRefreshTick = _session.State.Tick;
            var waves = _session.Level.Dto.Waves.ToArray();
            int tick = _session.State.Tick;
            var pending = new List<WaveDto>();
            foreach (var w in waves)
            {
                int lastEmission = w.Tick + (w.Count - 1) * w.SpacingTicks;
                if (lastEmission >= tick) pending.Add(w);
            }
            // review N8: STABLE order — ties on Tick keep the authored index order
            var indexed = new List<(WaveDto w, int i)>();
            for (int i = 0; i < pending.Count; i++) indexed.Add((pending[i], i));
            indexed.Sort((a, b) => a.w.Tick != b.w.Tick
                ? a.w.Tick.CompareTo(b.w.Tick) : a.i.CompareTo(b.i));
            for (int i = 0; i < pending.Count; i++) pending[i] = indexed[i].w;

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
                    _renderers[i].material.color = ColorFor(pending[i].Color);
                    _counts[i].text = "x" + pending[i].Count;
                    _symbols[i].text = TokenGlyph(pending[i]);
                    if (summary.Length > 0) summary.Append('|');
                    summary.Append(pending[i].Color).Append(" x").Append(pending[i].Count);
                }
                else
                {
                    _chips[i].SetActive(false);
                }
            }
            ChipSummary = summary.ToString();

            var flipStatus = _session.FlipStatus;
            _flipBudget.gameObject.SetActive(flipStatus.IsBudgeted);
            if (flipStatus.IsBudgeted)
            {
                var band = _camera.ViewportToWorldPoint(new Vector3(0.86f, 0.93f,
                    -_camera.transform.position.z));
                _flipBudget.transform.position = new Vector3(band.x, band.y, -1.5f);
                _flipBudget.text = Strings.UiStrings.Get("hud.flips")
                    .Replace("{used}", flipStatus.Used.ToString())
                    .Replace("{limit}", flipStatus.PerfectMaxSwitches.ToString());
                _flipBudget.color = flipStatus.RemainingToPerfect > 0
                    ? Theme.Palette.InkNavy : Theme.Palette.SignalRed;
            }
        }

        // review S6: no per-frame churn — the pending set can only change when the sim tick
        // moves, so refresh exactly then.
        private void LateUpdate()
        {
            if (_session.State.Tick != _lastRefreshTick) Refresh();
        }

        private static Color ColorFor(string name)
        {
            switch (name)
            {
                case "red": return new Color(0.85f, 0.2f, 0.2f);
                case "blue": return new Color(0.2f, 0.4f, 0.9f);
                case "yellow": return new Color(0.9f, 0.8f, 0.2f);
                case "green": return new Color(0.2f, 0.75f, 0.3f);
                case "wild": return Theme.Palette.CreamCard;
                default: return Color.magenta;
            }
        }

        private static string TokenGlyph(WaveDto wave)
        {
            string glyph;
            switch (wave.Shape)
            {
                case "square": glyph = "■"; break;
                case "triangle": glyph = "▲"; break;
                default: glyph = "●"; break;
            }
            if (wave.Stray) glyph += "↯";
            if (wave.Express) glyph += "⚡";
            return glyph;
        }
    }
}
