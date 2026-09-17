using System.Collections.Generic;
using PurrNet.Prediction;
using UnityEngine;

public class PinballSpawner : PredictedIdentity<PinballSpawner.State>
{
    [SerializeField] private Pinball pinballPrefab;
    [SerializeField] private float spawnIntervalMin = 3;
    [SerializeField] private float spawnIntervalMax = 10;
    [SerializeField] private List<Transform> spawns;

    protected override State GetInitialState()
    {
        return new()
        {
            SpawnTimer = 10,
        };
    }

    protected override void Simulate(ref State state, float delta)
    {
        state.SpawnTimer = Mathf.Max(0.0f,  state.SpawnTimer - delta);

        if (state.SpawnTimer <= 0.0f)
        {
            // spawn pinball
            var spawnPos = spawns[state.SpawnIndex].position;
            predictionManager.hierarchy.Create(pinballPrefab.gameObject, spawnPos, Quaternion.identity);
            state.SpawnIndex = (state.SpawnIndex + 1) % spawns.Count;
            state.SpawnTimer = state.random.NextFloat(spawnIntervalMin, spawnIntervalMax);
        }
    }

    public struct State : IPredictedData<State>
    {
        public PredictedRandom random;
        
        public float SpawnTimer;
        public int SpawnIndex;
        
        public void Dispose() { }
    }
}
