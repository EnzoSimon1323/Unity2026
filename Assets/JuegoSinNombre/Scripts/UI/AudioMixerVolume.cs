using UnityEngine;
using UnityEngine.Audio;

namespace JuegoSinNombre.UI
{
    /// <summary>
    /// Conversion de volumen lineal (0..1, el que usa un Slider de UI) a decibeles
    /// (la escala que usan los parametros expuestos de un AudioMixer).
    /// </summary>
    public static class AudioMixerVolume
    {
        private const float MinDb = -80f;

        public static void SetVolume(AudioMixer mixer, string exposedParameter, float linear01)
        {
            if (mixer == null || string.IsNullOrEmpty(exposedParameter)) return;

            float clamped = Mathf.Clamp(linear01, 0.0001f, 1f);
            float db = linear01 <= 0.0001f ? MinDb : Mathf.Log10(clamped) * 20f;
            mixer.SetFloat(exposedParameter, db);
        }
    }
}
