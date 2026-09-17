using System;
using System.Collections.Generic;
using PurrNet;
using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;

public class CoinManager : PredictedIdentity<CoinManager.State>
{
    [SerializeField] private float respawnDelay = 0.75f;
    [SerializeField] private float depositStayRadius = 1.5f;
    [SerializeField] private AudioSource audioSource;

    public Action OnCoinUpdate;
    
    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    protected override void LateAwake()
    {
        foreach (var player in predictionManager.players.players)
        {
            EnsurePlayer(player);
        }
    }

    protected override State GetInitialState()
    {
        return new State
        {
            CurrentCoins = DisposableDictionary<PlayerID, int>.Create(),
            DunkedCoins = DisposableDictionary<PlayerID, int>.Create(),
            PendingRespawns = DisposableList<PendingRespawn>.Create(),
        };
    }

    private void EnsurePlayer(PlayerID player)
    {
        if (currentState.CurrentCoins.ContainsKey(player)) return;
        currentState.CurrentCoins.Add(player, 0);
        currentState.DunkedCoins.Add(player, 0);
    }

    // amount is coin worth BEFORE the zone multiplier; multiplier is applied here, once, at pickup time
    public void AddCoin(PlayerID player, int amount)
    {
        EnsurePlayer(player);

        int zonesOwned = InstanceHandler.TryGetInstance(out GraffitiManager gm) ? gm.GetOwnedCount(player) : 0;
        currentState.CurrentCoins[player] += amount * (1 + zonesOwned);

        OnCoinUpdate.Invoke();
    }
    
    public void RemoveCoin(PlayerID player, int amount)
    {
        EnsurePlayer(player);
        currentState.CurrentCoins[player] = Mathf.Max(0, currentState.CurrentCoins[player] - amount);
        OnCoinUpdate.Invoke();
    }

    public int GetCurrentCoins(PlayerID player)
    {
        if (player == default) return 0;
        return currentState.CurrentCoins.TryGetValue(player, out var v) ? v : 0;
    }

    public void GetStandings(List<(PlayerID player, int dunked)> results)
    {
        results.Clear();

        if (predictionManager == null) return;

        foreach (var player in predictionManager.players.players)
        {
            int dunked = currentState.DunkedCoins.TryGetValue(player, out var v) ? v : 0;
            results.Add((player, dunked));
        }

        results.Sort((a, b) => b.dunked.CompareTo(a.dunked));
    }
    
    public void DunkCoins(PlayerID player, PredictedComponentID playerCID, Vector2 depositZonePosition)
    {
        EnsurePlayer(player);

        currentState.DunkedCoins[player] += currentState.CurrentCoins[player];
        currentState.CurrentCoins[player] = 0;
        OnCoinUpdate.Invoke();

        for (int i = 0; i < currentState.PendingRespawns.Count; i++)
        {
            if (currentState.PendingRespawns[i].player == player)
                return;
        }

        currentState.PendingRespawns.Add(new PendingRespawn
        {
            player = player,
            playerCID = playerCID,
            timer = respawnDelay,
            depositZonePosition = depositZonePosition,
        });
    }

    protected override void Simulate(ref State state, float delta)
    {
        for (int i = state.PendingRespawns.Count - 1; i >= 0; i--)
        {
            var pending = state.PendingRespawns[i];
            pending.timer -= delta;

            if (pending.timer > 0f)
            {
                state.PendingRespawns[i] = pending;
                continue;
            }

            state.PendingRespawns.RemoveAt(i);

            if (!pending.playerCID.objectId.TryGetComponent<PlayerMovement>(predictionManager, out var movement))
            {
                Debug.Log($"Cant find movement for {pending.playerCID}");
                continue;
            }

            float sqrDist = (movement.pRigidbody.position - pending.depositZonePosition).sqrMagnitude;
            if (sqrDist > depositStayRadius * depositStayRadius)
            {
                continue;
            }

            if (!InstanceHandler.TryGetInstance(out SpawnManager spawnManager))
            {
                Debug.Log("Cant find spawn manager");
                continue;
            }

            var nextSpawn = spawnManager.GetNextSpawnPoint();
            audioSource.Play();
            movement.Respawn(nextSpawn.position);
        }
    }

    public struct PendingRespawn
    {
        public PlayerID player;
        public PredictedComponentID playerCID;
        public float timer;
        public Vector2 depositZonePosition;
    }

    public struct State : IPredictedData<State>
    {
        public DisposableDictionary<PlayerID, int> CurrentCoins;
        public DisposableDictionary<PlayerID, int> DunkedCoins;
        public DisposableList<PendingRespawn> PendingRespawns;

        public void Dispose()
        {
            CurrentCoins.Dispose();
            DunkedCoins.Dispose();
            PendingRespawns.Dispose();
        }
    }
}