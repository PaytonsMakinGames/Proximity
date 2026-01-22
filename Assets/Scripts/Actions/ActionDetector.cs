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

    [Header("Wall Frenzy (All 4 Walls)")]
    [SerializeField] int wallFrenzyMinWalls = 12;
    [SerializeField, Range(0.1f, 0.5f)] float wallFrenzyMinCoverage = 0.25f;
    [SerializeField] int wallFrenzyMinWallsHit = 4;

    [Header("Greed / Desperation")]
    [SerializeField, Min(0f)] float preLandingDecisionWindowSeconds = 0.6f;

    [Header("Predicted Stop Marker")]
    [SerializeField] Transform predictedStopMarker;
    [SerializeField, Range(0.1f, 0.5f)] float stopMarkerSmoothSpeed = 0.25f;

    // Per-throw state
    int wallBouncesThisThrow;
    bool throwInFlight;
    bool wallFrenzyAwardedThisThrow;

    int leftHits, rightHits, topHits, bottomHits;
    bool greedDoneThisRun;
    bool desperationDoneThisRun;

    float throwDistance;
    Vector2 lastPos;
    bool markerJustTeleported;

    void Awake()
    {
        if (!scoring) scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);
        if (!grab) grab = FindFirstObjectByType<FingerGrabInertia2D>(FindObjectsInactive.Include);
        if (!rewarder) rewarder = FindFirstObjectByType<ActionRewarder>(FindObjectsInactive.Include);
    }

    void Update()
    {
        // Track distance for dynamic Wall Frenzy calculation
        if (throwInFlight && grab != null)
        {
            var rb = grab.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 p = rb.position;
                throwDistance += Vector2.Distance(p, lastPos);
                lastPos = p;

                // Update predicted stop marker
                UpdatePredictedStopMarker(rb);
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
    }

    public void OnRunEnded()
    {
        // Called by RunScoring2D when run ends
        throwInFlight = false;
        greedDoneThisRun = false;
        desperationDoneThisRun = false;

        if (predictedStopMarker && predictedStopMarker.gameObject.activeSelf)
            predictedStopMarker.gameObject.SetActive(false);
    }

    // Called by external systems
    public void OnRunStarted()
    {
        greedDoneThisRun = false;
        desperationDoneThisRun = false;
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

        if (grab) lastPos = grab.GetComponent<Rigidbody2D>().position;
    }

    public void OnWallBounce(int wallId)
    {
        if (!throwInFlight) return;

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

                if (futureIsGood && !greedDoneThisRun)
                {
                    greedDoneThisRun = true;
                    if (rewarder) rewarder.AwardAction("Greed");
                }
                else if (!futureIsGood && !desperationDoneThisRun)
                {
                    desperationDoneThisRun = true;
                    if (rewarder) rewarder.AwardAction("Desperation");
                }
            }
        }

        throwInFlight = false;
    }

    bool IsPreLastThrowPickup()
    {
        if (!scoring) return false;
        return scoring.RunActive && scoring.ThrowsLeft == 1;
    }
}
