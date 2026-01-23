using System;
using UnityEngine;

public class PowerupManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] PowerupDatabase db;
    [SerializeField] PowerupInventory inventory;
    [SerializeField] FloatingPopupSystem popups;

    [Header("Landing Amplifier (v1)")]
    [SerializeField] string landingAmplifierId = "landing_amplifier";
    [SerializeField] float landingAmpExponent = 5.5f;
    [SerializeField] float landingAmpEdgeMult = 2.0f;
    [SerializeField] float landingAmpCornerMult = 1.5f;
    [SerializeField] float landingAmpMaxMultiplier = 6.0f;
    [SerializeField] Color landingAmpPopupColor = new Color(0.85f, 0.9f, 1f, 1f);

    [Header("Insurance (v1)")]
    [SerializeField] string insuranceId = "insurance";
    [SerializeField] Color insurancePopupColor = new Color(0.85f, 1f, 0.85f, 1f);

    [Header("Sticky Ball (v1)")]
    [SerializeField] string stickyBallId = "sticky_ball";

    [Header("Hot Spot (v1)")]
    [SerializeField] string hotSpotId = "hot_spot";
    [SerializeField] int hotSpotDistancePerHit = 50;
    [SerializeField] Color hotSpotColor = new Color(1f, 0.75f, 0.75f, 1f);

    [Header("Overtime (v1)")]
    [SerializeField] string overtimeId = "overtime";
    [SerializeField] float overtimeRampMidTime = 4f;       // At 4s: +25%
    [SerializeField] float overtimeMaxMultiplier = 0.5f;   // Caps at +50%
    [SerializeField] Color overtimePopupColor = new Color(1f, 0.85f, 0.5f, 1f);

    [Header("Encore")]
    [SerializeField] Color encorePopupColor = new Color(1f, 0.9f, 0.75f, 1f);

    // Runtime state: which powerups are "active this throw"
    bool landingAmpActiveThisThrow;
    bool insuranceActiveThisThrow;
    bool stickyThrowActive;
    bool hotSpotUsedThisRun;
    bool hotSpotSpawnedThisRun;
    bool hotSpotJustSpawnedThisThrow;  // True only the throw Hot Spot is first triggered
    bool overtimeActiveThisRun;
    bool overtimeUsedThisRun;
    float overtimeElapsed;  // Total time spent in-air across all throws this run while moving
    // Saved overtime state for Encore revive window
    bool overtimeSavedActive;
    bool overtimeSavedUsed;
    float overtimeSavedElapsed;
    bool encoreUsedThisRun;
    bool encoreReviveUsedThisRun;

    public bool LandingAmpActiveThisThrow => landingAmpActiveThisThrow;
    public bool InsuranceActiveThisThrow => insuranceActiveThisThrow;
    public bool StickyThrowActive => stickyThrowActive;
    public bool HotSpotUsedThisRun => hotSpotUsedThisRun;
    public bool HotSpotSpawnedThisRun => hotSpotSpawnedThisRun;
    public bool HotSpotJustSpawnedThisThrow => hotSpotJustSpawnedThisThrow;
    public bool OvertimeActiveThisRun => overtimeActiveThisRun;
    public bool OvertimeUsedThisRun => overtimeUsedThisRun;
    public bool EncoreAnyUsedThisRun => encoreUsedThisRun || encoreReviveUsedThisRun;

    public event Action OnArmedChanged;

    public string ArmedId { get; private set; } = null;

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<PowerupInventory>(FindObjectsInactive.Include);
        if (!popups) popups = FindFirstObjectByType<FloatingPopupSystem>(FindObjectsInactive.Include);
    }

    public bool HasArmed => !string.IsNullOrEmpty(ArmedId);

    public PowerupDefinition GetArmedDef()
    {
        if (!db || string.IsNullOrEmpty(ArmedId)) return null;
        return db.Get(ArmedId);
    }

    // Manual arming. DOES NOT consume.
    public bool TryArm(string powerupId)
    {
        if (string.IsNullOrEmpty(powerupId)) return false;
        if (!inventory) return false;

        if (inventory.GetCount(powerupId) <= 0)
            return false;

        ArmedId = powerupId;
        OnArmedChanged?.Invoke();
        return true;
    }

    public void Disarm_NoConsume()
    {
        if (string.IsNullOrEmpty(ArmedId)) return;
        ArmedId = null;
        OnArmedChanged?.Invoke();
    }

    // Called by RunScoring2D when a run ends for any reason.
    public void OnRunEnded()
    {
        Disarm_NoConsume();

        // Reset all "active this throw" flags for next run
        landingAmpActiveThisThrow = false;
        insuranceActiveThisThrow = false;
        stickyThrowActive = false;
        hotSpotUsedThisRun = false;
        hotSpotSpawnedThisRun = false;
        hotSpotJustSpawnedThisThrow = false;

        // Preserve overtime state in case an Encore revive happens immediately after run end
        overtimeSavedActive = overtimeActiveThisRun;
        overtimeSavedUsed = overtimeUsedThisRun;
        overtimeSavedElapsed = overtimeElapsed;

        overtimeActiveThisRun = false;
        overtimeUsedThisRun = false;
        overtimeElapsed = 0f;
    }

    // Called by RunScoring2D when a new run starts. Reset Encore usage here so
    // it cannot be used twice across a run end + revive window.
    public void OnRunStarted()
    {
        encoreUsedThisRun = false;
        encoreReviveUsedThisRun = false;
        // DO NOT reset Overtime here; it should persist if already armed/active in previous throw

        overtimeSavedActive = false;
        overtimeSavedUsed = false;
        overtimeSavedElapsed = 0f;
    }

    // Called by RunScoring2D when a new throw is released.
    public void OnThrowReleased(Vector2 ballWorldPos, bool isEncoreRevive = false)
    {
        if (!HasArmed) return;
        if (!db || !inventory) return;

        var def = db.Get(ArmedId);
        if (!def || def.trigger != PowerupTrigger.NextThrowRelease) return;

        // Landing Amplifier
        if (def.id == landingAmplifierId)
        {
            if (TryConsumeIfArmedMatches(PowerupTrigger.NextThrowRelease))
            {
                landingAmpActiveThisThrow = true;
                if (popups)
                    popups.PopAtWorldWithExtraOffset(ballWorldPos, "Landing Amp!", landingAmpPopupColor, new Vector2(0f, 0f));
            }
        }
        // Insurance
        else if (def.id == insuranceId)
        {
            if (TryConsumeIfArmedMatches(PowerupTrigger.NextThrowRelease))
            {
                insuranceActiveThisThrow = true;
                if (popups)
                {
                    popups.PopAtWorldWithExtraOffset(ballWorldPos, "Insurance!", insurancePopupColor, new Vector2(0f, 0f));
                    popups.PopAtWorldWithExtraOffset(ballWorldPos, "0x Blocked", insurancePopupColor, new Vector2(0f, -60f));
                }
            }
        }
        // Hot Spot
        else if (def.id == hotSpotId)
        {
            // Allow spawning if not currently active (even if one was used earlier this run)
            if (!hotSpotSpawnedThisRun)
            {
                if (TryConsumeIfArmedMatches(PowerupTrigger.NextThrowRelease))
                {
                    hotSpotSpawnedThisRun = true;
                    hotSpotJustSpawnedThisThrow = true;
                    // Popup will be handled by RunScoring2D with proper positioning
                }
            }
            else
            {
                Disarm_NoConsume();
            }
        }
        // Encore
        else if (def.id == "encore")
        {
            // Only one Encore per run (either mid-run +1 throw OR revive, not both)
            if (EncoreAnyUsedThisRun)
            {
                Disarm_NoConsume();
                return;
            }

            if (TryConsumeIfArmedMatches(PowerupTrigger.NextThrowRelease))
            {
                if (isEncoreRevive)
                    encoreReviveUsedThisRun = true;
                else
                    encoreUsedThisRun = true;

                if (popups)
                {
                    popups.PopAtWorldWithExtraOffset(ballWorldPos, "Encore!", encorePopupColor, new Vector2(0f, 0f));
                    popups.PopAtWorldWithExtraOffset(ballWorldPos, isEncoreRevive ? "Run Saved" : "+1 Throw", encorePopupColor, new Vector2(0f, -60f));
                }

                // If this was a revive, restore any saved overtime state so visuals/multiplier continue
                if (isEncoreRevive && overtimeSavedUsed)
                {
                    overtimeActiveThisRun = overtimeSavedActive;
                    overtimeUsedThisRun = overtimeSavedUsed;
                    overtimeElapsed = overtimeSavedElapsed;
                }
            }
        }
        // Overtime
        else if (def.id == overtimeId)
        {
            if (overtimeUsedThisRun)
            {
                Disarm_NoConsume();
                return;
            }

            if (TryConsumeIfArmedMatches(PowerupTrigger.NextThrowRelease))
            {
                // Start counting only from the moment Overtime is activated (not from run start)
                overtimeElapsed = 0f;
                overtimeActiveThisRun = true;
                overtimeUsedThisRun = true;

                // Clear any prior saved overtime snapshot; we're starting fresh
                overtimeSavedActive = false;
                overtimeSavedUsed = false;
                overtimeSavedElapsed = 0f;
                // Don't reset elapsed - accumulates across all throws in the run
                if (popups)
                    popups.PopAtWorldWithExtraOffset(ballWorldPos, "Overtime!", overtimePopupColor, new Vector2(0f, 0f));
            }
        }
    }

    // Called by RunScoring2D when a pickup happens.
    public void OnPickupHappened()
    {
        landingAmpActiveThisThrow = false;
        insuranceActiveThisThrow = false;
        hotSpotJustSpawnedThisThrow = false;
    }

    // Called by RunScoring2D when hot spot is exhausted or disappears
    public void DisableHotSpot()
    {
        hotSpotSpawnedThisRun = false;
    }

    // Called by RunScoring2D to track sticky throw state (for anti-exploit logic).
    public void SetStickyThrowActive(bool active)
    {
        stickyThrowActive = active;
    }

    // Query methods for scoring logic
    public float GetLandingAmpExponent() => landingAmpExponent;
    public float GetLandingAmpEdgeMult() => landingAmpEdgeMult;
    public float GetLandingAmpCornerMult() => landingAmpCornerMult;
    public float GetLandingAmpMaxMultiplier() => landingAmpMaxMultiplier;
    public int GetHotSpotDistancePerHit() => hotSpotDistancePerHit;
    public string GetStickyBallId() => stickyBallId;
    public string GetHotSpotId() => hotSpotId;
    public float GetOvertimeMaxBonus() => overtimeMaxMultiplier;

    public (bool active, bool used, float elapsed) GetOvertimeSnapshot()
    {
        return (overtimeActiveThisRun, overtimeUsedThisRun, overtimeElapsed);
    }

    public void RestoreOvertimeSnapshot(bool active, bool used, float elapsed)
    {
        overtimeActiveThisRun = active;
        overtimeUsedThisRun = used;
        overtimeElapsed = elapsed;
    }

    // Tick overtime timer only while ball is not held.
    public void TickOvertime(bool isHeld)
    {
        if (!overtimeActiveThisRun) return;
        if (isHeld) return;

        overtimeElapsed += Time.deltaTime;
    }

    // Calculate Overtime multiplier based on flight time
    public float GetOvertimeMultiplier()
    {
        if (!overtimeActiveThisRun) return 1f;

        // Linear ramp: progress from 0% to max% based on elapsed time vs ramp time
        float t = Mathf.Clamp01(overtimeElapsed / overtimeRampMidTime);
        float mult = t * overtimeMaxMultiplier;

        return 1f + mult;
    }

    // Public method for spending a powerup (used by RunScoring2D for Sticky Ball)
    public bool TrySpend(string powerupId, int count)
    {
        if (!inventory) return false;
        return inventory.TrySpend(powerupId, count);
    }

    bool TryConsumeIfArmedMatches(PowerupTrigger trigger)
    {
        if (!HasArmed) return false;
        if (!db || !inventory) return false;

        var def = db.Get(ArmedId);
        if (!def) return false;

        if (def.trigger != trigger)
            return false;

        bool spent = inventory.TrySpend(def.id, 1);
        if (!spent) return false;

        ArmedId = null;
        OnArmedChanged?.Invoke();
        return true;
    }
}
