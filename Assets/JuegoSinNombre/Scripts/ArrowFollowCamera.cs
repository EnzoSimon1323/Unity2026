using System;
using UnityEngine;

namespace JuegoSinNombre
{
    /// <summary>
    /// Cámara cinemática de seguimiento de flecha (Kill/Flight Cam).
    /// Cuando el jugador local dispara, la cámara acompaña a su flecha en vuelo
    /// en tercera persona, se enfoca brevemente en el punto de impacto y retorna
    /// suavemente al arquero, dejándolo en estado Idle (requiriendo volver a apuntar).
    /// </summary>
    [DisallowMultipleComponent]
    public class ArrowFollowCamera : MonoBehaviour
    {
        private enum CameraState
        {
            Inactive,
            Following,
            ImpactHold,
            Returning
        }

        [Header("Referencias (Auto-detectadas si están vacías)")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private AimController _aimController;
        [SerializeField] private ArcherPlayer _archerPlayer;

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

        [Header("Impacto y Retorno")]
        [Tooltip("Tiempo en segundos que la cámara observa el impacto.")]
        [SerializeField] private float _impactHoldDuration = 1.0f;

        [Tooltip("Duración de la transición suave de regreso al jugador.")]
        [SerializeField] private float _returnDuration = 0.55f;

        private CameraState _state = CameraState.Inactive;
        private ArrowProjectile _currentArrow;
        private float _impactTimer;
        private Vector3 _returnStartPos;
        private Quaternion _returnStartRot;
        private float _returnStartFov;
        private float _returnTimer;
        private Vector3 _lastKnownDirection = Vector3.forward;

        private Vector3 _defaultCameraLocalPos = new Vector3(0f, 0f, -4.42f);
        private Quaternion _defaultCameraLocalRot = Quaternion.identity;

        private void Awake()
        {
            if (_aimController == null) TryGetComponent(out _aimController);
            if (_archerPlayer == null) TryGetComponent(out _archerPlayer);
        }

        private void Start()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (_aimController != null)
            {
                if (_playerCamera == null)
                    _playerCamera = GetComponentInChildren<Camera>(true);

                if (_cameraPivot == null)
                {
                    var pivotField = typeof(AimController).GetField("cameraPivot", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (pivotField != null)
                        _cameraPivot = pivotField.GetValue(_aimController) as Transform;
                }
            }

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_playerCamera != null)
            {
                _defaultCameraLocalPos = _playerCamera.transform.localPosition;
                _defaultCameraLocalRot = _playerCamera.transform.localRotation;
            }
        }

        private void OnEnable()
        {
            ArrowProjectile.OnAnyArrowSpawned += HandleAnyArrowSpawned;
        }

        private void OnDisable()
        {
            ArrowProjectile.OnAnyArrowSpawned -= HandleAnyArrowSpawned;
            UnbindCurrentArrow();

            if (_state != CameraState.Inactive)
            {
                FinishCameraControl();
            }
        }

        private void HandleAnyArrowSpawned(ArrowProjectile arrow)
        {
            if (arrow == null) return;

            // Solo seguir la flecha si pertenece al jugador local
            if (!arrow.isOwner) return;

            ResolveReferences();
            if (_playerCamera == null || _cameraPivot == null || _aimController == null) return;

            UnbindCurrentArrow();

            _currentArrow = arrow;
            _currentArrow.OnHit += HandleArrowHit;
            _currentArrow.OnStopped += HandleArrowStopped;

            _state = CameraState.Following;
            _aimController.OverrideCameraControl = true;
            _aimController.ForceIdle();

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
            if (_state == CameraState.Inactive) return;

            switch (_state)
            {
                case CameraState.Following:
                    UpdateFollowing();
                    break;

                case CameraState.ImpactHold:
                    UpdateImpactHold();
                    break;

                case CameraState.Returning:
                    UpdateReturning();
                    break;
            }
        }

        private void UpdateFollowing()
        {
            if (_currentArrow == null)
            {
                StartReturnTransition();
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

            _playerCamera.transform.position = Vector3.Lerp(
                _playerCamera.transform.position, 
                targetPos, 
                Time.deltaTime * _positionSmoothSpeed);

            Vector3 lookTarget = arrowPos + _lastKnownDirection * _lookAheadDistance;
            Vector3 lookDir = (lookTarget - _playerCamera.transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                _playerCamera.transform.rotation = Quaternion.Slerp(
                    _playerCamera.transform.rotation, 
                    targetRot, 
                    Time.deltaTime * _rotationSpeed);
            }

            _playerCamera.fieldOfView = Mathf.Lerp(_playerCamera.fieldOfView, _flightFov, Time.deltaTime * 6f);
        }

        private void UpdateImpactHold()
        {
            _impactTimer -= Time.deltaTime;

            if (_currentArrow != null)
            {
                Vector3 lookTarget = _currentArrow.transform.position;
                Vector3 lookDir = (lookTarget - _playerCamera.transform.position).normalized;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    _playerCamera.transform.rotation = Quaternion.Slerp(
                        _playerCamera.transform.rotation, 
                        targetRot, 
                        Time.deltaTime * 8f);
                }
            }

            if (_impactTimer <= 0f)
            {
                StartReturnTransition();
            }
        }

        private void StartReturnTransition()
        {
            if (_playerCamera == null)
            {
                FinishCameraControl();
                return;
            }

            _state = CameraState.Returning;
            _returnTimer = 0f;
            _returnStartPos = _playerCamera.transform.position;
            _returnStartRot = _playerCamera.transform.rotation;
            _returnStartFov = _playerCamera.fieldOfView;
        }

        private void UpdateReturning()
        {
            if (_playerCamera == null || _cameraPivot == null)
            {
                FinishCameraControl();
                return;
            }

            _returnTimer += Time.deltaTime;
            float t = _returnDuration > 0f ? Mathf.Clamp01(_returnTimer / _returnDuration) : 1f;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 targetWorldPos = _cameraPivot.TransformPoint(_defaultCameraLocalPos);
            Quaternion targetWorldRot = _cameraPivot.rotation * _defaultCameraLocalRot;
            float targetFov = _archerPlayer != null ? _archerPlayer.Fov : 60f;

            _playerCamera.transform.position = Vector3.Lerp(_returnStartPos, targetWorldPos, smoothT);
            _playerCamera.transform.rotation = Quaternion.Slerp(_returnStartRot, targetWorldRot, smoothT);
            _playerCamera.fieldOfView = Mathf.Lerp(_returnStartFov, targetFov, smoothT);

            if (t >= 1f)
            {
                FinishCameraControl();
            }
        }

        private void FinishCameraControl()
        {
            _state = CameraState.Inactive;
            UnbindCurrentArrow();

            if (_playerCamera != null && _cameraPivot != null)
            {
                _playerCamera.transform.localPosition = _defaultCameraLocalPos;
                _playerCamera.transform.localRotation = _defaultCameraLocalRot;
                float baseFov = _archerPlayer != null ? _archerPlayer.Fov : 60f;
                _playerCamera.fieldOfView = baseFov;
            }

            if (_aimController != null)
            {
                _aimController.OverrideCameraControl = false;
                _aimController.ForceIdle();
            }
        }
    }
}
