using System;
using UnityEngine;

public class PowerupManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] PowerupDatabase db;
    [SerializeField] PowerupInventory inventory;

    public event Action OnArmedChanged; // for UI later

    public string ArmedId { get; private set; } = null;

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<PowerupInventory>(FindObjectsInactive.Include);
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

        // Swap: old one simply becomes un-armed (no consumption).
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
        // Rule: armed but unused returns to inventory (meaning: do nothing except unarm).
        Disarm_NoConsume();
    }

    // ---------------- Trigger entry points (we’ll wire these in later) ----------------
    // These are intentionally "skeleton" methods: they only enforce consumption rules and clear armed state.
    // Actual gameplay effects get implemented in later steps.

    public bool TryTrigger_NextWallContact()
    {
        return TryConsumeIfArmedMatches(PowerupTrigger.NextWallContact);
    }

    public bool TryTrigger_NextLandingEval()
    {
        return TryConsumeIfArmedMatches(PowerupTrigger.NextLandingEval);
    }

    public bool TryTrigger_MissedCatchRetro()
    {
        return TryConsumeIfArmedMatches(PowerupTrigger.MissedCatchRetro);
    }

    public bool TryTrigger_EndOfRunOffer()
    {
        return TryConsumeIfArmedMatches(PowerupTrigger.EndOfRunOffer);
    }

    public bool TryTrigger_NextThrowRelease()
    {
        return TryConsumeIfArmedMatches(PowerupTrigger.NextThrowRelease);
    }

    bool TryConsumeIfArmedMatches(PowerupTrigger trigger)
    {
        if (!HasArmed) return false;
        if (!db || !inventory) return false;

        var def = db.Get(ArmedId);
        if (!def) return false;

        if (def.trigger != trigger)
            return false;

        // Consume ONLY when it actually triggers.
        bool spent = inventory.TrySpend(def.id, 1);
        if (!spent) return false;

        ArmedId = null;
        OnArmedChanged?.Invoke();
        return true;
    }
}