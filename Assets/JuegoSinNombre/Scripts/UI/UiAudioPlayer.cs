using UnityEngine;
using UnityEngine.Audio;

namespace JuegoSinNombre.UI
{
    /// <summary>
    /// Reproductor central de sonidos de UI. Un solo AudioSource compartido para
    /// no tener que agregar uno por cada boton de la interfaz.
    /// </summary>
    public class UiAudioPlayer : MonoBehaviour
    {
        public static UiAudioPlayer Instance { get; private set; }

        [SerializeField] private AudioSource audioSource;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Header("Mixer")]
        [Tooltip("Grupo 'SFX' del AudioMixer. Opcional: si no se asigna, usa el Output ya configurado en el AudioSource.")]
        [SerializeField] private AudioMixerGroup sfxGroup;
        [Tooltip("Nombre del parametro expuesto en el AudioMixer para el volumen de SFX (en dB).")]
        [SerializeField] private string volumeParameter = "SFXVolume";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            if (sfxGroup != null)
                audioSource.outputAudioMixerGroup = sfxGroup;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Controla el volumen global de SFX via el AudioMixer (afecta a todo lo
        /// ruteado al grupo SFX, no solo a este AudioSource). Pensado para un slider
        /// de opciones. linear01 en 0..1, se convierte a dB internamente.
        /// </summary>
        public void SetVolume(float linear01)
        {
            if (sfxGroup == null) return;
            AudioMixerVolume.SetVolume(sfxGroup.audioMixer, volumeParameter, linear01);
        }
    }
}
