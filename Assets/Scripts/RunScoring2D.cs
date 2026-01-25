using UnityEngine;
using TMPro;

[DefaultExecutionOrder(100)]
public class RunScoring2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Rigidbody2D ballRb;
    [SerializeField] CircleCollider2D ballCollider;
    [SerializeField] Camera cam;
    [SerializeField] FingerGrabInertia2D grab;

    [Header("Landing VFX (pooled)")]
    [SerializeField] ParticleSystem landedLoopVfxPrefab;
    [SerializeField, Min(1)] int poolInitialSize = 3;
    [SerializeField, Min(1)] int poolMaxSize = 3;
    [SerializeField] bool poolAutoExpand = false;
    [SerializeField] float landingVfxZOffset = 10f;
    [SerializeField] Transform vfxPoolRoot;

    [Header("UI")]
    [SerializeField] TextMeshProUGUI distanceText;
    [SerializeField] TextMeshProUGUI landMultText;
    [SerializeField] TextMeshProUGUI xpMultText;
    [SerializeField] TextMeshProUGUI finalScoreText;
    [SerializeField] TextMeshProUGUI bestText;
    [SerializeField] TextMeshProUGUI overtimeComparisonText;

    [Header("Totals UI")]
    [SerializeField] TextMeshProUGUI totalDistanceText;
    [SerializeField] TextMeshProUGUI totalBouncesText;

    [Header("Lines + In-ball")]
    [SerializeField] LineRenderer wallLineA;
    [SerializeField] LineRenderer wallLineB;
    [SerializeField] TextMeshPro inBallLabel;

    [Header("World / Bounds")]
    [SerializeField] bool lockWorldHeight = true;
    [SerializeField] float targetWorldHeight = 10f;
    [SerializeField] float boundsInset = 0f;

    [Header("Visual Rules")]
    [SerializeField] float showBelowSpeed = 2.5f;
    [SerializeField] float ballEdgeOffset = 0.01f;

    [Header("Wall Colors")]
    [SerializeField] Color leftWallColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] Color rightWallColor = new Color(1f, 0.3f, 0.8f);
    [SerializeField] Color bottomWallColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] Color topWallColor = new Color(1f, 0.85f, 0.2f);

    [Header("Score Text Dimming")]
    [SerializeField] Color scoreTextLiveColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] Color scoreTextEndedColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] Color finalScoreColor = new Color(1f, 0.2f, 0.2f, 1f);  // Red
    [SerializeField] Color overtimeBonusColor = new Color(1f, 0.85f, 0.5f, 1f);
    [SerializeField] Color hotSpotBonusColor = new Color(1f, 0.75f, 0.75f, 1f); // Match Hot Spot popup

    [Header("Ruler Fade")]
    [SerializeField] float rulerFadeExponent = 1.5f;
    [SerializeField, Range(0.5f, 0.99f)] float popStart = 0.90f;
    [SerializeField, Range(0f, 2f)] float popStrength = 0.85f;

    [Header("Run End Detection")]
    [SerializeField] float stopSpeed = 0.18f;
    [SerializeField] float stopHoldTime = 0.18f;

    [Header("Score / XP")]
    [Tooltip("Boost distance units so the number keeps ticking while the ball crawls to a stop.")]
    [SerializeField] float distanceUnitScale = 1.5f;

    [Header("Landing Multiplier")]
    [SerializeField] float closenessExponent = 2.0f;
    [SerializeField] float maxMultiplier = 4.0f;

    [Header("Normal Shape (Uncapped)")]
    [SerializeField] float normalEdgeValue = 2f;
    [SerializeField] float normalCornerTotal = 5f;

    [Header("Capped Shape (Lane Penalty)")]
    [SerializeField] float cappedEdgeValue = 1.0f;
    [SerializeField] float cappedCornerTotal = 2.0f;

    [Header("Lane Cap (No Speed Factor)")]
    [SerializeField] float laneHalfWidth = 0.35f;
    [SerializeField] LaneAxisMode laneAxisMode = LaneAxisMode.AutoFromInitialVelocity;
    [SerializeField] ForcedLaneAxis forcedLaneAxis = ForcedLaneAxis.Vertical;

    [Header("Run Throw Limit")]
    [SerializeField, Min(0)] int throwsPerRun = 7;
    [SerializeField] bool consumeThrowOnRelease = true;
    [SerializeField] bool consumeThrowOnMiss = true;

    [Header("UI (optional)")]
    [SerializeField] TextMeshProUGUI throwsLeftText;

    [Header("Totals Save Keys")]
    [SerializeField] string totalDistanceKey = "TotalDistance";
    [SerializeField] string totalBouncesKey = "TotalBounces";

    [Header("Totals Banking")]
    [SerializeField] float bankDistanceStep = 0.25f;
    [SerializeField] float saveInterval = 1.0f;

    [Header("Best Color (when beaten)")]
    [SerializeField] Color bestBeatenColor = new Color(0.8039216f, 0.6431373f, 0.1019608f, 1f);

    [Header("Best UI")]
    [SerializeField] Color bestNormalColor = Color.white;

    [Header("XP")]
    [SerializeField] XpManager xp;
    [SerializeField] PlayerInventory inventory;

    [Header("Edge Case")]
    [SerializeField, Min(0.1f)] float edgeCaseDistanceMultiplier = 3f;  // Multiplier on thrown distance before landing mult
    [SerializeField, Min(0.01f)] float edgeCaseClosenessBaseline = 0.1f; // Minimum closeness factor at threshold
    [SerializeField, Min(0.1f)] float edgeCaseClosenessExponent = 2.5f;  // Steepness toward the wall

    [Header("Powerups (v1)")]
    [SerializeField] PowerupManager powerups;

    [Header("Popups (optional)")]
    [SerializeField] FloatingPopupSystem popups;

    [Header("Sticky Ball Anti-Exploit")]
    [SerializeField, Min(0.01f)]
    float stickyFullDistanceForNormalLanding = 2.0f;

    // Sticky Ball runtime state
    bool stickyPinned;
    int stickyPinnedFrame = -9999;
    RigidbodyConstraints2D stickyPrevConstraints;
    RigidbodyInterpolation2D stickyPrevInterpolation;

    float stickyThrowDistance;
    Vector2 stickyThrowLastPos;

    [Header("Hot Spot Visuals")]
    // Visuals (world objects)
    [SerializeField] Transform hotSpotMarker;   // circle sprite object
    [SerializeField] TextMeshPro hotSpotText;   // text centered in circle

    // Colors
    [SerializeField] Color hotSpotLiveColor = new Color(1f, 0.75f, 0.75f, 1f);
    [SerializeField] Color hotSpotDimmedColor = new Color(1f, 0.75f, 0.75f, 0.35f);

    // Gameplay tuning
    [SerializeField, Min(0.05f)] float hotSpotRadiusStart = 0.9f;

    [SerializeField, Min(0f)] float hotSpotHitCooldown = 0.05f;

    // Runtime state
    bool hotSpotInside;
    Vector2 hotSpotLastPos;
    Vector2 hotSpotCenter;
    float hotSpotRadius;
    int hotSpotBonusDistanceThisRun;
    int hotSpotTotalPointsThisRun;
    float hotSpotLastHitTime;  // Prevent rapid multi-hits with cooldown
    bool hotSpotEncoreRestored; // True if revived via Encore after run end

    // Hot Spot snapshot for Encore revive
    bool savedHotSpotActive;
    Vector2 savedHotSpotCenter;
    float savedHotSpotRadius;
    int savedHotSpotBonusDistanceThisRun;
    int savedHotSpotTotalPointsThisRun;
    float savedHotSpotLastHitTime;

    [Header("Run Cancel")]
    [SerializeField] KeyCode cancelRunKey = KeyCode.X;
    [SerializeField] int cancelRunFingerCount = 5;

    [SerializeField] ActionDetector actions;

    bool cancelGestureArmed = true;

    public int EffectiveThrowsPerRun
    {
        get
        {
            // Apply inventory bonus throws from equipped ball (always visible in UI).
            // During runs, include run-earned bonus throws as well.
            int bonus = inventory ? inventory.GetBonusThrows() : 0;
            return Mathf.Max(0, throwsPerRun + bonus + bonusThrowsThisRun);
        }
    }

    public float BestScore { get; private set; }
    public float StopSpeed => stopSpeed;
    public int ThrowsLeft => Mathf.Max(0, EffectiveThrowsPerRun - throwsUsedThisRun);
    public bool ThrowsExhausted => EffectiveThrowsPerRun > 0 && throwsExhausted;
    public bool RunActive => streakActive;

    public bool CanPickUpBallNow()
    {
        if (!streakActive) return true;
        if (EffectiveThrowsPerRun <= 0) return true;
        return !throwsExhausted;
    }

    float totalDistance;
    float bankDistanceRemainder;
    float saveCooldown;

    float stopTimer;
    float travelDistance;
    float travelDistanceWithoutOvertime;  // For comparison display
    float hotSpotBonusDistance;  // Separate tracker so Hot Spot doesn't interfere with overtime bonus calc

    // Overtime snapshot for Encore revive
    bool savedOvertimeActive;
    bool savedOvertimeUsed;
    float savedOvertimeElapsed;
    int savedFirstPowerupUsed;  // Preserve powerup order across Encore revive

    int overtimeBonusDisplayed;  // Only increases, never decreases
    int firstPowerupUsed;  // 0 = none, 1 = overtime, 2 = hot spot (used to reorder UI)

    float laneCenterX, laneCenterY;
    float shownLandingMult = 1f;

    float nextUiTick;
    const float uiTickRate = 1f / 30f;

    int totalBounces;
    int catchesThisRun;
    int throwsUsedThisRun;
    bool throwsExhausted;

    int bonusThrowsThisRun;
    bool encoreUsedThisRun;
    bool encoreReviveUsedThisRun;
    bool EncoreAnyUsedThisRun => encoreUsedThisRun || encoreReviveUsedThisRun;
    bool pendingEncoreBonusForNewRun;

    // After a run ends, we allow one revive window where XP and Best can adjust.
    bool pendingRunAdjustActive;
    float pendingBestBaseline;

    bool totalsDirty;
    bool streakActive;
    bool prevHeld;
    bool laneBroken;
    bool disableLaneCapThisSegment;
    bool resultLatched;
    bool showLandingInfo;
    bool lastRunNoLanding;
    bool prevWasThrown;
    bool prevWasDropped;
    bool landingAllowedThisSegment;
    bool suppressResultVisualsUntilThrow;

    // VFX state
    ParticleSystem activeLandingPs;
    bool activeLandingFollowBall;
    Quaternion landedPrefabRotation = Quaternion.identity;
    Vector3 landedPrefabScale = Vector3.one;
    ParticleSystem.MinMaxGradient landedPrefabStartColor;

    Color bestDefaultColor;
    Vector2 lastRunPos;
    LaneAxis laneAxis;

    enum WallSide { Left, Right, Bottom, Top }
    enum LaneAxis { AlongX, AlongY }
    enum LaneAxisMode { AutoFromInitialVelocity, ForceAxis }
    enum ForcedLaneAxis { Horizontal, Vertical }

    struct WorldBounds
    {
        public float left, right, bottom, top, r;
        public float centerX, centerY;
        public float halfW, halfH;
    }

    static int RoundInt(float v) => Mathf.RoundToInt(v);
    static float Round2(float v) => Mathf.Round(v * 100f) / 100f;
    static float Round3(float v) => Mathf.Round(v * 1000f) / 1000f;

    class PooledVfx { public ParticleSystem ps; public float lastUsedTime; }
    PooledVfx[] pool;
    int poolCount;

    const string SCORE_VALUE_COLOR = "#FF0000FF";
    const string DETAILS_DIM_COLOR = "#FFFFFF33";

    float GetXpMultRaw() => inventory ? inventory.GetXpMultiplier() : 1f;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        if (!inventory) inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (!xp) xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
        if (!powerups) powerups = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);
        if (!actions) actions = FindFirstObjectByType<ActionDetector>(FindObjectsInactive.Include);
        if (!popups) popups = FindFirstObjectByType<FloatingPopupSystem>(FindObjectsInactive.Include);

        totalDistance = PlayerPrefs.GetFloat(totalDistanceKey, 0f);
        totalBounces = PlayerPrefs.GetInt(totalBouncesKey, 0);
        BestScore = PlayerPrefs.GetFloat("BestScore", 0f);

        bestDefaultColor = bestNormalColor;

        if (distanceText) distanceText.richText = true;
        if (landMultText) landMultText.richText = true;
        if (xpMultText) xpMultText.richText = true;
        if (finalScoreText) finalScoreText.richText = true;
        if (bestText)
        {
            bestText.color = bestDefaultColor;
            bestText.gameObject.SetActive(true);
        }

        if (landedLoopVfxPrefab)
        {
            landedPrefabRotation = landedLoopVfxPrefab.transform.rotation;
            landedPrefabScale = landedLoopVfxPrefab.transform.localScale;
            landedPrefabStartColor = landedLoopVfxPrefab.main.startColor;
        }

        saveCooldown = saveInterval;

        BuildVfxPool();
        InitLines();
        HideAll();
        RefreshAllUI();
        UpdateThrowsUi();

        // Initialize powerup order tracking
        firstPowerupUsed = 0;
        resultLatched = false;
    }

    void OnEnable()
    {
        // Awake already tries to find inventory, but this keeps it safe if the object gets re-enabled later.
        if (!inventory) inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (inventory) inventory.OnChanged += OnInventoryChanged_RefreshThrows;
    }

    void OnDisable()
    {
        if (inventory) inventory.OnChanged -= OnInventoryChanged_RefreshThrows;
    }

    void Update()
    {
        if (!ballRb || !ballCollider || !cam) return;

        // Single decrement per frame
        saveCooldown -= Time.deltaTime;

        bool uiTick = UiTick();
        bool pausedOrLocked = GameInputLock.Locked;

        if (!pausedOrLocked)
        {
            if (CancelRunPressedThisFrame() && CanCancelNow())
            {
                CancelRun_NoBank_NoScore();
                return;
            }
        }

        if (lockWorldHeight && cam.orthographic)
            cam.orthographicSize = targetWorldHeight * 0.5f;

        if (activeLandingFollowBall && activeLandingPs)
            activeLandingPs.transform.position = BallPosWithVfxZ();

        bool isHeld = !pausedOrLocked && grab && grab.IsDragging;
        if (!pausedOrLocked && powerups)
            powerups.TickOvertime(isHeld);

        UpdateHotSpotHitState(isHeld);
        bool wasThrownFlag = !pausedOrLocked && grab && grab.WasThrown;
        bool wasDroppedFlag = !pausedOrLocked && grab && grab.WasDropped;

        bool throwEvent = wasThrownFlag && !prevWasThrown;
        prevWasThrown = wasThrownFlag;

        bool dropEvent = wasDroppedFlag && !prevWasDropped;
        prevWasDropped = wasDroppedFlag;

        if (isHeld && !prevHeld) OnAnyPickupStarted();

        if (throwEvent)
        {
            // Let PowerupManager handle all throw-release powerup logic first,
            // so Encore revive can be detected before deciding a new run start.
            if (powerups && ballRb)
            {
                // Check for Encore revive (after a latched run, before a new streak starts)
                bool isEncoreRevive = !streakActive && pendingRunAdjustActive && !powerups.EncoreAnyUsedThisRun;
                bool runStartHandled = false;

                // If NOT a revive and we're not in a run yet, start a brand-new run BEFORE processing powerups
                if (!streakActive && !isEncoreRevive)
                {
                    // Reset powerup detection order for this new run
                    firstPowerupUsed = 0;
                    savedFirstPowerupUsed = 0;

                    ClearHotSpotAll();
                    // Also clear the saved snapshot so it can't interfere with the new hot spot
                    savedHotSpotActive = false;
                    savedHotSpotCenter = Vector2.zero;
                    savedHotSpotRadius = 0f;
                    savedHotSpotBonusDistanceThisRun = 0;
                    savedHotSpotTotalPointsThisRun = 0;
                    savedHotSpotLastHitTime = -9999f;

                    if (powerups) powerups.DisableHotSpot();

                    if (powerups) powerups.OnRunStarted();
                    // Do NOT reset Overtime here; let it persist across multiple throws in logical sequence
                    if (actions) actions.OnRunStarted();

                    runStartHandled = true;
                }

                // Detect Encore consumption on THIS throw
                bool encoreWasUsed = powerups.EncoreAnyUsedThisRun;
                powerups.OnThrowReleased((Vector2)ballRb.position, isEncoreRevive);
                bool encoreJustUsed = !encoreWasUsed && powerups.EncoreAnyUsedThisRun;

                // If this throw was in the revive window but Encore wasn't consumed, start a normal run now
                if (isEncoreRevive && !encoreJustUsed && !runStartHandled)
                {
                    ClearHotSpotAll();
                    savedHotSpotActive = false;
                    savedHotSpotCenter = Vector2.zero;
                    savedHotSpotRadius = 0f;
                    savedHotSpotBonusDistanceThisRun = 0;
                    savedHotSpotTotalPointsThisRun = 0;
                    savedHotSpotLastHitTime = -9999f;

                    if (powerups)
                    {
                        powerups.OnRunStarted();
                    }
                    if (actions) actions.OnRunStarted();

                    runStartHandled = true;
                    isEncoreRevive = false;
                }

                // If Encore was used on the very first throw of a brand-new run, grant its bonus after StartStreak runs.
                if (encoreJustUsed && !isEncoreRevive && !streakActive)
                    pendingEncoreBonusForNewRun = true;

                // Determine which powerup activated FIRST in the run (only set once)
                if (firstPowerupUsed == 0)
                {
                    // If Hot Spot just spawned, check if Overtime was already active
                    if (powerups.HotSpotJustSpawnedThisThrow && ballRb)
                    {
                        if (powerups.OvertimeUsedThisRun)
                            firstPowerupUsed = 1;  // Overtime was already active, so it's first
                        else
                            firstPowerupUsed = 2;  // Hot Spot is first to activate
                    }
                    else if (powerups.OvertimeUsedThisRun)
                    {
                        // Overtime is active and Hot Spot didn't just spawn now, so Overtime is first
                        firstPowerupUsed = 1;
                    }
                }

                // Handle Hot Spot initialization (only on first throw when spawned)
                if (powerups.HotSpotJustSpawnedThisThrow && ballRb)
                {
                    hotSpotCenter = ballRb.position;
                    hotSpotRadius = hotSpotRadiusStart;
                    hotSpotInside = false;
                    hotSpotLastPos = ballRb.position;
                    hotSpotBonusDistanceThisRun = 0;
                    hotSpotTotalPointsThisRun = 0;  // Reset points for this hot spot instance
                    hotSpotLastHitTime = -9999f;  // Reset hit timer so first hit counts immediately
                    HotSpot_SetVisualsActive(true);
                    HotSpot_UpdateVisuals(true);  // Force live color on spawn
                    hotSpotInside = IsBallOverlappingHotSpot(ballRb.position);
                }
                else if (HotSpotActive() && ballRb)
                {
                    // Keep visual active but don't reposition - it stays where it spawned
                    HotSpot_SetVisualsActive(true);
                    hotSpotInside = IsBallOverlappingHotSpot(ballRb.position);
                    hotSpotLastPos = ballRb.position;
                }

                if (encoreJustUsed)
                {
                    if (isEncoreRevive)
                    {
                        // Revive the just-ended run and grant +1 throw
                        streakActive = true;
                        resultLatched = false;
                        bonusThrowsThisRun += 1;
                        throwsExhausted = false;
                        UpdateThrowsUi();
                        stopTimer = 0f;

                        // Restore overtime state if it was active before the run ended
                        if (powerups && savedOvertimeUsed)
                        {
                            powerups.RestoreOvertimeSnapshot(savedOvertimeActive, savedOvertimeUsed, savedOvertimeElapsed);
                        }

                        // Restore powerup order for this revived run
                        firstPowerupUsed = savedFirstPowerupUsed;

                        // Restore Hot Spot state/visuals for Encore revive (un-dim)
                        RestoreHotSpotSnapshotIfEncore();
                        if (HotSpotActive())
                            HotSpot_UpdateVisuals(true);
                        hotSpotInside = IsBallOverlappingHotSpot(ballRb.position);
                        hotSpotLastPos = ballRb.position;

                        UpdateOvertimeComparisonUI();
                    }
                    else if (streakActive)
                    {
                        // Mid-run Encore: +1 throw and clear exhaustion
                        bonusThrowsThisRun += 1;
                        throwsExhausted = false;
                        UpdateThrowsUi();
                    }
                }

                // If this was a revive, we already kept the previous run active above.
            }

            OnThrown();

            // Apply pending Encore bonus after StartStreak has reset run state
            if (pendingEncoreBonusForNewRun)
            {
                bonusThrowsThisRun += 1;
                throwsExhausted = false;
                UpdateThrowsUi();
                pendingEncoreBonusForNewRun = false;
            }
            ConsumeThrow(fromMiss: false);

            if (grab) grab.ConsumeWasThrown();
            prevWasThrown = false;
        }

        if (dropEvent)
        {
            if (grab) grab.ConsumeWasDropped();
            prevWasDropped = false;

            if (!streakActive)
                return;

            EndStreak_DropInstant();
        }

        if (streakActive && isHeld && !prevHeld) OnPickupStarted();
        if (streakActive && !isHeld) TickStreakFlight();

        UpdatePlacementVisualsAndMultiplier(isHeld, uiTick);

        if (uiTick)
            UpdateScoreText();

        prevHeld = isHeld;
    }

    // ---------------- VFX Pool ----------------

    void BuildVfxPool()
    {
        poolInitialSize = Mathf.Clamp(poolInitialSize, 1, 256);
        poolMaxSize = Mathf.Clamp(poolMaxSize, poolInitialSize, 512);

        if (!landedLoopVfxPrefab)
        {
            pool = null;
            poolCount = 0;
            return;
        }

        pool = new PooledVfx[poolMaxSize];
        poolCount = 0;

        for (int i = 0; i < poolInitialSize; i++) CreateAndAddPooledSystem();
        for (int i = 0; i < poolCount; i++) StopAndClear(pool[i].ps);
    }

    void CreateAndAddPooledSystem()
    {
        if (poolCount >= poolMaxSize) return;

        ParticleSystem ps = Instantiate(landedLoopVfxPrefab, vfxPoolRoot ? vfxPoolRoot : transform);
        ps.transform.localPosition = Vector3.zero;
        ps.transform.rotation = landedPrefabRotation;
        ps.transform.localScale = landedPrefabScale;

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true; // looping is fine; we stop emitting when we need it to retire
        main.stopAction = ParticleSystemStopAction.None;

        StopAndClear(ps);

        pool[poolCount++] = new PooledVfx { ps = ps, lastUsedTime = -9999f };
    }

    static void StopAndClear(ParticleSystem ps)
    {
        if (!ps) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(true);
    }

    static void StopEmittingOnly(ParticleSystem ps)
    {
        if (!ps) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    static bool IsFree(PooledVfx p) => p != null && p.ps && !p.ps.IsAlive(true);

    ParticleSystem AcquireFreshLandingEmitter()
    {
        if (pool == null || poolCount == 0) return null;

        for (int i = 0; i < poolCount; i++)
        {
            if (!IsFree(pool[i])) continue;
            pool[i].lastUsedTime = Time.time;
            StopAndClear(pool[i].ps);
            return pool[i].ps;
        }

        if (poolAutoExpand && poolCount < poolMaxSize)
        {
            CreateAndAddPooledSystem();
            var p = pool[poolCount - 1];
            p.lastUsedTime = Time.time;
            StopAndClear(p.ps);
            return p.ps;
        }

        // Steal oldest if allowed to overlap more than pool size.
        int oldestIndex = 0;
        float oldestTime = float.PositiveInfinity;
        for (int i = 0; i < poolCount; i++)
        {
            if (pool[i].lastUsedTime < oldestTime)
            {
                oldestTime = pool[i].lastUsedTime;
                oldestIndex = i;
            }
        }

        pool[oldestIndex].lastUsedTime = Time.time;
        StopAndClear(pool[oldestIndex].ps);
        return pool[oldestIndex].ps;
    }

    Vector3 BallPosWithVfxZ()
    {
        Vector3 p = ballRb.transform.position;
        p.z += landingVfxZOffset;
        return p;
    }

    void ResetLandingVfxToPrefabDefaults(ParticleSystem ps)
    {
        if (!ps) return;
        var main = ps.main;
        main.startColor = landedPrefabStartColor;
    }

    void SetLandingVfxColor(ParticleSystem ps, Color c)
    {
        if (!ps) return;
        var main = ps.main;
        var sc = main.startColor;
        sc.mode = ParticleSystemGradientMode.Color;
        sc.color = c;
        main.startColor = sc;
    }

    // IMPORTANT: always start a NEW landing emitter for a run end
    void StartNewLandingVfx(bool followBall)
    {
        if (!ballRb) return;

        activeLandingPs = AcquireFreshLandingEmitter();
        if (!activeLandingPs) return;

        activeLandingPs.transform.SetParent(vfxPoolRoot ? vfxPoolRoot : transform, true);
        activeLandingPs.transform.rotation = landedPrefabRotation;
        activeLandingPs.transform.localScale = landedPrefabScale;

        ResetLandingVfxToPrefabDefaults(activeLandingPs);

        activeLandingFollowBall = followBall;
        activeLandingPs.transform.position = BallPosWithVfxZ();

        activeLandingPs.Play(true);
    }

    // Let the previous landing VFX finish naturally, but free the "active slot"
    void RetireActiveLandingVfxOnThrow()
    {
        if (!activeLandingPs) return;

        activeLandingFollowBall = false;

        if (vfxPoolRoot)
            activeLandingPs.transform.SetParent(vfxPoolRoot, true);

        // Let particles finish; don't hard clear here
        StopEmittingOnly(activeLandingPs);

        // Free the slot so the next landing can acquire a different emitter
        activeLandingPs = null;
    }

    void PauseLandingVfxIfAny()
    {
        if (!activeLandingPs) return;
        activeLandingFollowBall = false;
        StopEmittingOnly(activeLandingPs);
        activeLandingPs = null;
    }

    // ---------------- Collisions / Totals ----------------

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!streakActive) return;
        if (grab && grab.IsDragging) return;

        if (!c.collider.CompareTag("Wall") && c.collider.gameObject.layer != LayerMask.NameToLayer("Walls"))
            return;

        totalBounces++;
        totalsDirty = true;

        UpdateTotalsUI_LiveBanked();
        SaveTotalsIfDirty(false);
    }

    // ---------------- Sticky Ball ----------------

    // Called by WallBounceReporter on *any* wall contact.
    public void OnWallContact(int wallId, Vector2 contactWorld)
    {
        if (!streakActive) return;
        if (grab && grab.IsDragging) return;
        if (!powerups || !powerups.HasArmed) return;

        var def = powerups.GetArmedDef();
        if (!def) return;

        // Only Sticky Ball cares about wall contact right now.
        if (def.id != powerups.GetStickyBallId()) return;
        if (def.trigger != PowerupTrigger.NextWallContact) return;

        // Consume ONLY when it actually triggers.
        if (!powerups.TrySpend(def.id, 1)) return;

        ApplyStickyBall(wallId, contactWorld);
    }

    void ApplyStickyBall(int wallId, Vector2 contactWorld)
    {
        if (!ballRb) return;

        // Prevent double-trigger if we get multiple collision callbacks this frame.
        if (stickyPinned) return;

        WorldBounds b = ComputeBounds();
        float r = b.r;

        // Start from current position, clamp to reachable, then hard-snap one axis to the wall.
        Vector2 p = ballRb.position;

        // Clamp within reachable area first
        p.x = Mathf.Clamp(p.x, b.left + r, b.right - r);
        p.y = Mathf.Clamp(p.y, b.bottom + r, b.top - r);

        // Hard snap to the wall based on wallId
        switch (wallId)
        {
            case 0: // Left
                p.x = b.left + r;
                break;
            case 1: // Right
                p.x = b.right - r;
                break;
            case 2: // Top
                p.y = b.top - r;
                break;
            case 3: // Bottom
                p.y = b.bottom + r;
                break;
        }

        // Put it there FIRST (before freezing), then kill motion, then freeze.
        ballRb.position = p;
        ballRb.linearVelocity = Vector2.zero;
        ballRb.angularVelocity = 0f;

        // Save state and pin.
        stickyPinned = true;
        stickyPinnedFrame = Time.frameCount;

        stickyPrevConstraints = ballRb.constraints;
        stickyPrevInterpolation = ballRb.interpolation;

        ballRb.interpolation = RigidbodyInterpolation2D.None;
        ballRb.constraints = RigidbodyConstraints2D.FreezeAll;

        // Force landing visuals to be allowed for this "segment" so label/lines can show.
        landingAllowedThisSegment = true;
        suppressResultVisualsUntilThrow = false;

        // Make the run end almost instantly (still goes through the normal stop end path).
        stopTimer = Mathf.Max(stopTimer, stopHoldTime * 0.98f);

        if (popups)
        {
            // Popups at pinned position feels better than contact point.
            popups.PopAtWorldWithExtraOffset(p, "Sticky Ball!", new Color(0.75f, 0.9f, 1f, 1f), new Vector2(0f, 0f));
        }
    }

    void ClearStickyIfAny()
    {
        if (!stickyPinned) return;

        stickyPinned = false;

        if (ballRb)
        {
            ballRb.constraints = stickyPrevConstraints;
            ballRb.interpolation = stickyPrevInterpolation;

            // Make sure no residual motion leaks into the next grab/throw
            ballRb.linearVelocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
        }

        // Reset sticky-throw tracking so the next throw behaves normally
        if (powerups) powerups.SetStickyThrowActive(false);
        stickyThrowDistance = 0f;
    }

    // ---------------- Hot Spot ----------------

    bool HotSpotActive()
    {
        return (powerups && powerups.HotSpotSpawnedThisRun) || hotSpotEncoreRestored;
    }

    void HotSpot_SetVisualsActive(bool on)
    {
        if (hotSpotMarker) hotSpotMarker.gameObject.SetActive(on);
        if (!on && hotSpotText) hotSpotText.text = "";
    }

    void HotSpot_UpdateVisuals()
    {
        HotSpot_UpdateVisuals(streakActive);
    }

    void HotSpot_UpdateVisuals(bool useLiveColor)
    {
        if (hotSpotMarker)
        {
            hotSpotMarker.position = hotSpotCenter;

            float d = hotSpotRadius * 2f;
            hotSpotMarker.localScale = new Vector3(d, d, 1f);

            var sr = hotSpotMarker.GetComponent<SpriteRenderer>();
            if (sr) sr.color = useLiveColor ? hotSpotLiveColor : hotSpotDimmedColor;
        }

        if (hotSpotText)
            hotSpotText.text = $"+{hotSpotBonusDistanceThisRun}d";
    }

    void ClearHotSpotAll()
    {
        // hotSpot state flags are now managed by PowerupManager

        hotSpotInside = false;
        hotSpotCenter = Vector2.zero;
        hotSpotRadius = 0f;
        hotSpotBonusDistanceThisRun = 0;
        hotSpotTotalPointsThisRun = 0;
        hotSpotLastHitTime = -9999f;
        hotSpotEncoreRestored = false;

        savedHotSpotActive = false;
        savedHotSpotCenter = Vector2.zero;
        savedHotSpotRadius = 0f;
        savedHotSpotBonusDistanceThisRun = 0;
        savedHotSpotTotalPointsThisRun = 0;
        savedHotSpotLastHitTime = -9999f;

        HotSpot_SetVisualsActive(false);
    }

    void SaveHotSpotSnapshotForEncore()
    {
        if (!HotSpotActive())
        {
            savedHotSpotActive = false;
            return;
        }

        savedHotSpotActive = true;
        savedHotSpotCenter = hotSpotCenter;
        savedHotSpotRadius = hotSpotRadius;
        savedHotSpotBonusDistanceThisRun = hotSpotBonusDistanceThisRun;
        savedHotSpotTotalPointsThisRun = hotSpotTotalPointsThisRun;
        savedHotSpotLastHitTime = hotSpotLastHitTime;
    }

    void RestoreHotSpotSnapshotIfEncore()
    {
        if (!savedHotSpotActive) return;

        hotSpotCenter = savedHotSpotCenter;
        hotSpotRadius = savedHotSpotRadius;
        hotSpotBonusDistanceThisRun = savedHotSpotBonusDistanceThisRun;
        hotSpotTotalPointsThisRun = savedHotSpotTotalPointsThisRun;
        hotSpotLastHitTime = savedHotSpotLastHitTime;
        hotSpotEncoreRestored = true;

        savedHotSpotActive = false;

        HotSpot_SetVisualsActive(true);
        HotSpot_UpdateVisuals(true);  // Explicitly brighten on Encore restore
    }

    // Overlap test: ball circle vs hot spot circle
    bool IsBallOverlappingHotSpot(Vector2 ballCenter)
    {
        if (!HotSpotActive()) return false;

        WorldBounds b = ComputeBounds();
        float ballR = b.r;

        float rSum = hotSpotRadius + ballR;
        return (ballCenter - hotSpotCenter).sqrMagnitude <= (rSum * rSum);
    }

    // Swept test: use Physics2D to detect if ball passed through hot spot region
    bool DidSweepHitHotSpot(Vector2 a, Vector2 b)
    {
        if (!HotSpotActive()) return false;
        if (!ballRb) return false;

        WorldBounds w = ComputeBounds();
        float ballR = w.r;
        float castRadius = hotSpotRadius + ballR;
        Vector2 direction = b - a;
        float distance = direction.magnitude;

        // If barely moving, no sweep hit
        if (distance < 0.0001f) return false;

        // Use CircleCast to detect if the ball path intersects the hot spot region
        RaycastHit2D hit = Physics2D.CircleCast(
            a,
            ballR,
            direction.normalized,
            distance,
            LayerMask.GetMask("Default")
        );

        // If no physics hit, fall back to manual closest-point test
        if (!hit.collider)
        {
            Vector2 center = hotSpotCenter;
            Vector2 ac = center - a;
            float abLenSq = direction.sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(ac, direction) / abLenSq);
            Vector2 closest = a + direction * t;
            return (closest - center).sqrMagnitude <= (castRadius * castRadius);
        }

        return true;
    }

    // Call every frame to detect outside->inside entry hits
    void UpdateHotSpotHitState(bool isHeld)
    {
        if (!streakActive) return;
        if (!HotSpotActive()) return;
        if (!ballRb) return;

        // Don’t count while held, but keep state synced
        if (isHeld)
        {
            hotSpotInside = IsBallOverlappingHotSpot(ballRb.position);
            hotSpotLastPos = ballRb.position;
            return;
        }

        Vector2 posNow = ballRb.position;
        bool insideNow = IsBallOverlappingHotSpot(posNow);

        // Catch fast passes that skip insideNow due to high speed
        bool sweptHit = !hotSpotInside && DidSweepHitHotSpot(hotSpotLastPos, posNow);

        // Count a hit only on outside->inside transition (and not too soon after last hit)
        if ((insideNow || sweptHit) && !hotSpotInside)
        {
            float timeSinceLastHit = Time.time - hotSpotLastHitTime;
            if (timeSinceLastHit < hotSpotHitCooldown)
            {
                hotSpotInside = insideNow || sweptHit;
                hotSpotLastPos = posNow;
                return;  // Cooldown active, ignore this hit
            }

            // Record hit time and award points (no speed limit)
            hotSpotLastHitTime = Time.time;
            int bonus = powerups ? powerups.GetHotSpotDistancePerHit() : 50;

            // Cap at 1000 total points
            int remainingCapacity = 1000 - hotSpotTotalPointsThisRun;
            if (remainingCapacity <= 0)
            {
                // Hot spot is exhausted, disable it
                if (powerups) powerups.DisableHotSpot();
                HotSpot_SetVisualsActive(false);
                hotSpotEncoreRestored = false;
                savedHotSpotActive = false;
                hotSpotInside = insideNow;
                return;
            }

            // Award only up to the cap
            bonus = Mathf.Min(bonus, remainingCapacity);
            // Hot Spot adds as a separate flat bonus (not to base, to avoid interfering with overtime bonus calc)
            hotSpotBonusDistance += bonus;
            hotSpotBonusDistanceThisRun += bonus;
            hotSpotTotalPointsThisRun += bonus;
            BankDistance(bonus);
            UpdateTotalsUI_LiveBanked();
            SaveTotalsIfDirty(false);

            // Linear shrink: 20 increments from start size to 0
            hotSpotRadius = Mathf.Max(0f, hotSpotRadius - (hotSpotRadiusStart / 20f));

            // Disappear when radius is gone
            if (hotSpotRadius <= 0f)
            {
                HotSpot_SetVisualsActive(false);
                if (powerups) powerups.DisableHotSpot();
                hotSpotEncoreRestored = false;
                savedHotSpotActive = false;
            }
            else
            {
                HotSpot_UpdateVisuals(streakActive);
            }
        }

        // Update hotSpotInside: set to true if inside now, false if exited entirely
        hotSpotInside = insideNow;
        hotSpotLastPos = posNow;
    }

    // ---------------- Events ----------------

    void OnAnyPickupStarted()
    {
        // Stop emitting if something was still following, but do NOT clear instantly.
        // This also prevents �resume� behavior.
        PauseLandingVfxIfAny();
        ClearStickyIfAny();
        disableLaneCapThisSegment = false;
        if (powerups) powerups.SetStickyThrowActive(false);

        if (!streakActive && resultLatched && !lastRunNoLanding)
            suppressResultVisualsUntilThrow = true;

        showLandingInfo = false;
        shownLandingMult = 1f;
        HideAll();
    }

    void OnThrown()
    {
        ClearBestHighlight();

        // Let previous landing VFX finish; free slot for next landing
        RetireActiveLandingVfxOnThrow();

        suppressResultVisualsUntilThrow = false;

        if (!streakActive) StartStreak();
        if (actions) actions.OnThrowStarted();

        lastRunPos = ballRb.position;
        stopTimer = 0f;

        laneBroken = false;
        DetermineLaneAxisAndCenter();

        // If Sticky Ball is armed when the throw starts, do NOT apply lane cap for this whole flight.
        // This resets on the next pickup.
        bool isStickBallArmed = powerups && powerups.HasArmed &&
            powerups.ArmedId == powerups.GetStickyBallId();
        disableLaneCapThisSegment = isStickBallArmed;

        // Sticky anti-exploit: track how far the ball travels on a Sticky-armed throw.
        if (powerups) powerups.SetStickyThrowActive(isStickBallArmed);
        stickyThrowDistance = 0f;
        stickyThrowLastPos = ballRb.position;

        showLandingInfo = false;
        shownLandingMult = 1f;

        landingAllowedThisSegment = true;

        if (HotSpotActive() && ballRb)
            hotSpotInside = IsBallOverlappingHotSpot(ballRb.position);
    }

    void StartStreak()
    {
        // Starting a brand new run closes the previous run's adjust window.
        if (pendingRunAdjustActive)
        {
            if (xp) xp.EndPendingRunXp();
            pendingRunAdjustActive = false;
            pendingBestBaseline = 0f;
        }

        // OnRunStarted is now called earlier in throwEvent before powerups are processed
        // Don't call it again here or it will reset powerup state

        streakActive = true;
        stopTimer = 0f;

        // IMPORTANT: Only reset cumulative distances and overtime bonus when starting a TRUE NEW RUN
        // (when resultLatched is true, meaning the previous run ended).
        // If we're throwing again within the same run, DO NOT reset these.
        if (resultLatched)
        {
            // Truly new run - reset everything
            travelDistance = 0f;
            travelDistanceWithoutOvertime = 0f;
            hotSpotBonusDistance = 0f;
            overtimeBonusDisplayed = 0;
            // Note: firstPowerupUsed is reset in throwEvent just before powerup detection runs
        }
        // else: continuing within same run - keep cumulative distances and overtime bonus

        lastRunPos = ballRb.position;

        catchesThisRun = 0;

        throwsUsedThisRun = 0;
        throwsExhausted = false;
        UpdateThrowsUi();

        bonusThrowsThisRun = 0;
        encoreUsedThisRun = false;
        encoreReviveUsedThisRun = false;

        // ActionDetector OnRunStarted was already called earlier in throwEvent
        // No need to call it again here

        resultLatched = false;

        latchedSnapshot = default;

        showLandingInfo = false;
        shownLandingMult = 1f;

        lastRunNoLanding = false;

        landingAllowedThisSegment = false;
        suppressResultVisualsUntilThrow = false;

        laneBroken = false;

        savedOvertimeActive = false;
        savedOvertimeUsed = false;
        savedOvertimeElapsed = 0f;

        HideAllScoreElements();
        if (overtimeComparisonText) overtimeComparisonText.text = "";
        HideAll();
    }

    void OnPickupStarted()
    {
        bool wasCatch = grab && grab.LastPickupWasCatch;
        if (actions) actions.OnPickup(wasCatch);
        if (powerups) powerups.OnPickupHappened();

        if (wasCatch)
        {
            catchesThisRun++;
            stopTimer = 0f;
            landingAllowedThisSegment = false;
            return;
        }

        stopTimer = 0f;
        landingAllowedThisSegment = false;
    }

    void ConsumeThrow(bool fromMiss)
    {
        if (EffectiveThrowsPerRun <= 0) return;

        if (fromMiss)
        {
            if (!consumeThrowOnMiss) return;
        }
        else
        {
            if (!consumeThrowOnRelease) return;
        }

        if (!streakActive) return;
        if (throwsExhausted) return;

        throwsUsedThisRun++;
        if (throwsUsedThisRun >= EffectiveThrowsPerRun)
            throwsExhausted = true;

        UpdateThrowsUi();
    }

    void UpdateThrowsUi()
    {
        if (!throwsLeftText) return;

        if (EffectiveThrowsPerRun <= 0)
        {
            throwsLeftText.text = "";
            return;
        }

        throwsLeftText.SetText("{0}/{1}", ThrowsLeft, EffectiveThrowsPerRun);
    }

    void OnInventoryChanged_RefreshThrows()
    {
        // If max throws changed mid-run, update the exhausted flag so pickup rules match immediately.
        int max = EffectiveThrowsPerRun;

        if (max <= 0)
        {
            throwsExhausted = false;
            UpdateThrowsUi();
            return;
        }

        // If the run is active, exhaustion depends on the new max.
        throwsExhausted = streakActive && (throwsUsedThisRun >= max);

        UpdateThrowsUi();
    }

    void TickStreakFlight()
    {
        Vector2 p = ballRb.position;
        Vector2 delta = p - lastRunPos;

        float step = delta.magnitude * Mathf.Max(0.0001f, distanceUnitScale);
        float baseStep = step;  // Store base step before multiplier

        // Apply Overtime multiplier if active (before Hot Spot bonus so they don't stack)
        if (powerups && powerups.OvertimeActiveThisRun)
        {
            step *= powerups.GetOvertimeMultiplier();
        }

        travelDistance += step;
        travelDistanceWithoutOvertime += baseStep;
        lastRunPos = p;

        // Update overtime bonus display (ratcheting): difference between multiplied and base distances
        if (powerups && powerups.OvertimeActiveThisRun)
        {
            int currentBonus = RoundInt(travelDistance) - RoundInt(travelDistanceWithoutOvertime);
            overtimeBonusDisplayed = Mathf.Max(overtimeBonusDisplayed, currentBonus);
        }

        // Update overtime comparison UI
        UpdateOvertimeComparisonUI();

        if (powerups && powerups.StickyThrowActive)
        {
            stickyThrowDistance += delta.magnitude;
            stickyThrowLastPos = p;
        }

        BankDistance(step);
        UpdateTotalsUI_LiveBanked();
        SaveTotalsIfDirty(false);

        UpdateLaneBrokenState(p);

        float speed = ballRb.linearVelocity.magnitude;
        stopTimer = (speed <= stopSpeed) ? (stopTimer + Time.deltaTime) : 0f;

        if (stopTimer >= stopHoldTime)
        {
            // If Sticky Ball pinned this exact frame, wait 1 frame so the landing visuals
            // (label + wall lines) get a chance to turn on before the run ends.
            if (stickyPinned && Time.frameCount == stickyPinnedFrame)
                return;

            EndStreak_Stop();
        }
    }

    void ApplyPendingRunRewardsNow(int runXpInt)
    {
        runXpInt = Mathf.Max(0, runXpInt);

        // XP: apply now and allow later delta updates (can be negative)
        if (xp) xp.BeginOrUpdatePendingRunXp(runXpInt);

        // Best: allow rollback but never below the best that existed before this run.
        if (!pendingRunAdjustActive)
        {
            pendingRunAdjustActive = true;
            pendingBestBaseline = BestScore;
        }

        float candidate = Mathf.Max(pendingBestBaseline, runXpInt);

        bool changed = !Mathf.Approximately(candidate, BestScore);
        BestScore = candidate;

        if (changed)
        {
            PlayerPrefs.SetFloat("BestScore", BestScore);
            PlayerPrefs.Save();
        }

        ApplyBestUI((int)BestScore, BestScore > pendingBestBaseline);
    }

    void EndStreak_DropInstant()
    {
        if (!streakActive) return;
        ClearStickyIfAny();

        // Dim hot spot BEFORE clearing powerup state and keep it visible
        if (HotSpotActive())
        {
            HotSpot_SetVisualsActive(true);  // Ensure marker stays active
            HotSpot_UpdateVisuals(false);    // Dim it
        }

        streakActive = false;
        if (powerups)
        {
            (savedOvertimeActive, savedOvertimeUsed, savedOvertimeElapsed) = powerups.GetOvertimeSnapshot();
            SaveHotSpotSnapshotForEncore();
            powerups.OnRunEnded();
        }
        lastRunNoLanding = true;

        BankRemainingDistance();

        int distInt = RoundInt(travelDistance);
        int distBase = RoundInt(travelDistanceWithoutOvertime);
        int distHotSpot = RoundInt(hotSpotBonusDistance);
        int overtimeFinalBonus = Mathf.Max(0, distInt - distBase);
        overtimeBonusDisplayed = Mathf.Max(overtimeBonusDisplayed, overtimeFinalBonus);

        float landingShown = 1f;
        float xpMultShown = Round2(GetXpMultRaw());

        latchedSnapshot = BuildSnapshot(distInt, distBase, distHotSpot, overtimeBonusDisplayed, landingShown, xpMultShown);
        resultLatched = true;

        UpdateScoreText();
        savedFirstPowerupUsed = firstPowerupUsed;  // Preserve for possible Encore revive
        firstPowerupUsed = 0;  // Reset powerup order after score is displayed, before next run
        ApplyPendingRunRewardsNow(latchedSnapshot.xpTotal);

        SaveTotalsIfDirty(true);
        UpdateTotalsUI();

        HideAll();
        showLandingInfo = false;
        shownLandingMult = 1f;

        if (overtimeComparisonText) overtimeComparisonText.text = "";
        if (actions) actions.OnRunEnded();
        UpdateThrowsUi();
    }

    void EndStreak_Stop()
    {
        if (!streakActive) return;
        ClearStickyIfAny();

        // Consume pending insurance if the ball stopped (insurance protected the landing)
        if (powerups)
        {
            powerups.ConsumePendingInsuranceIfAny();
        }

        // Dim hot spot BEFORE clearing powerup state and keep it visible
        if (HotSpotActive())
        {
            HotSpot_SetVisualsActive(true);  // Ensure marker stays active
            HotSpot_UpdateVisuals(false);    // Dim it
        }

        streakActive = false;
        if (powerups)
        {
            (savedOvertimeActive, savedOvertimeUsed, savedOvertimeElapsed) = powerups.GetOvertimeSnapshot();
            SaveHotSpotSnapshotForEncore();
            powerups.OnRunEnded();
        }
        BankRemainingDistance();

        bool landingShownToPlayer = landingAllowedThisSegment && showLandingInfo;

        if (!landingShownToPlayer)
        {
            lastRunNoLanding = true;
        }
        else
        {
            lastRunNoLanding = false;
        }

        int distInt = RoundInt(travelDistance);
        int distBase = RoundInt(travelDistanceWithoutOvertime);
        int distHotSpot = RoundInt(hotSpotBonusDistance);
        int overtimeFinalBonus = Mathf.Max(0, distInt - distBase);
        overtimeBonusDisplayed = Mathf.Max(overtimeBonusDisplayed, overtimeFinalBonus);
        float landingShown = landingShownToPlayer ? Round2(shownLandingMult) : 1f;
        float xpMultShown = Round2(GetXpMultRaw());

        latchedSnapshot = BuildSnapshot(distInt, distBase, distHotSpot, overtimeBonusDisplayed, landingShown, xpMultShown);
        resultLatched = true;

        UpdateScoreText();
        savedFirstPowerupUsed = firstPowerupUsed;  // Preserve for possible Encore revive
        firstPowerupUsed = 0;  // Reset powerup order after score is displayed, before next run
        ApplyPendingRunRewardsNow(latchedSnapshot.xpTotal);
        if (landingShownToPlayer)
        {
            StartNewLandingVfx(followBall: true);

            // Best highlight is determined by current BestScore vs baseline
            if (pendingRunAdjustActive && BestScore > pendingBestBaseline)
                SetLandingVfxColor(activeLandingPs, bestBeatenColor);
        }
        else
        {
            // No landing visuals this time
            PauseLandingVfxIfAny();
            HideAll();
            showLandingInfo = false;
            shownLandingMult = 1f;
        }

        if (actions) actions.OnRunEnded();

        if (overtimeComparisonText) overtimeComparisonText.text = "";
    }

    void AwardXp(int xpToAdd)
    {
        if (!xp) return;
        if (xpToAdd <= 0) return;
        xp.AddXp(xpToAdd);
    }

    // ---------------- Best UI ----------------

    void ApplyBestUI(int bestXp, bool isNewBest)
    {
        if (!bestText) return;
        bestText.gameObject.SetActive(true);
        bestText.SetText("BEST\n{0}", bestXp);
        bestText.color = isNewBest ? bestBeatenColor : bestDefaultColor;
    }

    void ClearBestHighlight()
    {
        if (!bestText) return;
        bestText.color = bestDefaultColor;
    }

    // ---------------- Lane + multiplier visuals ----------------

    void DetermineLaneAxisAndCenter()
    {
        Vector2 startPos = ballRb.position;

        if (laneAxisMode == LaneAxisMode.ForceAxis)
            laneAxis = (forcedLaneAxis == ForcedLaneAxis.Vertical) ? LaneAxis.AlongY : LaneAxis.AlongX;
        else
        {
            Vector2 v = ballRb.linearVelocity;
            laneAxis = (Mathf.Abs(v.y) >= Mathf.Abs(v.x)) ? LaneAxis.AlongY : LaneAxis.AlongX;
        }

        laneCenterX = startPos.x;
        laneCenterY = startPos.y;
    }

    void UpdateLaneBrokenState(Vector2 pos)
    {
        if (laneBroken) return;

        float perpDeviation = (laneAxis == LaneAxis.AlongY)
            ? Mathf.Abs(pos.x - laneCenterX)
            : Mathf.Abs(pos.y - laneCenterY);

        if (perpDeviation > laneHalfWidth) laneBroken = true;
    }

    bool ShouldCapRightNow() => false;  // Lane cap disabled

    float ComputeLandingMultiplier(bool capped)
    {
        WorldBounds b = ComputeBounds();
        Vector2 pos = ballRb.position;

        GetCenterNormalized(pos, b, out float nx, out float ny);

        float edgeValue = capped ? cappedEdgeValue : normalEdgeValue;
        float cornerTotal = capped ? cappedCornerTotal : normalCornerTotal;

        // Apply Landing Amplifier (this throw only)
        float e = Mathf.Max(0.0001f, closenessExponent);
        float maxM = maxMultiplier;

        if (powerups && powerups.LandingAmpActiveThisThrow)
        {
            edgeValue *= powerups.GetLandingAmpEdgeMult();
            cornerTotal *= powerups.GetLandingAmpCornerMult();

            e = Mathf.Max(0.0001f, powerups.GetLandingAmpExponent());
            maxM = Mathf.Max(maxM, powerups.GetLandingAmpMaxMultiplier());
        }

        float cornerBoost = cornerTotal - edgeValue;

        float ax = Mathf.Pow(Mathf.Clamp01(nx), e);
        float ay = Mathf.Pow(Mathf.Clamp01(ny), e);

        float edge = Mathf.Max(ax, ay);
        float corner = ax * ay;

        float m = (edgeValue * edge) + (cornerBoost * corner);
        if (m < 0.01f) m = 0f;

        m = Mathf.Clamp(m, 0f, maxM);

        // Insurance: clamp anything under 2x up to 2x. Anything 2x or higher stays normal.
        // E.g. 1.9x → 2.0x, 2.1x → 2.1x
        if (powerups && powerups.InsuranceActiveThisThrow && m < 2f)
            m = 2f;

        // Sticky anti-exploit: if this throw was Sticky-armed, scale DOWN the bonus
        // based on how far the ball actually traveled this throw.
        if (powerups && powerups.StickyThrowActive && stickyFullDistanceForNormalLanding > 0.0001f && m > 1f)
        {
            float factor = Mathf.Clamp01(stickyThrowDistance / stickyFullDistanceForNormalLanding);
            m = 1f + (m - 1f) * factor;
        }

        return m;
    }

    // Award Edge Case: add weighted distance directly as XP (bypasses run scoring).
    // closeness01: 0..1 where 1 is essentially on the wall; weighted with exponent and baseline.
    // Returns the rounded value for popup/feedback.
    public int AwardEdgeCaseDistanceLikeNormal(float throwDistance, Vector2 landingWorldPos, float closeness01)
    {
        // For Edge Case, use only edge value (top wall), not corner bonuses
        WorldBounds b = ComputeBounds();
        GetCenterNormalized(landingWorldPos, b, out float nx, out float ny);
        bool capped = streakActive && ShouldCapRightNow();
        float edgeValue = capped ? cappedEdgeValue : normalEdgeValue;
        float scaled = throwDistance * Mathf.Max(0.0001f, distanceUnitScale) * Mathf.Max(0.1f, edgeCaseDistanceMultiplier);
        // Closeness weighting: floor at baseline, curve toward 1 with exponent to heavily reward tighter shots.
        float c = Mathf.Clamp01(closeness01);
        float closenessBoost = Mathf.Pow(c, Mathf.Max(0.1f, edgeCaseClosenessExponent));
        float closenessFactor = Mathf.Max(edgeCaseClosenessBaseline, closenessBoost);
        // Use only the top wall multiplier, not corner bonuses
        float add = Mathf.Max(0f, scaled * closenessFactor * edgeValue);

        int xpToAdd = RoundInt(add);
        if (xp) xp.AddXp(xpToAdd);

        return xpToAdd;
    }

    void UpdatePlacementVisualsAndMultiplier(bool isHeld, bool uiTick)
    {
        if (isHeld || suppressResultVisualsUntilThrow)
        {
            showLandingInfo = false;
            shownLandingMult = 1f;
            HideAll();
            return;
        }

        WorldBounds b = ComputeBounds();
        Vector2 pos = ballRb.position;

        GetCenterNormalized(pos, b, out float nx, out float ny);

        float horizAlpha = FadeWithPop(nx);
        float vertAlpha = FadeWithPop(ny);

        float speed = ballRb.linearVelocity.magnitude;

        bool allowVisuals =
            (streakActive || resultLatched) &&
            (speed <= showBelowSpeed) &&
            landingAllowedThisSegment;

        if (resultLatched && lastRunNoLanding)
            allowVisuals = false;

        showLandingInfo = allowVisuals;

        if (showLandingInfo)
        {
            if (resultLatched) shownLandingMult = latchedSnapshot.landingShown;
            else shownLandingMult = ComputeLandingMultiplier(streakActive && ShouldCapRightNow());
        }
        else shownLandingMult = 1f;

        if (!showLandingInfo)
        {
            HideAll();
            return;
        }

        UpdateRulersAndBallLabel(true, pos, b, horizAlpha, vertAlpha);
    }

    // ---------------- Land Mult Prediction ----------------

    // Fold a point into [min,max] with mirror reflections (unfolded space -> real space).
    static float FoldMirrored(float x, float min, float max)
    {
        float w = max - min;
        if (w <= 0f) return min;

        float t = (x - min) / w;
        t = Mathf.Repeat(t, 2f);         // [0,2)
        if (t > 1f) t = 2f - t;          // mirror [1,2) back to [0,1]
        return min + t * w;
    }

    // Predict where the ball would come to rest if it was NOT picked up.
    // Assumes axis-aligned bounds and linear damping.
    public Vector2 PredictStopPosition(Vector2 posNow, Vector2 velNow)
    {
        // You must use the same stop threshold you already use to end a run.
        float stopSpeed = this.stopSpeed;        // <-- this field exists in your scoring script already
        float d = ballRb ? ballRb.linearDamping : 0f;

        float v0 = velNow.magnitude;
        if (v0 <= stopSpeed || v0 <= 0.0001f)
            return posNow;

        if (d <= 0.0001f)
        {
            // If there is no damping, your game can’t stop deterministically.
            // Fall back to "no prediction" (treat as not good) rather than lying.
            return posNow;
        }

        // With linear damping v(t)=v0*e^(-d t). Stop when v<=stopSpeed.
        // Total travel distance along the unfolded straight line:
        // s = (v0 - stopSpeed) / d
        float s = (v0 - stopSpeed) / d;

        Vector2 dir = velNow / v0;
        Vector2 unfoldedEnd = posNow + dir * s;

        // Use GameViewport for consistent bounds calculation
        GameViewport.GetWorldBounds(out var min, out var max);

        float r = ballCollider ? (ballCollider.radius * Mathf.Max(ballRb.transform.lossyScale.x, ballRb.transform.lossyScale.y)) : 0f;
        float minX = min.x + boundsInset + r;
        float maxX = max.x - boundsInset - r;
        float minY = min.y + boundsInset + r;
        float maxY = max.y - boundsInset - r;

        return new Vector2(
            FoldMirrored(unfoldedEnd.x, minX, maxX),
            FoldMirrored(unfoldedEnd.y, minY, maxY)
        );
    }

    public float PredictTimeToStopFromSpeed(float speedNow)
    {
        float stopSpeed = showBelowSpeed;
        float d = ballRb ? ballRb.linearDamping : 0f;

        if (speedNow <= stopSpeed) return 0f;
        if (d <= 0.0001f) return Mathf.Infinity;

        return Mathf.Log(speedNow / stopSpeed) / d;
    }

    public float GetLandingMultiplierAt(Vector2 worldPos)
    {
        // Default behavior (gameplay): landing amp only if it was actually triggered this throw.
        bool applyAmp = powerups && powerups.LandingAmpActiveThisThrow;
        return GetLandingMultiplierAt(worldPos, applyAmp);
    }

    // Check if ball position qualifies for Edge Case (close to top wall)
    public bool IsCloseToTopWall(Vector2 worldPos, float proximityThreshold, out float distancePct)
    {
        WorldBounds b = ComputeBounds();
        GetCenterNormalized(worldPos, b, out float nx, out float ny);
        distancePct = ny;  // ny is 0 at center, 1 at top/bottom edge
        return ny >= (1f - proximityThreshold);
    }

    // Heatmap / preview overload: lets UI request landing-amp curve without changing gameplay state.
    public float GetLandingMultiplierAt(Vector2 worldPos, bool applyLandingAmp)
    {
        WorldBounds b = ComputeBounds();
        GetCenterNormalized(worldPos, b, out float nx, out float ny);

        bool capped = streakActive && ShouldCapRightNow();

        float edgeValue = capped ? cappedEdgeValue : normalEdgeValue;
        float cornerTotal = capped ? cappedCornerTotal : normalCornerTotal;

        // Apply Landing Amplifier curve if requested
        float e = Mathf.Max(0.0001f, closenessExponent);
        float maxM = maxMultiplier;

        if (applyLandingAmp && powerups)
        {
            edgeValue *= powerups.GetLandingAmpEdgeMult();
            cornerTotal *= powerups.GetLandingAmpCornerMult();

            e = Mathf.Max(0.0001f, powerups.GetLandingAmpExponent());
            maxM = Mathf.Max(maxM, powerups.GetLandingAmpMaxMultiplier());
        }

        float cornerBoost = cornerTotal - edgeValue;

        float ax = Mathf.Pow(Mathf.Clamp01(nx), e);
        float ay = Mathf.Pow(Mathf.Clamp01(ny), e);

        float edge = Mathf.Max(ax, ay);
        float corner = ax * ay;

        float m = (edgeValue * edge) + (cornerBoost * corner);
        if (m < 0.01f) m = 0f;

        return Mathf.Clamp(m, 0f, maxM);
    }

    // ---------------- UI ----------------

    struct RunSnapshot
    {
        public int distInt;
        public int distBase;
        public int distHotSpot;
        public int overtimeBonus;
        public int distForScore;
        public float landingShown;
        public float xpMultShown;
        public int xpPct;
        public int xpTotal;   // this is THE score (score == xp)
        public int powerupOrder;  // 0 = none, 1 = overtime first, 2 = hot spot first
    }

    RunSnapshot latchedSnapshot;

    RunSnapshot BuildSnapshot(int distInt, int distBase, int distHotSpot, int overtimeBonus, float landingShown, float xpMultShown)
    {
        RunSnapshot s = default;

        s.distInt = Mathf.Max(0, distInt);
        s.distBase = Mathf.Max(0, distBase);
        s.distHotSpot = Mathf.Max(0, distHotSpot);
        s.overtimeBonus = Mathf.Max(0, overtimeBonus);
        s.landingShown = landingShown;
        s.powerupOrder = firstPowerupUsed;  // Save the powerup order for this run

        s.xpMultShown = xpMultShown;
        s.xpPct = Mathf.RoundToInt((xpMultShown - 1f) * 100f);

        // Score distance uses base distance + Hot Spot + displayed overtime bonus
        s.distForScore = Mathf.Max(0, s.distBase + s.distHotSpot + s.overtimeBonus);

        float raw = (s.distForScore * s.landingShown * s.xpMultShown);
        s.xpTotal = Mathf.RoundToInt(raw);

        return s;
    }

    RunSnapshot BuildLiveSnapshot()
    {
        int distInt = RoundInt(travelDistance);
        int distBase = RoundInt(travelDistanceWithoutOvertime);
        int distHotSpot = RoundInt(hotSpotBonusDistance);
        int overtimeBonus = Mathf.Max(0, overtimeBonusDisplayed);
        float landingShown = showLandingInfo ? Round2(shownLandingMult) : 1f;

        float xpMultShown = Round2(GetXpMultRaw());

        return BuildSnapshot(distInt, distBase, distHotSpot, overtimeBonus, landingShown, xpMultShown);
    }

    public void AddActionWhiffXp(int amount)
    {
        if (amount <= 0) return;
        if (!streakActive) return;

        // Add bonus distance to both totals without Overtime multiplication
        travelDistance += amount;
        travelDistanceWithoutOvertime += amount;

        BankDistance(amount);
        UpdateTotalsUI_LiveBanked();
        SaveTotalsIfDirty(false);
    }

    void UpdateScoreText()
    {
        bool ended = (!streakActive && resultLatched);
        Color liveColor = scoreTextLiveColor;
        Color endedColor = scoreTextEndedColor;

        if (!streakActive && !resultLatched)
        {
            HideAllScoreElements();
            return;
        }

        RunSnapshot s = streakActive ? BuildLiveSnapshot() : latchedSnapshot;
        bool showLandLine = ended ? !lastRunNoLanding : showLandingInfo;

        // Distance text (BASE distance) with optional bonuses inline in powerup order
        if (distanceText)
        {
            string distanceStr = $"{s.distBase}";

            // Display bonuses in the order powerups were first used
            if (s.powerupOrder == 1)  // Overtime first
            {
                // Show Overtime then Hot Spot
                if (s.overtimeBonus > 0)
                {
                    string bonusColor = ColorUtility.ToHtmlStringRGB(overtimeBonusColor);
                    distanceStr += $" <color=#{bonusColor}>+ {s.overtimeBonus}</color>";
                }
                if (s.distHotSpot > 0)
                {
                    string hotColor = ColorUtility.ToHtmlStringRGB(hotSpotBonusColor);
                    distanceStr += $" <color=#{hotColor}>+ {s.distHotSpot}</color>";
                }
            }
            else  // Hot Spot first or neither used yet
            {
                // Show Hot Spot then Overtime
                if (s.distHotSpot > 0)
                {
                    string hotColor = ColorUtility.ToHtmlStringRGB(hotSpotBonusColor);
                    distanceStr += $" <color=#{hotColor}>+ {s.distHotSpot}</color>";
                }
                if (s.overtimeBonus > 0)
                {
                    string bonusColor = ColorUtility.ToHtmlStringRGB(overtimeBonusColor);
                    distanceStr += $" <color=#{bonusColor}>+ {s.overtimeBonus}</color>";
                }
            }

            distanceText.text = distanceStr;
            distanceText.color = ended ? endedColor : liveColor;
            distanceText.gameObject.SetActive(true);
        }

        // Landing multiplier text
        if (landMultText)
        {
            if (showLandLine)
            {
                landMultText.text = $"land x{s.landingShown:0.00}";
                landMultText.color = ended ? endedColor : liveColor;
                landMultText.gameObject.SetActive(true);
            }
            else
            {
                landMultText.gameObject.SetActive(false);
            }
        }

        // XP multiplier text
        bool showXpMult = s.xpPct != 0;
        if (xpMultText)
        {
            if (showXpMult)
            {
                string xpLine = s.xpPct > 0 ? $"+{s.xpPct}% XP" : $"{s.xpPct}% XP";
                xpMultText.text = xpLine;
                xpMultText.color = ended ? endedColor : liveColor;
                xpMultText.gameObject.SetActive(true);
            }
            else
            {
                xpMultText.gameObject.SetActive(false);
            }
        }

        // Final score text
        bool somethingBeyondDistance = showLandLine || (s.xpPct != 0);
        if (finalScoreText)
        {
            if (somethingBeyondDistance)
            {
                finalScoreText.text = s.xpTotal.ToString();
                Color finalColor = ended ? new Color(finalScoreColor.r, finalScoreColor.g, finalScoreColor.b, scoreTextEndedColor.a) : finalScoreColor;
                finalScoreText.color = finalColor;
                finalScoreText.gameObject.SetActive(true);
            }
            else
            {
                finalScoreText.gameObject.SetActive(false);
            }
        }
    }

    void HideAllScoreElements()
    {
        if (distanceText) distanceText.gameObject.SetActive(false);
        if (landMultText) landMultText.gameObject.SetActive(false);
        if (xpMultText) xpMultText.gameObject.SetActive(false);
        if (finalScoreText) finalScoreText.gameObject.SetActive(false);
    }

    void RefreshAllUI()
    {
        if (bestText)
        {
            bestText.gameObject.SetActive(true);
            bestText.SetText("BEST\n{0}", Mathf.RoundToInt(BestScore));
            bestText.color = bestDefaultColor;
        }

        HideAllScoreElements();
        if (overtimeComparisonText) overtimeComparisonText.text = "";
        UpdateTotalsUI();
    }

    void UpdateOvertimeComparisonUI()
    {
        if (!overtimeComparisonText) return;

        if (powerups && powerups.OvertimeActiveThisRun && streakActive)
        {
            int withOvertime = RoundInt(travelDistance);
            int withoutOvertime = RoundInt(travelDistanceWithoutOvertime);
            int bonus = withOvertime - withoutOvertime;
            float mult = powerups.GetOvertimeMultiplier();

            overtimeComparisonText.text = $"WITHOUT OVERTIME: {withoutOvertime}\n(+{bonus} bonus, {mult:F2}x)";
        }
        else
        {
            overtimeComparisonText.text = "";
        }
    }

    void UpdateTotalsUI()
    {
        if (totalDistanceText) totalDistanceText.text = $"{Mathf.RoundToInt(totalDistance)}";
        if (totalBouncesText) totalBouncesText.text = $"{totalBounces}";
    }

    void UpdateTotalsUI_LiveBanked()
    {
        float liveTotalDist = totalDistance + bankDistanceRemainder;
        if (totalDistanceText) totalDistanceText.text = $"{Mathf.RoundToInt(liveTotalDist)}";
        if (totalBouncesText) totalBouncesText.text = $"{totalBounces}";
    }

    // ---------------- Totals ----------------

    void BankDistance(float step)
    {
        bankDistanceRemainder += step;

        if (bankDistanceRemainder >= bankDistanceStep)
        {
            float bank = Mathf.Floor(bankDistanceRemainder / bankDistanceStep) * bankDistanceStep;
            bankDistanceRemainder -= bank;
            totalDistance += bank;
            totalsDirty = true;
        }
    }

    void BankRemainingDistance()
    {
        if (bankDistanceRemainder <= 0f) return;
        totalDistance += bankDistanceRemainder;
        bankDistanceRemainder = 0f;
        totalsDirty = true;
    }

    void SaveTotalsIfDirty(bool force)
    {
        if (!totalsDirty) return;

        if (!force && saveCooldown > 0f)
            return;

        saveCooldown = saveInterval;

        PlayerPrefs.SetFloat(totalDistanceKey, totalDistance);
        PlayerPrefs.SetInt(totalBouncesKey, totalBounces);
        PlayerPrefs.Save();

        totalsDirty = false;
    }

    // ---------------- Lines + label ----------------

    void InitLines()
    {
        if (wallLineA) wallLineA.positionCount = 0;
        if (wallLineB) wallLineB.positionCount = 0;
    }

    void HideAll()
    {
        if (wallLineA) wallLineA.positionCount = 0;
        if (wallLineB) wallLineB.positionCount = 0;

        if (inBallLabel)
        {
            var c = inBallLabel.color;
            c.a = 0f;
            inBallLabel.color = c;
        }
    }

    void UpdateRulersAndBallLabel(bool showVisuals, Vector2 pos, WorldBounds b, float horizAlpha, float vertAlpha)
    {
        if (wallLineA) wallLineA.positionCount = 0;
        if (wallLineB) wallLineB.positionCount = 0;

        if (!showVisuals)
        {
            if (inBallLabel)
            {
                var c = inBallLabel.color;
                c.a = 0f;
                inBallLabel.color = c;
            }
            return;
        }

        PickNearestWalls(pos, b, out WallSide horizSide, out WallSide vertSide);

        DrawLineForSide(horizSide, pos, b, wallLineA, horizAlpha);
        DrawLineForSide(vertSide, pos, b, wallLineB, vertAlpha);

        if (inBallLabel)
        {
            var c = inBallLabel.color;
            c.a = 1f;
            inBallLabel.color = c;

            inBallLabel.SetText("x{0:0.00}", shownLandingMult);
            inBallLabel.transform.position = new Vector3(pos.x, pos.y, 0f);
        }
    }

    void DrawLineForSide(WallSide side, Vector2 pos, WorldBounds b, LineRenderer line, float alpha01)
    {
        if (!line) return;

        Color c = GetWallColor(side);
        c.a *= Mathf.Clamp01(alpha01);
        line.startColor = c;
        line.endColor = c;

        Vector2 wallPoint, startPoint;

        switch (side)
        {
            case WallSide.Left:
                wallPoint = new Vector2(b.left, pos.y);
                startPoint = new Vector2(pos.x - b.r, pos.y);
                break;
            case WallSide.Right:
                wallPoint = new Vector2(b.right, pos.y);
                startPoint = new Vector2(pos.x + b.r, pos.y);
                break;
            case WallSide.Bottom:
                wallPoint = new Vector2(pos.x, b.bottom);
                startPoint = new Vector2(pos.x, pos.y - b.r);
                break;
            default:
                wallPoint = new Vector2(pos.x, b.top);
                startPoint = new Vector2(pos.x, pos.y + b.r);
                break;
        }

        Vector2 dir = (wallPoint - startPoint).sqrMagnitude > 0f ? (wallPoint - startPoint).normalized : Vector2.right;
        Vector2 a = startPoint + dir * ballEdgeOffset;

        line.positionCount = 2;
        line.SetPosition(0, new Vector3(a.x, a.y, 0f));
        line.SetPosition(1, new Vector3(wallPoint.x, wallPoint.y, 0f));
    }

    void PickNearestWalls(Vector2 pos, WorldBounds b, out WallSide horizSide, out WallSide vertSide)
    {
        float dLeft = Mathf.Max(0f, (pos.x - b.r) - b.left);
        float dRight = Mathf.Max(0f, b.right - (pos.x + b.r));
        float dBottom = Mathf.Max(0f, (pos.y - b.r) - b.bottom);
        float dTop = Mathf.Max(0f, b.top - (pos.y + b.r));

        horizSide = (dLeft <= dRight) ? WallSide.Left : WallSide.Right;
        vertSide = (dBottom <= dTop) ? WallSide.Bottom : WallSide.Top;
    }

    Color GetWallColor(WallSide side)
    {
        switch (side)
        {
            case WallSide.Left: return leftWallColor;
            case WallSide.Right: return rightWallColor;
            case WallSide.Bottom: return bottomWallColor;
            default: return topWallColor;
        }
    }

    float FadeWithPop(float t)
    {
        t = Mathf.Clamp01(t);
        float baseFade = Mathf.Pow(t, rulerFadeExponent);

        float pop = Mathf.InverseLerp(popStart, 1f, t);
        pop = pop * pop * (3f - 2f * pop);

        return Mathf.Clamp01(Mathf.Lerp(baseFade, 1f, pop * popStrength));
    }

    // ---------------- Cancel ----------------

    bool CancelRunPressedThisFrame()
    {
        if (Input.GetKeyDown(cancelRunKey))
            return true;

        int touches = Input.touchCount;

        if (touches == 0)
        {
            cancelGestureArmed = true;
            return false;
        }

        if (touches >= cancelRunFingerCount && cancelGestureArmed)
        {
            cancelGestureArmed = false;
            return true;
        }

        return false;
    }

    bool CanCancelNow()
    {
        if (!streakActive) return false;
        if (grab && grab.IsDragging) return false;
        return ThrowsExhausted;
    }

    void CancelRun_NoBank_NoScore()
    {
        firstPowerupUsed = 0;  // Reset powerup order when run is cancelled
        savedFirstPowerupUsed = 0;
        streakActive = false;
        if (powerups) powerups.OnRunEnded();
        ClearHotSpotAll();
        if (powerups) powerups.DisableHotSpot();
        resultLatched = false;
        lastRunNoLanding = false;
        ClearStickyIfAny();

        stopTimer = 0f;
        travelDistance = 0f;
        travelDistanceWithoutOvertime = 0f;
        catchesThisRun = 0;

        throwsUsedThisRun = 0;
        throwsExhausted = false;
        UpdateThrowsUi();

        latchedSnapshot = default;

        showLandingInfo = false;
        shownLandingMult = 1f;
        landingAllowedThisSegment = false;
        suppressResultVisualsUntilThrow = false;

        laneBroken = false;

        PauseLandingVfxIfAny();
        HideAll();

        HideAllScoreElements();
        if (overtimeComparisonText) overtimeComparisonText.text = "";

        savedOvertimeActive = false;
        savedOvertimeUsed = false;
        savedOvertimeElapsed = 0f;
    }

    // ---------------- Timing ----------------

    bool UiTick()
    {
        if (Time.unscaledTime < nextUiTick) return false;
        nextUiTick = Time.unscaledTime + uiTickRate;
        return true;
    }

    // ---------------- Bounds ----------------

    WorldBounds ComputeBounds()
    {
        GameViewport.GetWorldBounds(out var min, out var max);

        float scale = Mathf.Max(ballRb.transform.lossyScale.x, ballRb.transform.lossyScale.y);
        float r = ballCollider.radius * scale;

        WorldBounds b;
        b.left = min.x + boundsInset;
        b.right = max.x - boundsInset;
        b.bottom = min.y + boundsInset;
        b.top = max.y - boundsInset;
        b.r = r;

        b.centerX = (b.left + b.right) * 0.5f;
        b.centerY = (b.bottom + b.top) * 0.5f;

        b.halfW = (b.right - b.left) * 0.5f - r;
        b.halfH = (b.top - b.bottom) * 0.5f - r;

        if (b.halfW <= 0.0001f) b.halfW = 0.0001f;
        if (b.halfH <= 0.0001f) b.halfH = 0.0001f;

        return b;
    }

    public void GetHeatmapWorldRect(out Vector2 min, out Vector2 max)
    {
        WorldBounds b = ComputeBounds();
        min = new Vector2(b.left + b.r, b.bottom + b.r);
        max = new Vector2(b.right - b.r, b.top - b.r);
    }

    public void GetWallWorldRect(out Vector2 min, out Vector2 max)
    {
        WorldBounds b = ComputeBounds();
        min = new Vector2(b.left, b.bottom);
        max = new Vector2(b.right, b.top);
    }

    public void GetHeatmapThresholds(out float t1, out float tGood, out float tMax)
    {
        t1 = 1f;

        // These thresholds match what you described:
        // normal: green starts at 2, max at 4
        // landing amp: green starts at 4, max at 6
        if (powerups && powerups.LandingAmpActiveThisThrow)
        {
            tGood = 4f;
            tMax = 6f;
        }
        else
        {
            tGood = 2f;
            tMax = 4f;
        }
    }

    public void GetHeatmapCurveParams(out float edgeValue, out float cornerTotal, out float exponent, out float maxMul)
    {
        // Match whatever ComputeLandingMultiplier uses RIGHT NOW
        bool capped = streakActive && ShouldCapRightNow();

        // base params
        edgeValue = capped ? cappedEdgeValue : normalEdgeValue;
        cornerTotal = capped ? cappedCornerTotal : normalCornerTotal;
        exponent = closenessExponent;
        maxMul = maxMultiplier;

        // Landing Amplifier overrides (if active this throw)
        if (powerups && powerups.LandingAmpActiveThisThrow)
        {
            // This must match your Landing Amplifier logic used in multiplier calc
            edgeValue *= powerups.GetLandingAmpEdgeMult();
            cornerTotal *= powerups.GetLandingAmpCornerMult();
            exponent = powerups.GetLandingAmpExponent();
            maxMul = powerups.GetLandingAmpMaxMultiplier();
        }
    }

    public float GetBallRadiusWorld()
    {
        if (!ballCollider || !ballRb) return 0f;
        float scale = Mathf.Max(ballRb.transform.lossyScale.x, ballRb.transform.lossyScale.y);
        return ballCollider.radius * scale;
    }

    void GetCenterNormalized(Vector2 pos, WorldBounds b, out float nx, out float ny)
    {
        nx = Mathf.Clamp01(Mathf.Abs(pos.x - b.centerX) / b.halfW);
        ny = Mathf.Clamp01(Mathf.Abs(pos.y - b.centerY) / b.halfH);
    }

    public float GetFieldScaleForNormalization()
    {
        Vector2 dims = GameViewport.GetWorldDimensions();
        return dims.magnitude;
    }

#if UNITY_EDITOR
    [ContextMenu("Reset PlayerPrefs (RunScoring2D)")]
    void ResetPlayerPrefs_ContextMenu()
    {
        PlayerPrefs.DeleteKey("BestScore");
        PlayerPrefs.DeleteKey(totalDistanceKey);
        PlayerPrefs.DeleteKey(totalBouncesKey);
        PlayerPrefs.Save();

        BestScore = 0f;
        totalDistance = 0f;
        totalBounces = 0;
        bankDistanceRemainder = 0f;
        totalsDirty = false;

        if (bestText) bestText.color = bestDefaultColor;

        RefreshAllUI();
        Debug.Log("[RunScoring2D] PlayerPrefs reset.");
    }
#endif
}