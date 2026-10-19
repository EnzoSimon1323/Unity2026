using UnityEngine;

/// <summary>
/// OnAnimatorIK solo lo llama Unity en el mismo GameObject que el Animator.
/// Este puente vive ahi (en el modelo/rig) y lee el estado del AimController
/// que esta en la raiz del Player.
/// </summary>
public class PlayerAimIK : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AimController aim;
    [SerializeField] private Transform cameraPivot;
    [SerializeField, Range(0f, 1f)] private float bodyAimWeight = 0.6f;

    private float _ikBlend;

    private void OnAnimatorIK(int layerIndex)
    {
        if (aim == null || !aim.isOwner)
        {
            return;
        }

        float targetWeight = aim.IsAiming ? bodyAimWeight : 0f;
        _ikBlend = Mathf.MoveTowards(_ikBlend, targetWeight, Time.deltaTime * 6f);

        if (_ikBlend <= 0.001f)
        {
            animator.SetLookAtWeight(0f);
            return;
        }

        Vector3 lookTarget = cameraPivot.position + cameraPivot.forward * 10f;
        animator.SetLookAtWeight(_ikBlend, 0.9f, 0.5f, 0f, 0.1f);
        animator.SetLookAtPosition(lookTarget);
    }
}