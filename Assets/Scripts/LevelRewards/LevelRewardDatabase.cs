using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized database of all level-up rewards (levels 1-99).
/// Create one instance and assign it to LevelRewardManager.
/// Easy to edit in the Inspector: just add levels and drag rewards into each list.
/// </summary>
[CreateAssetMenu(menuName = "Game/Level Rewards/Level Reward Database")]
public class LevelRewardDatabase : ScriptableObject
{
    [SerializeField] public List<LevelRewardEntry> levelRewards = new List<LevelRewardEntry>();

    /// <summary>
    /// Get all rewards for a specific level.
    /// Returns an empty list if the level has no entries.
    /// </summary>
    public List<Reward> GetRewardsForLevel(int level)
    {
        foreach (var entry in levelRewards)
        {
            if (entry.level == level)
            {
                return entry.rewards;
            }
        }

        // Level not found; return empty list
        return new List<Reward>();
    }

    /// <summary>
    /// Check if a level has any rewards defined.
    /// </summary>
    public bool HasRewardsForLevel(int level)
    {
        var rewards = GetRewardsForLevel(level);
        return rewards != null && rewards.Count > 0;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Empty Entries (1-40)")]
    void GenerateEmptyEntries_1to40()
    {
        levelRewards.Clear();
        for (int i = 1; i <= 40; i++)
        {
            levelRewards.Add(new LevelRewardEntry(i));
        }
        Debug.Log("[LevelRewardDatabase] Generated 40 empty level entries (1-40).");
    }
    
    [ContextMenu("Generate Empty Entries (1-99)")]
    void GenerateEmptyEntries_1to99()
    {
        levelRewards.Clear();
        for (int i = 1; i <= 99; i++)
        {
            levelRewards.Add(new LevelRewardEntry(i));
        }
        Debug.Log("[LevelRewardDatabase] Generated 99 empty level entries (1-99).");
    }
#endif
}
