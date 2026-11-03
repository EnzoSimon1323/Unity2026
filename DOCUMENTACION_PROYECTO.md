# 🏹 DOCUMENTACIÓN TÉCNICA Y ARQUITECTÓNICA COMPLETA DEL PROYECTO
## *JuegoSinNombre — Duelo Táctico de Arqueros Multijugador*

---

## 1. Visión General del Proyecto

### 1.1 Premisa y Mecánica Central
**JuegoSinNombre** es un videojuego de disparos tácticos y precisión en tercera/primera persona para dos jugadores (1v1) en red. El combate se estructura en **micro-rondas tácticas de disparo simultáneo** dentro de una partida al mejor de 3 o 5 rondas (**Best of 3 / Best of 5**).

El núcleo de la experiencia combina:
- **Balística física realista**: Trayectorias parabólicas gobernadas por masa, velocidad inicial, gravedad y un sistema dinámico de viento tridimensional.
- **Mano Táctica de 3 Flechas (Arsenal Dinámico)**: Selección estratégica de flechas especiales con reparto procedimental y presentación visual mediante una ruleta estilo tragamonedas (*Slot Machine*).
- **Efectos de Estado Temporales**: Mecánicas de control (congelamiento/pesadez de mira, veneno continuo, inversión de ejes, visión borrosa con blur post-procesado y robo de vida).
- **Cálculo Anatómico de Daño**: Zonas de impacto diferenciadas (*Headshot* crítico x2.0, *Torso* x1.0, *Extremidades* x0.75) conectadas a simulación física de **Ragdoll** y cámaras cinemáticas de impacto y victoria.

### 1.2 Stack Tecnológico
- **Motor**: Unity 6 (6000.x)
- **Pipeline de Renderizado**: Universal Render Pipeline (URP)
- **Networking Framework**: **PurrNet** (Arquitectura autoritativa en servidor con replicación reactiva mediante `SyncVar` y RPCs optimizados)
- **Texto y Gráficos UI**: TextMeshPro con fuente personalizada (*PirataOne-Regular*) y Canvas interactivos
- **Módulo de Entrada**: Unity Input System moderno + teclado/mouse tradicional

---

## 2. Arquitectura de Software y Organización Modular

El proyecto implementa una arquitectura desacoplada organizada formalmente mediante **Assembly Definitions (`.asmdef`)**. Esto garantiza tiempos de compilación mínimos, previene dependencias circulares y asegura una separación estricta de responsabilidades.

```
                    +---------------------------+
                    |    JuegoSinNombre.UI      |
                    +-------------+-------------+
                                  |
                                  v
+------------------------>+---------------+<------------------------+
| JuegoSinNombre.Editor  |                |  PurrNet Networking     |
+------------------------+  Gameplay Layer|  (Runtime / Core)       |
                          |               |                         |
                          +-------+-------+                         |
                                  |                                 |
                                  v                                 |
                    +-------------+-------------+                   |
                    |    JuegoSinNombre.Core    |<------------------+
                    +---------------------------+
```

### 2.1 Especificación de Ensamblados (`.asmdef`)

| Ensamblado | Dependencias | Responsabilidad |
| :--- | :--- | :--- |
| **`JuegoSinNombre.Core`** | `PurrNet` | **Capa Fundamental Agnóstica**. Define contratos (`IEffectTarget`, `IWindProvider`), tipos de datos de red (`ArrowHitInfo`, `PendingShot`), enums globales (`RoundPhase`, `BodyPartType`, `MatchFormat`) y los ScriptableObjects de definición (`ArrowDefinition`, `EffectDefinition`). **No depende de Gameplay ni de UI**. |
| **`JuegoSinNombre.Gameplay`** | `JuegoSinNombre.Core`, `PurrNet`, `Unity.TextMeshPro` | **Capa de Dominio y Mecánicas**. Contiene la máquina de estados del juego (`GameManager`), lógica del arquero (`ArcherPlayer`, `BowController`, `AimController`, `PlayerShooter`), simulación de proyectiles (`ArrowProjectile`, `ArrowSpawner`), resolución de combate (`CombatResolver`, `DamageHitbox`, `HealthModule`), cámaras (`ArrowFollowCamera`, `MatchEndPresenter`) y red de salas (`NetworkRoomController`, `RoomNetworkService`). |
| **`JuegoSinNombre.UI`** | `JuegoSinNombre.Core`, `JuegoSinNombre.Gameplay`, `Unity.TextMeshPro` | **Capa de Presentación y HUD**. Maneja los widgets gráficos: ruleta de selección de flechas (`ArrowSlotMachineUI`), pantalla de victoria/derrota (`MatchEndUI`), rosa de los vientos (`WindUi`) y barras de salud flotantes (`WorldHealthBar`). |
| **`JuegoSinNombre.Editor`** | `JuegoSinNombre.Gameplay`, `PurrNet` | **Herramientas de Desarrollo**. Automatización de limpieza de conexiones de red y estados de PurrNet al detener o pausar el Play Mode (`PlayModeNetworkCleanup`). |

---

## 3. Patrones de Diseño de Software

El código sigue estrictamente patrones de diseño orientados a la mantenibilidad, escalabilidad y rendimiento:

### 3.1 Type Object / Flyweight (Data-Driven Design)
- **Implementación**: [`ArrowDefinition.cs`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Core/ArrowDefinition.cs) y [`EffectDefinition.cs`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Core/EffectDefinition.cs).
- **Problema que resuelve**: Evita la proliferación incontrolada de clases heredadas (`ArrowColosal`, `ArrowSkadi`, `ArrowVampirica`). 
- **Solución**: Se define **una única entidad física** ([`ArrowProjectile.cs`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Gameplay/ArrowProjectile.cs)) y el comportamiento varía según el contenedor inmutable de datos (`ScriptableObject`). Crear o balancear flechas consiste en crear assets en disco y alterar sus propiedades numéricas en el Inspector sin recompilación.

### 3.2 Stateless Domain Services (Servicios de Dominio Estáticos)
- **Implementación**: [`CombatResolver.cs`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Gameplay/CombatResolver.cs) y [`ArrowSpawner.cs`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Gameplay/ArrowSpawner.cs).
- **Problema que resuelve**: Elimina el acoplamiento directo entre el tirador, el proyectil, la interfaz y la gestión de partida.
- **Solución**: Métodos puros y autoritativos en el servidor:
  - `ArrowSpawner.SpawnAndLaunch(...)`: Maneja la instanciación física, ignora colisiones entre el proyectil y el cuerpo del tirador (`Physics.IgnoreCollision`), asigna propiedad en la red (`GiveOwnership`) y aplica el vector de impulso inicial.
  - `CombatResolver.ResolveHit(...)`: Evalúa el daño base, amplifica por multiplicadores corporales y debufos existentes, aplica robo de vida al tirador, despacha efectos de estado a la víctima e impulsa el ragdoll si el impacto fue letal.

### 3.3 Observer / Event-Driven Architecture (Publicador-Suscriptor)
- **Implementación**: Eventos C# nativos (`System.Action`) desacoplados:
  - `CombatResolver.OnHitResolved`: Notifica a audio y managers sobre el resultado del combate.
  - `HealthModule.OnAnyPlayerDied`: Notifica al `GameManager` la muerte de un contrincante.
  - `ArrowProjectile.OnAnyArrowSpawned`: Permite que la cámara local identifique cuándo su dueño lanzó una flecha para seguirla.
  - `GameManager.OnPhaseChangedGlobal`: Sincroniza arcos, miras y timers al cambiar de fase.
  - `PlayerShooter.OnHandDealt` / `OnHandChanged`: Comunica a la UI cuándo activar la ruleta o actualizar el slot seleccionado.

### 3.4 Interface Segregation & Dependency Inversion (SOLID)
- **Implementación**: [`IEffectTarget`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Core/GameContracts.cs#L27) y [`IWindProvider`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Core/GameContracts.cs#L47).
- **Beneficio**: El gestor de efectos ([`EffectRunner.cs`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Gameplay/EffectRunner.cs)) y el servidor interactúan con el jugador a través de un contrato estricto (`SetSensitivityMultiplier`, `SetAxisInversion`, `SetBlur`, `ResetModifiers`). No tienen idea de cómo están implementadas las cámaras, la cinemática inversa o el hardware de entrada.

### 3.5 Finite State Machine (FSM)
- **Implementación**: [`GameManager.cs`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Gameplay/GameManager.cs) utilizando el enum [`RoundPhase.cs`](file:///D:/Unity2026/Assets/JuegoSinNombre/Scripts/Core/RoundPhase.cs).
- **Beneficio**: Regula inequívocamente qué acciones son válidas en cada instante del turno, previniendo condiciones de carrera en red y trampas por desincronización.

---

## 4. Arquitectura de Red y Ciclo de Vida (PurrNet)

### 4.1 Principio de Autoridad del Servidor
Toda la lógica decisiva reside en el **Servidor (Host)**. Los clientes son terminales visuales predictivas:
1. El cliente local apunta y tensa la cuerda.
2. Al soltar el disparo, envía un `PendingShot` mediante RPC al servidor.
3. El servidor valida que la micro-ronda esté en fase `Aiming` y registra el tiro en `_pendingShots`.
4. Cuando ambos jugadores dispararon (o vence el tiempo de turno), el servidor instancia los proyectiles de flecha simultáneamente.
5. El proyectil viaja físicamente en el servidor; los clientes interpolan la posición replicada.
6. La colisión se detecta y resuelve en el servidor mediante `CombatResolver`.

### 4.2 Sincronización de Estado con `SyncVar`

| Script | Variable Sincronizada | Propósito | Autoridad |
| :--- | :--- | :--- | :--- |
| `GameManager` | `currentPhase` | Fase actual de la ronda (`RoundPhase`). | Servidor |
| `GameManager` | `roundNumber` | Número de ronda actual. | Servidor |
| `GameManager` | `player1Wins` / `player2Wins` | Conteo de rondas ganadas para Bo3/Bo5. | Servidor |
| `HealthModule` | `currentHealth` | Salud del jugador (0 a maxHealth). | Servidor |
| `HealthModule` | `maxHealth` | Vida máxima (base 100). | Servidor |
| `AimController` | `_isAiming` | Estado de apuntado (para animación y postura). | Propietario (`ownerAuth: true`) |
| `BowController` | `_isArrowVisible` | Flecha visible en la mano del modelo 3D. | Propietario (`ownerAuth: true`) |
| `ArrowProjectile`| `_velocity` | Vector de velocidad para interpolación continua. | Servidor |
| `ArrowProjectile`| `_mass`, `_windMultiplier` | Factores físicos replicados al spawnear. | Servidor |

### 4.3 Serialización Personalizada (`NetworkTypeRegistry.cs`)
Tipos complejos y structs como `ArrowHitInfo`, `PendingShot` y `BodyPartType` se encuentran registrados en `NetworkTypeRegistry` para garantizar serialización binaria ultraligera y libre de asignaciones de recolección de basura (*GC Allocations*).

---

## 5. Ciclo de Juego y Máquina de Estados (Game Loop)

```
[ WaitingForPlayers ] 
         │ (Ambos jugadores conectados en Test.unity)
         ▼
  [ RoundStart ] ──────> Tirada de viento procedural (WindManager)
         │
         ▼
[ ConsumableWindow ] ──> Ventana táctica para consumibles (si aplica)
         │
         ▼
    [ Aiming ] ────────> Arcos desbloqueados. Jugadores apuntan y seleccionan flecha.
         │               Cada uno dispara y queda bloqueado a la espera del rival.
         ▼
   [ Resolving ] ──────> Disparo simultáneo ejecutado. Flechas vuelan en red.
         │               Entra en acción ArrowFollowCamera. Input bloqueado.
         ▼
    [ Outcome ] ────────> Impactos resueltos. Daños y debufos aplicados.
         │
         ├─── ¿Murió algún jugador? ───► SI ───► [ Evaluar Ganador de Ronda ]
         │                                            │
         │ NO                                         ├─── ¿Alcanzó victorias necesarias (Bo3/Bo5)?
         │                                            │         │
         ▼                                            │         ├──► SI ──► [ GameOver ] ──► Podio y MatchEndUI
  [ Siguiente Micro-ronda ] <─────────────────────────┘         │
  (Vuelve a Aiming)                                             └──► NO ──► Reiniciar Ronda (RoundStart)
```

### 5.1 Fases del Enum `RoundPhase`
1. **`WaitingForPlayers`**: El servidor espera a que ambos avatares de arquero hayan sido instanciados y tengan propiedad de red asignada.
2. **`RoundStart`**: Se genera una nueva semilla vectorial de viento (`WindManager.GenerateNewWind()`).
3. **`ConsumableWindow`**: Período de bloqueo de arco donde se habilitan ítems consumibles (si el jugador dispone de ellos).
4. **`Aiming`**: Se desbloquean los arcos (`IEffectTarget.SetCanShoot(true)`). Ambos jugadores tienen libertad para apuntar, ajustar elevación y seleccionar cuál de sus 3 flechas de la mano disparar.
5. **`Resolving`**: Ambos disparos fueron confirmados. Las flechas vuelan simultáneamente en la simulación física. Se activa un temporizador de seguridad (*watchdog*) de 12 segundos para evitar bloqueos si una flecha sale disparada al vacío infinito.
6. **`Outcome`**: Se procesa el resultado del cruce de flechas, se limpian los efectos que duraban un único turno y se verifica la vida de ambos combatientes.
7. **`StepForward`**: Mecánica de desempate por aproximación progresiva si ambos jugadores fallan consecutivamente.
8. **`MatchPoint`**: Indicador de tensión dramática cuando un combatiente queda a un impacto de la derrota.
9. **`GameOver`**: La partida concluye. Se acciona `MatchEndPresenter` y se despliega `MatchEndUI`.

---

## 6. Subsistemas Detallados del Gameplay

### 6.1 Subsistema del Arquero (`ArcherPlayer`, `BowController`, `AimController`)

```
                          +------------------------+
                          |      ArcherPlayer      | <--- (IEffectTarget Hub)
                          +---+----------------+---+
                              |                |
             +----------------+                +----------------+
             v                                                  v
   +-------------------+                              +-------------------+
   |   AimController   |                              |   BowController   |
   +---------+---------+                              +---------+---------+
             |                                                  |
             | Modifica FOV, offsets y                          | Modifica tensión, animación
             | pulso procedimental (sway)                       | y visibilidad de flecha
             v                                                  v
   +-------------------+                              +-------------------+
   |   PlayerShooter   |<-----------------------------+   PlayerAimIK     |
   +-------------------+  Notifica tiro confirmado    +-------------------+
```

#### A. `ArcherPlayer.cs`
- **Función**: Actúa como punto central de integración del jugador y fachada hacia el exterior.
- **Implementa**: `IEffectTarget`. Aloja los valores vigentes de juego (`Sensitivity`, `AxesInverted`, `DrawTimeMultiplier`, `BlurAmount`, `Fov`, `CrosshairVisible`, `CanShoot`, `IncomingDamageMultiplier`).
- **Registro Global**: Mantiene `AllPlayers` (lista de arqueros) y `LocalPlayer` (referencia estática al arquero del cliente local para acceso instantáneo sin búsquedas).

#### B. `BowController.cs`
- **Tensión Progresiva**: Controla el tensado de la cuerda con una `AnimationCurve` de aceleración (`drawTime = 1.2s`).
- **Validación de Disparo**: Exige mantener apuntado (clic derecho) para poder tensar (clic izquierdo). Si la tensión es inferior a `minShootTension` (0.15), el disparo no se gatilla. Soltar el clic derecho cancela el tensado sin gastar munición.
- **Sincronización Visual**: Activa y desactiva el GameObject de la flecha en la mano del modelo (`HumanArcher_ArrowInHand`) mediante una `SyncVar<bool>` con autoridad de propietario.

#### C. `AimController.cs`
- **Doble Modo de Cámara**:
  - *Cámara Normal (Tercera persona)*: Offset `(0, 0, -4.42)`.
  - *Cámara de Apuntado (Sobre el hombro / Primera persona)*: Offset `(0, 0, 0.05)` con interpolación suave (`cameraMoveSpeed = 8f`) y reducción de FOV para precisión.
- **Sway de Pulso Humano Procedural (Inspirado en Wii Sports Resort)**:
  - Genera micro-movimientos oscilatorios de pulso utilizando funciones de onda seno con semillas asimétricas para Pitch y Yaw (`swayFrequency = 1.1f`, `swayAmplitude = 1.5°`).
  - **Fatiga Acumulativa**: Si el jugador mantiene la cuerda tensada por más de 2.5 segundos (`fatigueRampTime`), el temblor se duplica progresivamente, simulando el agotamiento muscular del arquero.

#### D. `PlayerShooter.cs` (Mano Táctica de 3 Flechas)
- **Sistema de Ruleta y Munición**:
  - Cada jugador dispone de una mano activa de 3 flechas (`_currentHand[3]`).
  - La selección se realiza con las teclas numéricas `[1]`, `[2]`, `[3]` o la rueda del ratón (`Mouse ScrollWheel`).
  - Al disparar, la ranura seleccionada se marca como consumida.
  - Al consumir las 3 flechas, el servidor genera una nueva tirada aleatoria del `arsenalPool` y notifica a los clientes mediante `OnHandDealt` para accionar la ruleta gráfica.

#### E. `PlayerAimIK.cs`
- Ajusta procedimentalmente la columna, pecho y brazos del rig humanoide para que el torso apunte exactamente hacia donde mira la cámara, garantizando coherencia visual en multijugador.

#### F. `RagdollController.cs`
- Controla el cambio dinámico entre el componente `Animator` y la jerarquía de `Rigidbody` y `CharacterJoint` en las extremidades.
- Al morir, apaga el Animator, activa la física de todos los huesos y aplica el impulso vectorial de la flecha letal (`ApplyImpactForce`).

---

### 6.2 Subsistema de Proyectiles y Balística (`ArrowProjectile`, `ArrowSpawner`)

#### A. `ArrowProjectile.cs`
- **Física Autoritativa**: Maneja su propio `Rigidbody`. Inicialmente permanece en modo cinemático hasta recibir la velocidad inicial en `Launch()`.
- **Integración con Viento**: En cada frame de física (`FixedUpdate`), consulta a `WindManager.Instance.Wind` y aplica una aceleración directa multiplicada por el `windMultiplier` propio de la flecha.
- **Alineación con la Trayectoria**: Si `alignToVelocity == true`, la rotación del proyectil se actualiza en tiempo real apuntando hacia el vector de velocidad lineal (`Quaternion.LookRotation(_rb.linearVelocity)`).
- **Mecánica de Autoguiado Homing (Buscadora de Apolo)**:
  - Cuando `definition.homing == true`, el proyectil busca automáticamente el transform del torso del rival (`_homingTargetTorso`).
  - Mediante `Vector3.RotateTowards`, desvía el vector de velocidad en cada paso de física hacia el pecho del enemigo con una velocidad de giro calibrable (`homingTurnSpeed`).
  - *Regla de Balance*: Nunca puede ejecutar un impacto crítico a la cabeza; si la física detecta colisión con la cabeza, se degrada forzosamente a daño normal de torso (1.0x).
- **Impacto y Anclaje**:
  - Al colisionar, desactiva la física (`isKinematic = true`), emite el evento `OnHit`, se emparenta jerárquicamente a la superficie o hueso impactado (`transform.SetParent(collision.transform)`) y transmite el struct `ArrowHitInfo` al `CombatResolver`.

#### B. `ArrowSpawner.cs`
- Servicio estático que aísla la instanciación de red de proyectiles.
- Configura reglas de capas y llama a `Physics.IgnoreCollision` iterando todos los colliders del tirador para evitar que la flecha colisione con el arco o las manos al salir.

---

### 6.3 Subsistema de Daño y Combate (`DamageHitbox`, `CombatResolver`, `HealthModule`)

#### A. `DamageHitbox.cs`
Se acopla a los colliders de las partes anatómicas del jugador:
- **Head**: Multiplicador `2.0x` (Headshot crítico).
- **Torso**: Multiplicador `1.0x` (Impacto estándar).
- **Arm / Leg**: Multiplicador `0.75x` (Daño reducido).

#### B. `CombatResolver.cs`
Calcula la secuencia de impacto autoritativa en el servidor:
1. **Debilitación**: Si la víctima tiene un debufo de vulnerabilidad activo (por ejemplo `incomingDamageMultiplier = 1.35`), multiplica el daño entrante.
2. **Aplicación de Daño**: Invoca `HealthModule.ApplyDamage()`.
3. **Robo de Vida (Lamia Lifesteal)**: Si `lifestealRatio > 0`, cura al tirador una fracción equivalente del daño efectivamente aplicado.
4. **Aplicación de Estados**: Si la flecha contiene `EffectDefinition[]` y no fue un auto-impacto (`!hit.IsSelfHit`), los inyecta en el `ArcherPlayer` del enemigo.
5. **Ragdoll Impulso**: Si la víctima murió, transfiere la energía cinética del proyectil (`arrowSpeed * mass * 80f`) al hueso colisionado.

#### C. `HealthModule.cs`
- Mantiene la vida actual con una `SyncVar<float> currentHealth` (base 100).
- Provee métodos autoritativos en servidor: `ApplyDamage(amount)`, `ApplyHeal(amount)`.
- Dispara `OnHealthChanged` para la UI y `OnDeath` / `OnAnyPlayerDied` cuando la vida llega a cero.

---

### 6.4 Subsistema de Efectos de Estado (`EffectDefinition`, `EffectRunner`)

#### A. `EffectDefinition.cs` (ScriptableObject)
Define cualquier modificación de juego mediante datos configurables:
- **Identidad**: `displayName`, `icon`, `hudDescription`.
- **Expiración**: `expiresOnPhase` (`Outcome` para 1 turno, `Resolving` para persistencia continua hasta disparar), `phaseCount`.
- **Modificadores de Input**: `invertAxes` (Sello de Loki), `sensitivityMultiplier` (Skadi pesadez = 0.4), `drawTimeMultiplier` (Skadi tiempo de tensado = 2.0).
- **Modificadores de Cámara**: `blurAmount` (Polvo de Nix = 0.6), `fovMultiplier` (Catalejo de Argos zoom = 0.5), `hideCrosshair`.
- **Modificadores de Combate**: `damagePerSecond` (DoT de veneno de Jörmungandr), `incomingDamageMultiplier` (Vulnerabilidad amplificada).
- **Stacking**: Propiedad `StackId`. Efectos con idéntico identificador reemplazan y refrescan la duración del anterior en lugar de duplicarse erráticamente.

#### B. `EffectRunner.cs`
Instancia pura en C# asociada a cada `ArcherPlayer`. En el servidor:
- Mantiene la lista de efectos activos.
- En cada cambio de fase de `GameManager`, descuenta `RemainingPhases` y remueve los efectos vencidos.
- Recalcula los modificadores consolidados y actualiza al jugador mediante la interfaz `IEffectTarget`.

---

### 6.5 Subsistema de Cámaras Cinemáticas

#### A. `ArrowFollowCamera.cs` (Kill / Flight Cam)
- **Activación**: Se suscribe a `ArrowProjectile.OnAnyArrowSpawned`. Si el proyectil pertenece al jugador local (`shooter == ArcherPlayer.LocalPlayer.owner`), toma el control exclusivo de la cámara (`aimController.OverrideCameraControl = true`).
- **Cinemática de Persecución**: Se posiciona suavemente detrás de la flecha con desfase sobre el hombro (`_distanceBehind = 2.4m`, `_heightAbove = 0.55m`, `_sideOffset = 0.35m`) y enfoca hacia adelante (`lookAheadDistance = 8m`).
- **Impact Hold & Retorno**: Al chocar la flecha, congela el encuadre en el punto de contacto durante 1.0 segundo (`_impactHoldDuration`), y luego interpola suavemente la cámara de regreso a la cabeza del arquero (`_returnDuration = 0.55s`), restaurando el control en estado Idle.

#### B. `MatchEndPresenter.cs`
- Se activa al concluir la partida en `GameManager.GameOver`.
- Teletransporta a los dos arqueros al escenario de podio o los posiciona lado a lado en la arena.
- Al ganador lo mantiene de pie y dispara su animación de victoria (`_winnerAnimationTrigger = "Victory"`).
- Al perdedor lo colapsa en ragdoll completo en el suelo al lado del vencedor.
- Cambia a una cámara cinemática en tercera persona que encuadra a ambos combatientes.

---

### 6.6 Subsistema de UI y HUD

#### A. `ArrowSlotMachineUI.cs` (Mano Táctica Estilo Tragamoneda)
- **Ruleta Central**: Al repartirse una mano de 3 flechas, los 3 slots aparecen aumentados en el centro de la pantalla (`_centerScale = 1.2x`) y giran a alta velocidad alternando iconos y nombres.
- **Frenado Escalonado**: Los slots se detienen uno tras otro con sensación de peso (Slot 0 a los 1.0s, Slot 1 a los 1.65s, Slot 2 a los 2.3s).
- **Desplazamiento al HUD**: Tras una breve pausa, el contenedor completo viaja con interpolación suave hacia la esquina inferior derecha (`_bottomRightOffset`).
- **Feedback Interactivo**:
  - Ranura seleccionada: Halo brillante y escala aumentada.
  - Atajos de teclado: Badges visuales `[1]`, `[2]`, `[3]`.
  - Ranura consumida: Overlay oscuro de bloqueo tras disparar.

#### B. `PlayerTurnStatusUI.cs`
- Muestra mensajes dinámicos contextuales según la fase (`"TU TURNO DE DISPARAR"`, `"ESPERANDO AL OPONENTE..."`, `"FLECHAS EN VUELO"`).

#### C. `WindUi.cs`
- Consume `IWindProvider` para orientar una flecha en brújula y desplegar la velocidad del viento en metros por segundo.

#### D. `MatchEndUI.cs`
- Cartel de "VICTORIA" o "DERROTA", panel de estadísticas de rondas ganadas en el Bo3/Bo5 y botón para reiniciar la partida o retornar a la sala.

#### E. `NetworkRoomController.cs` y `RoomNetworkService.cs`
- Interfaz gráfica del Menú Principal (`MainMenu.unity`).
- Generación de códigos de sala alfanuméricos de 5 caracteres (`RoomCode`).
- Gestión de conexión Host / Cliente y transición sincronizada a la escena de combate (`Test.unity`).

---

## 7. Catálogo Completo de Assets de Datos Existentes

### 7.1 Flechas (`Assets/JuegoSinNombre/Data/`)

| Asset | Nombre en Pantalla | Masa | Viento | Daño Base | Homing | Lifesteal | Rasgo / Efectos |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **`ArrowDefault.asset`** | Flecha Común | 0.10 | 1.0x | 35 | No | 0% | Flecha de madera balanceada estándar. |
| **`ArrowColosal.asset`** | Colosal | 0.40 | 0.0x | 65 | No | 0% | Muy pesada, daño destructivo, ignora el viento por completo. |
| **`ArrowBuscadora.asset`**| Apolo | 0.08 | 0.5x | 30 | **Sí** (3.5 turn) | 0% | Guiado autoritativo al torso; prohibido el headshot. |
| **`ArrowCongelante.asset`**| Skadi | 0.12 | 1.0x | 25 | No | 0% | Aplica `EffectCongelacion` (Ralentiza mira y tensado). |
| **`ArrowVenenosa.asset`** | Jörmungandr | 0.10 | 1.0x | 20 | No | 0% | Aplica `EffectVeneno` (Daño continuo por segundo y debufo). |
| **`ArrowVampirica.asset`**| Lamia | 0.11 | 1.0x | 30 | No | **35%** | Transfiere el 35% del daño infligido como curación al tirador. |
| **`ArrowPenetrante.asset`**| Gae Bulg | 0.05 | 1.8x | 45 | No | 0% | Ultraliviana, altísima velocidad, muy desviada por el viento. |

### 7.2 Efectos de Estado (`Assets/JuegoSinNombre/Data/`)

| Asset | Nombre | Caducidad | Sensibilidad | Tensado | Blur | DoT | Daño Recibido |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **`EffectCongelacion.asset`** | Congelamiento | 1 Fase (Outcome) | 0.40x | 2.0x | 0.0 | 0 HP/s | 1.0x |
| **`EffectVeneno.asset`** | Veneno Jörmungandr | Resolving (hasta disparar) | 1.00x | 1.0x | 0.2 | 4 HP/s | 1.35x (+35% daño) |

---

## 8. Guía de Extensión y Buenas Prácticas

### 8.1 Cómo Agregar una Nueva Flecha
1. Ir a la carpeta `Assets/JuegoSinNombre/Data/`.
2. Click derecho -> **`Create` > `Arqueria` > `Arrow`**.
3. Asignar el prefab base `ArrowDefault.prefab` (o duplicarlo si requiere una malla 3D o VFX único).
4. Ajustar sliders de física y daño en el Inspector.
5. Abrir el prefab `Player.prefab` (`Assets/JuegoSinNombre/Prefabs/Player.prefab`), localizar `PlayerShooter` y agregar el nuevo asset al array `Arsenal Pool`.

### 8.2 Cómo Agregar un Nuevo Efecto de Estado
1. Click derecho en `Assets/JuegoSinNombre/Data/` -> **`Create` > `Arqueria` > `Effect`**.
2. Calibrar los modificadores deseados (inversión de ejes, sensibilidad, blur, zoom de FOV, daño continuo).
3. Asignar este efecto en la ranura `Effects On Target` de cualquier `ArrowDefinition`.

### 8.3 Convenciones de Código y Mantenimiento
- **Nada de lógica de combate en el Cliente**: Cualquier cálculo que afecte vida, estados o trayectorias de flechas debe residir dentro de `if (isServer)` o dentro de los servicios estáticos autoritativos.
- **Memoria Zero-Alloc**: Estructuras de datos que viajen en eventos frecuentes deben ser `struct` (`ArrowHitInfo`, `PendingShot`) para evitar la fragmentación de memoria del garbage collector.
- **Respeto a los Ensamblados**: Nunca referenciar clases de `JuegoSinNombre.UI` desde `JuegoSinNombre.Core` o `JuegoSinNombre.Gameplay`.

