using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    /// <summary>Disposable full-topology diagnostic; never changes the source mesh or its materials.</summary>
    internal sealed class WeightedHeadMaskGeometry : IDisposable
    {
        internal const float InfluenceThreshold = 0.25f;
        private readonly SkinnedMeshRenderer _skin;
        private readonly bool _wasEnabled;
        private Mesh _baked;
        private Color[] _headColors;
        private Color[] _zeroColors;
        internal GameObject Root { get; private set; }
        internal Material Material { get; private set; }
        internal int VertexCount => _baked.vertexCount;
        internal int TriangleCount { get; private set; }
        internal int HeadVertexCount { get; private set; }

        internal WeightedHeadMaskGeometry(SkinnedMeshRenderer skin, Transform head, bool useScale = true)
        {
            _skin = skin;
            _wasEnabled = skin.enabled;
            try
            {
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                Transform[] bones = skin.bones;
                bool[] headBones = bones.Select(b => b != null && (b == head || b.IsChildOf(head))).ToArray();
                Assert.That(headBones.Any(b => b), Is.True, "the measured head must own actual skin bones");
                Assert.That(weights.Length, Is.EqualTo(skin.sharedMesh.vertexCount));
                _baked = new Mesh { name = "DiagnosticWeightedHeadBake" };
                // Keep the renderer-relative bake under the same transform. The rendered
                // full-bake control must agree with the original skin before any head metric.
                skin.BakeMesh(_baked, useScale);
                Assert.That(_baked.vertexCount, Is.EqualTo(weights.Length));
                Assert.That(_baked.subMeshCount, Is.EqualTo(skin.sharedMesh.subMeshCount));
                for (int s = 0; s < _baked.subMeshCount; s++)
                {
                    Assert.That(_baked.GetTopology(s), Is.EqualTo(MeshTopology.Triangles));
                    Assert.That(_baked.GetIndexCount(s), Is.EqualTo(skin.sharedMesh.GetIndexCount(s)),
                        "the diagnostic must retain the complete source topology");
                    TriangleCount += (int)_baked.GetIndexCount(s) / 3;
                }
                Assert.That(TriangleCount, Is.GreaterThan(0));
                var colors = new Color[weights.Length];
                for (int i = 0; i < weights.Length; i++)
                {
                    BoneWeight w = weights[i];
                    float influence = (headBones[w.boneIndex0] ? w.weight0 : 0f)
                        + (headBones[w.boneIndex1] ? w.weight1 : 0f)
                        + (headBones[w.boneIndex2] ? w.weight2 : 0f)
                        + (headBones[w.boneIndex3] ? w.weight3 : 0f);
                    colors[i] = new Color(influence, 0f, 0f, 1f);
                    if (influence >= InfluenceThreshold) HeadVertexCount++;
                }
                Assert.That(HeadVertexCount, Is.GreaterThan(1000), "a weak ear parent is not the measured paid head");
                Assert.That(HeadVertexCount, Is.LessThan(weights.Length), "the head mask cannot classify the whole rig as head");
                _headColors = colors;
                _zeroColors = new Color[weights.Length];
                _baked.colors = colors;
                Shader shader = Shader.Find("Hidden/CatMetro/Tests/WeightedHeadMask");
                Assert.That(shader, Is.Not.Null, "import the test-only weighted-mask shader");
                Assert.That(shader.isSupported, Is.True, "the diagnostic shader must run on this graphics backend");
                Material = new Material(shader);
                Material.SetFloat("_Threshold", InfluenceThreshold);
                Material.SetFloat("_HeadOnly", 1f);
                Root = new GameObject("DiagnosticWeightedHead", typeof(MeshFilter), typeof(MeshRenderer));
                Root.transform.SetParent(skin.transform, false);
                Root.layer = skin.gameObject.layer;
                Root.GetComponent<MeshFilter>().sharedMesh = _baked;
                Root.GetComponent<MeshRenderer>().sharedMaterials =
                    Enumerable.Repeat(Material, _baked.subMeshCount).ToArray();
                skin.enabled = false;
            }
            catch { Dispose(); throw; }
        }

        internal void SetZeroInfluence(bool zero) => _baked.colors = zero ? _zeroColors : _headColors;

        public void Dispose()
        {
            if (_skin != null) _skin.enabled = _wasEnabled;
            if (Root != null) Object.DestroyImmediate(Root);
            if (_baked != null) Object.DestroyImmediate(_baked);
            if (Material != null) Object.DestroyImmediate(Material);
        }
    }
}
