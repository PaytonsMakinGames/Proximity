using UnityEngine;

[CreateAssetMenu(menuName = "Game/Powerups/Powerup Definition")]
public class PowerupDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id;                 // ex: "sticky_ball"
    public string displayName;        // ex: "Sticky Ball"

    [Header("Trigger")]
    public PowerupTrigger trigger;

    [Header("UI (later)")]
    public Sprite icon;

    [TextArea(2, 6)]
    public string description;

    [Header("Params (generic, interpreted by trigger handlers later)")]
    public float f0;
    public float f1;
    public int i0;
    public int i1;

    public Color uiColor = Color.white;
}