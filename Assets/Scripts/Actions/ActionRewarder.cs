using UnityEngine;

/// <summary>
/// Handles powerup rewards from action triggers.
/// Separate from ActionDetector for cleaner responsibility separation.
/// </summary>
public class ActionRewarder : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] PowerupInventory inventory;
    [SerializeField] PowerupDatabase db;
    [SerializeField] RunScoring2D scoring;
    [SerializeField] FloatingPopupSystem popups;
    [SerializeField] Rigidbody2D ballRb;

    [System.Serializable]
    public struct DropEntry
    {
        public string id;
        [Min(1)] public int weight;
    }

    [Header("Drop Tables")]
    [SerializeField] DropEntry[] dropsWallFrenzy;
    [SerializeField] DropEntry[] dropsQuickCatch;
    [SerializeField] DropEntry[] dropsGreed;
    [SerializeField] DropEntry[] dropsDesperation;
    [SerializeField] DropEntry[] dropsEdgeCase;

    [Header("Reward Colors")]
    [SerializeField] Color rewardXpColor = new Color32(143, 179, 200, 255);      // XP/distance bonus
    [SerializeField] Color rewardItemColor = new Color32(124, 255, 178, 255);    // Item drop

    [Header("Action Colors")]
    [SerializeField] Color wallFrenzyColor = new Color32(165, 85, 50, 255);      // Brick/terracotta
    [SerializeField] Color quickCatchColor = new Color32(240, 240, 240, 255);    // White (baseball)
    [SerializeField] Color greedColor = new Color32(255, 215, 0, 255);           // Rich gold
    [SerializeField] Color desperationColor = new Color32(70, 100, 200, 255);    // Deep blue
    [SerializeField] Color edgeCaseColor = new Color32(140, 150, 160, 255);      // Slate gray (precision/technical)

    [Header("Reward Variety")]
    [SerializeField, Min(1)] int rewardHistoryWindow = 10;        // Track last N rewards
    [SerializeField, Min(1)] int maxDuplicateInWindow = 2;        // Max times same powerup in window

    // History of recent rewards (stores powerup IDs that were actually awarded)
    System.Collections.Generic.List<string> rewardHistory = new System.Collections.Generic.List<string>();

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<PowerupInventory>(FindObjectsInactive.Include);
        if (!db) db = FindFirstObjectByType<PowerupDatabase>(FindObjectsInactive.Include);
        if (!scoring) scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);
        if (!popups) popups = FindFirstObjectByType<FloatingPopupSystem>(FindObjectsInactive.Include);
        if (!ballRb) ballRb = FindFirstObjectByType<Rigidbody2D>(FindObjectsInactive.Include);
    }

    // Check if a powerup would exceed the duplicate limit in recent history
    bool WouldExceedDuplicateLimit(string powerupId)
    {
        if (string.IsNullOrEmpty(powerupId)) return false; // Whiffs don't count toward limit

        int count = 0;
        for (int i = Mathf.Max(0, rewardHistory.Count - rewardHistoryWindow); i < rewardHistory.Count; i++)
        {
            if (rewardHistory[i] == powerupId)
                count++;
        }
        return count >= maxDuplicateInWindow;
    }

    // Add a reward to history (including whiffs)
    void AddToRewardHistory(string powerupId)
    {
        // Add everything to history (null/empty for whiffs, powerup ID for rewards)
        // This way whiffs fill the window and push old powerups out
        rewardHistory.Add(powerupId);

        // Trim old entries if over window size
        if (rewardHistory.Count > rewardHistoryWindow)
            rewardHistory.RemoveAt(0);
    }

    public void AwardAction(string actionId)
    {
        if (!inventory) return;

        // Check if action is unlocked
        var detector = FindFirstObjectByType<ActionDetector>(FindObjectsInactive.Include);
        if (detector && !detector.IsActionUnlocked(actionId))
        {
            // Action not unlocked yet, don't award
            return;
        }

        DropEntry[] table = null;

        switch (actionId)
        {
            case "WallFrenzy": table = dropsWallFrenzy; break;
            case "QuickCatch": table = dropsQuickCatch; break;
            case "Greed": table = dropsGreed; break;
            case "Desperation": table = dropsDesperation; break;
            case "EdgeCase": table = dropsEdgeCase; break;
        }

        if (table == null || table.Length == 0) return;

        // Filter table to only include unlocked powerups
        var manager = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);
        DropEntry[] filteredTable = FilterUnlockedPowerups(table, manager);
        if (filteredTable == null || filteredTable.Length == 0) return;

        string id = Roll(filteredTable);

        Vector2 where = ballRb ? ballRb.position : Vector2.zero;
        Color actionColor = GetActionColor(actionId);

        if (string.IsNullOrEmpty(id))
        {
            if (scoring) scoring.AddActionWhiffXp(50);
            AddToRewardHistory(null); // Add whiff to history to fill the window

            if (popups)
            {
                string actionName = GetActionDisplayName(actionId);
                string actionHex = GetActionColorHex(actionId);
                string xpHex = ColorToHex(rewardXpColor);
                popups.PopAtWorld(where, $"<color=#{actionHex}>{actionName}</color>\n<color=#{xpHex}>+50</color>", Color.white);
            }
            return;
        }

        inventory.Add(id, 1);
        AddToRewardHistory(id);

        string prettyName = id;
        if (db)
        {
            var def = db.Get(id);
            if (def != null && !string.IsNullOrEmpty(def.displayName))
                prettyName = def.displayName;
        }

        if (popups)
        {
            string actionName = GetActionDisplayName(actionId);
            string actionHex = GetActionColorHex(actionId);
            string itemHex = ColorToHex(manager ? manager.GetPowerupColor(id) : rewardItemColor);
            popups.PopAtWorld(where, $"<color=#{actionHex}>{actionName}</color>\n<color=#{itemHex}>+1 {prettyName}</color>", Color.white);
        }
    }

    public void AwardEdgeCase(float throwDistance, float closeness01)
    {
        if (!inventory) return;

        // Don't reward Edge Case if ball is stuck via Sticky Ball (they're mutually exclusive mechanics)
        if (scoring && scoring.StickyBallPinned) return;

        // Check if Edge Case action is unlocked
        var detector = FindFirstObjectByType<ActionDetector>(FindObjectsInactive.Include);
        if (detector && !detector.IsActionUnlocked("EdgeCase"))
        {
            return;
        }

        DropEntry[] table = dropsEdgeCase;
        if (table == null || table.Length == 0) return;

        // Filter table to only include unlocked powerups
        var manager = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);
        table = FilterUnlockedPowerups(table, manager);

        Vector2 where = ballRb ? ballRb.position : Vector2.zero;
        int distanceBonus = scoring ? scoring.AwardEdgeCaseDistanceLikeNormal(throwDistance, where, closeness01) : Mathf.RoundToInt(throwDistance);
        string actionHex = "96C8FF";  // Edge Case color
        string xpHex = ColorToHex(rewardXpColor);

        if (table == null || table.Length == 0)
        {
            // No unlocked powerups, just give distance bonus
            if (popups)
            {
                popups.PopAtWorld(where, $"<color=#{actionHex}>Edge Case!</color>\n<color=#{xpHex}>+{distanceBonus} XP</color>", Color.white);
            }
            return;
        }

        string id = Roll(table);
        string itemHex = ColorToHex(rewardItemColor);

        if (string.IsNullOrEmpty(id))
        {
            // No drop - just distance bonus (single popup with newline)
            AddToRewardHistory(null); // Add whiff to history to fill the window
            if (popups)
            {
                popups.PopAtWorld(where, $"<color=#{actionHex}>Edge Case!</color>\n<color=#{xpHex}>+{distanceBonus} XP</color>", Color.white);
            }
            return;
        }

        // Got a drop (single popup with both lines)
        inventory.Add(id, 1);
        AddToRewardHistory(id);

        string prettyName = id;
        if (db)
        {
            var def = db.Get(id);
            if (def != null && !string.IsNullOrEmpty(def.displayName))
                prettyName = def.displayName;
        }

        if (popups)
        {
            // Get powerup color from manager instead of using default reward color
            var powerupManager = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);
            itemHex = ColorToHex(powerupManager ? powerupManager.GetPowerupColor(id) : rewardItemColor);
            popups.PopAtWorld(where, $"<color=#{actionHex}>Edge Case!</color>\n<color=#{itemHex}>+1 {prettyName}</color>\n<color=#{xpHex}>+{distanceBonus} XP</color>", Color.white);
        }
    }

    string Roll(DropEntry[] table)
    {
        if (table == null || table.Length == 0) return null;

        // Try to roll without hitting duplicate limit
        string id = RollOnce(table);

        // If result would exceed duplicate limit, try to find alternative
        if (WouldExceedDuplicateLimit(id))
        {
            // Try up to 5 times to get a non-duplicate
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string altId = RollOnce(table);
                if (!WouldExceedDuplicateLimit(altId))
                {
                    id = altId;
                    break;
                }
            }
        }

        return id;
    }

    // Roll once without duplicate checking
    string RollOnce(DropEntry[] table)
    {
        if (table == null || table.Length == 0) return null;

        int total = 0;
        foreach (var e in table)
            total += e.weight;

        if (total <= 0) return null;

        int roll = Random.Range(0, total);
        int sum = 0;

        foreach (var e in table)
        {
            sum += e.weight;
            if (roll < sum)
                return e.id;
        }

        return table.Length > 0 ? table[0].id : null;
    }

    Color GetActionColor(string actionId)
    {
        switch (actionId)
        {
            case "WallFrenzy": return wallFrenzyColor;
            case "QuickCatch": return quickCatchColor;
            case "Greed": return greedColor;
            case "Desperation": return desperationColor;
            case "EdgeCase": return edgeCaseColor;
            default: return rewardItemColor;
        }
    }

    string GetActionDisplayName(string actionId)
    {
        switch (actionId)
        {
            case "WallFrenzy": return "Wall Frenzy!";
            case "QuickCatch": return "Quick Catch!";
            case "Greed": return "Greed!";
            case "Desperation": return "Desperation!";
            case "EdgeCase": return "Edge Case!";
            default: return "Action?";
        }
    }

    string GetActionColorHex(string actionId)
    {
        return ColorToHex(GetActionColor(actionId));
    }

    string ColorToHex(Color color)
    {
        Color32 c = color;
        return c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
    }

    /// <summary>
    /// Filter drop table to only include unlocked powerups.
    /// </summary>
    DropEntry[] FilterUnlockedPowerups(DropEntry[] table, PowerupManager manager)
    {
        if (table == null || table.Length == 0) return table;
        if (!manager) return table; // If no manager, don't filter (shouldn't happen)

        var filtered = new System.Collections.Generic.List<DropEntry>();
        foreach (var entry in table)
        {
            // Empty ID means "whiff" (no drop), always allow
            if (string.IsNullOrEmpty(entry.id))
            {
                filtered.Add(entry);
                continue;
            }

            // Only include if powerup is unlocked
            if (manager.IsPowerupUnlocked(entry.id))
            {
                filtered.Add(entry);
            }
        }

        return filtered.ToArray();
    }
}
