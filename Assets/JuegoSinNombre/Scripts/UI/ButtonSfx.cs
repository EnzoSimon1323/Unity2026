using UnityEngine;
using UnityEngine.EventSystems;

namespace JuegoSinNombre.UI
{
    /// <summary>
    /// Agrega sonido de hover y de click a cualquier elemento de UI con Raycast Target
    /// (Button, Image, etc). Se apoya en UiAudioPlayer para no requerir un AudioSource propio.
    /// </summary>
    public class ButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiAudioPlayer.Instance != null)
                UiAudioPlayer.Instance.PlaySfx(hoverClip);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (UiAudioPlayer.Instance != null)
                UiAudioPlayer.Instance.PlaySfx(clickClip);
        }
    }
}
