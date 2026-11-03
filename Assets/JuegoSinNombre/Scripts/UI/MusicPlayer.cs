using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace JuegoSinNombre.UI
{
    /// <summary>
    /// Reproductor de musica de fondo, persistente entre escenas. Usa dos AudioSource
    /// para poder crossfadear entre tracks en vez de cortar en seco al cambiar de musica.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        [Serializable]
        private struct SceneTrack
        {
            [Tooltip("Debe coincidir exactamente con el nombre de la escena (sin la extension).")]
            public string sceneName;
            public AudioClip track;
        }

        public static MusicPlayer Instance { get; private set; }

        [SerializeField] private AudioSource sourceA;
        [SerializeField] private AudioSource sourceB;
        [SerializeField] private AudioClip defaultTrack;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private float defaultFadeDuration = 1.5f;

        [Header("Musica por escena")]
        [Tooltip("Al cargar una escena cuyo nombre coincida, se crossfadea automaticamente a su track.")]
        [SerializeField] private SceneTrack[] sceneTracks;

        [Header("Mixer")]
        [Tooltip("Grupo 'Music' del AudioMixer.")]
        [SerializeField] private AudioMixerGroup musicGroup;
        [Tooltip("Nombre del parametro expuesto en el AudioMixer para el volumen de musica (en dB).")]
        [SerializeField] private string volumeParameter = "MusicVolume";

        private AudioSource _active;
        private AudioSource _inactive;
        private Coroutine _fadeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (sourceA == null) sourceA = gameObject.AddComponent<AudioSource>();
            if (sourceB == null) sourceB = gameObject.AddComponent<AudioSource>();

            foreach (var source in new[] { sourceA, sourceB })
            {
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                if (musicGroup != null) source.outputAudioMixerGroup = musicGroup;
            }

            _active = sourceA;
            _inactive = sourceB;
        }

        private void Start()
        {
            if (playOnStart && defaultTrack != null)
                Play(defaultTrack, defaultFadeDuration);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[MusicPlayer] Escena cargada: '{scene.name}' (mode: {mode}). Tracks configurados: {sceneTracks.Length}");

            foreach (var entry in sceneTracks)
            {
                if (entry.track != null && entry.sceneName == scene.name)
                {
                    Debug.Log($"[MusicPlayer] Coincidencia encontrada para '{scene.name}', reproduciendo '{entry.track.name}'.");
                    Play(entry.track);
                    return;
                }
            }

            Debug.LogWarning($"[MusicPlayer] No hay track configurado para la escena '{scene.name}'.");
        }

        public void Play(AudioClip clip, float fadeDuration = -1f)
        {
            if (clip == null || _active.clip == clip) return;

            if (fadeDuration < 0f) fadeDuration = defaultFadeDuration;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(CrossfadeRoutine(clip, fadeDuration));
        }

        public void Stop(float fadeDuration = -1f)
        {
            if (fadeDuration < 0f) fadeDuration = defaultFadeDuration;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOutRoutine(fadeDuration));
        }

        /// <summary>
        /// Controla el volumen global de musica via el AudioMixer. Pensado para un
        /// slider de opciones. linear01 en 0..1, se convierte a dB internamente.
        /// </summary>
        public void SetVolume(float linear01)
        {
            if (musicGroup == null) return;
            AudioMixerVolume.SetVolume(musicGroup.audioMixer, volumeParameter, linear01);
        }

        private IEnumerator CrossfadeRoutine(AudioClip clip, float duration)
        {
            _inactive.clip = clip;
            _inactive.volume = 0f;
            _inactive.Play();

            float startVolumeActive = _active.volume;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float normalized = duration > 0f ? t / duration : 1f;
                _active.volume = Mathf.Lerp(startVolumeActive, 0f, normalized);
                _inactive.volume = Mathf.Lerp(0f, 1f, normalized);
                yield return null;
            }

            _active.Stop();
            _active.volume = startVolumeActive;

            (_active, _inactive) = (_inactive, _active);
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            float startVolume = _active.volume;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                _active.volume = Mathf.Lerp(startVolume, 0f, duration > 0f ? t / duration : 1f);
                yield return null;
            }

            _active.Stop();
            _active.volume = startVolume;
        }
    }
}
