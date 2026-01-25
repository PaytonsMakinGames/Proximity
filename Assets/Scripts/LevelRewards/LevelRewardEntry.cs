using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Associates a level with the rewards that should be granted on that level.
/// Serializable so it appears in the Inspector as an array.
/// </summary>
[System.Serializable]
public class LevelRewardEntry
{
    [SerializeField] public int level;  // The level that triggers these rewards (e.g., 1, 5, 10)
    [SerializeField] public List<Reward> rewards = new List<Reward>();  // The rewards to grant

    public LevelRewardEntry(int lvl)
    {
        level = lvl;
        rewards = new List<Reward>();
    }
}
