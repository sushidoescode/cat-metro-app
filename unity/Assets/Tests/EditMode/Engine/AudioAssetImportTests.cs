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
            "cat-mew-1.wav", "cat-mew-2.wav", "cat-mew-3.wav", "cat-mew-4.wav", "cat-mew-5.wav",
            "cat-purr-1.wav", "cat-purr-2.wav", "cat-grumble.wav",
            "win-cadence.wav", "fail-sting.wav", "train-chuff-96.wav",
        };

        [Test]
        public void SourcePayload_RetainsTheCoreSoundsAndVoicesBelowTwoMegabytes()
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
        public void PlayerAudioPolicy_LeavesOtherMusicPlayingForTheBootCourtesyProbe()
        {
            Assert.That(PlayerSettings.muteOtherAudioSources, Is.False,
                "Unity must not stop the user's music before AudioManager.isMusicActive is sampled");
        }

        [TestCase("bed", 3528000)]
        [TestCase("shaker", 3528000)]
        [TestCase("melody", 3528000)]
        [TestCase("sparkle", 3528000)]
        [TestCase("home", 1764000)]
        public void MusicMasters_StaySampleLockedAndStreamAsStereoVorbis(string name, int samples)
        {
            string path = AssetRoot + "/music/" + name + ".wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.defaultSampleSettings.loadType,
                Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(importer.defaultSampleSettings.compressionFormat,
                Is.EqualTo(AudioCompressionFormat.Vorbis));
            Assert.That(importer.defaultSampleSettings.quality, Is.EqualTo(.5f));
            Assert.That(importer.forceToMono, Is.False);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.samples, Is.EqualTo(samples));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.channels, Is.EqualTo(2));
        }
    }
}
