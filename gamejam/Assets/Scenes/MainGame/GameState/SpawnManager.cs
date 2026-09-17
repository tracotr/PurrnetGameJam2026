using System.Collections.Generic;
using PurrNet;
using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;

public class SpawnManager : PredictedIdentity<SpawnManager.State>
{
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }
    
    protected override void Simulate(ref State state, float delta)
    {

    }

    public Transform GetNextSpawnPoint()
    {
        var spawnPoint = spawnPoints[currentState.SpawnPointIndex];
        currentState.SpawnPointIndex = (currentState.SpawnPointIndex + 1) % spawnPoints.Count;
        return spawnPoint;
    }

    public struct State : IPredictedData<State>
    {
        public int SpawnPointIndex;

        public void Dispose() { }
    }
}