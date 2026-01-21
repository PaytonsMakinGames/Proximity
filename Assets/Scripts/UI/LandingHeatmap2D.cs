using UnityEngine;

[DefaultExecutionOrder(200)]
public class LandingHeatmap2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RunScoring2D scoring;
    [SerializeField] SpriteRenderer targetRenderer;
    [SerializeField] ScreenBounds2D screenBounds;
    [SerializeField] PowerupManager powerups;

    [Header("Resolution")]
    [SerializeField, Range(16, 256)] int width = 96;
    [SerializeField, Range(16, 256)] int height = 54;

    [Header("Update")]
    [Tooltip("Master toggle (dev).")]
    [SerializeField] bool show = false;
    [SerializeField, Min(0.02f)] float refreshSeconds = 0.12f;

    [Header("Powerup Gate")]
    [SerializeField] string landingAmpId = "landing_amplifier";
    [SerializeField] string insuranceId = "insurance";

    [Header("Border Extension")]
    [Tooltip("If true: pixels in the ball-radius border copy the nearest reachable pixel (seamless extension).")]
    [SerializeField] bool extendIntoRadiusBorder = true;

    [Header("Optional Smoothing")]
    [Tooltip("Light blur to hide pixel stepping and make edges look cleaner.")]
    [SerializeField] bool blur = true;
    [SerializeField, Range(1, 3)] int blurPasses = 1;

    [Header("Colors")]
    [SerializeField] Color colorZero = Color.black;                     // 0x
    [SerializeField] Color colorRed = new Color(1f, 0.1f, 0.1f, 1f);     // 0–1
    [SerializeField] Color colorOrange = new Color(1f, 0.5f, 0.1f, 1f);  // 1–2
    [SerializeField] Color colorGreen = new Color(0.2f, 1f, 0.2f, 1f);   // 2–4
    [SerializeField] Color colorBlue = new Color(0.3f, 0.6f, 1f, 1f);    // 4–6

    Texture2D tex;
    Color32[] pixels;
    Color32[] blurTemp;
    float nextRefresh;
    bool lastApplyAmpForHeatmap;
    bool lastApplyInsuranceForHeatmap;


    void Awake()
    {
        if (!scoring) scoring = FindFirstObjectByType<RunScoring2D>(FindObjectsInactive.Include);
        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        if (!screenBounds) screenBounds = FindFirstObjectByType<ScreenBounds2D>(FindObjectsInactive.Include);
        if (!powerups) powerups = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);

        if (powerups)
            powerups.OnArmedChanged += ForceRefreshNow;

        EnsureTexture();
        ApplyVisible(false);
        nextRefresh = 0f;
        lastApplyAmpForHeatmap = false;
        lastApplyInsuranceForHeatmap = false;
    }

    void OnDisable()
    {
        if (powerups)
            powerups.OnArmedChanged -= ForceRefreshNow;

        ApplyVisible(false);
    }

    void Update()
    {
        if (!scoring || !targetRenderer) return;

        if (targetRenderer.enabled != show)
            ApplyVisible(show);

        if (!show) return;

        bool armedAmp = powerups && powerups.ArmedId == landingAmpId;
        bool lockedAmp = powerups && powerups.LandingAmpActiveThisThrow;
        bool applyAmpForHeatmap = armedAmp || lockedAmp;

        bool armedInsurance = powerups && powerups.ArmedId == insuranceId;
        bool lockedInsurance = powerups && powerups.InsuranceActiveThisThrow;
        bool applyInsuranceForHeatmap = armedInsurance || lockedInsurance;

        if (applyAmpForHeatmap != lastApplyAmpForHeatmap ||
            applyInsuranceForHeatmap != lastApplyInsuranceForHeatmap)
        {
            lastApplyAmpForHeatmap = applyAmpForHeatmap;
            lastApplyInsuranceForHeatmap = applyInsuranceForHeatmap;
            nextRefresh = 0f;
        }

        if (nextRefresh > 0f)
        {
            nextRefresh -= Time.deltaTime;
            return;
        }

        RebuildNow(applyAmpForHeatmap, applyInsuranceForHeatmap);
        nextRefresh = refreshSeconds;
    }

    public void ForceRefreshNow()
    {
        bool armedAmp = powerups && powerups.ArmedId == landingAmpId;
        bool lockedAmp = powerups && powerups.LandingAmpActiveThisThrow;

        bool armedInsurance = powerups && powerups.ArmedId == insuranceId;
        bool lockedInsurance = powerups && powerups.InsuranceActiveThisThrow;

        RebuildNow((armedAmp || lockedAmp), (armedInsurance || lockedInsurance));
    }

    void ApplyVisible(bool v)
    {
        if (targetRenderer) targetRenderer.enabled = v;
    }

    void EnsureTexture()
    {
        if (tex && tex.width == width && tex.height == height && pixels != null && pixels.Length == width * height)
            return;

        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        pixels = new Color32[width * height];
        blurTemp = new Color32[width * height];

        var sp = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        targetRenderer.sprite = sp;
    }

    void RebuildNow(bool applyAmpForHeatmap, bool applyInsuranceForHeatmap)
    {
        EnsureTexture();

        GameViewport.GetWorldBounds(out var wallMinW, out var wallMaxW);

        float r = scoring.GetBallRadiusWorld();
        Vector2 reachMinW = wallMinW + Vector2.one * r;
        Vector2 reachMaxW = wallMaxW - Vector2.one * r;

        if (reachMaxW.x <= reachMinW.x || reachMaxW.y <= reachMinW.y)
            return;

        float tMax = applyAmpForHeatmap ? 6f : 4f;

        float w = Mathf.Max(0.0001f, wallMaxW.x - wallMinW.x);
        float h = Mathf.Max(0.0001f, wallMaxW.y - wallMinW.y);

        for (int y = 0; y < height; y++)
        {
            float fy = (y + 0.5f) / height;
            float wy = wallMinW.y + fy * h;

            for (int x = 0; x < width; x++)
            {
                float fx = (x + 0.5f) / width;
                float wx = wallMinW.x + fx * w;

                bool outsideReach =
                    wx < reachMinW.x || wx > reachMaxW.x ||
                    wy < reachMinW.y || wy > reachMaxW.y;

                Vector2 sampleW;

                if (!outsideReach)
                {
                    sampleW = new Vector2(wx, wy);
                }
                else
                {
                    if (!extendIntoRadiusBorder)
                    {
                        pixels[y * width + x] = colorZero;
                        continue;
                    }

                    float sx = Mathf.Clamp(wx, reachMinW.x, reachMaxW.x);
                    float sy = Mathf.Clamp(wy, reachMinW.y, reachMaxW.y);
                    sampleW = new Vector2(sx, sy);
                }

                float m = scoring.GetLandingMultiplierAt(sampleW, applyAmpForHeatmap);

                // Insurance preview/lock: show 0x..0.99x as 1x (no boost above 1x)
                if (applyInsuranceForHeatmap && m < 1f)
                    m = 1f;

                pixels[y * width + x] = EvaluateColor(m, tMax);
            }
        }

        if (blur && blurPasses > 0)
        {
            for (int i = 0; i < blurPasses; i++)
                BoxBlur3x3(pixels, blurTemp, width, height);
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        FitSpriteToWorldRect(targetRenderer, wallMinW, wallMaxW);
    }

    static void FitSpriteToWorldRect(SpriteRenderer sr, Vector2 minW, Vector2 maxW)
    {
        if (!sr || !sr.sprite) return;

        Vector2 center = (minW + maxW) * 0.5f;
        sr.transform.position = new Vector3(center.x, center.y, sr.transform.position.z);

        float w = Mathf.Max(0.0001f, maxW.x - minW.x);
        float h = Mathf.Max(0.0001f, maxW.y - minW.y);

        float ppu = sr.sprite.pixelsPerUnit;
        float spriteWorldW = sr.sprite.rect.width / ppu;
        float spriteWorldH = sr.sprite.rect.height / ppu;

        sr.transform.localScale = new Vector3(w / spriteWorldW, h / spriteWorldH, 1f);
    }

    static void BoxBlur3x3(Color32[] srcDst, Color32[] temp, int w, int h)
    {
        System.Array.Copy(srcDst, temp, srcDst.Length);

        for (int y = 0; y < h; y++)
        {
            int y0 = Mathf.Max(0, y - 1);
            int y1 = y;
            int y2 = Mathf.Min(h - 1, y + 1);

            for (int x = 0; x < w; x++)
            {
                int x0 = Mathf.Max(0, x - 1);
                int x1 = x;
                int x2 = Mathf.Min(w - 1, x + 1);

                Color32 c00 = temp[y0 * w + x0];
                Color32 c10 = temp[y0 * w + x1];
                Color32 c20 = temp[y0 * w + x2];

                Color32 c01 = temp[y1 * w + x0];
                Color32 c11 = temp[y1 * w + x1];
                Color32 c21 = temp[y1 * w + x2];

                Color32 c02 = temp[y2 * w + x0];
                Color32 c12 = temp[y2 * w + x1];
                Color32 c22 = temp[y2 * w + x2];

                int rr = c00.r + c10.r + c20.r + c01.r + c11.r + c21.r + c02.r + c12.r + c22.r;
                int gg = c00.g + c10.g + c20.g + c01.g + c11.g + c21.g + c02.g + c12.g + c22.g;
                int bb = c00.b + c10.b + c20.b + c01.b + c11.b + c21.b + c02.b + c12.b + c22.b;
                int aa = c00.a + c10.a + c20.a + c01.a + c11.a + c21.a + c02.a + c12.a + c22.a;

                srcDst[y * w + x] = new Color32(
                    (byte)(rr / 9),
                    (byte)(gg / 9),
                    (byte)(bb / 9),
                    (byte)(aa / 9)
                );
            }
        }
    }

    Color32 EvaluateColor(float m, float tMax)
    {
        if (m <= 0.0001f)
            return colorZero;

        if (m < 1f)
        {
            float a = Mathf.InverseLerp(0f, 1f, m);
            return Color.Lerp(colorZero, colorRed, a);
        }

        if (m < 2f)
        {
            float a = Mathf.InverseLerp(1f, 2f, m);
            return Color.Lerp(colorRed, colorOrange, a);
        }

        if (m < 4f)
        {
            float a = Mathf.InverseLerp(2f, 4f, m);
            return Color.Lerp(colorOrange, colorGreen, a);
        }

        float b = Mathf.InverseLerp(4f, tMax, m);
        return Color.Lerp(colorGreen, colorBlue, b);
    }
}