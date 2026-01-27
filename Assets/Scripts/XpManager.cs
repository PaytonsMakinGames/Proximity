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
    public event Action<int> OnLevelDown;   // sends new level (lower)

    bool pendingRunActive;
    int pendingRunScaledTotal;

    void Awake()
    {
        // One global XP manager.
        var existing = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
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

    /// <summary>
    /// Directly set XP to the specified level (instant jump). Fires level up/down once.
    /// </summary>
    public void SetLevelImmediate(int targetLevel)
    {
        targetLevel = Mathf.Clamp(targetLevel, 1, XpCurveRS.MaxLevel);

        int beforeLevel = Level;

        Xp = Mathf.Max(0, XpCurveRS.XpForLevel(targetLevel));

        PlayerPrefs.SetInt(xpKey, Xp);
        PlayerPrefs.Save();

        OnXpChanged?.Invoke(Xp);

        int afterLevel = Level;
        if (afterLevel > beforeLevel)
            OnLevelUp?.Invoke(afterLevel);
        else if (afterLevel < beforeLevel)
            OnLevelDown?.Invoke(afterLevel);
    }

    int ScaleRawToScaled(int rawAmount)
    {
        if (rawAmount <= 0) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(rawAmount * xpScale));
    }

    /// <summary>
    /// Applies and SAVES XP for a run total that is still allowed to change (Encore revive).
    /// Call again with a new rawTotal to apply the delta (can be negative).
    /// </summary>
    public void BeginOrUpdatePendingRunXp(int rawTotal)
    {
        rawTotal = Mathf.Max(0, rawTotal);

        int newScaledTotal = ScaleRawToScaled(rawTotal);

        // First time: just set the total.
        if (!pendingRunActive)
        {
            pendingRunActive = true;
            pendingRunScaledTotal = newScaledTotal;

            int beforeLevel = Level;

            Xp = Mathf.Max(0, Xp + newScaledTotal);

            PlayerPrefs.SetInt(xpKey, Xp);
            PlayerPrefs.Save();

            OnXpChanged?.Invoke(Xp);

            int afterLevel = Level;
            if (afterLevel > beforeLevel)
                OnLevelUp?.Invoke(afterLevel);

            return;
        }

        // Update: apply delta from the last applied total.
        int deltaScaled = newScaledTotal - pendingRunScaledTotal;
        if (deltaScaled == 0) return;

        pendingRunScaledTotal = newScaledTotal;

        int beforeLevel2 = Level;

        Xp = Mathf.Max(0, Xp + deltaScaled);

        PlayerPrefs.SetInt(xpKey, Xp);
        PlayerPrefs.Save();

        OnXpChanged?.Invoke(Xp);

        int afterLevel2 = Level;
        if (afterLevel2 > beforeLevel2)
            OnLevelUp?.Invoke(afterLevel2);
        else if (afterLevel2 < beforeLevel2)
            OnLevelDown?.Invoke(afterLevel2);
    }

    /// <summary>
    /// Call when the run is no longer allowed to change (player starts next run).
    /// This does not change XP; it only closes the adjustable window.
    /// </summary>
    public void EndPendingRunXp()
    {
        pendingRunActive = false;
        pendingRunScaledTotal = 0;
    }

#if UNITY_EDITOR
[ContextMenu("Reset XP")]
void ResetXp_ContextMenu()
{
    Xp = 0;
    PlayerPrefs.DeleteKey(xpKey);
    PlayerPrefs.Save();

    OnXpChanged?.Invoke(Xp);
}
#endif
}