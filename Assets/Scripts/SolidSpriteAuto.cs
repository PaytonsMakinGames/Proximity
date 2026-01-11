using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SolidSpriteAuto : MonoBehaviour
{
    [SerializeField] int texSize = 16;
    [SerializeField] float pixelsPerUnit = 100f;

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr.sprite != null) return;

        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false, true);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var cols = new Color32[texSize * texSize];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols);
        tex.Apply(false, true);

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }
}