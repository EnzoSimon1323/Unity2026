using UnityEngine;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Mini-camara (picture-in-picture) que aparece en una esquina de la pantalla al
    /// disparar y sigue la flecha propia en vuelo. La camara principal del jugador
    /// (AimController) nunca se toca: sigue apuntando como siempre durante el vuelo.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class ArrowFollowCamera : MonoBehaviour
    {
        private enum CameraState
        {
            Inactive,
            Following,
            ImpactHold
        }

        [Header("Referencias (Auto-detectadas si están vacías)")]
        [SerializeField] private ArcherPlayer _archerPlayer;

        [Header("Viewport (esquina de pantalla)")]
        [Tooltip("Rectangulo en coordenadas de viewport (0..1, origen abajo-izquierda). Por defecto, esquina superior izquierda.")]
        [SerializeField] private Rect _viewportRect = new Rect(0.03f, 0.60f, 0.24f, 0.25f);
        [Tooltip("Depth de render: mas alto que la camara principal para que se dibuje encima.")]
        [SerializeField] private float _cameraDepth = 10f;

        [Header("Cinemática de Vuelo")]
        [Tooltip("Distancia detrás de la flecha.")]
        [SerializeField] private float _distanceBehind = 2.4f;

        [Tooltip("Altura por encima de la flecha.")]
        [SerializeField] private float _heightAbove = 0.55f;

        [Tooltip("Desfase lateral (sobre el hombro para no tapar el blanco).")]
        [SerializeField] private float _sideOffset = 0.35f;

        [Tooltip("Distancia hacia adelante hacia donde enfoca la cámara.")]
        [SerializeField] private float _lookAheadDistance = 8.0f;

        [Tooltip("Velocidad de suavizado de posición en vuelo.")]
        [SerializeField] private float _positionSmoothSpeed = 16.0f;

        [Tooltip("Velocidad de rotación en vuelo.")]
        [SerializeField] private float _rotationSpeed = 14.0f;

        [Tooltip("Campo de visión (FOV) durante el vuelo.")]
        [SerializeField] private float _flightFov = 65f;

        [Header("Impacto")]
        [Tooltip("Tiempo en segundos que la mini-camara observa el impacto antes de apagarse.")]
        [SerializeField] private float _impactHoldDuration = 1.0f;

        private Camera _camera;
        private CameraState _state = CameraState.Inactive;
        private ArrowProjectile _currentArrow;
        private float _impactTimer;
        private Vector3 _lastKnownDirection = Vector3.forward;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.rect = _viewportRect;
            _camera.depth = _cameraDepth;
            _camera.enabled = false;

            if (_archerPlayer == null) TryGetComponent(out _archerPlayer);
            if (_archerPlayer == null) _archerPlayer = GetComponentInParent<ArcherPlayer>();
        }

        private void OnEnable()
        {
            ArrowProjectile.OnAnyArrowSpawned += HandleAnyArrowSpawned;
        }

        private void OnDisable()
        {
            ArrowProjectile.OnAnyArrowSpawned -= HandleAnyArrowSpawned;
            UnbindCurrentArrow();
            _state = CameraState.Inactive;
            if (_camera != null) _camera.enabled = false;
        }

        private void HandleAnyArrowSpawned(ArrowProjectile arrow)
        {
            if (arrow == null) return;

            if (_archerPlayer == null) TryGetComponent(out _archerPlayer);
            if (_archerPlayer != null && !_archerPlayer.isOwner) return;

            // Solo seguir la flecha si pertenece al jugador local
            if (!arrow.isOwner) return;

            UnbindCurrentArrow();

            _currentArrow = arrow;
            _currentArrow.OnHit += HandleArrowHit;
            _currentArrow.OnStopped += HandleArrowStopped;

            _state = CameraState.Following;
            _camera.enabled = true;
            _camera.fieldOfView = _flightFov;

            _lastKnownDirection = arrow.transform.forward;
        }

        private void HandleArrowHit(Vector3 hitPoint)
        {
            if (_state != CameraState.Following) return;

            _state = CameraState.ImpactHold;
            _impactTimer = _impactHoldDuration;
        }

        private void HandleArrowStopped()
        {
            if (_state == CameraState.Following)
            {
                _state = CameraState.ImpactHold;
                _impactTimer = _impactHoldDuration;
            }
        }

        private void UnbindCurrentArrow()
        {
            if (_currentArrow != null)
            {
                _currentArrow.OnHit -= HandleArrowHit;
                _currentArrow.OnStopped -= HandleArrowStopped;
                _currentArrow = null;
            }
        }

        private void LateUpdate()
        {
            switch (_state)
            {
                case CameraState.Following:
                    UpdateFollowing();
                    break;

                case CameraState.ImpactHold:
                    UpdateImpactHold();
                    break;
            }
        }

        private void UpdateFollowing()
        {
            if (_currentArrow == null)
            {
                EndMiniCam();
                return;
            }

            Vector3 arrowPos = _currentArrow.transform.position;
            Vector3 vel = _currentArrow.Rigidbody != null ? _currentArrow.Rigidbody.linearVelocity : Vector3.zero;

            if (vel.sqrMagnitude > 0.1f)
            {
                _lastKnownDirection = vel.normalized;
            }

            Vector3 right = Vector3.Cross(Vector3.up, _lastKnownDirection).normalized;
            Vector3 targetPos = arrowPos - _lastKnownDirection * _distanceBehind
                                        + Vector3.up * _heightAbove
                                        + right * _sideOffset;

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _positionSmoothSpeed);

            Vector3 lookTarget = arrowPos + _lastKnownDirection * _lookAheadDistance;
            Vector3 lookDir = (lookTarget - transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _rotationSpeed);
            }
        }

        private void UpdateImpactHold()
        {
            _impactTimer -= Time.deltaTime;

            if (_currentArrow != null)
            {
                Vector3 lookTarget = _currentArrow.transform.position;
                Vector3 lookDir = (lookTarget - transform.position).normalized;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
                }
            }

            if (_impactTimer <= 0f)
            {
                EndMiniCam();
            }
        }

        private void EndMiniCam()
        {
            _state = CameraState.Inactive;
            UnbindCurrentArrow();
            if (_camera != null) _camera.enabled = false;
        }
    }
}
