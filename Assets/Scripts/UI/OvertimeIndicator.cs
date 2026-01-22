using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OvertimeIndicator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] PowerupManager powerups;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image background;
    [SerializeField] Image fillBar;
    [SerializeField] TextMeshProUGUI multiplierText;

    [Header("Visual")]
    [SerializeField] Gradient barColorGradient;
    [SerializeField] float fadeInSpeed = 5f;
    [SerializeField] float fadeOutSpeed = 3f;

    Color backgroundBaseColor;
    bool wasActive;

    void Awake()
    {
        if (!powerups) powerups = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);

        // Force a local CanvasGroup so we never fade the parent HUD canvas.
        if (!canvasGroup || canvasGroup.gameObject != gameObject)
            canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (canvasGroup)
            canvasGroup.alpha = 0f;

        if (background)
            backgroundBaseColor = background.color;
    }

    void Update()
    {
        if (!powerups) return;

        bool isActive = powerups.OvertimeActiveThisRun;
        float mult = powerups.GetOvertimeMultiplier();
        float maxBonus = powerups.GetOvertimeMaxBonus();
        if (maxBonus <= 0.0001f) maxBonus = 0.5f;

        // Fade in/out
        if (canvasGroup)
        {
            float targetAlpha = isActive ? 1f : 0f;
            float speed = isActive ? fadeInSpeed : fadeOutSpeed;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, speed * Time.deltaTime);
        }

        if (isActive)
        {
            float fillAmount = maxBonus > 0.0001f ? Mathf.Clamp01((mult - 1f) / maxBonus) : 1f;

            // Update fill bar
            if (fillBar)
            {
                fillBar.fillAmount = fillAmount;

                // Color gradient based on progress
                if (barColorGradient != null && barColorGradient.colorKeys.Length > 0)
                    fillBar.color = barColorGradient.Evaluate(fillAmount);
            }

            if (background)
            {
                background.color = backgroundBaseColor;
                background.transform.localScale = Vector3.one;
            }

            // Update multiplier text
            if (multiplierText)
            {
                multiplierText.text = $"{mult:F2}x";
            }
        }

        wasActive = isActive;
    }
}
