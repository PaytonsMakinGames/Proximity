using UnityEngine;

public enum EquipSlot
{
    Ball,
    Trail
}

[CreateAssetMenu(menuName = "Game/Item Definition")]
public class ItemDef : ScriptableObject
{
    [Header("Identity")]
    public string id;             // ex: "trail_xp_10"
    public string displayName;    // ex: "Scholar Trail"
    public EquipSlot slot;

    [Header("UI")]
    public Sprite icon;

    [Header("Unlocking")]
    public bool isStarter = false;

    [Header("Buffs")]
    [Range(0f, 1f)]
    public float xpBonus01;       // 0.10 = +10% XP

    [Header("Gameplay Buffs (optional)")]
    public int bonusThrows; // +1 = one extra throw per run

    [Header("Cosmetics (optional)")]
    public Sprite ballSprite;
    public Color ballColor = Color.white;
    public Material ballMaterial;
    public Material trailMaterial;

    [Header("Ball Skin Prefab (optional)")]
    public GameObject ballSkinPrefab;

    [Header("Trail Prefab (optional)")]
    public GameObject trailPrefab;
}