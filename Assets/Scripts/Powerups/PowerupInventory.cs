using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PowerupInventorySave
{
    public List<string> ids = new List<string>();
    public List<int> counts = new List<int>();
}

public class PowerupInventory : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] PowerupDatabase db;

    [Header("Save")]
    [SerializeField] string saveKey = "PowerupInventory_v1";

    [Header("DEV seed (testing only)")]
    [SerializeField] bool devSeedOnFirstInstall = true;
    [SerializeField] int devSeedEach = 99;

    public event Action OnChanged;

    PowerupInventorySave save = new PowerupInventorySave();
    Dictionary<string, int> map = new Dictionary<string, int>();

    void Awake()
    {
        bool hadSave = PlayerPrefs.HasKey(saveKey);

        Load();
        RebuildMapFromSave();

        // DEV: fresh installs on Android have no inventory, so the radial looks "invisible".
        // Seed once (only when there was no save yet).
        if (devSeedOnFirstInstall && !hadSave)
        {
            DevSeedAll(devSeedEach);
            SaveToPrefs();
            OnChanged?.Invoke();
        }
    }

    void RebuildMapFromSave()
    {
        map.Clear();
        int n = Mathf.Min(save.ids.Count, save.counts.Count);
        for (int i = 0; i < n; i++)
        {
            string id = save.ids[i];
            int c = save.counts[i];
            if (string.IsNullOrEmpty(id)) continue;
            if (c <= 0) continue;
            map[id] = c;
        }
    }

    public int GetCount(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        return map.TryGetValue(id, out int c) ? Mathf.Max(0, c) : 0;
    }

    public bool Has(string id) => GetCount(id) > 0;

    public void Add(string id, int amount)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (amount <= 0) return;

        int cur = GetCount(id);
        map[id] = cur + amount;

        SaveToPrefs();
        OnChanged?.Invoke();
    }

    public bool TrySpend(string id, int amount)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (amount <= 0) return false;

        int cur = GetCount(id);
        if (cur < amount) return false;

        int next = cur - amount;
        if (next <= 0) map.Remove(id);
        else map[id] = next;

        SaveToPrefs();
        OnChanged?.Invoke();
        return true;
    }

    void Load()
    {
        if (!PlayerPrefs.HasKey(saveKey)) return;

        var json = PlayerPrefs.GetString(saveKey, "");
        if (string.IsNullOrEmpty(json)) return;

        var loaded = JsonUtility.FromJson<PowerupInventorySave>(json);
        if (loaded != null) save = loaded;
    }

    void SaveToPrefs()
    {
        save.ids.Clear();
        save.counts.Clear();

        foreach (var kv in map)
        {
            save.ids.Add(kv.Key);
            save.counts.Add(Mathf.Max(0, kv.Value));
        }

        var json = JsonUtility.ToJson(save);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();
    }

    void DevSeedAll(int each)
    {
        if (!db)
        {
            Debug.LogWarning("[PowerupInventory] No db assigned; cannot seed.");
            return;
        }

        if (each < 1) each = 1;

        foreach (var p in db.powerups)
        {
            if (!p) continue;
            if (string.IsNullOrEmpty(p.id)) continue;

            int cur = GetCount(p.id);
            map[p.id] = cur + each;
        }

        Debug.Log($"[PowerupInventory] DEV seeded {each} of each powerup (first install).");
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Powerup Inventory")]
    void ResetPowerupInventory_ContextMenu()
    {
        map.Clear();
        save = new PowerupInventorySave();
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        Debug.Log("[PowerupInventory] Reset.");
    }

    [ContextMenu("DEV: Grant 1 of each powerup in database")]
    void DevGrantAll()
    {
        DevSeedAll(99);
        SaveToPrefs();
        OnChanged?.Invoke();
        Debug.Log("[PowerupInventory] Granted 1 of each powerup.");
    }
#endif
}