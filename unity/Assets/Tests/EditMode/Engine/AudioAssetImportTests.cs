using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CatMetro.Tests.Engine
{
    public sealed class AudioAssetImportTests
    {
        private const string AssetRoot = "Assets/Resources/Audio/CatMetro";
        private static readonly string[] Filenames =
        {
            "wooden-tap.wav",
            "switch-clunk.wav",
            "train-chuff-loop.wav",
            "delivery-chime.wav",
            "wrong-station-thud.wav",
            "celebrate-flourish.wav",
            "purchase-success.wav",
        };

        [Test]
        public void SourcePayload_IsExactlyTheSevenCoreSoundsAndBelowTwoMegabytes()
        {
            string[] actual = Directory.GetFiles(AssetRoot, "*.wav")
                .Select(Path.GetFileName)
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToArray();
            string[] expected = Filenames
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(actual, Is.EqualTo(expected));
            long payloadBytes = Filenames.Sum(name => new FileInfo(
                Path.Combine(AssetRoot, name)).Length);
            Assert.That(payloadBytes, Is.LessThan(2_000_000L));
        }

        [TestCaseSource(nameof(Filenames))]
        public void CoreSound_ImportsAsLowLatencyMobileMono(string filename)
        {
            string assetPath = AssetRoot + "/" + filename;
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            Assert.That(importer, Is.Not.Null, assetPath);
            Assert.That(importer.forceToMono, Is.True);
            var serializedImporter = new SerializedObject(importer);
            SerializedProperty normalize = serializedImporter.FindProperty("m_Normalize");
            SerializedProperty legacy3D = serializedImporter.FindProperty("m_3D");
            Assert.That(normalize, Is.Not.Null,
                "Unity importer serialization changed; review the level-balance pin");
            Assert.That(legacy3D, Is.Not.Null,
                "Unity importer serialization changed; review the 2D clip pin");
            Assert.That(normalize.boolValue, Is.False,
                "normalization would undo the authored quiet level balance");
            Assert.That(legacy3D.boolValue, Is.False);
            Assert.That(importer.loadInBackground, Is.False);
            Assert.That(importer.ambisonic, Is.False);

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(settings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.OverrideSampleRate));
            Assert.That(settings.sampleRateOverride, Is.EqualTo(44100));
            Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.ADPCM));
            Assert.That(settings.preloadAudioData, Is.True);

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            Assert.That(clip, Is.Not.Null, assetPath);
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
        }

        [Test]
        public void PlayerAudioPolicy_MutesOtherSourcesForUnitysFocusPath()
        {
            Assert.That(PlayerSettings.muteOtherAudioSources, Is.True,
                "Project audio focus posture drifted; notification ducking still needs a device check");
        }
    }
}
