using UnityEngine;

[CreateAssetMenu(fileName = "CharacterVisual", menuName = "Game/Character Visual")]
public class PlayerSkinData : ScriptableObject
{
    public AnimatorOverrideController animatorController;
    public Material textMaterial;
    public Color color;
    public Color accentColor;
    public bool isRecolor;
}