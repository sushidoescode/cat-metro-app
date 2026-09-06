using CatMetro.Presentation.Board;
using CatMetro.Presentation.Hud.WavePreview;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Theme
{
    // Punctuation and speed chevrons preserve the stray/express signals without letter labels.
    public sealed class PassengerStatusMarks : MonoBehaviour
    {
        private GameObject _stray, _express;
        public bool StrayVisible => _stray.activeSelf;
        public bool ExpressVisible => _express.activeSelf;

        public void Bind(bool stray, bool express)
        {
            _stray.SetActive(stray);
            _express.SetActive(express);
            _stray.transform.localPosition = stray && express ? Vector3.left * 0.48f : Vector3.zero;
            _express.transform.localPosition = stray && express ? Vector3.right * 0.32f : Vector3.zero;
        }

        public static PassengerStatusMarks Create(Transform parent, bool hud)
        {
            var root = new GameObject("Passenger status", hud ? typeof(RectTransform) : typeof(Transform));
            root.transform.SetParent(parent, false);
            var marks = root.AddComponent<PassengerStatusMarks>();
            marks._stray = NewMark(root.transform, "Stray", hud);
            marks._express = NewMark(root.transform, "Express", hud);
            Part(marks._stray.transform, hud, new Vector2(0f, 0.16f), new Vector2(0.2f, 0.55f));
            Part(marks._stray.transform, hud, new Vector2(0f, -0.32f), new Vector2(0.2f, 0.2f));
            foreach (float x in new[] { -0.23f, 0.23f })
            {
                Part(marks._express.transform, hud, new Vector2(x, 0.18f), new Vector2(0.16f, 0.52f), 35f);
                Part(marks._express.transform, hud, new Vector2(x, -0.18f), new Vector2(0.16f, 0.52f), -35f);
            }
            marks.Bind(false, false);
            return marks;
        }

        private static GameObject NewMark(Transform parent, string name, bool hud)
        {
            var go = new GameObject(name, hud ? typeof(RectTransform) : typeof(Transform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Part(Transform parent, bool hud, Vector2 position, Vector2 size, float angle = 0f)
        {
            var go = new GameObject("stroke", hud ? typeof(RectTransform) : typeof(Transform));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (hud)
            {
                var rect = (RectTransform)go.transform;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                var image = go.AddComponent<Image>();
                image.sprite = HudShapeSprites.RoundedSquare;
                image.color = Palette.InkNavy;
                image.raycastTarget = false;
            }
            else
            {
                go.transform.localPosition = position;
                go.transform.localScale = new Vector3(size.x, size.y, 0.08f);
                go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = GreyboxMaterial.Shared;
                var paint = new MaterialPropertyBlock();
                paint.SetColor("_BaseColor", Palette.InkNavy);
                paint.SetColor("_Color", Palette.InkNavy);
                renderer.SetPropertyBlock(paint);
            }
        }
    }
}
