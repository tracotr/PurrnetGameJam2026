using PurrNet.Prediction;
using TMPro;
using UnityEngine;

public class PlayerSkin : PredictedIdentity<PlayerSkin.State>
{
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private TextMeshPro hintText;
    [SerializeField] private TextMeshPro coinText;
    [SerializeField] private TextMeshPro nameText;
    [SerializeField] private TextMeshPro zoneText;
    [SerializeField] private SpriteRenderer graffitiGameSprite;
    [SerializeField] private LineRenderer graffitiLine;
    [SerializeField] private SpriteRenderer targetMarker;
    [SerializeField] private Animator animator;
    
    private bool _subscribed;

    protected override void ViewStart(State viewState, State? verified)
    {
        if (!_subscribed)
        {
            PlayerNameRegistry.onNamesChanged += ApplyName;
            _subscribed = true;
        }

        ApplyName();

        string path = $"PlayerSkins/PlayerSkin{owner}";
        PlayerSkinData data = Resources.Load<PlayerSkinData>(path);

        if (data)
            ChangeSkin(data);
        else
            Debug.Log($"Player data not found for {owner} at {path}");
    }

    protected override void OnDestroy()
    {
        if (!_subscribed) return;
        PlayerNameRegistry.onNamesChanged -= ApplyName;
        _subscribed = false;
    }

    private void ApplyName()
    {
        if (!owner.HasValue) return;
        nameText.text = PlayerNameRegistry.Get(owner.Value);
    }

    private void ChangeSkin(PlayerSkinData data)
    {
        animator.runtimeAnimatorController = data.animatorController;
        hintText.fontSharedMaterial = data.textMaterial;
        coinText.fontSharedMaterial = data.textMaterial;
        nameText.fontSharedMaterial = data.textMaterial;
        zoneText.fontSharedMaterial = data.textMaterial;
        graffitiGameSprite.color = data.color;
        graffitiLine.startColor = data.color;
        graffitiLine.endColor = data.color;
        targetMarker.color = data.accentColor;

        if (data.isRecolor)
        {
            sprite.color = data.color;
        }
    }

    public struct State : IPredictedData<State>
    {
        public void Dispose() { }
    }
}