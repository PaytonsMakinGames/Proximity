# Proximity - AI Agent Instructions

## Project Overview
Unity 2D mobile physics game where players throw a ball against walls to score points. Core loop: throw → wall bounces → catch → repeat. Features powerup system, run-based scoring with streak multipliers, and XP progression.

## Architecture & Key Systems

### Core Game Loop (RunScoring2D.cs - 2000+ lines)
The central orchestrator managing the entire game state:
- **Run lifecycle**: Tracks active runs, throw counts, and run endings (stop speed + hold time detection)
- **Scoring engine**: Complex multiplier system based on catch proximity to walls (edges/corners), catch speed, and lane adherence
- **VFX pooling**: Custom particle system pool (avoid instantiate/destroy overhead) - see `BuildVfxPool()`, `AcquireFreshLandingEmitter()`
- **Integration hub**: Coordinates FingerGrabInertia2D (input), PowerupManager (powerups), XpManager (progression), and FloatingPopupSystem (feedback)

**Critical pattern**: Uses `[DefaultExecutionOrder(100)]` to ensure it runs after input system. Always preserve execution order when modifying.

### Input System (FingerGrabInertia2D.cs)
Touch/mouse input with sophisticated physics:
- **Velocity sampling**: Averages last 8 samples over 0.1s window for consistent throw feel (prevents jittery input from affecting throw direction)
- **Kinematic dragging**: Ball becomes `RigidbodyType2D.Kinematic` while held to avoid physics interference
- **Catch forgiveness**: Speed-scaled tap radius (0.2 + up to 0.6 at max speed) - faster balls are easier to catch
- **Wall release assist**: When releasing near walls, redirects velocity inward (min 0.75 u/s) while preserving speed magnitude
- **Trail teleport handling**: Clears and pauses TrailRenderer when teleporting ball to finger on miss (prevents visual artifacts)
- **Pause blocking**: Blocks 3+ finger gestures for 0.3s after throw to prevent accidental pauses

**Integration**: `OnDragBegan`/`OnDragEnded` events signal RunScoring2D. `WasThrown`/`WasDropped` flags consumed each frame.

### Powerup System (PowerupManager.cs)
Event-driven powerup architecture with "armed → trigger → consume" lifecycle:
- **Arming**: Player selects powerup from inventory, stored in `ArmedId` (does NOT consume yet)
- **Triggering**: Powerups fire on specific events (`PowerupTrigger.NextThrowRelease`, `NextWallContact`)
- **Consumption**: Only spent when trigger condition met and powerup successfully activates
- **State tracking**: Per-run flags (`hotSpotUsedThisRun`, `overtimeActiveThisRun`) and per-throw flags (`landingAmpActiveThisThrow`)

**Key powerups**:
- **Sticky Ball**: Pins ball to wall via `RigidbodyConstraints2D.FreezePosition`, tracks throw distance to prevent exploits
- **Hot Spot**: Spawns shrinking target zone, grants bonus distance per hit (swept circle collision detection)
- **Overtime**: Accumulates air time across throws, provides scaling multiplier (up to +50% at 4s)
- **Encore**: Grants +1 throw mid-run OR saves from run end (only one per run via `EncoreAnyUsedThisRun`)

**Critical**: Powerups integrate deeply with RunScoring2D via callbacks (`OnThrowReleased()`, `OnPickupHappened()`, `OnRunEnded()`)

## Unity-Specific Conventions

### Inspector Configuration Pattern
All tunable values use `[SerializeField]` with `[Header()]` grouping:
```csharp
[Header("Landing Multiplier")]
[SerializeField] float closenessExponent = 2.0f;
[SerializeField] float maxMultiplier = 4.0f;
```
**When adding features**: Always expose tuning parameters via SerializeField for designer iteration.

### Execution Order Dependencies
- FingerGrabInertia2D: `[DefaultExecutionOrder(-100)]` (runs first)
- RunScoring2D: `[DefaultExecutionOrder(100)]` (runs last)
- Ensures input is processed before game logic evaluates throw states

### Component References
- Auto-find pattern in `Awake()`: `if (!component) component = FindFirstObjectByType<T>(FindObjectsInactive.Include);`
- Always null-check before using optional dependencies (popups, actions, etc.)

## Critical Development Patterns

### State Management
**Run vs Throw vs Segment granularity**:
- **Run state**: Resets on `OnRunStarted()` - use for powerup "once per run" logic
- **Throw state**: Resets on `OnThrowReleased()` - use for per-throw effects
- **Segment state**: Resets on pickup - use for catch multipliers, landing bonuses

Example from PowerupManager:
```csharp
public void OnRunStarted() {
    encoreUsedThisRun = false;
    overtimeUsedThisRun = false;
}

public void OnThrowReleased(...) {
    landingAmpActiveThisThrow = true;
}

public void OnPickupHappened() {
    landingAmpActiveThisThrow = false;  // Segment ended
}
```

### Event-Driven Communication
Components communicate via C# events, not direct calls:
```csharp
// In FingerGrabInertia2D:
public event Action OnDragBegan;
public event Action<bool> OnDragEnded;

// In RunScoring2D.Awake():
if (grab) {
    grab.OnDragBegan += OnAnyPickupStarted;
    grab.OnDragEnded += (thrown) => { /* ... */ };
}
```
**When adding features**: Subscribe in `OnEnable()`, unsubscribe in `OnDisable()` to prevent memory leaks.

### VFX Pooling Pattern
Never instantiate ParticleSystem at runtime:
```csharp
ParticleSystem AcquireFreshLandingEmitter() {
    // Find free pooled system
    for (int i = 0; i < poolCount; i++) {
        if (IsFree(pool[i])) return pool[i].ps;
    }
    // Auto-expand if allowed
    if (poolAutoExpand && poolCount < poolMaxSize) {
        CreateAndAddPooledSystem();
    }
}
```
Always use `StopAndClear()` or `StopEmittingOnly()` helpers.

## Common Tasks

### Adding a New Powerup
1. Add ID constant and config fields in PowerupManager under new `[Header("Name")]`
2. Add runtime state flags (e.g., `bool newPowerupActiveThisRun`)
3. Implement trigger logic in `OnThrowReleased()` or create new callback
4. Reset state in `OnRunEnded()` and/or `OnRunStarted()`
5. Integrate scoring effects in RunScoring2D (query PowerupManager state)
6. Add popup feedback via `FloatingPopupSystem.PopAtWorldWithExtraOffset()`

### Modifying Scoring Calculations
Core scoring in RunScoring2D's `Update()` (lines ~1500-1900):
- `travelDistance` tracks total movement via `Vector2.Distance(ballRb.position, lastRunPos)`
- Multipliers stack: base × catch multiplier × powerup multipliers (Landing Amp, Overtime)
- XP awarded in `AwardRunXp()` with `GetXpMultRaw()` from equipped ball
- Hot Spot bonus adds direct distance: `hotSpotBonusDistanceThisRun += ...`

### Touch Input Debugging
Check `FingerGrabInertia2D`:
- `ActiveTouchCount()` - counts active touches (cancels drag at 3+)
- `IsTapOnBall()` - uses speed-scaled forgiveness radius
- `pauseBlockTimer` - prevents pause gestures after throws

## Testing & Debugging

### Inspector Shortcuts
- **RunScoring2D**: Observe `throwsUsedThisRun`, `catchMultiplier`, `travelDistance` at runtime
- **PowerupManager**: Check `ArmedId` and per-run flags to debug powerup state
- **FingerGrabInertia2D**: Watch `IsDragging`, `LastPickupWasCatch` to debug input

### Common Pitfalls
1. **Forgetting execution order**: Changes to input/scoring timing? Check `[DefaultExecutionOrder]`
2. **Event leaks**: Always unsubscribe in `OnDisable()` when subscribing in `OnEnable()`
3. **VFX not stopping**: Must call `StopAndClear(ps)` AND reset to parent transform
4. **Powerup state leaks**: Ensure all flags reset in `OnRunEnded()` or `OnRunStarted()`
5. **Trail artifacts on teleport**: Call `ClearAllTrails()` + pause emitting (see `ResetTrailsAfterTeleport()`)

## File Organization
- `Assets/Scripts/` - Core gameplay
- `Assets/Scripts/Powerups/` - Powerup system (Manager, Database, Inventory, Definitions)
- MonoBehaviours follow Unity naming: `SystemName2D.cs` pattern

## Build & Run
Unity project - use Unity Editor (2021.3+ recommended based on TextMeshPro usage). No custom build scripts detected.
