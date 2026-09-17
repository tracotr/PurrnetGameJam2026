using System;
using System.Collections;
using PurrNet;
using PurrNet.Lobby;

public class PlayerNameRegistry : NetworkIdentity
{
    private readonly SyncDictionary<PlayerID, string> _names = new();

    public static PlayerNameRegistry instance { get; private set; }

    public static event Action onNamesChanged;

    private void Awake()
    {
        instance = this;
    }
    
    protected override void OnSpawned()
    {
        _names.onChanged += OnNamesChanged;
        onNamesChanged?.Invoke();
        StartCoroutine(SubmitWhenReady());
    }

    protected override void OnDestroy()
    {
        _names.onChanged -= OnNamesChanged;
        if (instance == this)
            instance = null;
    }


    
    private IEnumerator SubmitWhenReady()
    {
        while (!localPlayer.HasValue)
            yield return null;

        string myName = GameOrchestrator.active && GameOrchestrator.active.sessionProvider
            ? GameOrchestrator.active.sessionProvider.playerName
            : null;

        Submit(myName);
    }

    protected override void OnDespawned()
    {
        _names.onChanged -= OnNamesChanged;
    }

    private void OnNamesChanged(SyncDictionaryChange<PlayerID, string> change)
    {
        onNamesChanged?.Invoke();
    }

    public static string Get(PlayerID id)
    {
        if (instance == null) return id.ToString();
        return instance._names.TryGetValue(id, out var name) ? name : id.ToString();
    }

    private void SetName(PlayerID id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = id.ToString();

        if (name.Length > 16)
            name = name.Substring(0, 16);

        _names[id] = name;
    }

    [ServerRpc(requireOwnership: false)]
    private void Submit(string name, RPCInfo info = default)
    {
        SetName(info.sender, name);
    }
}