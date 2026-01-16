using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Powerups/Powerup Database")]
public class PowerupDatabase : ScriptableObject
{
    public List<PowerupDefinition> powerups = new List<PowerupDefinition>();

    Dictionary<string, PowerupDefinition> lookup;

    void OnEnable()
    {
        lookup = new Dictionary<string, PowerupDefinition>();

        foreach (var p in powerups)
        {
            if (!p) continue;
            if (string.IsNullOrEmpty(p.id)) continue;
            lookup[p.id] = p;
        }
    }

    public PowerupDefinition Get(string id)
    {
        if (lookup == null || lookup.Count == 0) OnEnable();
        if (string.IsNullOrEmpty(id)) return null;

        lookup.TryGetValue(id, out var p);
        return p;
    }

    public IEnumerable<PowerupDefinition> All()
    {
        if (lookup == null || lookup.Count == 0) OnEnable();
        return powerups;
    }
}