using UnityEngine;

/// <summary>
/// Grants a powerup unlock to the player.
/// The powerup becomes available in the drop tables and UI.
/// </summary>
[CreateAssetMenu(menuName = "Game/Level Rewards/Powerup Unlock")]
public class PowerupUnlockReward : Reward
{
    [Header("Powerup Settings")]
    [SerializeField] string powerupId;  // Must match PowerupDefinition.id (e.g., "sticky_ball")
    [SerializeField] int initialCount = 1;  // How many copies to grant

    void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = "Unlock: " + powerupId;
    }

    public override void Grant()
    {
        var manager = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);
        if (!manager)
        {
            Debug.LogWarning($"[PowerupUnlockReward] No PowerupManager found; cannot unlock {powerupId}");
            return;
        }

        var inventory = FindFirstObjectByType<PowerupInventory>(FindObjectsInactive.Include);
        if (!inventory)
        {
            Debug.LogWarning($"[PowerupUnlockReward] No PowerupInventory found; cannot grant {powerupId}");
            return;
        }

        // Unlock the powerup first
        manager.UnlockPowerup(powerupId);

        // Then grant initial copies
        if (initialCount > 0)
        {
            inventory.Add(powerupId, initialCount);
            Debug.Log($"[LevelRewards] Granted powerup: {powerupId} x{initialCount}");
        }
        else
        {
            Debug.Log($"[LevelRewards] Unlocked powerup: {powerupId} (no initial copies)");
        }
    }

    public override string GetDebugLabel()
    {
        return $"Powerup: {powerupId}";
    }
}
