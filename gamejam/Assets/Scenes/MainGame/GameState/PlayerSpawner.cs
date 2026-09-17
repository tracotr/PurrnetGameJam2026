using System.Collections.Generic;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Packing;
using PurrNet.Pooling;
using PurrNet.Prediction;
using PurrNet.Utils;
using UnityEngine;

public struct PlayerWithObject
{
    public PredictedObjectID objectID;
    public PlayerID playerID;
}

public struct PlayerSpawnerState : IPredictedData<PlayerSpawnerState>, IDuplicate<PlayerSpawnerState>
{
    public int spawnPointIndex;
    public DisposableList<PlayerWithObject> values;

    public PredictedObjectID this[PlayerID player]
    {
        set
        {
            for (var i = 0; i < values.Count; i++)
            {
                var playerWithObject = values[i];
                if (playerWithObject.playerID == player)
                {
                    playerWithObject.objectID = value;
                    values[i] = playerWithObject;
                    return;
                }
            }
            values.Add(new PlayerWithObject { objectID = value, playerID = player });
        }
    }

    public void Dispose()
    {
        values.Dispose();
    }

    public PlayerSpawnerState Duplicate()
    {
        return new PlayerSpawnerState
        {
            values = DisposableList<PlayerWithObject>.Create(values),
            spawnPointIndex = spawnPointIndex
        };
    }

    public bool TryGetValue(PlayerID player, out PredictedObjectID o)
    {
        for (var i = 0; i < values.Count; i++)
        {
            var playerWithObject = values[i];
            if (playerWithObject.playerID == player)
            {
                o = playerWithObject.objectID;
                return true;
            }
        }
        o = default;
        return false;
    }

    public bool ContainsKey(PlayerID player)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i].playerID == player)
                return true;
        }
        return false;
    }

    public void Remove(PlayerID player)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i].playerID == player)
            {
                values.RemoveAt(i);
                break;
            }
        }
    }
}

public class PlayerSpawner : DeterministicIdentity<PlayerSpawnerState>
{
    [SerializeField, PurrLock] private bool _destroyOnDisconnect;
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private bool _waitForSpawnCall;
    [SerializeField] private GameObject[] characterPrefabs;
    [SerializeField] private GameObject fallbackPrefab;

    private void Awake() => CleanupSpawnPoints();

    protected override void LateAwake()
    {
        if (predictionManager.players)
        {
            predictionManager.players.onPlayerAdded += OnPlayerLoadedScene;
            predictionManager.players.onPlayerRemoved += OnPlayerUnloadedScene;
        }
    }

    protected override void SimulationStart()
    {
        if (!predictionManager.players)
            return;

        var players = predictionManager.players.players;
        for (var i = 0; i < players.Count; i++)
            OnPlayerLoadedScene(players[i]);
    }

    protected override PlayerSpawnerState GetInitialState()
    {
        return new PlayerSpawnerState
        {
            spawnPointIndex = 0,
            values = DisposableList<PlayerWithObject>.Create()
        };
    }

    protected override void Destroyed()
    {
        if (predictionManager && predictionManager.players)
        {
            predictionManager.players.onPlayerAdded -= OnPlayerLoadedScene;
            predictionManager.players.onPlayerRemoved -= OnPlayerUnloadedScene;
        }
    }

    protected override PlayerSpawnerState Interpolate(PlayerSpawnerState from, PlayerSpawnerState to, float t)
        => to;
    
    public void TeleportPlayer(PlayerID player)
    {
        if (!enabled) return;
        
        Debug.Log($"Teleporting player ${player}");
        
        CleanupSpawnPoints();

        Vector2 targetPos = Vector2.zero;
        if (spawnPoints.Count > 0)
        {
            var spawnPoint = spawnPoints[currentState.spawnPointIndex];
            currentState.spawnPointIndex = (currentState.spawnPointIndex + 1) % spawnPoints.Count;
            targetPos = spawnPoint.position;
        }

        if (currentState.TryGetValue(player, out var playerID) &&
            playerID.TryGetComponent<PlayerMovement>(predictionManager, out var movement))
        {
            movement.Respawn(targetPos);
            return;
        }

        SpawnPlayerInternal(player);
    }

    private GameObject GetPrefabFor(PlayerID player)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0)
            return fallbackPrefab;

        var players = predictionManager.players.players;
        int index = -1;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == player)
            {
                index = i;
                break;
            }
        }

        return index < 0 ? fallbackPrefab : characterPrefabs[index % characterPrefabs.Length];
    }

    private void CleanupSpawnPoints()
    {
        bool hadNullEntry = false;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (!spawnPoints[i])
            {
                hadNullEntry = true;
                spawnPoints.RemoveAt(i);
                i--;
            }
        }

        if (hadNullEntry)
            PurrLogger.LogWarning($"Some spawn points were invalid and have been cleaned up.", this);
    }

    private void OnPlayerUnloadedScene(PlayerID player)
    {
        if (!_destroyOnDisconnect)
            return;

        if (currentState.TryGetValue(player, out var playerID))
        {
            hierarchy.Delete(playerID);
            currentState.Remove(player);
        }
    }

    private void OnPlayerLoadedScene(PlayerID player)
    {
        if (!enabled)
            return;

        if (_waitForSpawnCall)
            return;

        if (currentState.ContainsKey(player))
            return;

        SpawnPlayerInternal(player);
    }

    private void SpawnPlayerInternal(PlayerID player)
    {
        PredictedObjectID? newPlayer;

        CleanupSpawnPoints();

        var prefab = GetPrefabFor(player);

        if (spawnPoints.Count > 0)
        {
            var spawnPoint = spawnPoints[currentState.spawnPointIndex];
            currentState.spawnPointIndex = (currentState.spawnPointIndex + 1) % spawnPoints.Count;
            newPlayer = hierarchy.Create(prefab, spawnPoint.position, spawnPoint.rotation, player);
        }
        else
        {
            newPlayer = hierarchy.Create(prefab, owner: player);
        }

        if (!newPlayer.HasValue)
            return;

        currentState[player] = newPlayer.Value;
        predictionManager.SetOwnership(newPlayer, player);
    }

    public void SpawnPlayer(PlayerID player)
    {
        if (!enabled) return;
        if (currentState.ContainsKey(player)) return;
        SpawnPlayerInternal(player);
    }
}