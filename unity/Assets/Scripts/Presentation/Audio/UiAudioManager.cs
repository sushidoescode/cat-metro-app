using UnityEngine;

namespace CatMetro.Presentation.Audio
{
    public enum UiAudioCue
    {
        None,
        Tap,
        Warning,
        Win,
    }

    // UI-CHROME criterion 7: a view-tree-local, prewarmed three-source pool. It is an
    // acknowledgement channel only; every state remains complete when audio is muted.
    [DisallowMultipleComponent]
    public sealed class UiAudioManager : MonoBehaviour
    {
        private readonly AudioSource[] _sources = new AudioSource[3];
        private bool _initialized;

        public string LastCueName { get; private set; } = UiAudioCue.None.ToString();
        public int PlayCount { get; private set; }

        public static UiAudioManager Ensure(Transform viewRoot)
        {
            if (viewRoot == null) return null;
            var owner = viewRoot.root;
            var existing = owner.GetComponentInChildren<UiAudioManager>(true);
            if (existing != null)
            {
                existing.Initialize();
                return existing;
            }

            var manager = owner.gameObject.AddComponent<UiAudioManager>();
            manager.Initialize();
            return manager;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            var clips = new[]
            {
                Resources.Load<AudioClip>("CatMetroUI/Audio/ui-tap"),
                Resources.Load<AudioClip>("CatMetroUI/Audio/ui-warning"),
                Resources.Load<AudioClip>("CatMetroUI/Audio/ui-win"),
            };
            for (int i = 0; i < _sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.volume = i == 1 ? 0.74f : 0.68f;
                source.clip = clips[i];
                _sources[i] = source;
            }
        }

        public void PlayTap()
        {
            Play(UiAudioCue.Tap);
        }

        public void PlayWarning()
        {
            Play(UiAudioCue.Warning);
        }

        public void PlayWin()
        {
            Play(UiAudioCue.Win);
        }

        private void Play(UiAudioCue cue)
        {
            Initialize();
            int index = (int)cue - 1;
            if (index < 0 || index >= _sources.Length) return;
            var source = _sources[index];
            if (source.clip != null)
            {
                source.Stop();
                source.Play();
            }
            LastCueName = cue.ToString();
            PlayCount++;
        }
    }
}
