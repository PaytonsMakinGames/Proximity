using UnityEngine;

/// <summary>
/// Abstract base class for all level-up rewards.
/// Each reward type knows how to grant itself.
/// </summary>
public abstract class Reward : ScriptableObject
{
    [Header("Identity")]
    public string rewardId;  // Unique identifier (e.g., "grant_sticky_ball_level_15")
    [TextArea(2, 4)]
    public string displayName;  // What to show the player (e.g., "Sticky Ball Unlock")
    [TextArea(3, 6)]
    public string description;  // Detailed text about the reward

    /// <summary>
    /// Grant this reward to the player.
    /// Called by LevelRewardManager when level-up threshold is met.
    /// </summary>
    public abstract void Grant();

    /// <summary>
    /// Optional: Called by LevelRewardManager for debug logging.
    /// </summary>
    public virtual string GetDebugLabel()
    {
        return displayName;
    }
}
