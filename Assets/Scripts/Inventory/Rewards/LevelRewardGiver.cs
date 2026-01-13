using UnityEngine;

public class LevelRewardGiver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] XpManager xp;
    [SerializeField] PlayerInventory inventory;

    [Header("Reward")]
    [SerializeField] int rewardLevel = 25;
    [SerializeField] string rewardItemId = "YOUR_LEVEL25_TRAIL_ID"; // must match ItemDef.id exactly

    void Awake()
    {
        if (!xp) xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
        if (!inventory) inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        if (xp) xp.OnLevelUp += HandleLevelUp;
        StartCoroutine(CatchUpNextFrame());
    }

    System.Collections.IEnumerator CatchUpNextFrame()
    {
        yield return null;
        TryGrant();
    }

    void OnDisable()
    {
        if (xp) xp.OnLevelUp -= HandleLevelUp;
    }

    void HandleLevelUp(int _)
    {
        TryGrant();
    }

    void TryGrant()
    {
        if (!xp || !inventory) return;

        if (xp.Level >= rewardLevel && !inventory.Owns(rewardItemId))
        {
            inventory.Grant(rewardItemId);
        }
    }
}