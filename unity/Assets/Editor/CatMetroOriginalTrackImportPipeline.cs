using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace CatMetro.EditorTools
{
    // Configure the checked-in original atlas only. Runtime materials clone the
    // existing included URP material and bind this texture explicitly; no FBX,
    // prefab, paid model, scene or material asset is rewritten by this command.
    public static class CatMetroOriginalTrackImportPipeline
    {
        private const string AtlasPath = "Assets/Art/Original/Track/Resources/Track/original-toy-atlas.png";
        private const string AtlasSha256 = "3081f50fa32d3954e3417b440afd8e92ea7b24008ab071bf1556232d2f4c9c45";

        [MenuItem("Cat Metro/Art/Configure Original Track Atlas")]
        public static void Build()
        {
            if (UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("Configure the original track atlas outside Play Mode.");
            string path = Path.Combine(UnityEngine.Application.dataPath, AtlasPath.Substring(7));
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != AtlasSha256) throw new InvalidDataException("Original track atlas differs from its source hash.");
            }
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Original track atlas importer missing.");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.anisoLevel = 2;
            importer.SaveAndReimport();
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null || atlas.width != 1024 || atlas.height != 512)
                throw new InvalidDataException("Original track atlas must remain 1024x512.");
            AssetDatabase.SaveAssets();
            Debug.Log("ORIGINAL_TRACK_ATLAS PASS path=" + AtlasPath + " sha256=" + AtlasSha256
                + " srgb=true mipmaps=true readable=false wrap=Clamp");
        }

        public static void BuildAndExit()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
