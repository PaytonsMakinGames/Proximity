using UnityEngine;

/// <summary>
/// Grants a cosmetic item (ball skin, trail, etc.) to the player.
/// Uses the existing PlayerInventory.Grant() system.
/// </summary>
[CreateAssetMenu(menuName = "Game/Level Rewards/Cosmetic Item")]
public class CosmeticReward : Reward
{
    [Header("Cosmetic Settings")]
    [SerializeField] string itemId;  // Must match ItemDef.id in ItemDatabase

    void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = "Unlock: " + itemId;
    }

    public override void Grant()
    {
        var inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (!inventory)
        {
            Debug.LogWarning($"[CosmeticReward] No PlayerInventory found; cannot grant {itemId}");
            return;
        }

        // Only grant if not already owned
        if (!inventory.Owns(itemId))
        {
            inventory.Grant(itemId);
            Debug.Log($"[LevelRewards] Granted cosmetic: {itemId}");
        }
        else
        {
            Debug.Log($"[LevelRewards] Player already owns cosmetic: {itemId}");
        }
    }

    public override string GetDebugLabel()
    {
        return $"Cosmetic: {itemId}";
    }
}
