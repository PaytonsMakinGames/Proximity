using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerupRadialItemUI : MonoBehaviour
{
    [Header("UI")]
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Image highlight;
    public Image background;

    [Header("Data")]
    [HideInInspector] public string powerupId;

    public void SetCount(int count)
    {
        if (countText) countText.text = count.ToString();
    }

    public void SetHighlighted(bool on)
    {
        // Highlight overlay on/off
        if (highlight)
            highlight.enabled = on;

        // Dim background when highlighted
        if (background)
        {
            Color c = background.color;
            c.a = on ? 0.5f : 1f;
            background.color = c;
        }

        // Slight scale for feedback (keep subtle)
        transform.localScale = on ? Vector3.one * 1.08f : Vector3.one;
    }
}