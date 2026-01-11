using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class XpHud : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] TextMeshProUGUI xpText;
    [SerializeField] Image xpFill; // optional

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

        if (levelText) levelText.text = $"LV {level}";

        if (xpText)
        {
            if (toNext <= 0)
                xpText.text = $"{xpVal:n0} XP\nMAX";
            else
                xpText.text = $"{xpVal:n0} XP\n{toNext:n0} until next\n({p * 100f:0.#}%)";
        }

        if (xpFill) xpFill.fillAmount = p;
    }
}