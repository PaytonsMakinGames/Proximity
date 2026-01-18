using TMPro;
using UnityEngine;

public class FloatingPopup : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI text;
    [SerializeField] CanvasGroup canvasGroup;

    // motion
    Vector2 screenPos;
    Vector2 vel;

    float life;
    float age;
    float gravity;
    float fadeSeconds;

    public bool Alive => age < life;

    public void Init(
        Vector2 startScreenPos,
        string message,
        float lifetimeSeconds,
        Vector2 startVelocity,
        float gravityPerSecond,
        float fadeDurationSeconds)
    {
        if (!text) text = GetComponent<TextMeshProUGUI>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

        screenPos = startScreenPos;
        vel = startVelocity;

        life = Mathf.Max(0.05f, lifetimeSeconds);
        age = 0f;

        gravity = gravityPerSecond;
        fadeSeconds = Mathf.Max(0.01f, fadeDurationSeconds);

        text.text = message;
        canvasGroup.alpha = 1f;

        var rt = (RectTransform)transform;
        rt.anchoredPosition = screenPos;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;

        // integrate velocity with "gravity" pulling downward
        vel.y -= gravity * dt;
        screenPos += vel * dt;

        var rt = (RectTransform)transform;
        rt.anchoredPosition = screenPos;

        // fade out near the end
        float remaining = life - age;
        float a = (remaining <= fadeSeconds) ? Mathf.Clamp01(remaining / fadeSeconds) : 1f;
        if (canvasGroup) canvasGroup.alpha = a;

        if (age >= life)
            gameObject.SetActive(false);
    }

    public void SetColor(Color c)
    {
        if (!text) text = GetComponent<TMPro.TextMeshProUGUI>();
        if (text) text.color = c;
    }
}