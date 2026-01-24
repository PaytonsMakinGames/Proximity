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

    static readonly Color COLOR_WALLFRENZY = new Color32(255, 140, 60, 255);
    static readonly Color COLOR_QUICKCATCH = new Color32(90, 220, 255, 255);
    static readonly Color COLOR_GREED = new Color32(255, 200, 90, 255);
    static readonly Color COLOR_DESPERATION = new Color32(180, 140, 255, 255);
    static readonly Color COLOR_EDGECASE = new Color32(150, 200, 255, 255);
    static readonly Color COLOR_ITEM = new Color32(124, 255, 178, 255);
    static readonly Color COLOR_XP = new Color32(143, 179, 200, 255);

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<PowerupInventory>(FindObjectsInactive.Include);
        if (!db) db = FindFirstObjectByType<PowerupDatabase>(FindObjectsInactive.Include);
        if (!scoring) scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);
        if (!popups) popups = FindFirstObjectByType<FloatingPopupSystem>(FindObjectsInactive.Include);
        if (!ballRb) ballRb = FindFirstObjectByType<Rigidbody2D>(FindObjectsInactive.Include);
    }

    public void AwardAction(string actionId)
    {
        if (!inventory) return;

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

        string id = Roll(table);

        Vector2 where = ballRb ? ballRb.position : Vector2.zero;
        Color actionColor = GetActionColor(actionId);

        if (string.IsNullOrEmpty(id))
        {
            if (scoring) scoring.AddActionWhiffXp(50);

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
            string itemHex = ColorToHex(rewardItemColor);
            popups.PopAtWorld(where, $"<color=#{actionHex}>{actionName}</color>\n<color=#{itemHex}>+1 {prettyName}</color>", Color.white);
        }
    }

    public void AwardEdgeCase(float throwDistance, float closeness01)
    {
        if (!inventory) return;

        DropEntry[] table = dropsEdgeCase;
        if (table == null || table.Length == 0) return;

        string id = Roll(table);
        Vector2 where = ballRb ? ballRb.position : Vector2.zero;
        int distanceBonus = scoring ? scoring.AwardEdgeCaseDistanceLikeNormal(throwDistance, where, closeness01) : Mathf.RoundToInt(throwDistance);

        string actionHex = "96C8FF";  // Edge Case color
        string xpHex = ColorToHex(rewardXpColor);
        string itemHex = ColorToHex(rewardItemColor);

        if (string.IsNullOrEmpty(id))
        {
            // No drop - just distance bonus (single popup with newline)
            if (popups)
            {
                popups.PopAtWorld(where, $"<color=#{actionHex}>Edge Case!</color>\n<color=#{xpHex}>+{distanceBonus}d</color>", Color.white);
            }
            return;
        }

        // Got a drop (single popup with both lines)
        inventory.Add(id, 1);

        string prettyName = id;
        if (db)
        {
            var def = db.Get(id);
            if (def != null && !string.IsNullOrEmpty(def.displayName))
                prettyName = def.displayName;
        }

        if (popups)
        {
            popups.PopAtWorld(where, $"<color=#{actionHex}>Edge Case!</color>\n<color=#{itemHex}>+1 {prettyName}</color>\n<color=#{xpHex}>+{distanceBonus}d</color>", Color.white);
        }
    }

    string Roll(DropEntry[] table)
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
            case "WallFrenzy": return COLOR_WALLFRENZY;
            case "QuickCatch": return COLOR_QUICKCATCH;
            case "Greed": return COLOR_GREED;
            case "Desperation": return COLOR_DESPERATION;
            case "EdgeCase": return COLOR_EDGECASE;
            default: return COLOR_ITEM;
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
        switch (actionId)
        {
            case "WallFrenzy": return "FF8C3C";
            case "QuickCatch": return "5ADCFF";
            case "Greed": return "FFC85A";
            case "Desperation": return "B48CFF";
            case "EdgeCase": return "96C8FF";
            default: return "FFFFFF";
        }
    }

    string ColorToHex(Color color)
    {
        Color32 c = color;
        return c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
    }
}
