using System;
using UnityEngine;

public class XpManager : MonoBehaviour
{
    [Header("Save Keys")]
    [SerializeField] string xpKey = "PlayerXP";

    [Header("Tuning")]
    [Tooltip("If XP feels too fast/slow, adjust this first.")]
    [SerializeField] float xpScale = 1f;

    public int Xp { get; private set; }
    public int Level => XpCurveRS.LevelForXp(Xp);
    public float LevelProgress01 => XpCurveRS.Progress01(Xp);

    public event Action<int> OnXpChanged;   // sends new XP
    public event Action<int> OnLevelUp;     // sends new level

    void Awake()
    {
        // One global XP manager.
        var existing = FindFirstObjectByType<XpManager>(FindObjectsInactive.Exclude);
        if (existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        Xp = PlayerPrefs.GetInt(xpKey, 0);
        OnXpChanged?.Invoke(Xp);
    }

    public void AddXp(int rawAmount)
    {
        if (rawAmount <= 0) return;

        int beforeLevel = Level;

        int add = Mathf.Max(1, Mathf.RoundToInt(rawAmount * xpScale));
        Xp = Mathf.Max(0, Xp + add);

        PlayerPrefs.SetInt(xpKey, Xp);
        PlayerPrefs.Save();

        OnXpChanged?.Invoke(Xp);

        int afterLevel = Level;
        if (afterLevel > beforeLevel)
            OnLevelUp?.Invoke(afterLevel);
    }

#if UNITY_EDITOR
[ContextMenu("Reset XP")]
void ResetXp_ContextMenu()
{
    Xp = 0;
    PlayerPrefs.DeleteKey(xpKey);
    PlayerPrefs.Save();

    OnXpChanged?.Invoke(Xp);
    Debug.Log("[XpManager] XP reset.");
}
#endif
}