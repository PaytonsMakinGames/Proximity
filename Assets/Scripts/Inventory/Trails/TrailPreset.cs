using UnityEngine;

[CreateAssetMenu(menuName = "Game/Trail Preset")]
public class TrailPreset : ScriptableObject
{
    [Header("Core")]
    [Min(0f)] public float time = 0.35f;
    [Min(0f)] public float minVertexDistance = 0.1f;

    [Header("Width")]
    public AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Min(0f)] public float widthMultiplier = 0.2f;

    [Header("Color")]
    public Gradient colorGradient;

    [Header("Geometry")]
    public int cornerVertices = 0;
    public int endCapVertices = 0;
    public LineAlignment alignment = LineAlignment.View;

    [Header("Texture")]
    public Material material;
    public int textureMode = 0; // 0=Stretch, 1=Tile, etc. (we set via int to avoid enum mismatch)
    public bool generateLightingData = false;
}