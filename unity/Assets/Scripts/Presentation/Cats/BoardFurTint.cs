using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Cats
{
    /// <summary>Owns board-clone materials; the licensed atlas and Home source stay untouched.</summary>
    // TryInstall also owns explicit Editor previews. Their destruction must restore
    // renderer bindings even when no Play Mode lifecycle has run.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class BoardFurTint : MonoBehaviour
    {
        public const string ShaderResourcePath = "CatMetroFur/BoardFur";
        public const string ShaderName = "CatMetro/Board Fur";
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
        private static readonly int FurColor = Shader.PropertyToID("_FurColor");
        private static readonly int Strength = Shader.PropertyToID("_FurStrength");
        private static readonly int DebugMask = Shader.PropertyToID("_FurDebug");
        private readonly List<Material> _ownedMaterials = new List<Material>();
        private readonly List<Binding> _bindings = new List<Binding>();
        private MaterialPropertyBlock _properties;
        private Color _routeColor = Color.white;
        private float _strength = 1f;
        private bool _showMask;

        private sealed class Binding
        {
            public Renderer Renderer;
            public Material[] SourceMaterials;
            public MaterialPropertyBlock SourceProperties;
            public bool SourceHadProperties;
        }

        public static BoardFurTint TryInstall(GameObject instance)
        {
            if (instance == null || instance == Resources.Load<GameObject>(CatModelCatalog.ResourcePath))
                return null;
            var existing = instance.GetComponent<BoardFurTint>();
            if (existing != null) return existing;
            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null || shader.name != ShaderName) return null;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return null;
            // Validate the whole mounted model before changing any renderer. This adapter
            // is deliberately limited to the opaque, textured Lit material it was measured for.
            foreach (Renderer renderer in renderers)
            {
                if (renderer.sharedMaterials.Length == 0) return null;
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null || material.shader == null
                        || material.shader.name != "Universal Render Pipeline/Lit"
                        || !material.HasProperty("_BaseMap") || material.GetTexture("_BaseMap") == null
                        || material.shaderKeywords.Length != 0
                        || Vector4.Distance(material.GetColor("_BaseColor"), Color.white) > .00001f
                        || material.GetFloat("_Surface") != 0f
                        || material.GetFloat("_AlphaClip") != 0f) return null;
            }
            var tint = instance.AddComponent<BoardFurTint>();
            // Native allocation is forbidden in a MonoBehaviour field initializer.
            // Install also supports inactive clones, before Awake has run.
            tint._properties = new MaterialPropertyBlock();
            var replacements = new Dictionary<Material, Material>();
            foreach (Renderer renderer in renderers)
            {
                Material[] source = renderer.sharedMaterials;
                var mounted = new Material[source.Length];
                for (int index = 0; index < source.Length; index++)
                {
                    if (!replacements.TryGetValue(source[index], out Material material))
                    {
                        material = new Material(source[index])
                        {
                            name = source[index].name + " (board fur)",
                            shader = shader,
                        };
                        material.SetTexture("_BaseMap", source[index].GetTexture("_BaseMap"));
                        material.SetTextureScale("_BaseMap", source[index].GetTextureScale("_BaseMap"));
                        material.SetTextureOffset("_BaseMap", source[index].GetTextureOffset("_BaseMap"));
                        material.SetColor(BaseColor, Color.white);
                        material.SetColor(LegacyColor, Color.white);
                        replacements.Add(source[index], material);
                        tint._ownedMaterials.Add(material);
                    }
                    mounted[index] = material;
                }
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                tint._bindings.Add(new Binding { Renderer = renderer,
                    SourceMaterials = source, SourceProperties = properties,
                    SourceHadProperties = renderer.HasPropertyBlock() });
                renderer.sharedMaterials = mounted;
            }
            return tint;
        }

        public void Apply(Color routeColor)
        {
            _routeColor = routeColor;
            foreach (Binding binding in _bindings)
            {
                if (binding.Renderer == null) continue;
                _properties.Clear();
                binding.Renderer.GetPropertyBlock(_properties);
                _properties.SetColor(BaseColor, Color.white);
                _properties.SetColor(LegacyColor, Color.white);
                _properties.SetColor(FurColor, routeColor);
                _properties.SetFloat(Strength, _strength);
                _properties.SetFloat(DebugMask, _showMask ? 1f : 0f);
                binding.Renderer.SetPropertyBlock(_properties);
            }
        }

        // Material preview controls also permit a natural-atlas negative control without
        // swapping texture/model bytes or changing the passenger's route identity.
        public void SetPreview(float strength, bool showMask)
        {
            _strength = float.IsFinite(strength) ? Mathf.Clamp01(strength) : 0f;
            _showMask = showMask;
            Apply(_routeColor);
        }

        private void OnDestroy()
        {
            foreach (Binding binding in _bindings)
            {
                if (binding.Renderer == null) continue;
                binding.Renderer.sharedMaterials = binding.SourceMaterials;
                binding.Renderer.SetPropertyBlock(binding.SourceHadProperties ? binding.SourceProperties : null);
            }
            foreach (Material material in _ownedMaterials)
            {
                if (material == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            _bindings.Clear();
            _ownedMaterials.Clear();
        }
    }
}
