using UnityEngine;

public class LevelRewardGiver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] XpManager xp;
    [SerializeField] PlayerInventory inventory;

    [Header("Reward")]
    [SerializeField] int rewardLevel = 5;
    [SerializeField] string rewardItemId = "trail_xp_10";

    void Awake()
    {
        if (!xp) xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
        if (!inventory) inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
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
        if (!inventory) return;

        if (newLevel == rewardLevel)
        {
            inventory.Grant(rewardItemId);
        }
    }
}