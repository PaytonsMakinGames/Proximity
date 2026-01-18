using UnityEngine;

// V1: detects the 4 actions and awards exactly 1 drop roll per action trigger.
public class ActionManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RunScoring2D scoring;
    [SerializeField] PowerupInventory inventory;
    [SerializeField] PowerupDatabase db;
    [SerializeField] FingerGrabInertia2D grab;
    [SerializeField] FloatingPopupSystem popups;
    [SerializeField] Rigidbody2D ballRb; // for popup position

    [Header("Quick Catch")]
    [SerializeField, Min(0f)] float quickCatchMinSpeed = 8f;


    [Header("Wall Frenzy (Dynamic)")]
    [SerializeField] int wallFrenzyMinWalls = 30;
    [SerializeField] float wallFrenzyWallsPerUnit = 12f;

    [Header("Greed / Desperation")]
    [SerializeField, Min(0f)] float preLandingDecisionWindowSeconds = 0.6f;

    [Header("Drop Tables (V1)")]

    // Wall Frenzy
    [SerializeField] DropEntry[] dropsWallFrenzy;

    // Quick Catch
    [SerializeField] DropEntry[] dropsQuickCatch;

    // Greed
    [SerializeField] DropEntry[] dropsGreed;

    // Desperation
    [SerializeField] DropEntry[] dropsDesperation;

    [SerializeField] Transform predictedStopMarker;
    [SerializeField] bool debugShowPrediction = true;

    [SerializeField, Min(0f)] float predictedMarkerFollowHz = 1.2f;
    bool markerWasVisible;

    [System.Serializable]
    public struct DropEntry
    {
        public string id;
        [Min(1)] public int weight;
    }

    // ---- Per-throw state ----
    int wallBouncesThisThrow;
    bool throwInFlight;
    bool wallFrenzyAwardedThisThrow;

    int leftHits, rightHits, topHits, bottomHits;

    [Header("Wall Frenzy (Anti-Farm)")]
    [SerializeField, Range(0.5f, 0.95f)] float wallFrenzyMaxDominantAxisShare = 0.75f;

    bool greedDoneThisRun;
    bool desperationDoneThisRun;

    float throwDistance;
    Vector2 lastPos;

    static readonly Color COLOR_WALLFRENZY = new Color32(255, 140, 60, 255);  // orange
    static readonly Color COLOR_QUICKCATCH = new Color32(90, 220, 255, 255);  // cyan
    static readonly Color COLOR_GREED = new Color32(255, 200, 90, 255);  // gold 
    static readonly Color COLOR_DESPERATION = new Color32(180, 140, 255, 255); // violet
    static readonly Color COLOR_ITEM = new Color32(124, 255, 178, 255); // #7CFFB2
    static readonly Color COLOR_XP = new Color32(143, 179, 200, 255); // #8FB3C8

    void Awake()
    {
        if (!scoring) scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);
        if (!inventory) inventory = FindFirstObjectByType<PowerupInventory>(FindObjectsInactive.Include);
        if (!db) db = FindFirstObjectByType<PowerupDatabase>(FindObjectsInactive.Include);
        if (!grab) grab = FindFirstObjectByType<FingerGrabInertia2D>(FindObjectsInactive.Include);
        if (!popups) popups = FindFirstObjectByType<FloatingPopupSystem>(FindObjectsInactive.Include);
        if (!ballRb && grab) ballRb = grab.GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Track distance traveled during the current throw (for dynamic Wall Frenzy)
        if (throwInFlight && grab != null)
        {
            var rbDist = grab.GetComponent<Rigidbody2D>();
            if (rbDist != null)
            {
                Vector2 p = rbDist.position;
                throwDistance += Vector2.Distance(p, lastPos);
                lastPos = p;
            }
        }

        if (!debugShowPrediction) return;
        if (!predictedStopMarker) return;

        bool shouldShow = throwInFlight && scoring != null && grab != null;

        if (!shouldShow)
        {
            if (predictedStopMarker.gameObject.activeSelf)
                predictedStopMarker.gameObject.SetActive(false);

            markerWasVisible = false;
            return;
        }

        var rb = grab.GetComponent<Rigidbody2D>();
        if (!rb)
        {
            if (predictedStopMarker.gameObject.activeSelf)
                predictedStopMarker.gameObject.SetActive(false);

            markerWasVisible = false;
            return;
        }

        Vector2 predicted2 = scoring.PredictStopPosition(rb.position, rb.linearVelocity);
        Vector3 target = new Vector3(predicted2.x, predicted2.y, predictedStopMarker.position.z);

        // Turn marker on
        if (!predictedStopMarker.gameObject.activeSelf)
            predictedStopMarker.gameObject.SetActive(true);

        // First frame visible: snap directly to prediction (no sprint from ball)
        if (!markerWasVisible)
        {
            predictedStopMarker.position = target;
            markerWasVisible = true;
            return;
        }

        // Heavy follow (exponential smoothing)
        float a = 1f - Mathf.Exp(-predictedMarkerFollowHz * Time.unscaledDeltaTime);
        predictedStopMarker.position = Vector3.Lerp(predictedStopMarker.position, target, a);
    }


    void OnEnable()
    {
        // We'll wire events in Step 2 by exposing minimal hooks in RunScoring2D.
    }

    void OnDisable()
    {
    }

    // ---- Hooks we will call from RunScoring2D (Step 2) ----

    public void OnThrowStarted()
    {
        throwDistance = 0f;
        lastPos = grab.GetComponent<Rigidbody2D>().position;

        wallBouncesThisThrow = 0;
        leftHits = rightHits = topHits = bottomHits = 0;
        throwInFlight = true;
        wallFrenzyAwardedThisThrow = false;

        //Debug.Log("[ActionManager] Throw started");

    }

    public void OnRunStarted()
    {
        greedDoneThisRun = false;
        desperationDoneThisRun = false;
    }

    public void OnWallBounce(int wallId)
    {
        if (!throwInFlight) return;
        if (!scoring) return;

        wallBouncesThisThrow++;

        switch (wallId)
        {
            case 0: leftHits++; break;
            case 1: rightHits++; break;
            case 2: topHits++; break;
            case 3: bottomHits++; break;
        }

        if (wallFrenzyAwardedThisThrow) return;

        float fieldScale = scoring.GetFieldScaleForNormalization();
        if (fieldScale <= 0.0001f) return;

        float normDist = throwDistance / fieldScale;
        float requiredWalls = wallFrenzyMinWalls + (normDist * wallFrenzyWallsPerUnit);

        int total = wallBouncesThisThrow;
        int h = leftHits + rightHits;
        int v = topHits + bottomHits;

        int dominant = Mathf.Max(h, v);
        float share = (total > 0) ? (dominant / (float)total) : 1f;

        // Block obvious farming: too many hits on one axis
        if (share > wallFrenzyMaxDominantAxisShare)
            return;


        if (wallBouncesThisThrow >= requiredWalls)
        {
            wallFrenzyAwardedThisThrow = true;
            AwardActionDrop("WallFrenzy");
        }
    }

    public void OnPickup(bool wasCatch)
    {
        if (!throwInFlight)
            return;

        // --- Quick Catch (speed-based) ---
        if (wasCatch && grab != null)
        {
            float catchSpeed = grab.LastPickupVelocity.magnitude;

            if (catchSpeed >= quickCatchMinSpeed)
                AwardActionDrop("QuickCatch");
        }

        // --- Greed / Desperation ---
        if (IsPreLastThrowPickup() && scoring != null && grab != null)
        {
            float tStop = scoring.PredictTimeToStopFromSpeed(grab.LastPickupSpeed);

            // Only count if the pickup happened close to when the ball would stop
            if (tStop <= preLandingDecisionWindowSeconds)
            {
                Vector2 predictedStop = scoring.PredictStopPosition(grab.LastPickupPosition, grab.LastPickupVelocity);
                float predictedMult = scoring.GetLandingMultiplierAt(predictedStop);

                bool futureIsGood = predictedMult >= 1f;

                if (futureIsGood && !greedDoneThisRun)
                {
                    greedDoneThisRun = true;
                    AwardActionDrop("Greed");
                }
                else if (!futureIsGood && !desperationDoneThisRun)
                {
                    desperationDoneThisRun = true;
                    AwardActionDrop("Desperation");
                }
            }

            // Optional debug while tuning:
            //Debug.Log($"[GreedWindow] tStop={tStop:F2}s window={preLandingDecisionWindowSeconds:F2}s");
        }

        // End of this throw
        throwInFlight = false;
    }

    bool IsPreLastThrowPickup()
    {
        // This pickup happens after the second-to-last throw, before the final throw is taken.
        // At that moment, ThrowsLeft should be exactly 1.
        return scoring != null && scoring.RunActive && scoring.ThrowsLeft == 1;
    }

    public void OnRunEnded()
    {
        throwInFlight = false;

        if (predictedStopMarker && predictedStopMarker.gameObject.activeSelf)
            predictedStopMarker.gameObject.SetActive(false);

        markerWasVisible = false;
    }

    string Roll(DropEntry[] table)
    {
        if (table == null || table.Length == 0) return null;

        int total = 0;
        for (int i = 0; i < table.Length; i++)
            total += Mathf.Max(0, table[i].weight);

        if (total <= 0) return null;

        int r = Random.Range(0, total);
        for (int i = 0; i < table.Length; i++)
        {
            int w = Mathf.Max(0, table[i].weight);
            if (w == 0) continue;

            if (r < w) return table[i].id;
            r -= w;
        }

        return null;
    }

    Color GetActionColor(string actionId)
    {
        switch (actionId)
        {
            case "WallFrenzy": return COLOR_WALLFRENZY;
            case "QuickCatch": return COLOR_QUICKCATCH;
            case "Greed": return COLOR_GREED;
            case "Desperation": return COLOR_DESPERATION;
            default: return new Color32(255, 209, 102, 255); // fallback gold
        }
    }

    void AwardActionDrop(string actionId)
    {
        if (!inventory) return;

        DropEntry[] table = null;

        switch (actionId)
        {
            case "WallFrenzy": table = dropsWallFrenzy; break;
            case "QuickCatch": table = dropsQuickCatch; break;
            case "Greed": table = dropsGreed; break;
            case "Desperation": table = dropsDesperation; break;
        }

        string id = Roll(table);

        Vector2 where = ballRb ? ballRb.position : (grab ? (Vector2)grab.transform.position : Vector2.zero);
        Color actionColor = GetActionColor(actionId);

        if (string.IsNullOrEmpty(id))
        {
            if (scoring)
                scoring.AddActionWhiffXp(10);

            if (popups)
            {
                popups.PopAtWorldWithExtraOffset(where, $"{actionId}!", actionColor, new Vector2(0f, 0f));
                popups.PopAtWorldWithExtraOffset(where, $"+10 XP", COLOR_XP, new Vector2(0f, -60f));
            }

            Debug.Log($"[ActionManager] {actionId} → nothing");
            return;
        }

        inventory.Add(id, 1);

        string prettyName = id;
        if (db)
        {
            PowerupDefinition def = db.Get(id);
            if (def != null && !string.IsNullOrEmpty(def.displayName))
                prettyName = def.displayName;
        }

        if (popups)
        {
            popups.PopAtWorldWithExtraOffset(where, $"{actionId}!", actionColor, new Vector2(0f, 0f));
            popups.PopAtWorldWithExtraOffset(where, $"+1 {prettyName}", COLOR_ITEM, new Vector2(0f, -60f));
        }

        Debug.Log($"[ActionManager] {actionId} → +1 {id}");
    }
}