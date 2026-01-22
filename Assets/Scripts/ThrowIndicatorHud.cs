using UnityEngine;
using UnityEngine.UI;

public class ThrowIndicatorHud : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] Transform circleContainer;
    [SerializeField] GameObject circlePrefab;
    [SerializeField] float circleSpacing = 40f;

    [Header("Colors")]
    [SerializeField] Color availableColor = Color.white;
    [SerializeField] Color usedColor = new Color(0.3f, 0.3f, 0.3f, 1f); // dimmed
    [SerializeField] Color warningColor = Color.yellow; // 2 throws left
    [SerializeField] Color criticalColor = Color.red; // 1 throw left
    [SerializeField] Color exhaustedColor = new Color(0.2f, 0.2f, 0.2f, 1f); // all gone

    RunScoring2D scoring;
    Image[] circleImages;
    int lastKnownMaxThrows = -1;

    void Awake()
    {
        scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);
    }

    void Update()
    {
        if (!scoring) return;

        int maxThrows = scoring.EffectiveThrowsPerRun;
        int throwsLeft = scoring.ThrowsLeft;

        // Rebuild circles if max throws changed
        if (maxThrows != lastKnownMaxThrows)
        {
            RebuildCircles(maxThrows);
            lastKnownMaxThrows = maxThrows;
        }

        // Update circle colors
        UpdateCircleColors(throwsLeft, maxThrows);
    }

    void RebuildCircles(int count)
    {
        // Clear existing circles
        if (circleImages != null)
        {
            foreach (var img in circleImages)
            {
                if (img) Destroy(img.gameObject);
            }
        }

        if (count <= 0)
        {
            circleImages = new Image[0];
            return;
        }

        // Create new circles
        circleImages = new Image[count];

        // Calculate centered positioning
        float totalWidth = (count - 1) * circleSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            GameObject circle = Instantiate(circlePrefab, circleContainer);
            circle.name = $"Circle_{i}";

            Image img = circle.GetComponent<Image>();
            if (!img) img = circle.AddComponent<Image>();

            circleImages[i] = img;

            // Position centered horizontally
            RectTransform rt = circle.GetComponent<RectTransform>();
            if (rt)
            {
                rt.anchoredPosition = new Vector2(startX + (i * circleSpacing), 0);
            }
        }
    }

    void UpdateCircleColors(int throwsLeft, int maxThrows)
    {
        if (circleImages == null || circleImages.Length == 0) return;

        int throwsUsed = maxThrows - throwsLeft;
        bool exhausted = throwsLeft == 0;

        for (int i = 0; i < circleImages.Length; i++)
        {
            if (!circleImages[i]) continue;

            if (exhausted)
            {
                // All throws used: grey out everything
                circleImages[i].color = exhaustedColor;
            }
            else if (i < throwsUsed)
            {
                // This throw was used: dim it
                circleImages[i].color = usedColor;
            }
            else
            {
                // Remaining throws: color based on how many left
                if (throwsLeft == 1)
                    circleImages[i].color = criticalColor; // red
                else if (throwsLeft == 2)
                    circleImages[i].color = warningColor; // yellow
                else
                    circleImages[i].color = availableColor; // white
            }
        }
    }
}
