using UnityEngine;

/// <summary>
/// Utility for normalizing game metrics (distances, speeds, timings) across screen resolutions and aspect ratios.
/// Use this to ensure consistent gameplay feel regardless of device.
/// </summary>
public static class ScreenNormalizer
{
    /// <summary>
    /// Normalize a distance value to account for screen resolution changes.
    /// Returns the scaled value such that gameplay feels consistent across resolutions.
    /// </summary>
    public static float NormalizeDistance(float worldDistance)
    {
        float scale = GameViewport.GetNormalizedScale();
        return worldDistance * scale;
    }

    /// <summary>
    /// Normalize a speed value. Useful for throw speeds, UI animations, etc.
    /// </summary>
    public static float NormalizeSpeed(float worldSpeed)
    {
        float scale = GameViewport.GetNormalizedScale();
        return worldSpeed * scale;
    }

    /// <summary>
    /// Normalize a time duration. Useful for animation lengths, cooldowns, etc.
    /// </summary>
    public static float NormalizeTime(float baseDuration)
    {
        float scale = GameViewport.GetNormalizedScale();
        return baseDuration / scale;  // Inverse: faster on larger screens
    }

    /// <summary>
    /// Get a responsive value that scales with aspect ratio.
    /// Useful for UI sizing, particle spread, etc.
    /// baseOnHeight: if true, scales based on height; if false, scales based on width.
    /// </summary>
    public static float GetAspectScaledValue(float baseValue, bool baseOnHeight = true)
    {
        Vector2 dims = GameViewport.GetWorldDimensions();
        float reference = baseOnHeight ? dims.y : dims.x;
        return baseValue * (reference / 10f);  // Normalize to default height of 10
    }

    /// <summary>
    /// Check if we're in "wide aspect ratio" mode (landscape).
    /// Useful for adjusting UI or gameplay mechanics based on screen shape.
    /// </summary>
    public static bool IsWideAspect()
    {
        return GameViewport.GetAspectRatio() > 1.2f;
    }

    /// <summary>
    /// Get a field-scale factor. Use this to adjust action thresholds, particle counts, etc.
    /// </summary>
    public static float GetFieldScaleFactor()
    {
        Vector2 dims = GameViewport.GetWorldDimensions();
        return dims.magnitude / Mathf.Sqrt(200);  // Normalize to ~14.14 (sqrt(10*10*2))
    }
}
