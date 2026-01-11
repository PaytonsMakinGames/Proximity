using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySave
{
    public List<string> owned = new List<string>();
    public string equippedBall;
    public string equippedTrail;
}

public class PlayerInventory : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] ItemDatabase db;

    [Header("Save")]
    [SerializeField] string saveKey = "InventorySave_v1";

    public InventorySave Save { get; private set; } = new InventorySave();

    public event Action OnChanged;

    void Awake()
    {
        Load();
        EnsureStarterItems();

        // Force one refresh after load so visuals re-apply on a fresh Play
        OnChanged?.Invoke();
    }

    void EnsureStarterItems()
    {
        if (db == null || db.items == null) return;

        bool changed = false;

        foreach (var item in db.items)
        {
            if (item == null) continue;
            if (!item.isStarter) continue;

            if (!Save.owned.Contains(item.id))
            {
                Save.owned.Add(item.id);
                changed = true;
            }

            // Auto-equip a starter if slot is empty
            if (item.slot == EquipSlot.Ball && string.IsNullOrEmpty(Save.equippedBall))
            {
                Save.equippedBall = item.id;
                changed = true;
            }

            if (item.slot == EquipSlot.Trail && string.IsNullOrEmpty(Save.equippedTrail))
            {
                Save.equippedTrail = item.id;
                changed = true;
            }
        }

        if (changed) SaveToPrefs();
    }

    public int GetBonusThrows()
    {
        int bonus = 0;

        var ball = GetEquipped(EquipSlot.Ball);
        var trail = GetEquipped(EquipSlot.Trail);

        if (ball != null) bonus += ball.bonusThrows;
        if (trail != null) bonus += trail.bonusThrows;

        return Mathf.Max(0, bonus);
    }

    public bool Owns(string itemId) => Save.owned.Contains(itemId);

    public void Grant(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        if (!Save.owned.Contains(itemId))
        {
            Save.owned.Add(itemId);
            SaveToPrefs();
            OnChanged?.Invoke();
        }

        Debug.Log("Granted item: " + itemId);
    }

    public void Equip(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        if (!Owns(itemId)) return;
        if (db == null) return;

        var item = db.Get(itemId);
        if (item == null) return;

        if (item.slot == EquipSlot.Ball) Save.equippedBall = itemId;
        if (item.slot == EquipSlot.Trail) Save.equippedTrail = itemId;

        SaveToPrefs();
        OnChanged?.Invoke();
    }

    public ItemDef GetEquipped(EquipSlot slot)
    {
        if (db == null) return null;

        string id = slot == EquipSlot.Ball ? Save.equippedBall : Save.equippedTrail;
        if (string.IsNullOrEmpty(id)) return null;

        return db.Get(id);
    }

    public ItemDef GetById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        // db is your serialized field in PlayerInventory
        return db ? db.Get(itemId) : null;
    }

    public IEnumerable<string> GetOwnedIds()
    {
        return Save.owned;
    }

    public float GetXpMultiplier()
    {
        float bonus = 0f;

        var ball = GetEquipped(EquipSlot.Ball);
        var trail = GetEquipped(EquipSlot.Trail);

        if (ball != null) bonus += ball.xpBonus01;
        if (trail != null) bonus += trail.xpBonus01;

        return 1f + Mathf.Max(0f, bonus);
    }

    void Load()
    {
        if (!PlayerPrefs.HasKey(saveKey)) return;

        var json = PlayerPrefs.GetString(saveKey, "");
        if (string.IsNullOrEmpty(json)) return;

        var loaded = JsonUtility.FromJson<InventorySave>(json);
        if (loaded != null) Save = loaded;
    }

    void SaveToPrefs()
    {
        var json = JsonUtility.ToJson(Save);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();
    }
}