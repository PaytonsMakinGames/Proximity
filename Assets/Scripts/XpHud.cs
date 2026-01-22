using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class XpHud : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] Image xpFill;
    [SerializeField] TextMeshProUGUI untilNextText;
    [SerializeField] TextMeshProUGUI totalXpText; // optional: for tooltip

    XpManager xp;

    void Awake()
    {
        xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        if (!xp) xp = FindFirstObjectByType<XpManager>(FindObjectsInactive.Include);
        if (!xp) return;

        xp.OnXpChanged += HandleXpChanged;
        xp.OnLevelUp += HandleLevelUp;

        Refresh();
    }

    void OnDisable()
    {
        if (!xp) return;
        xp.OnXpChanged -= HandleXpChanged;
        xp.OnLevelUp -= HandleLevelUp;
    }

    void HandleXpChanged(int _) => Refresh();
    void HandleLevelUp(int _) => Refresh();

    void Refresh()
    {
        if (!xp) return;

        int xpVal = xp.Xp;
        int level = xp.Level;
        float p = Mathf.Clamp01(xp.LevelProgress01);
        int toNext = XpCurveRS.XpToNextLevel(xpVal);

        // Level text above bar: "LVL 35"
        if (levelText)
            levelText.text = $"LVL {level}";

        // Progress bar
        if (xpFill)
            xpFill.fillAmount = p;

        // Text below bar: "68,266 until next"
        if (untilNextText)
        {
            // Show remaining XP to next level; if leveled, show 0 until next refresh
            untilNextText.text = $"{Mathf.Max(0, toNext):n0}";
        }

        // Optional total XP display (can be shown/hidden via tap)
        if (totalXpText)
            totalXpText.text = $"{xpVal:n0} XP";
    }
}