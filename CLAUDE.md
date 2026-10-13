# Archery PvP — Unity final project

Multiplayer 1v1 archery game. Two stationary players shoot at each other across a gap,
stepping closer each round if both miss. Networking is **PurrNet**.

Team of two: **Programmer A** (client: input, bow, arrow flight, wind) and
**Programmer B** (server: round state machine, combat, health, effects, HUD).

## PurrNet API — the GDD is wrong about these

The project's GDD workplan was written against a different networking library.
Do not follow its code snippets. The real API:

- **Ownership check is `isOwner`**, lowercase. Also `isServer`, `isClient`, `IsController`.
- **`SyncVar` is not an attribute.** It is an instantiated generic field:
  `private readonly SyncVar<float> health = new(100f);` — read and write via `.value`,
  subscribe with `.onChanged`. Pass `ownerAuth: true` only for things the owner should
  control directly (camera-ish values), never for health or score.
- **Spawning needs no RPC.** A plain Unity `Instantiate()` replicates automatically if the
  prefab has a NetworkIdentity and is registered in the NetworkPrefabs ScriptableObject.
  `Destroy()` despawns. Whether a client may do this is decided by the Network Rules asset.
- **Lifecycle hooks are `OnSpawned(bool asServer)` / `OnDespawned(bool asServer)`**, not
  `Start`. `NetworkModule` uses `OnSpawn` (no "ed") — the two differ, watch it.
- `owner` is a **nullable** `PlayerID`. Check `.HasValue` before using `.Value`.
- Everything networked derives from `NetworkIdentity`; it is a base class, not a component
  you add separately.

Docs: https://purrnet.dev/docs

## Architecture — three layers

Written this way because the real complexity is not the shooting, it is the combinatorics
of 6 arrows x 6 consumables x status effects. The layers keep that from becoming a giant
switch statement.

**1. Data — `Scripts/Core/`.** ScriptableObjects, no logic, no networking.
`ArrowDefinition`, `ConsumableDefinition`, `EffectDefinition`, plus the `RoundPhase` enum
and the interfaces in `GameContracts.cs`. The six arrows of the GDD are six **assets**, not
six classes. Balancing is done in the Inspector, never by editing code.

**2. Server — `Scripts/Server/`.** Sole authority. `RoundStateMachine`, `CombatResolver`,
`EffectRunner`, health. Anything a player could cheat at lives here.

**3. Client — `Scripts/Client/`, `Scripts/UI/`.** Input, aim, camera, HUD, VFX.

### The rule that holds it together

**The client never decides anything, it only reacts.** A frozen player's client does not
know Skadi exists — it receives "sensitivity x0.4, draw time x2" and obeys. This is why
adding an arrow in Phase 3 touches zero lines of client code.

Concretely: `ArcherPlayer` implements `IEffectTarget` and is the **only** entry point for
effects. `BowController` and `AimController` read their multipliers from it every frame;
they hold no flags of their own.

### Effects are recalculated, never accumulated

`EffectRunner` calls `ResetModifiers()` and then re-applies every live effect from scratch.
It must never do `sensitivity *= 0.4` on apply and `/= 0.4` on expiry — with overlapping
effects, float rounding leaves the player permanently wrong. Recalculating costs nothing;
it happens once per phase, not per frame.

Live effects are a **list**. Argos (zoom) + Nix (blur), or Loki + Skadi, can be active on
the same player at once. A single `currentEffect` field is the tempting shortcut and it is
wrong.

## Round state machine

Does not exist in the GDD and must. Without it, the rule "consumables only before drawing"
is unimplementable, because no moment is defined as "before drawing".

`WaitingForPlayers -> RoundStart -> ConsumableWindow -> Aiming -> Resolving -> Outcome`
then `StepForward` (both missed) or `MatchPoint` / `GameOver`.

- `RoundStart` — server rolls the wind.
- `ConsumableWindow` — the only phase accepting consumables. Bow locked.
- `Aiming` — bows unlocked, consumables refused.
- `Resolving` — arrows in flight, input locked.
- `Outcome` — damage applied, one-turn effects expire.

Everything else asks the FSM what phase it is in rather than keeping its own flag.
Jörmungandr's poison (drains until the victim shoots) is just an effect expiring on
`Resolving`.

## Contracts between the two programmers

In `Scripts/Core/GameContracts.cs`. Each side codes against the interface and mocks the
other's with a three-line stub. Do not change a signature without telling the other person.

- `IEffectTarget` — what the client exposes for the server to modify.
- `IWindProvider` — what `WindManager` exposes for the HUD's wind rose.
- `ArrowHitInfo` — struct passed from `CombatResolver` to the health module.

## Repo layout

```
Assets/_Project/
  Scripts/{Core,Server,Client,UI}/
  Data/        ScriptableObject assets (arrows, effects, consumables)
  Prefabs/     NetworkPrefabs points its folder search here
  Scenes/  Art/
Packages/      PurrNet lives here — do not touch
```

## Unity / project settings that matter

- **Asset Serialization: Force Text** and **Visible Meta Files** (Project Settings >
  Editor / Version Control). Without them, scenes and prefabs save as binary and any
  two-person merge is unresolvable.
- **Commit every `.meta` file.** They hold the GUIDs that map scripts to components. A lost
  `.meta` breaks every Inspector reference on the other machine.
- `.gitignore` must cover `Library/ Temp/ Obj/ Build/ Logs/ UserSettings/`.
  Use https://github.com/github/gitignore/blob/main/Unity.gitignore
- Arrow prefab Rigidbody needs **Collision Detection: Continuous Dynamic**. On Discrete, a
  fast arrow tunnels through thin targets and registers no hit.
- `Rigidbody.linearVelocity` is Unity 6 / 2023+. On older versions it is `velocity`.

## Conventions

- Code comments in Spanish, identifiers in English.
- Prefer `AnimationCurve` and `[SerializeField]` over hardcoded tuning values, so the
  designer-facing feel is editable without recompiling.
- Expose state changes as C# `event Action<T>` (observer pattern) rather than having the UI
  poll in `Update`.
- Validate on the server: clamp anything a client sends, and reject implausible values.

## Current status

Phase 1. Client layer written (`ArcherPlayer`, `AimController`, `BowController`,
`PlayerShooter`, `ArrowProjectile`, `WindManager`, `LocalPlayerSetup`). Core layer written.
Server layer not started.

`RoundPhaseStub.cs` is a temporary mock standing in for the FSM — **delete it** once
`RoundStateMachine` exists.

Next milestone is the vertical slice: one arrow, no effects, leaving one screen and landing
on the other. Nothing else gets built until that works.
