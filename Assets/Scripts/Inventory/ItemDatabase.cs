using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDef> items = new List<ItemDef>();

    Dictionary<string, ItemDef> lookup;

    void OnEnable()
    {
        lookup = new Dictionary<string, ItemDef>();

        foreach (var item in items)
        {
            if (item == null) continue;
            if (string.IsNullOrEmpty(item.id)) continue;

            lookup[item.id] = item;
        }
    }

    public ItemDef Get(string id)
    {
        if (lookup == null || lookup.Count == 0)
            OnEnable();

        if (lookup.TryGetValue(id, out var item))
            return item;

        return null;
    }
}