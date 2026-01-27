using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The brain of the level reward system.
/// Listens to XpManager.OnLevelUp and grants rewards from the database.
/// Tracks which rewards have been claimed to prevent duplicates.
/// </summary>
public class LevelRewardManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] XpManager xp;
    [SerializeField] LevelRewardDatabase rewardDatabase;

    [Header("Settings")]
    [SerializeField] bool debugLog = true;

    [Header("Testing")]
    [SerializeField] int testLevel = 5;
    [SerializeField] int teleportLevel = 50; // Inspector shortcut to jump to a level and auto-grant rewards

    // Track which level-ups have been processed to avoid granting twice
    HashSet<int> processedLevels = new HashSet<int>();
    const string PROCESSED_LEVELS_KEY = "LevelRewardManager_ProcessedLevels_v1";

    // Future: Fire this event so UI can show a reward screen
    public event Action<int, List<Reward>> OnLevelUpRewardsGranted;

    void Awake()
    {
        if (!xp) xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
        if (!rewardDatabase) rewardDatabase = FindFirstObjectByType<LevelRewardDatabase>(FindObjectsInactive.Include);

        // Load processed levels from save
        LoadProcessedLevels();

        // Retroactively grant any rewards below current level that weren't already granted
        RetroactivelyGrantMissedRewards();
    }

    void LoadProcessedLevels()
    {
        if (!PlayerPrefs.HasKey(PROCESSED_LEVELS_KEY)) return;

        var json = PlayerPrefs.GetString(PROCESSED_LEVELS_KEY, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var loaded = JsonUtility.FromJson<IntListWrapper>(json);
            if (loaded?.items != null)
            {
                processedLevels = new HashSet<int>(loaded.items);
            }
        }
        catch
        {
            Debug.LogWarning("[LevelRewardManager] Failed to load processed levels");
        }
    }

    void SaveProcessedLevels()
    {
        var list = new List<int>(processedLevels);
        var json = JsonUtility.ToJson(new IntListWrapper { items = list });
        PlayerPrefs.SetString(PROCESSED_LEVELS_KEY, json);
        PlayerPrefs.Save();
    }

    void RetroactivelyGrantMissedRewards()
    {
        if (!xp) return;

        int currentLevel = xp.Level;

        // Grant all levels from 1 to current level that haven't been processed
        for (int level = 1; level <= currentLevel; level++)
        {
            if (!processedLevels.Contains(level))
            {
                processedLevels.Add(level);
                GrantRewardsForLevel(level);
            }
        }

        SaveProcessedLevels();
    }

    void OnEnable()
    {
        if (xp) xp.OnLevelUp += HandleLevelUp;
    }

    void OnDisable()
    {
        if (xp) xp.OnLevelUp -= HandleLevelUp;
    }

    void HandleLevelUp(int newLevel)
    {
        // Don't grant the same level twice
        if (processedLevels.Contains(newLevel))
            return;

        processedLevels.Add(newLevel);
        GrantRewardsForLevel(newLevel);
        SaveProcessedLevels();
    }

    void GrantRewardsForLevel(int level)
    {
        if (!rewardDatabase)
        {
            Debug.LogWarning("[LevelRewardManager] No reward database assigned!");
            return;
        }

        var rewards = rewardDatabase.GetRewardsForLevel(level);

        if (rewards == null || rewards.Count == 0)
        {
            if (debugLog)
                Debug.Log($"[LevelRewards] Level {level} has no rewards configured.");
            return;
        }

        // Grant each reward
        foreach (var reward in rewards)
        {
            if (reward == null)
            {
                Debug.LogWarning("[LevelRewardManager] Null reward in database!");
                continue;
            }

            reward.Grant();

            if (debugLog)
                Debug.Log($"[LevelRewards] Level {level} - Granted: {reward.GetDebugLabel()}");
        }

        // Fire event for future UI reward screen
        OnLevelUpRewardsGranted?.Invoke(level, rewards);

        if (debugLog)
            Debug.Log($"[LevelRewards] === Level {level} Complete ({rewards.Count} rewards) ===");
    }

    /// <summary>
    /// Jump to a target level and grant all rewards up to that level.
    /// </summary>
    public void TeleportToLevel(int level)
    {
        if (!xp) xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
        if (!rewardDatabase) rewardDatabase = FindFirstObjectByType<LevelRewardDatabase>(FindObjectsInactive.Include);

        if (!xp)
        {
            Debug.LogWarning("[LevelRewardManager] No XP manager found!");
            return;
        }

        level = Mathf.Clamp(level, 1, XpCurveRS.MaxLevel);

        int beforeLevel = xp.Level;

        xp.SetLevelImmediate(level);

        // Grant any unprocessed rewards up to the new level.
        for (int lvl = 1; lvl <= level; lvl++)
        {
            if (!processedLevels.Contains(lvl))
            {
                processedLevels.Add(lvl);
                GrantRewardsForLevel(lvl);
            }
        }

        SaveProcessedLevels();

        if (debugLog)
            Debug.Log($"[LevelRewards] Teleported from level {beforeLevel} to {level} (granted missing rewards)");
    }

    /// <summary>
    /// Dev method: Manually grant rewards for a level (useful for testing).
    /// </summary>
    public void TestGrantLevel(int level)
    {
        // Clear the cache so it can be granted again
        processedLevels.Remove(level);
        GrantRewardsForLevel(level);
    }

    [ContextMenu("Teleport To Level (uses teleportLevel field)")]
    void TeleportToLevelFromInspector()
    {
        TeleportToLevel(teleportLevel);
    }

    [ContextMenu("Test Grant Level (uses testLevel field)")]
    void TestGrantLevelFromInspector()
    {
        TestGrantLevel(testLevel);
    }
}

// Helper for JSON serialization of processed levels
[System.Serializable]
public class IntListWrapper
{
    public List<int> items = new List<int>();
}
