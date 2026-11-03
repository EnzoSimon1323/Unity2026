using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Gestiona el arsenal táctico y la ejecución del disparo.
    /// Sistema de Mano de 3 Flechas:
    /// - Al inicio se reparten 3 flechas aleatorias del arsenal pool.
    /// - El jugador puede seleccionar libremente entre las disponibles (teclas 1, 2, 3 o rueda).
    /// - Al disparar, la flecha seleccionada se consume.
    /// - Cuando se consumen las 3 flechas, el servidor reparte 3 nuevas flechas.
    /// </summary>
    public class PlayerShooter : NetworkIdentity
    {
        [Header("Arsenal Pool")]
        [Tooltip("Las 6 flechas especiales disponibles en el juego.")]
        [SerializeField] private ArrowDefinition[] arsenalPool;

        [Tooltip("Fallback retrocompatible si arsenalPool estuviera vacío.")]
        [SerializeField] private ArrowDefinition[] loadout;

        [SerializeField] private float baseLaunchSpeed = 40f;

        [Header("Spawn Offset")]
        [SerializeField] private float spawnHeight = 1.4f;
        [SerializeField] private float spawnForwardOffset = 0.5f;

        [Header("Referencias")]
        [SerializeField] private AimController aim;
        [SerializeField] private ArcherPlayer player;

        // ---- Estado de la Mano Táctica (3 flechas) ----
        private readonly ArrowDefinition[] _currentHand = new ArrowDefinition[3];
        private readonly int[] _handIndices = new int[3] { -1, -1, -1 };
        private int _selectedSlot = 0;
        private bool _hasShotThisRound;

        public bool HasShotThisRound => _hasShotThisRound;
        public ArrowDefinition[] CurrentHand => _currentHand;
        public int SelectedSlotIndex => _selectedSlot;

        /// <summary>La flecha actualmente seleccionada en la mano.</summary>
        public ArrowDefinition Selected =>
            (_selectedSlot >= 0 && _selectedSlot < _currentHand.Length) ? _currentHand[_selectedSlot] : null;

        /// <summary>Disparado cuando el servidor reparte una nueva mano de 3 flechas (para animar la ruleta/tragamoneda).</summary>
        public event Action<ArrowDefinition[]> OnHandDealt;

        /// <summary>Disparado cuando cambia el estado de la mano o la ranura seleccionada.</summary>
        public event Action<ArrowDefinition[], int> OnHandChanged;

        /// <summary>Indica si el jugador local ya confirmó su tiro en esta micro-ronda.</summary>
        public event Action<bool> OnShotStatusChanged;

        private void Awake()
        {
            if (aim == null) TryGetComponent(out aim);
            if (player == null) TryGetComponent(out player);
        }

        protected override void OnSpawned(bool asServer)
        {
            base.OnSpawned(asServer);

            if (isServer)
            {
                DealNewHandOnServer();
            }
        }

        private ArrowDefinition[] ActivePool =>
            (arsenalPool != null && arsenalPool.Length > 0) ? arsenalPool : loadout;

        private ArrowDefinition ResolveArrowDef(int poolIndex)
        {
            var pool = ActivePool;
            if (pool != null && poolIndex >= 0 && poolIndex < pool.Length)
                return pool[poolIndex];
            return null;
        }

        private void DealNewHandOnServer()
        {
            var pool = ActivePool;
            if (pool == null || pool.Length == 0) return;

            List<int> availableIndices = new List<int>();
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null)
                    availableIndices.Add(i);
            }

            int[] newIndices = new int[3];
            for (int s = 0; s < 3; s++)
            {
                if (availableIndices.Count > 0)
                {
                    int randIdx = UnityEngine.Random.Range(0, availableIndices.Count);
                    newIndices[s] = availableIndices[randIdx];
                    availableIndices.RemoveAt(randIdx);
                }
                else
                {
                    newIndices[s] = UnityEngine.Random.Range(0, pool.Length);
                }
            }

            _handIndices[0] = newIndices[0];
            _handIndices[1] = newIndices[1];
            _handIndices[2] = newIndices[2];

            SyncHandObserversRpc(newIndices[0], newIndices[1], newIndices[2]);
        }

        [ObserversRpc(runLocally: true)]
        private void SyncHandObserversRpc(int idx0, int idx1, int idx2)
        {
            _handIndices[0] = idx0;
            _handIndices[1] = idx1;
            _handIndices[2] = idx2;

            _currentHand[0] = ResolveArrowDef(idx0);
            _currentHand[1] = ResolveArrowDef(idx1);
            _currentHand[2] = ResolveArrowDef(idx2);

            SelectFirstAvailableSlot();
            OnHandDealt?.Invoke(_currentHand);
            OnHandChanged?.Invoke(_currentHand, _selectedSlot);
        }

        [ObserversRpc(runLocally: true)]
        private void ConsumeSlotObserversRpc(int consumedSlot)
        {
            if (consumedSlot >= 0 && consumedSlot < 3)
            {
                _handIndices[consumedSlot] = -1;
                _currentHand[consumedSlot] = null;

                if (_selectedSlot == consumedSlot)
                {
                    SelectFirstAvailableSlot();
                }

                OnHandChanged?.Invoke(_currentHand, _selectedSlot);
            }
        }

        private void SelectFirstAvailableSlot()
        {
            if (_selectedSlot >= 0 && _selectedSlot < 3 && _currentHand[_selectedSlot] != null)
                return;

            for (int i = 0; i < 3; i++)
            {
                if (_currentHand[i] != null)
                {
                    _selectedSlot = i;
                    return;
                }
            }
            _selectedSlot = -1;
        }

        private void Update()
        {
            if (!isOwner || _hasShotThisRound) return;

            // Selección por teclas 1, 2, 3
            if (Input.GetKeyDown(KeyCode.Alpha1)) TrySelectSlot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TrySelectSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TrySelectSlot(2);

            // Selección por rueda del mouse
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.1f)
            {
                int dir = scroll > 0f ? 1 : -1;
                CycleSlot(dir);
            }
        }

        private void TrySelectSlot(int slot)
        {
            if (slot >= 0 && slot < 3 && _currentHand[slot] != null && _selectedSlot != slot)
            {
                _selectedSlot = slot;
                OnHandChanged?.Invoke(_currentHand, _selectedSlot);
            }
        }

        private void CycleSlot(int direction)
        {
            for (int step = 1; step <= 3; step++)
            {
                int candidate = (_selectedSlot + direction * step + 3) % 3;
                if (_currentHand[candidate] != null)
                {
                    _selectedSlot = candidate;
                    OnHandChanged?.Invoke(_currentHand, _selectedSlot);
                    return;
                }
            }
        }

        /// <summary>Lo llama BowController al soltar. Valida y envía la intención de disparo.</summary>
        public void Shoot(float tension)
        {
            if (!isOwner || _hasShotThisRound) return;

            if (_selectedSlot < 0 || _selectedSlot >= 3 || _currentHand[_selectedSlot] == null)
            {
                Debug.LogWarning("[PlayerShooter] No hay una flecha válida seleccionada en la mano.");
                return;
            }

            Vector3 direction = aim != null ? aim.AimDirection : transform.forward;
            if (direction.sqrMagnitude < 0.0001f) return;

            _hasShotThisRound = true;
            OnShotStatusChanged?.Invoke(true);

            if (player != null)
            {
                player.SetCanShoot(false);
            }

            SubmitShotServerRpc(Mathf.Clamp01(tension), direction.normalized, _selectedSlot);
        }

        [ServerRpc(requireOwnership: true)]
        private void SubmitShotServerRpc(float tension, Vector3 direction, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 3) return;
            int poolIndex = _handIndices[slotIndex];
            if (poolIndex < 0) return;

            ArrowDefinition def = ResolveArrowDef(poolIndex);
            if (def == null || def.prefab == null) return;

            // Consumir ranura
            _handIndices[slotIndex] = -1;
            ConsumeSlotObserversRpc(slotIndex);

            tension = Mathf.Clamp01(tension);
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();

            Vector3 spawnPosition = transform.position + Vector3.up * spawnHeight + direction * spawnForwardOffset;
            float speed = baseLaunchSpeed * def.launchSpeedMultiplier * tension;

            if (GameManager.Instance != null && owner.HasValue)
            {
                GameManager.Instance.RegisterPendingShotServer(new PendingShot
                {
                    shooter = owner.Value,
                    spawnPosition = spawnPosition,
                    direction = direction,
                    speed = speed,
                    arrowDef = def,
                    shooterIdentity = this
                });
            }
            else
            {
                ArrowSpawner.SpawnAndLaunch(def, spawnPosition, direction, speed, owner, this);
            }

            // Si se consumieron las 3 flechas, repartir una mano nueva
            if (_handIndices[0] == -1 && _handIndices[1] == -1 && _handIndices[2] == -1)
            {
                Debug.Log($"<color=yellow>[PlayerShooter] Todas las flechas gastadas. Repartiendo nueva mano para {owner}.</color>");
                DealNewHandOnServer();
            }
        }

        /// <summary>Llamado por el servidor al arrancar una nueva micro-ronda.</summary>
        [ObserversRpc(runLocally: true)]
        public void ResetRoundShotRpc()
        {
            _hasShotThisRound = false;
            OnShotStatusChanged?.Invoke(false);
            if (player != null)
            {
                player.SetCanShoot(true);
            }
        }
    }
}
