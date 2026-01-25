using UnityEngine;

/// <summary>
/// Grants an action unlock to the player.
/// The action becomes available in their action progression system.
/// </summary>
[CreateAssetMenu(menuName = "Game/Level Rewards/Action Unlock")]
public class ActionUnlockReward : Reward
{
    [Header("Action Settings")]
    [SerializeField] string actionId;  // Must match action IDs: WallFrenzy, QuickCatch, Greed, Desperation, EdgeCase

    void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = "Unlock: " + actionId;
    }

    public override void Grant()
    {
        var actionDetector = FindFirstObjectByType<ActionDetector>(FindObjectsInactive.Include);
        if (!actionDetector)
        {
            Debug.LogWarning($"[ActionUnlockReward] No ActionDetector found; cannot unlock {actionId}");
            return;
        }

        // Unlock the action if the system has an unlock method
        // (If ActionDetector doesn't have this yet, you may need to add it)
        actionDetector.UnlockAction(actionId);
        Debug.Log($"[LevelRewards] Unlocked action: {actionId}");
    }

    public override string GetDebugLabel()
    {
        return $"Action: {actionId}";
    }
}
