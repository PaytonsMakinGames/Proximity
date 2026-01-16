using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerupRadialItemUI : MonoBehaviour
{
    [Header("UI")]
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Image highlight;

    [Header("Data")]
    [HideInInspector] public string powerupId;

    public void SetCount(int count)
    {
        if (countText) countText.text = count.ToString();
    }

    public void SetHighlighted(bool on)
    {
        Debug.Log($"[RadialItem] {powerupId} highlighted = {on}");

        if (highlight != null)
            highlight.enabled = on;

        transform.localScale = on ? Vector3.one * 1.08f : Vector3.one;
    }
}