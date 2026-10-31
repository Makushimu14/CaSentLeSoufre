using System.Collections.Generic;
using CaSentLeSoufre.Core;
using UnityEngine;

namespace CaSentLeSoufre.App
{
    /// <summary>Les effets sonores du jeu (fichiers dans Assets/Resources/Sounds, générés par Tools/SoundArt).</summary>
    public enum Sound
    {
        CardPlace,
        CardSlide,
        CardDeal,
        PickUp,
        Burn,
        Power,
        JumpIn,
        YourTurn,
        Win,
        Lose,
        RoundEnd,
        Click,
        Select
    }

    /// <summary>
    /// Joue les effets sonores et la musique d'ambiance, avec les volumes choisis par le joueur (enregistrés dans
    /// ses préférences). Se crée tout seul au premier appel ; un son absent est simplement ignoré.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int VoiceCount = 10;
        private const string SoundsFolder = "Sounds/";
        private const string MusicName = "ambient";

        private static AudioManager _instance;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _missing = new HashSet<string>();
        private AudioSource[] _voices;
        private AudioSource _music;
        private int _nextVoice;

        public static float MusicVolume { get; private set; } = MatchSetupModel.DefaultMusicVolume;
        public static float EffectsVolume { get; private set; } = MatchSetupModel.DefaultEffectsVolume;

        // ---------------------------------------------------------------- Utilisation

        /// <summary>Joue un effet. <paramref name="pitch"/> varie la hauteur (1 = normal) pour éviter la répétition.</summary>
        public static void Play(Sound sound, float volume = 1f, float pitch = 1f)
        {
            var manager = Instance;
            if (manager != null) manager.PlayEffect(sound, volume, pitch);
        }

        /// <summary>Lance la musique d'ambiance en boucle (sans effet si elle joue déjà).</summary>
        public static void StartMusic()
        {
            var manager = Instance;
            if (manager != null) manager.EnsureMusic();
        }

        /// <summary>Applique de nouveaux volumes (0 à 1). La musique suit tout de suite.</summary>
        public static void SetVolumes(float music, float effects)
        {
            MusicVolume = MatchSetupModel.SanitizeVolume(music, MatchSetupModel.DefaultMusicVolume);
            EffectsVolume = MatchSetupModel.SanitizeVolume(effects, MatchSetupModel.DefaultEffectsVolume);
            if (_instance != null && _instance._music != null) _instance._music.volume = MusicVolume;
        }

        /// <summary>Enregistre les volumes dans les préférences, sans toucher au reste.</summary>
        public static void Persist()
        {
            var preferences = SetupStorage.Load() ?? new SetupPreferences();
            preferences.MusicVolume = MusicVolume;
            preferences.EffectsVolume = EffectsVolume;
            SetupStorage.Save(preferences);
        }

        // ---------------------------------------------------------------- Mise en place

        private static AudioManager Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var host = new GameObject("Audio");
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<AudioManager>();

                // Volumes enregistrés : le premier appel peut venir d'un clic avant que le menu ne les ait appliqués.
                var saved = SetupStorage.Load();
                if (saved != null) SetVolumes(saved.MusicVolume, saved.EffectsVolume);

                _instance.Build();
                return _instance;
            }
        }

        private void Build()
        {
            // Sans écouteur dans la scène, rien ne s'entendrait.
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f;
            }

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = MusicVolume;
        }

        private void EnsureMusic()
        {
            if (_music == null || _music.isPlaying) return;

            var clip = Load(MusicName);
            if (clip == null) return;
            _music.clip = clip;
            _music.volume = MusicVolume;
            _music.Play();
        }

        private void PlayEffect(Sound sound, float volume, float pitch)
        {
            if (_voices == null || EffectsVolume <= 0f) return;

            var clip = Load(FileOf(sound));
            if (clip == null) return;

            // Chaque effet prend la voix suivante : les sons proches ne se coupent pas entre eux.
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % VoiceCount;
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = Mathf.Clamp01(volume * EffectsVolume);
            voice.Play();
        }

        private AudioClip Load(string name)
        {
            if (_missing.Contains(name)) return null;
            if (_clips.TryGetValue(name, out var cached) && cached != null) return cached;

            var clip = Resources.Load<AudioClip>(SoundsFolder + name);
            if (clip == null)
            {
                _missing.Add(name);
                return null;
            }

            _clips[name] = clip;
            return clip;
        }

        private static string FileOf(Sound sound)
        {
            switch (sound)
            {
                case Sound.CardPlace: return "card_place";
                case Sound.CardSlide: return "card_slide";
                case Sound.CardDeal: return "card_deal";
                case Sound.PickUp: return "pick_up";
                case Sound.Burn: return "burn";
                case Sound.Power: return "power";
                case Sound.JumpIn: return "jump_in";
                case Sound.YourTurn: return "your_turn";
                case Sound.Win: return "win";
                case Sound.Lose: return "lose";
                case Sound.RoundEnd: return "round_end";
                case Sound.Click: return "click";
                default: return "select";
            }
        }
    }
}
