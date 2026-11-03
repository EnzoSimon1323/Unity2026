# 🏹 Guía de Estudio de Arquitectura y Código — JuegoSinNombre (Arqueros Multijugador)

Esta guía documenta en profundidad la arquitectura, patrones de diseño, estructura de módulos, auditoría de rendimiento y el procedimiento paso a paso para extender el arsenal con nuevas flechas, estelas (*trails*) y efectos en el proyecto.

---

## 1. Visión General de la Arquitectura

El proyecto está diseñado bajo principios de **Clean Architecture**, **Programación Orientada a Datos (Data-Driven Design)** y un **Modelo de Red Autoritativo** utilizando **PurrNet**.

```
+-------------------------------------------------------------+
|                     JuegoSinNombre.UI                       |
|   (ArrowSlotMachineUI, MatchEndUI, EventSystemCleaner)      |
+------------------------------+------------------------------+
                               | consume eventos / interfaces
+------------------------------v------------------------------+
|                  JuegoSinNombre.Gameplay                    |
| (GameManager, ArcherPlayer, ArrowProjectile, CombatResolver,|
|  PerformanceTelemetryMonitor, MatchEndPresenter)            |
+------------------------------+------------------------------+
                               | depende de
+------------------------------v------------------------------+
|                    JuegoSinNombre.Core                      |
| (ArrowDefinition, EffectDefinition, IEffectTarget, Contracts)|
+-------------------------------------------------------------+
```

### Principios Fundamentales del Proyecto
1. **Arquitectura por Capas con Assembly Definitions (`.asmdef`)**:
   - `JuegoSinNombre.Core`: Contratos (`IEffectTarget`, `IWindProvider`), definiciones de datos (`ArrowDefinition`, `EffectDefinition`), enums (`RoundPhase`, `BodyPartType`) y estructuras puras de datos (`ArrowHitInfo`, `PendingShot`). **No tiene dependencias de Gameplay ni de UI**, garantizando máxima independencia.
   - `JuegoSinNombre.Gameplay`: Mecánicas de disparo, física balística, gestión de salud, ragdoll, FSM del ciclo de partida, estelas sincronizadas, telemetría y resolución de combate.
   - `JuegoSinNombre.UI`: Capa presentacional y widgets de interfaz (ruleta de selección estilo tragamonedas, barra de salud, feedback de viento, limpieza de EventSystems duplicados).
   - `JuegoSinNombre.Editor`: Utilidades para limpieza de estado de red al entrar/salir de Play Mode.

2. **Simulación Autoritativa en el Servidor (Server-Authoritative)**:
   - Toda lógica crítica (cálculo de daño, dispersión de viento, aplicación de efectos de estado, transiciones de turno y resolución de impactos) se ejecuta **exclusivamente en el servidor**.
   - Los clientes envían intenciones (`PendingShot` vía RPCs) y reciben replicación mediante `SyncVar` y eventos de red buffered.

3. **Ciclo de Rondas por FSM Sincronizada (`GameManager`)**:
   - El juego funciona por micro-rondas tácticas: ambos jugadores apuntan simultáneamente (`Aiming`), confirman su tiro, y al disparar ambos, las flechas vuelan en simultáneo (`Resolving`).
   - Se evalúa impacto (`Outcome`) y se determina si se pasa a la siguiente micro-ronda o si termina la ronda al morir un contrincante.

---

## 2. Patrones de Diseño Implementados

### A. Type Object / Flyweight (Data-Driven Design)
* **Dónde se usa**: `ArrowDefinition.cs` y `EffectDefinition.cs`.
* **Propósito**: Evitar la explosión de clases por herencia (`ArrowMjolnir`, `ArrowSkadi`, `ArrowApollo`). En su lugar, existe **una sola clase de datos** (`ArrowDefinition : ScriptableObject`) y **un solo proyectil físico** (`ArrowProjectile`).
* **Ventaja**: Crear o balancear una flecha consiste únicamente en crear un asset `.asset` en el Inspector de Unity y calibrar variables numéricas (masa, daño, arrastre de viento, lifesteal, homing, efectos asociados, colores de estela), sin recompilar código.

### B. Service / Static Dispatcher Pattern
* **Dónde se usa**: `ArrowSpawner.cs`, `CombatResolver.cs` y `EventSystemCleaner.cs`.
* **Propósito**:
  - `ArrowSpawner`: Centraliza la instanciación del prefab, configuración de colisiones (ignorar colisionadores del propio tirador), asignación de propiedad en la red (`GiveOwnership`) y llamada a `Launch()`.
  - `CombatResolver`: Centraliza la matemática del impacto: multiplicador por parte del cuerpo (`DamageHitbox`), amplificación por debufos previos, robo de vida (`lifestealRatio`), activación de ragdoll físico y aplicación de `EffectDefinition[]`.
  - `EventSystemCleaner`: Elimina automáticamente duplicados residuales de `EventSystem` tras transiciones de red.
* **Ventaja**: Desacopla al `GameManager` y al `PlayerShooter` de los detalles de física y combate.

### C. Observer Pattern (Event-Driven Architecture)
* **Dónde se usa**: Eventos nativos C# (`System.Action`) a lo largo de todo el sistema:
  - `CombatResolver.OnHitResolved`
  - `HealthModule.OnAnyPlayerDied`
  - `ArrowProjectile.OnAnyArrowSpawned`
  - `GameManager.OnPhaseChangedGlobal`
  - `PlayerShooter.OnHandDealt` / `OnHandChanged`
* **Ventaja**: Los módulos de UI (como `ArrowSlotMachineUI` y `MatchEndUI`) o audio reaccionan a cambios de estado sin necesidad de que el gameplay conozca la existencia de la interfaz.

### D. Interface Segregation & Dependency Inversion (SOLID)
* **Dónde se usa**:
  - `IEffectTarget`: El servidor y el `EffectRunner` no necesitan conocer los componentes internos de `AimController`, `BowController` o la cámara. Solo se comunican a través de métodos como `SetSensitivityMultiplier()`, `SetAxisInversion()`, `SetBlur()`, `ResetModifiers()`.
  - `IWindProvider`: Abstrae el proveedor de viento para la UI o la balística.

### E. Finite State Machine (FSM)
* **Dónde se usa**: `RoundPhase.cs` y `GameManager.cs`.
* **Estados**:
  - `WaitingForPlayers`: Espera a que ambos clientes estén en la sala.
  - `RoundStart`: Tirada aleatoria del vector de viento.
  - `ConsumableWindow`: Fase opcional para activar consumibles.
  - `Aiming`: Arcos desbloqueados; jugadores apuntan y confirman tiro.
  - `Resolving`: Flechas en el aire; inputs bloqueados hasta resolución o timeout.
  - `Outcome`: Cálculo de vida restante y limpieza de efectos de 1 turno.
  - `GameOver`: Partida finalizada con anunciador de ganador.

### F. Hitbox Multiplier / Component-Based Damage
* **Dónde se usa**: `DamageHitbox.cs`.
* **Propósito**: Cada hueso relevante del personaje tiene un `Collider` y un `DamageHitbox` con su tipo (`Head`, `Torso`, `Arm`, `Leg`).
* **Multiplicadores**:
  - Cabeza: `2.0x` (Headshot crítico).
  - Torso: `1.0x` (Daño estándar).
  - Brazos/Piernas: `0.75x`.
  - *Regla especial en `ArrowProjectile`*: La flecha buscadora de Apolo (`homing = true`) nunca puede hacer headshot; si impacta la cabeza, se degrada a daño de torso (`1.0x`).

---

## 3. Mapa de Clases y Responsabilidades

| Capa | Clase | Responsabilidad Principal |
| :--- | :--- | :--- |
| **Core** | `ArrowDefinition` | ScriptableObject con todas las propiedades de una flecha (física, daño, homing, efectos, icono, estela). |
| **Core** | `EffectDefinition` | ScriptableObject con propiedades de efectos de estado (sensibilidad, inversión de ejes, daño por segundo, FOV, blur). |
| **Core** | `GameContracts` | Interfaces (`IEffectTarget`, `IWindProvider`) y structs de red (`ArrowHitInfo`, `PendingShot`). |
| **Core** | `RoundPhase` | Enum de las fases del ciclo de micro-rondas y rondas. |
| **Gameplay** | `ArcherPlayer` | Hub del jugador en la escena. Implementa `IEffectTarget`, gestiona el `EffectRunner` y expone el jugador local. |
| **Gameplay** | `PlayerShooter` | Administra la mano de 3 flechas tácticas, recepción de nuevas manos con `bufferLast: true`, selección y envío del tiro. |
| **Gameplay** | `ArrowProjectile` | Comportamiento del proyectil en vuelo: integración con viento, corrección homing, estela `TrailRenderer` sincronizada por red y detección de impacto. |
| **Gameplay** | `ArrowSpawner` | Servicio estático que instancia el prefab de la flecha, configura capas/colisiones y lanza el proyectil. |
| **Gameplay** | `CombatResolver` | Servicio autoritativo que resuelve daños, lifesteal, efectos de estado e impulso al ragdoll. |
| **Gameplay** | `HealthModule` | Gestiona la vida (`SyncVar<float> currentHealth`), regeneración, escudo y evento de muerte. |
| **Gameplay** | `DamageHitbox` | Componente adjunto a las extremidades del jugador para diferenciar daño por zona anatómica. |
| **Gameplay** | `EffectRunner` | Administrador en memoria de los efectos temporales activos sobre un jugador. |
| **Gameplay** | `WindManager` | Genera y sincroniza la fuerza y dirección del viento en cada ronda. |
| **Gameplay** | `GameManager` | Orquestador de partida: sincroniza fases, contabiliza victorias por formato (Bo3 / Bo5) y gatilla disparos simultáneos. |
| **Gameplay** | `PerformanceTelemetryMonitor` | Monitor de rendimiento en tiempo real (FPS, frame time, GC allocations, rol de red, fase y jugadores). |
| **Gameplay** | `MatchEndPresenter` | Cinemática de victoria: podio, animación del ganador y colapso en ragdoll del rival. |
| **UI** | `ArrowSlotMachineUI` | Presentador HUD: animación de tragamonedas (ruleta de los 3 slots) y feedback visual de flecha seleccionada/gastada. |
| **UI** | `MatchEndUI` | Pantalla de fin de partida (animación de victoria/derrota, marcador final, rematch sin condición de carrera). |
| **UI** | `EventSystemCleaner` | Limpiador estático que garantiza exactamente un `EventSystem` activo tras cargas de escena. |

---

## 4. Guía Práctica: Cómo Crear una Flecha Nueva

Gracias a la arquitectura orientada a datos, **en el 95% de los casos NO es necesario tocar una sola línea de código C#**.

### Caso 1: Flecha Táctica Estándar o Especial (Solo Datos)
Ejemplo: Crear una flecha llamada **"Flecha Colosal de Asedio"** (muy pesada, inmune al viento, daño masivo y estela de fuego) o una **"Flecha Vampírica de Sangre"**.

1. **Crear el Asset de Definición**:
   - En el panel de Proyecto de Unity, navegar a: `Assets/JuegoSinNombre/Data/`.
   - Click derecho -> `Create` -> `Arqueria` -> `Arrow`.
   - Nombrar el archivo (ej: `ArrowColosal.asset`).

2. **Configurar los Parámetros en el Inspector**:
   - **Identidad**:
     - `Display Name`: Nombre que se verá en la UI (ej: `Colosal`).
     - `Icon`: Sprite que se mostrará en el slot de la tragamonedas.
     - `Description`: Breve texto descriptivo.
   - **Prefab**:
     - Asignar `ArrowDefault.prefab` (ubicado en `Assets/JuegoSinNombre/Prefabs/`).
     - *Nota*: Si la flecha tiene una malla 3D única, podés duplicar `ArrowDefault.prefab`, cambiarle el modelo y asignar ese nuevo prefab.
   - **Física**:
     - `Launch Speed Multiplier`: `0.8` (si es pesada sale más lento) o `1.2` (si es veloz).
     - `Mass`: `0.5` (más masa = mayor caída balística parabólica).
     - `Wind Multiplier`: `0` (ignora completamente el viento) o `1.5` (muy afectada por ráfagas).
     - `Align To Velocity`: `true` (para que la punta siempre mire en la dirección del vector de velocidad).
   - **Daño y Mecánicas**:
     - `Base Damage`: Daño base (ej: `50`).
     - `Homing`: `false` (o `true` con `Homing Turn Speed` si busca el torso automáticamente).
     - `Lifesteal Ratio`: `0.35` si querés que cure el 35% del daño infligido al tirador.
     - `Effects On Target`: Lista de efectos que sufrirá el rival impactado (ver siguiente sección).
   - **Estela Visual (Trail)**:
     - `Enable Trail`: `true`.
     - `Trail Color`: Color de nacimiento (ej: Naranja fuego `{r: 1, g: 0.45, b: 0.1, a: 0.95}`).
     - `Trail End Color`: Color de desvanecimiento con alfa en 0 (ej: `{r: 0.8, g: 0.1, b: 0, a: 0}`).
     - `Trail Width`: Ancho de salida (ej: `0.13m` para flechas pesadas, `0.06m` para comunes).
     - `Trail Time`: Duración en el aire (ej: `0.35s`).

3. **Agregar la Flecha al Pool de Disparo**:
   - Abrir el prefab `Player.prefab` (`Assets/JuegoSinNombre/Prefabs/Player.prefab`).
   - Seleccionar el componente `PlayerShooter`.
   - En el array `Arsenal Pool`, incrementar el tamaño y arrastrar el nuevo asset `ArrowColosal.asset`.
   - ¡Listo! En la siguiente ronda, el sistema de ruleta/tragamonedas podrá repartir tu nueva flecha a cualquiera de los 3 slots de los jugadores y ambos verán la estela en red.

---

### Caso 2: Flecha con un Efecto de Estado Nuevo
Ejemplo: Queremos una **"Flecha Maldita de Nix"** que nuble la vista del enemigo (blur) y le reduzca el campo de visión (zoom inverso).

1. **Crear el Asset del Efecto**:
   - Click derecho en `Assets/JuegoSinNombre/Data/` -> `Create` -> `Arqueria` -> `Effect`.
   - Nombrar el archivo (ej: `EffectPolvoNix.asset`).
   - Configurar:
     - `Expires On Phase`: `Outcome` (dura 1 turno) o `Resolving` (dura hasta que el rival dispare).
     - `Phase Count`: `1`.
     - `Blur Amount`: `0.7` (visión borrosa en la cámara del rival).
     - `Fov Multiplier`: `0.6` o `1.4`.
     - `Invert Axes`: `false` (o `true` si querés invertir controles).
     - `Sensitivity Multiplier`: `0.5` si querés que le cueste mover la mira.
     - `Damage Per Second`: Daño en el tiempo si es veneno/fuego.

2. **Vincular el Efecto a la Flecha**:
   - Abrir tu `ArrowDefinition` (del Caso 1).
   - En la sección `Effects On Target`, añadir un elemento y arrastrar `EffectPolvoNix.asset`.
   - Cuando la flecha impacte al rival en el servidor, `CombatResolver` automáticamente inyectará el efecto al `ArcherPlayer` de la víctima, quien modificará su cámara y mira mediante `IEffectTarget`.

---

### Caso 3: ¿Cuándo SÍ es Necesario Escribir Código? (Mecánicas Inéditas)
Solo necesitás modificar scripts si tu flecha introduce una **regla física o matemática que no existe en los campos de `ArrowDefinition` ni `EffectDefinition`**.

Por ejemplo, si quisiéramos una **"Flecha de Rebote (Ricochet)"** que rebotara 2 veces contra muros antes de clavarse:
1. **Extender el Contrato de Datos (`ArrowDefinition.cs`)**:
   ```csharp
   [Header("Mecánicas Especiales")]
   public int maxBounces = 0; // 0 = se clava al primer impacto
   ```
2. **Implementar el Comportamiento en `ArrowProjectile.cs`**:
   - En `OnCollisionEnter(Collision collision)`:
     ```csharp
     if (_definition != null && _bouncesLeft > 0 && !collision.gameObject.GetComponentInParent<ArcherPlayer>())
     {
         _bouncesLeft--;
         Vector3 reflect = Vector3.Reflect(flightDirection, collision.contacts[0].normal);
         _rb.linearVelocity = reflect * arrowSpeed * 0.8f;
         return; // No se clava todavía
     }
     ```
3. **Respeto a Open/Closed Principle**: Las demás flechas del juego mantienen `maxBounces = 0` y siguen comportándose idéntico, manteniendo compatibilidad total sin romper nada.

---

## 5. Preguntas Clave para Estudio y Defensa de Código

1. **¿Por qué `ArrowDefinition` es un ScriptableObject y no una clase MonoBehaviour con herencia?**
   * *Respuesta*: Porque separa los datos del comportamiento (patrón Flyweight). La memoria del juego no se desperdicia instanciando lógica duplicada en cada proyectil, y los diseñadores de juego pueden balancear o crear flechas directamente desde el editor sin recompilar ni introducir bugs.

2. **¿Por qué las estelas se configuran con `ConfigureTrailObserversRpc` con `bufferLast: true` en vez de sincronizar el ScriptableObject?**
   * *Respuesta*: Porque los ScriptableObjects son referencias locales de proyecto y serializarlos por red consume ancho de banda innecesario. Enviar solo los colores y el grosor mediante un RPC con buffer asegura que cualquier cliente (incluso con retardo) configure la estela idéntica sin sobrecargar la red.

3. **¿Cómo se resolvió la condición de carrera donde al perdedor le salía "EMPATE"?**
   * *Respuesta*: La SyncVar de `currentPhase` llegaba al cliente milisegundos antes que el RPC `NotifyMatchOverObserversRpc`, evaluando al ganador como `null` y marcando empate prematuro. Se solucionó emitiendo el RPC antes de actualizar la SyncVar en el servidor, y haciendo que la UI espere la confirmación del ganador y admita sobrescritura si hubo un empate provisional.

4. **¿Cómo garantiza el sistema un rendimiento de 240+ FPS en vuelo simultáneo?**
   * *Respuesta*: Diseñando los loops de frame sin recolección de basura (*Zero-Alloc*), usando `struct` para datos efímeros (`ArrowHitInfo`), pasando las flechas a `isKinematic` en cuanto impactan para sacarlas del cálculo de PhysX, y evitando recalcular mallas de TextMeshPro si el texto no varió.
