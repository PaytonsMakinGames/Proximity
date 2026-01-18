using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FloatingPopupSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Camera worldCam;                 // leave empty, will use Camera.main
    [SerializeField] RectTransform canvasRoot;        // your Canvas root rect transform
    [SerializeField] FloatingPopup popupPrefab;

    [Header("Spawn")]
    [SerializeField] Vector2 screenOffset = new Vector2(0f, 90f);

    [Header("Motion")]
    [SerializeField] float lifetimeSeconds = 1.15f;
    [SerializeField] Vector2 startVelocity = new Vector2(0f, 55f);
    [SerializeField] float gravityPerSecond = 140f;
    [SerializeField] float fadeSeconds = 0.55f;

    [Header("Pool")]
    [SerializeField, Min(1)] int prewarm = 12;

    readonly List<FloatingPopup> pool = new List<FloatingPopup>(64);

    void Awake()
    {
        if (!worldCam) worldCam = Camera.main;
        if (!canvasRoot) canvasRoot = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        Prewarm();
    }

    void Prewarm()
    {
        if (!popupPrefab || !canvasRoot) return;

        for (int i = 0; i < prewarm; i++)
        {
            var p = Instantiate(popupPrefab, canvasRoot);
            p.gameObject.SetActive(false);
            pool.Add(p);
        }
    }

    FloatingPopup GetPopup()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].gameObject.activeSelf)
                return pool[i];
        }

        // simple expand
        var p2 = Instantiate(popupPrefab, canvasRoot);
        p2.gameObject.SetActive(false);
        pool.Add(p2);
        return p2;
    }

    public void PopAtWorld(Vector2 worldPos, string message, Color? color = null)
    {
        if (!worldCam) worldCam = Camera.main;
        if (!popupPrefab || !canvasRoot || !worldCam) return;

        Vector2 screen = worldCam.WorldToScreenPoint(worldPos);
        screen += screenOffset;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, screen, null, out Vector2 local);

        local.x += Random.Range(-12f, 12f);

        var p = GetPopup();
        var rt = (RectTransform)p.transform;

        // Clamp inside canvas bounds
        Vector2 halfSize = rt.rect.size * 0.5f;
        Rect canvasRect = canvasRoot.rect;

        local.x = Mathf.Clamp(local.x, canvasRect.xMin + halfSize.x, canvasRect.xMax - halfSize.x);
        local.y = Mathf.Clamp(local.y, canvasRect.yMin + halfSize.y, canvasRect.yMax - halfSize.y);

        // Apply color BEFORE init
        if (color.HasValue)
            p.SetColor(color.Value);

        p.gameObject.SetActive(true);
        p.transform.SetAsLastSibling();

        p.Init(
            startScreenPos: local,
            message: message,
            lifetimeSeconds: lifetimeSeconds,
            startVelocity: startVelocity,
            gravityPerSecond: gravityPerSecond,
            fadeDurationSeconds: fadeSeconds);
    }

    public void PopAtWorldWithExtraOffset(Vector2 worldPos, string message, Color color, Vector2 extraScreenOffset)
    {
        Vector2 old = screenOffset;
        screenOffset = old + extraScreenOffset;
        PopAtWorld(worldPos, message, color);
        screenOffset = old;
    }
}