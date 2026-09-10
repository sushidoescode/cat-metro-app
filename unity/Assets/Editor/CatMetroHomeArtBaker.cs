using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatMetro.Editor
{
    // Native sprite geometry crops the existing launcher illustration to its cat silhouette.
    // The source PNG is shared with Android and remains byte-for-byte unchanged.
    public static class CatMetroHomeArtBaker
    {
        [Serializable] private sealed class HeadMesh
        {
            public string source;
            public Crop rect;
            public Vector2[] points;
            public int[] triangles;
        }
        [Serializable] private sealed class Crop
        {
            public float x, y, width, height;
            public Rect ToRect() => new Rect(x, y, width, height);
        }

        [MenuItem("Cat Metro/Art/Bake Home Cat Mark")]
        public static void Bake()
        {
            if (!UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("Bake in Play Mode: Sprite.OverrideGeometry requires the player loop.");
            var mesh = JsonUtility.FromJson<HeadMesh>(
                File.ReadAllText("Assets/Editor/ArtSource/HomeCatMark.json"));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(mesh.source);
            if (texture == null) throw new InvalidOperationException("Launcher icon texture missing.");
            var crop = mesh.rect.ToRect();
            if (crop.width <= 0 || crop.height <= 0)
                throw new InvalidOperationException("Home cat crop has no area.");
            var sprite = Sprite.Create(texture, crop, new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect);
            sprite.name = "HomeCatMark";
            var vertices = new Vector2[mesh.points.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = mesh.points[i] - crop.position;
            var triangles = Array.ConvertAll(mesh.triangles, i => checked((ushort)i));
            sprite.OverrideGeometry(vertices, triangles);
            if (sprite.vertices.Length != vertices.Length)
                throw new InvalidOperationException("Unity rejected the Home cat silhouette.");
            const string path = "Assets/Resources/Theme/HomeCatMark.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(sprite, existing);
                UnityEngine.Object.DestroyImmediate(sprite);
            }
            else AssetDatabase.CreateAsset(sprite, path);
            AssetDatabase.SaveAssets();
            Debug.Log("HOME_ART baked cat silhouette from unchanged launcher icon.");
        }
    }
}
