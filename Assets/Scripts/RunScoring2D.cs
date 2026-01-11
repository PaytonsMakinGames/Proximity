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
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] TextMeshProUGUI bestText;

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

    [Header("Catch Multiplier Gain")]
    [SerializeField] float catchGainSpeedMin = 3.5f;
    [SerializeField] float catchGainSpeedMax = 100f;
    [SerializeField] float catchGainAtMin = 0.00f;
    [SerializeField] float catchGainAtMax = 0.10f;
    [SerializeField] float catchMultiplierCap = 0f;

    [Header("Miss Penalty (keeps streak alive)")]
    [Tooltip("0.75 = remove 25% of the gap back to 1.00x each miss.")]
    [SerializeField, Range(0f, 1f)] float missPenaltyKeep01 = 0.75f;

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

    [Header("Run Cancel")]
    [SerializeField] KeyCode cancelRunKey = KeyCode.X;
    [SerializeField] int cancelRunFingerCount = 5;

    bool cancelGestureArmed = true;

    int EffectiveThrowsPerRun
    {
        get
        {
            int bonus = inventory ? inventory.GetBonusThrows() : 0;
            return Mathf.Max(0, throwsPerRun + bonus);
        }
    }

    public float BestScore { get; private set; }
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
    float catchMultiplier = 1f;

    float laneCenterX, laneCenterY;

    float displayedCatchMult = 1f;
    float displayedLandingMult = 1f;
    float shownLandingMult = 1f;

    float nextUiTick;
    const float uiTickRate = 1f / 30f;

    int totalBounces;
    int catchesThisRun;
    int throwsUsedThisRun;
    bool throwsExhausted;

    int displayedDistance;

    bool totalsDirty;
    bool streakActive;
    bool prevHeld;
    bool laneBroken;
    bool resultLatched;
    bool showLandingInfo;
    bool lastRunNoLanding;
    bool prevWasThrown;
    bool prevWasDropped;
    bool landingAllowedThisSegment;
    bool suppressResultVisualsUntilThrow;

    int lastLiveRunScoreInt;
    int displayedRunScoreInt;

    int lastLiveXpInt;
    int displayedXpInt;

    float lastLiveXpMultShown = 1f;
    int lastLiveXpPct = 0;

    int displayedXpPct = 0;

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

        totalDistance = PlayerPrefs.GetFloat(totalDistanceKey, 0f);
        totalBounces = PlayerPrefs.GetInt(totalBouncesKey, 0);
        BestScore = PlayerPrefs.GetFloat("BestScore", 0f);

        bestDefaultColor = bestNormalColor;

        if (scoreText) scoreText.richText = true;
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
        bool wasThrownFlag = !pausedOrLocked && grab && grab.WasThrown;
        bool wasDroppedFlag = !pausedOrLocked && grab && grab.WasDropped;

        bool throwEvent = wasThrownFlag && !prevWasThrown;
        prevWasThrown = wasThrownFlag;

        bool dropEvent = wasDroppedFlag && !prevWasDropped;
        prevWasDropped = wasDroppedFlag;

        if (isHeld && !prevHeld) OnAnyPickupStarted();

        if (throwEvent)
        {
            bool startedOnBall = grab && grab.LastPickupWasCatch;

            if (!streakActive && !startedOnBall)
            {
                RetireActiveLandingVfxOnThrow();
                suppressResultVisualsUntilThrow = false;
                landingAllowedThisSegment = false;
                showLandingInfo = false;
                shownLandingMult = 1f;
                HideAll();

                if (grab) grab.ConsumeWasThrown();
                prevWasThrown = false;

                prevHeld = isHeld;
                return;
            }

            OnThrown();
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

            ConsumeThrow(fromMiss: false);
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

    // ---------------- Events ----------------

    void OnAnyPickupStarted()
    {
        // Stop emitting if something was still following, but do NOT clear instantly.
        // This also prevents “resume” behavior.
        PauseLandingVfxIfAny();

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

        lastRunPos = ballRb.position;
        stopTimer = 0f;

        laneBroken = false;
        DetermineLaneAxisAndCenter();

        showLandingInfo = false;
        shownLandingMult = 1f;

        landingAllowedThisSegment = true;
    }

    void StartStreak()
    {
        streakActive = true;
        stopTimer = 0f;

        travelDistance = 0f;
        lastRunPos = ballRb.position;

        catchMultiplier = 1f;
        catchesThisRun = 0;

        throwsUsedThisRun = 0;
        throwsExhausted = false;
        UpdateThrowsUi();

        resultLatched = false;

        displayedDistance = 0;
        displayedCatchMult = 1f;
        displayedLandingMult = 1f;

        showLandingInfo = false;
        shownLandingMult = 1f;

        lastRunNoLanding = false;

        landingAllowedThisSegment = false;
        suppressResultVisualsUntilThrow = false;

        laneBroken = false;

        lastLiveRunScoreInt = 0;
        displayedRunScoreInt = 0;

        lastLiveXpInt = 0;
        displayedXpInt = 0;

        lastLiveXpMultShown = 1f;
        lastLiveXpPct = 0;

        displayedXpPct = 0;

        if (scoreText) scoreText.text = "";
        HideAll();
    }

    void OnPickupStarted()
    {
        bool wasCatch = grab && grab.LastPickupWasCatch;

        if (wasCatch)
        {
            catchesThisRun++;

            float catchSpeed = grab ? grab.LastPickupSpeed : 0f;
            catchMultiplier += ComputeCatchGainFromSpeed(catchSpeed);

            if (catchMultiplierCap > 0f)
                catchMultiplier = Mathf.Min(catchMultiplier, catchMultiplierCap);

            stopTimer = 0f;
            landingAllowedThisSegment = false;
            return;
        }

        catchMultiplier = 1f + (catchMultiplier - 1f) * Mathf.Clamp01(missPenaltyKeep01);
        if (catchMultiplier < 1f) catchMultiplier = 1f;

        stopTimer = 0f;
        landingAllowedThisSegment = false;

        ConsumeThrow(fromMiss: true);
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

    float ComputeCatchGainFromSpeed(float speed)
    {
        if (catchGainSpeedMax <= catchGainSpeedMin) return catchGainAtMin;
        float t = Mathf.Clamp01(Mathf.InverseLerp(catchGainSpeedMin, catchGainSpeedMax, speed));
        return Mathf.Lerp(catchGainAtMin, catchGainAtMax, t);
    }

    void TickStreakFlight()
    {
        Vector2 p = ballRb.position;
        Vector2 delta = p - lastRunPos;

        float step = delta.magnitude * Mathf.Max(0.0001f, distanceUnitScale);
        travelDistance += step;
        lastRunPos = p;

        BankDistance(step);
        UpdateTotalsUI_LiveBanked();
        SaveTotalsIfDirty(false);

        UpdateLaneBrokenState(p);

        float speed = ballRb.linearVelocity.magnitude;
        stopTimer = (speed <= stopSpeed) ? (stopTimer + Time.deltaTime) : 0f;

        if (stopTimer >= stopHoldTime)
            EndStreak_Stop();
    }

    void EndStreak_DropInstant()
    {
        if (!streakActive) return;

        streakActive = false;
        lastRunNoLanding = true;

        BankRemainingDistance();

        displayedDistance = RoundInt(travelDistance);
        displayedCatchMult = Round3(catchMultiplier);
        displayedLandingMult = 1f;

        resultLatched = true;
        UpdateScoreText();

        displayedRunScoreInt = lastLiveRunScoreInt;
        displayedXpInt = lastLiveXpInt;

        bool isNewBest = false;
        if (displayedXpInt > BestScore)
        {
            BestScore = displayedXpInt;
            PlayerPrefs.SetFloat("BestScore", BestScore);
            isNewBest = true;
        }

        ApplyBestUI((int)BestScore, isNewBest);
        AwardXp(displayedXpInt);

        SaveTotalsIfDirty(true);
        UpdateTotalsUI();

        HideAll();
        showLandingInfo = false;
        shownLandingMult = 1f;
    }

    void EndStreak_Stop()
    {
        if (!streakActive) return;

        streakActive = false;
        BankRemainingDistance();

        displayedDistance = RoundInt(travelDistance);
        displayedCatchMult = Round3(catchMultiplier);

        bool landingShownToPlayer = landingAllowedThisSegment && showLandingInfo;

        if (!landingShownToPlayer)
        {
            lastRunNoLanding = true;
            displayedLandingMult = 1f;
        }
        else
        {
            lastRunNoLanding = false;
            displayedLandingMult = Round2(shownLandingMult);
        }

        resultLatched = true;
        UpdateScoreText();

        displayedRunScoreInt = lastLiveRunScoreInt;
        displayedXpInt = lastLiveXpInt;

        bool isNewBest = false;
        if (displayedXpInt > BestScore)
        {
            BestScore = displayedXpInt;
            PlayerPrefs.SetFloat("BestScore", BestScore);
            isNewBest = true;
        }

        ApplyBestUI((int)BestScore, isNewBest);
        AwardXp(displayedXpInt);

        SaveTotalsIfDirty(true);
        UpdateTotalsUI();

        // IMPORTANT: start a NEW landing emitter for this stop
        if (landingShownToPlayer)
        {
            StartNewLandingVfx(followBall: true);
            if (isNewBest) SetLandingVfxColor(activeLandingPs, bestBeatenColor);
        }
        else
        {
            // No landing visuals this time
            PauseLandingVfxIfAny();
            HideAll();
            showLandingInfo = false;
            shownLandingMult = 1f;
        }
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

    bool ShouldCapRightNow() => !laneBroken;

    float ComputeLandingMultiplier(bool capped)
    {
        WorldBounds b = ComputeBounds();
        Vector2 pos = ballRb.position;

        GetCenterNormalized(pos, b, out float nx, out float ny);

        float edgeValue = capped ? cappedEdgeValue : normalEdgeValue;
        float cornerTotal = capped ? cappedCornerTotal : normalCornerTotal;
        float cornerBoost = cornerTotal - edgeValue;

        float e = Mathf.Max(0.0001f, closenessExponent);

        float ax = Mathf.Pow(Mathf.Clamp01(nx), e);
        float ay = Mathf.Pow(Mathf.Clamp01(ny), e);

        float edge = Mathf.Max(ax, ay);
        float corner = ax * ay;

        float m = (edgeValue * edge) + (cornerBoost * corner);
        if (m < 0.01f) m = 0f;

        return Mathf.Clamp(m, 0f, maxMultiplier);
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
            if (resultLatched) shownLandingMult = displayedLandingMult;
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

    // ---------------- UI ----------------

    string CatchLineText(float multShown)
    {
        int n = catchesThisRun;
        return $"{n} {(n == 1 ? "catch" : "catches")} x{multShown:F3}";
    }

    void ComputeLiveNumbers(
        out int distInt,
        out float catchShown,
        out float landingShown,
        out int baseScore,
        out int xpTotal,
        out float xpMultShown,
        out int xpPct)
    {
        distInt = RoundInt(travelDistance);

        catchShown = Round3(catchMultiplier);
        landingShown = showLandingInfo ? Round2(shownLandingMult) : 1f;

        xpMultShown = Round2(GetXpMultRaw());
        xpPct = Mathf.RoundToInt((xpMultShown - 1f) * 100f);

        float rawTotal = distInt * catchShown * landingShown * xpMultShown;
        xpTotal = Mathf.RoundToInt(rawTotal);

        baseScore = Mathf.RoundToInt(distInt * catchShown * landingShown);

        lastLiveXpMultShown = xpMultShown;
        lastLiveXpPct = xpPct;
    }

    void UpdateScoreText()
    {
        if (!scoreText) return;

        bool ended = (!streakActive && resultLatched);
        scoreText.color = ended ? scoreTextEndedColor : scoreTextLiveColor;

        if (!streakActive && !resultLatched)
        {
            scoreText.text = "";
            return;
        }

        string details;
        int redXpTotal;

        if (streakActive)
        {
            ComputeLiveNumbers(out int distInt, out float catchShown, out float landingShown, out int baseScore,
                out int xpTotal, out float xpMultShown, out int xpPct);

            lastLiveRunScoreInt = baseScore;
            lastLiveXpInt = xpTotal;

            bool showCatchLine = catchesThisRun > 0;
            bool showLandLine = showLandingInfo;

            details = $"{distInt}";
            if (showCatchLine) details += $"\n{CatchLineText(catchShown)}";
            if (showLandLine) details += $"\nland x{landingShown:0.00}";

            if (xpPct != 0)
            {
                string xpLine = xpPct > 0 ? $"+{xpPct}% XP" : $"{xpPct}% XP";
                details += $"\n{xpLine}";
            }

            redXpTotal = xpTotal;

            bool somethingBeyondDistance =
                (catchesThisRun > 0) ||
                (showLandingInfo && Round2(shownLandingMult) != 1f) ||
                (xpPct != 0);

            if (!somethingBeyondDistance)
            {
                scoreText.text = details;
                return;
            }

            scoreText.text =
                $"{details}\n\n" +
                $"<color={SCORE_VALUE_COLOR}>{redXpTotal}</color>";
            return;
        }

        // Latched result
        {
            bool showCatchLine = catchesThisRun > 0;
            bool showLandLine = !lastRunNoLanding;

            details = $"{displayedDistance}";
            if (showCatchLine) details += $"\n{CatchLineText(displayedCatchMult)}";
            if (showLandLine) details += $"\nland x{displayedLandingMult:0.00}";

            if (displayedXpPct != 0)
            {
                string xpLine = displayedXpPct > 0 ? $"+{displayedXpPct}% XP" : $"{displayedXpPct}% XP";
                details += $"\n{xpLine}";
            }

            redXpTotal = displayedXpInt;

            bool somethingBeyondDistance =
                (catchesThisRun > 0) ||
                (!lastRunNoLanding && displayedLandingMult != 1f) ||
                (displayedXpPct != 0);

            string full = details;
            if (somethingBeyondDistance)
            {
                full =
                    $"{details}\n\n" +
                    $"<color={SCORE_VALUE_COLOR}>{redXpTotal}</color>";
            }

            scoreText.text = $"<color={DETAILS_DIM_COLOR}>{full}</color>";
        }
    }

    void RefreshAllUI()
    {
        if (bestText)
        {
            bestText.gameObject.SetActive(true);
            bestText.SetText("BEST\n{0}", Mathf.RoundToInt(BestScore));
            bestText.color = bestDefaultColor;
        }

        if (scoreText) scoreText.text = "";
        UpdateTotalsUI();
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
        streakActive = false;
        resultLatched = false;
        lastRunNoLanding = false;

        stopTimer = 0f;
        travelDistance = 0f;
        catchMultiplier = 1f;
        catchesThisRun = 0;

        throwsUsedThisRun = 0;
        throwsExhausted = false;
        UpdateThrowsUi();

        displayedDistance = 0;
        displayedCatchMult = 1f;
        displayedLandingMult = 1f;

        showLandingInfo = false;
        shownLandingMult = 1f;
        landingAllowedThisSegment = false;
        suppressResultVisualsUntilThrow = false;

        laneBroken = false;

        lastLiveRunScoreInt = 0;
        displayedRunScoreInt = 0;

        lastLiveXpInt = 0;
        displayedXpInt = 0;

        lastLiveXpMultShown = 1f;
        lastLiveXpPct = 0;

        displayedXpPct = 0;

        PauseLandingVfxIfAny();
        HideAll();

        if (scoreText) scoreText.text = "";
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
        Vector2 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector2 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        float scale = Mathf.Max(ballRb.transform.lossyScale.x, ballRb.transform.lossyScale.y);
        float r = ballCollider.radius * scale;

        WorldBounds b;
        b.left = bl.x + boundsInset;
        b.right = tr.x - boundsInset;
        b.bottom = bl.y + boundsInset;
        b.top = tr.y - boundsInset;
        b.r = r;

        b.centerX = (b.left + b.right) * 0.5f;
        b.centerY = (b.bottom + b.top) * 0.5f;

        b.halfW = (b.right - b.left) * 0.5f - r;
        b.halfH = (b.top - b.bottom) * 0.5f - r;

        if (b.halfW <= 0.0001f) b.halfW = 0.0001f;
        if (b.halfH <= 0.0001f) b.halfH = 0.0001f;

        return b;
    }

    void GetCenterNormalized(Vector2 pos, WorldBounds b, out float nx, out float ny)
    {
        nx = Mathf.Clamp01(Mathf.Abs(pos.x - b.centerX) / b.halfW);
        ny = Mathf.Clamp01(Mathf.Abs(pos.y - b.centerY) / b.halfH);
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