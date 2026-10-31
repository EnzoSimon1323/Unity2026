using UnityEngine;
using PurrNet;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Servicio responsable exclusivo del spawn físico, configuración de colisiones
    /// y lanzamiento autoritativo de proyectiles de flecha en la red.
    /// Desacopla a GameManager y PlayerShooter de la instanciación y de la física de colliders.
    /// </summary>
    public static class ArrowSpawner
    {
        public static ArrowProjectile SpawnAndLaunch(
            ArrowDefinition def,
            Vector3 spawnPosition,
            Vector3 direction,
            float speed,
            PlayerID? shooterId,
            NetworkIdentity shooterIdentity = null)
        {
            if (def == null || def.prefab == null) 
                return null;

            GameObject go = Object.Instantiate(def.prefab, spawnPosition, Quaternion.LookRotation(direction));

            if (!go.TryGetComponent(out ArrowProjectile arrow))
            {
                Debug.LogError($"[ArrowSpawner] El prefab de {def.displayName} no contiene el componente ArrowProjectile.", go);
                Object.Destroy(go);
                return null;
            }

            if (shooterIdentity != null)
            {
                var arrowColliders = go.GetComponentsInChildren<Collider>(true);
                var ownColliders = shooterIdentity.GetComponentsInChildren<Collider>(true);

                foreach (var arrowCol in arrowColliders)
                {
                    if (arrowCol == null) continue;
                    foreach (var ownCol in ownColliders)
                    {
                        if (ownCol != null)
                        {
                            Physics.IgnoreCollision(arrowCol, ownCol, true);
                        }
                    }
                }
            }

            if (go.TryGetComponent(out NetworkIdentity identity) && shooterId.HasValue)
            {
                identity.GiveOwnership(shooterId.Value);
            }

            arrow.Launch(def, direction, speed, shooterId);
            return arrow;
        }

        public static ArrowProjectile SpawnAndLaunch(PendingShot shot)
        {
            return SpawnAndLaunch(
                shot.arrowDef,
                shot.spawnPosition,
                shot.direction,
                shot.speed,
                shot.shooter,
                shot.shooterIdentity);
        }
    }
}

