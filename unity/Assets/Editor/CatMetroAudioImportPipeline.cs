using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatMetro.EditorTools
{
    /// <summary>
    /// Pins the project-original Cat Metro SFX to a small, low-latency mobile import shape.
    /// The committed PCM WAVs remain the review/provenance masters; Unity builds use ADPCM.
    /// </summary>
    public sealed class CatMetroAudioImportPipeline : AssetPostprocessor
    {
        public const string AssetRoot = "Assets/Resources/Audio/CatMetro";
        public const int SampleRate = 44100;

        private void OnPreprocessAudio()
        {
            if (!IsManagedAsset(assetPath))
                return;

            Configure((AudioImporter)assetImporter);
        }

        public override uint GetVersion()
        {
            return 1;
        }

        public static bool IsManagedAsset(string path)
        {
            return path != null
                && path.StartsWith(AssetRoot + "/", StringComparison.Ordinal)
                && path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
        }

        public static AudioImporterSampleSettings DesiredSampleSettings()
        {
            return new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.DecompressOnLoad,
                sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate,
                sampleRateOverride = SampleRate,
                compressionFormat = AudioCompressionFormat.ADPCM,
                quality = 1f,
                preloadAudioData = true,
            };
        }

        public static void Configure(AudioImporter importer)
        {
            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.ambisonic = false;
            importer.defaultSampleSettings = DesiredSampleSettings();

            // Unity 6 still serializes the mono-downmix normalization flag but no longer
            // exposes it on AudioImporter. Pin the real importer field so quiet cues are not
            // independently boosted back toward full scale.
            var serializedImporter = new SerializedObject(importer);
            SerializedProperty normalize = serializedImporter.FindProperty("m_Normalize");
            SerializedProperty legacy3D = serializedImporter.FindProperty("m_3D");
            if (normalize == null || legacy3D == null)
                throw new InvalidOperationException(
                    "Unity AudioImporter serialized fields changed; review import balance.");
            normalize.boolValue = false;
            legacy3D.boolValue = false;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("CatMetro/Audio/Reimport Project SFX")]
        public static void ReimportAll()
        {
            string[] paths = AssetDatabase.FindAssets("t:AudioClip", new[] { AssetRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsManagedAsset)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            foreach (string path in paths)
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }

            Debug.Log("CAT_METRO_AUDIO_IMPORT PASS clips=" + paths.Length);
        }
    }
}
