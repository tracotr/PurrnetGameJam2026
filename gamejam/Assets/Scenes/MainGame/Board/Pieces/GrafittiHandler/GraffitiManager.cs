using System;
using PurrNet;
using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;

public class GraffitiManager : PredictedIdentity<GraffitiManager.State>
{
    [SerializeField] private float costPerZone = 0.5f;
    
    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    public Action OnZoneUpdate;
    
    protected override State GetInitialState()
    {
        return new State
        {
            ZoneOwners = DisposableDictionary<PredictedComponentID, PlayerID>.Create()
        };
    }
    
    public int ScaleCost(PlayerID player, int baseCost)
    {
        int zonesOwned = GetOwnedCount(player);
        return Mathf.CeilToInt(baseCost * (1f + costPerZone * zonesOwned));
    }

    public void RegisterZone(PredictedComponentID zone)
    {
        currentState.ZoneOwners[zone] = default;
    }
    
    public void SetOwner(PredictedComponentID zone, PlayerID player)
    {
        currentState.ZoneOwners[zone] = player;
        OnZoneUpdate.Invoke();
    }

    public bool IsOwner(PredictedComponentID player, PlayerID asker)
    {
        if (currentState.ZoneOwners[player] == asker) return true;
        return false;
    }

    public int GetOwnedCount(PlayerID playerID)
    {
        int count = 0;
        foreach (var zone in currentState.ZoneOwners)
        {
            if (zone.Value == playerID)
            {
                count++;
            }
        }
        return count;
    }
    
    public struct State : IPredictedData<State>
    {
        public DisposableDictionary<PredictedComponentID, PlayerID> ZoneOwners;

        public void Dispose()
        {
            ZoneOwners.Dispose();
        }
    }
}
