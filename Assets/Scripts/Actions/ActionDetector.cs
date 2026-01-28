using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects when actions are triggered during gameplay.
/// Delegates to ActionRewarder for reward handling.
/// </summary>
[DefaultExecutionOrder(100)]
public class ActionDetector : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RunScoring2D scoring;
    [SerializeField] FingerGrabInertia2D grab;
    [SerializeField] ActionRewarder rewarder;

    [Header("Quick Catch")]
    [SerializeField, Min(0f)] float quickCatchMinSpeed = 8f;

    // Track unlocked actions
    HashSet<string> unlockedActions = new HashSet<string>();
    const string UNLOCKED_ACTIONS_KEY = "ActionDetector_UnlockedActions_v1";

    [Header("Wall Frenzy (All 4 Walls)")]
    [SerializeField] int wallFrenzyMinWalls = 12;
    [SerializeField, Range(0.1f, 0.5f)] float wallFrenzyMinCoverage = 0.25f;
    [SerializeField] int wallFrenzyMinWallsHit = 4;

    [Header("Greed / Desperation")]
    [SerializeField, Min(0f)] float preLandingDecisionWindowSeconds = 0.6f;

    [Header("Edge Case")]
    [SerializeField, Min(0f)] float edgeCaseProximityThreshold = 0.05f;  // Distance from wall as % of screen dimension

    [Header("Predicted Stop Marker")]
    [SerializeField] Transform predictedStopMarker;
    [SerializeField, Range(0.1f, 0.5f)] float stopMarkerSmoothSpeed = 0.25f;

    // Per-throw state
    int wallBouncesThisThrow;
    bool throwInFlight;
    bool wallFrenzyAwardedThisThrow;

    int leftHits, rightHits, topHits, bottomHits;

    float throwDistance;
    Vector2 lastPos;
    Vector2 throwStartPos;
    bool markerJustTeleported;

    // Edge Case tracking
    int closestWallThisThrow = -1;  // -1 = none, 0 = left, 1 = right, 2 = top, 3 = bottom
    bool edgeCaseAwardedThisThrow;
    int edgeCaseWallTouches;  // Allow 1 wall touch
    float edgeCaseCloseness01;      // 0..1 closeness to top threshold (1 = on the edge)
    bool edgeCaseQualified;         // true once proximity condition met this throw
    bool stickyUsedThisThrow;       // Track if sticky ball was used (blocks Edge Case)

    void Awake()
    {
        if (!scoring) scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);
        if (!grab) grab = FindFirstObjectByType<FingerGrabInertia2D>(FindObjectsInactive.Include);
        if (!rewarder) rewarder = FindFirstObjectByType<ActionRewarder>(FindObjectsInactive.Include);

        // Load unlocked actions from save
        LoadUnlockedActions();
    }

    void Update()
    {
        // Clear marker if ball is being held (grabbed) - handles pickup after cancelled runs
        if (grab != null)
        {
            var rb = grab.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
            {
                if (predictedStopMarker && predictedStopMarker.gameObject.activeSelf)
                {
                    predictedStopMarker.gameObject.SetActive(false);
                    markerJustTeleported = false;
                }
            }
        }

        // Track distance for dynamic Wall Frenzy calculation
        if (throwInFlight && grab != null)
        {
            var rb = grab.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 p = rb.position;
                throwDistance += Mathf.Abs(p.y - lastPos.y);  // Y-axis only to prevent horizontal cheesing
                lastPos = p;

                // Update predicted stop marker
                UpdatePredictedStopMarker(rb);

                // Check Edge Case proximity
                CheckEdgeCaseProximity(rb);

            }
        }
    }

    void UpdatePredictedStopMarker(Rigidbody2D rb)
    {
        if (!predictedStopMarker || !scoring) return;

        Vector2 predicted = scoring.PredictStopPosition(rb.position, rb.linearVelocity);
        Vector3 target = new Vector3(predicted.x, predicted.y, predictedStopMarker.position.z);

        if (!predictedStopMarker.gameObject.activeSelf)
            predictedStopMarker.gameObject.SetActive(true);

        // Teleport on first prediction after throw
        if (!markerJustTeleported)
        {
            predictedStopMarker.position = target;
            markerJustTeleported = true;
        }
        else
        {
            // Smooth refinement after initial teleport (heavy, slow slide)
            predictedStopMarker.position = Vector3.Lerp(predictedStopMarker.position, target, stopMarkerSmoothSpeed * Time.deltaTime);
        }
    }

    void CheckEdgeCaseProximity(Rigidbody2D rb)
    {
        if (!scoring) return;
        if (edgeCaseAwardedThisThrow) return;
        if (edgeCaseWallTouches > 1) return;  // Allow 1 wall touch

        GameViewport.GetWorldBounds(out var min, out var max);
        Vector2 p = rb.position;
        float ballR = 0.5f;  // Approximate ball radius

        // Calculate screen dimensions
        float screenHeight = max.y - min.y;

        // Only track TOP wall (wallId == 2)
        float distToTop = (max.y - ballR) - p.y;
        float distToTopPct = distToTop / screenHeight;

        // If not close enough to top or touching, do nothing
        if (distToTopPct <= 0f || distToTopPct > edgeCaseProximityThreshold) return;

        // Qualified: track proximity and distance
        closestWallThisThrow = 2;  // Top wall

        // Calculate closeness multiplier (0-1, where 1 = at threshold edge)
        float closenessMultiplier = Mathf.Clamp01(1f - (distToTopPct / edgeCaseProximityThreshold));
        edgeCaseCloseness01 = closenessMultiplier;
        edgeCaseQualified = true;
    }

    void OnEnable()
    {
        if (grab) grab.OnDragEnded += OnThrowReleased;
    }

    void OnDisable()
    {
        if (grab) grab.OnDragEnded -= OnThrowReleased;
    }

    public void OnThrowStarted()
    {
        // Called by RunScoring2D when a throw begins
        // Reset per-throw tracking
        wallBouncesThisThrow = 0;
        wallFrenzyAwardedThisThrow = false;
        leftHits = rightHits = topHits = bottomHits = 0;
        throwDistance = 0f;

        closestWallThisThrow = -1;
        edgeCaseAwardedThisThrow = false;
        edgeCaseWallTouches = 0;
        edgeCaseQualified = false;
        edgeCaseCloseness01 = 0f;
    }

    public void OnRunEnded()
    {
        // Called by RunScoring2D when run ends
        throwInFlight = false;

        // Award Edge Case only if ball is still near the top wall when run ends
        // AND sticky ball was not used this throw (mutually exclusive mechanics)
        if (!edgeCaseAwardedThisThrow && !stickyUsedThisThrow && edgeCaseQualified && closestWallThisThrow == 2 && edgeCaseWallTouches <= 1 && grab != null && scoring != null)
        {
            var rb = grab.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // Use the same wall detection as landing multiplier
                if (scoring.IsCloseToTopWall(rb.position, edgeCaseProximityThreshold, out float distPct))
                {
                    edgeCaseAwardedThisThrow = true;
                    if (rewarder) rewarder.AwardEdgeCase(throwDistance, edgeCaseCloseness01);
                }
            }
        }

        edgeCaseQualified = false;
        edgeCaseCloseness01 = 0f;
        closestWallThisThrow = -1;
        edgeCaseWallTouches = 0;

        if (predictedStopMarker && predictedStopMarker.gameObject.activeSelf)
            predictedStopMarker.gameObject.SetActive(false);
    }

    // Called by external systems
    public void OnRunStarted()
    {
        // Greed/Desperation no longer use per-run caps
    }

    public void OnThrowReleased(bool wasThrown)
    {
        if (!wasThrown)
        {
            throwInFlight = false;
            return;
        }

        throwInFlight = true;
        wallBouncesThisThrow = 0;
        wallFrenzyAwardedThisThrow = false;
        leftHits = rightHits = topHits = bottomHits = 0;
        throwDistance = 0f;
        markerJustTeleported = false;
        edgeCaseQualified = false;
        edgeCaseCloseness01 = 0f;
        closestWallThisThrow = -1;
        edgeCaseWallTouches = 0;
        stickyUsedThisThrow = false;  // Reset sticky tracking

        // Check if sticky ball is armed for this throw (will be activated in OnThrown)
        var powerups = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);
        if (powerups && powerups.HasArmed && powerups.ArmedId == powerups.GetStickyBallId())
        {
            stickyUsedThisThrow = true;
        }

        if (grab)
        {
            throwStartPos = grab.GetComponent<Rigidbody2D>().position;
            lastPos = throwStartPos;
        }
    }

    public void OnWallBounce(int wallId)
    {
        if (!throwInFlight) return;

        // Edge Case allows 1 wall touch
        edgeCaseWallTouches++;

        wallBouncesThisThrow++;

        switch (wallId)
        {
            case 0: leftHits++; break;
            case 1: rightHits++; break;
            case 2: topHits++; break;
            case 3: bottomHits++; break;
        }

        if (wallFrenzyAwardedThisThrow) return;
        if (!scoring) return;

        if (wallBouncesThisThrow >= wallFrenzyMinWalls)
        {
            // Check distribution: all 4 walls hit with balanced coverage
            int minWallHits = Mathf.Min(leftHits, rightHits, topHits, bottomHits);
            int maxWallHits = Mathf.Max(leftHits, rightHits, topHits, bottomHits);

            int wallsHit = (leftHits > 0 ? 1 : 0) + (rightHits > 0 ? 1 : 0)
                         + (topHits > 0 ? 1 : 0) + (bottomHits > 0 ? 1 : 0);

            float coverage = maxWallHits > 0 ? minWallHits / (float)maxWallHits : 0f;

            if (wallsHit >= wallFrenzyMinWallsHit && coverage >= wallFrenzyMinCoverage)
            {
                wallFrenzyAwardedThisThrow = true;
                if (rewarder) rewarder.AwardAction("WallFrenzy");
            }
        }
    }

    public void OnPickup(bool wasCatch)
    {
        // Clear marker on any pickup (including after cancelled runs)
        if (predictedStopMarker && predictedStopMarker.gameObject.activeSelf)
            predictedStopMarker.gameObject.SetActive(false);
        markerJustTeleported = false;

        if (!throwInFlight)
        {
            throwInFlight = false;
            return;
        }

        if (wasCatch && grab != null)
        {
            float catchSpeed = grab.LastPickupVelocity.magnitude;
            if (catchSpeed >= quickCatchMinSpeed)
            {
                if (rewarder) rewarder.AwardAction("QuickCatch");
            }
        }

        // Check for Greed / Desperation
        if (IsPreLastThrowPickup() && scoring != null && grab != null)
        {
            float tStop = scoring.PredictTimeToStopFromSpeed(grab.LastPickupSpeed);

            if (tStop >= 0f && tStop <= preLandingDecisionWindowSeconds)
            {
                Vector2 predictedStop = scoring.PredictStopPosition(grab.LastPickupPosition, grab.LastPickupVelocity);
                float predictedMult = scoring.GetLandingMultiplierAt(predictedStop);

                bool futureIsGood = predictedMult >= 1f;

                // Allow Greed/Desperation on both eligible pickups (removed per-run cap)
                if (futureIsGood)
                {
                    if (rewarder) rewarder.AwardAction("Greed");
                }
                else
                {
                    if (rewarder) rewarder.AwardAction("Desperation");
                }
            }
        }

        throwInFlight = false;
    }

    bool IsPreLastThrowPickup()
    {
        if (!scoring) return false;
        // Allow Greed/Desperation during the final two pickups instead of only the penultimate
        return scoring.RunActive && scoring.ThrowsLeft <= 2;
    }

    /// <summary>
    /// Unlock an action so it can be triggered and rewarded.
    /// Called by LevelRewardManager when a player levels up.
    /// </summary>
    public void UnlockAction(string actionId)
    {
        if (string.IsNullOrEmpty(actionId)) return;

        if (!unlockedActions.Contains(actionId))
        {
            unlockedActions.Add(actionId);
            SaveUnlockedActions();
            Debug.Log($"[ActionDetector] Unlocked action: {actionId}");
        }
    }

    /// <summary>
    /// Check if an action has been unlocked.
    /// </summary>
    public bool IsActionUnlocked(string actionId)
    {
        return unlockedActions.Contains(actionId);
    }

    void SaveUnlockedActions()
    {
        var list = new System.Collections.Generic.List<string>(unlockedActions);
        var json = JsonUtility.ToJson(new ActionUnlockedList { actions = list });
        PlayerPrefs.SetString(UNLOCKED_ACTIONS_KEY, json);
        PlayerPrefs.Save();
    }

    void LoadUnlockedActions()
    {
        if (!PlayerPrefs.HasKey(UNLOCKED_ACTIONS_KEY)) return;

        var json = PlayerPrefs.GetString(UNLOCKED_ACTIONS_KEY, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var loaded = JsonUtility.FromJson<ActionUnlockedList>(json);
            if (loaded?.actions != null)
            {
                unlockedActions = new HashSet<string>(loaded.actions);
            }
        }
        catch
        {
            Debug.LogWarning("[ActionDetector] Failed to load unlocked actions");
        }
    }

    [System.Serializable]
    class ActionUnlockedList
    {
        public System.Collections.Generic.List<string> actions = new System.Collections.Generic.List<string>();
    }
}
