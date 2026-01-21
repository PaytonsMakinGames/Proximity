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

    static readonly Color COLOR_WALLFRENZY = new Color32(255, 140, 60, 255);
    static readonly Color COLOR_QUICKCATCH = new Color32(90, 220, 255, 255);
    static readonly Color COLOR_GREED = new Color32(255, 200, 90, 255);
    static readonly Color COLOR_DESPERATION = new Color32(180, 140, 255, 255);
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
                popups.PopAtWorldWithExtraOffset(where, $"{actionName}!", actionColor, Vector2.zero);
                popups.PopAtWorldWithExtraOffset(where, $"+50", COLOR_XP, new Vector2(0f, -60f));
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
            popups.PopAtWorldWithExtraOffset(where, $"{actionName}!", actionColor, Vector2.zero);
            popups.PopAtWorldWithExtraOffset(where, $"+1 {prettyName}", COLOR_ITEM, new Vector2(0f, -60f));
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
            default: return "Action?";
        }
    }
}
