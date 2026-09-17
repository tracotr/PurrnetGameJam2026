using System;
using PurrNet;
using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;

public class GraffitiZone : PredictedIdentity<GraffitiZone.State>
{
    [SerializeField] private float knockbackForce = 25f;
    public int zoneCost = 5;
    [SerializeField] private float exitAngleRange = 60f;
    private GraffitiManager graffitiManager;
    [SerializeField] private AudioSource audioSourceStart;
    [SerializeField] private AudioSource audioSourceEnd;

    [SerializeField] private SpriteRenderer uncappedSprite;
    [SerializeField] private SpriteRenderer cappedSprite;
    
    protected override State GetInitialState()
    {
        return new State()
        {
            PlayersInZone = DisposableList<PredictedComponentID>.Create()
        };
    }

    protected override void LateAwake()
    {
        if (InstanceHandler.TryGetInstance(out graffitiManager))
        {
            graffitiManager.RegisterZone(id);
        }
        else
        {
            Debug.LogError("GraffitiManager not found");
        }
    }

    public void FinishNoWinner()
    {
        foreach (var player in currentState.PlayersInZone)
        {
            if (!player.objectId.TryGetComponent<PlayerMovement>(predictionManager, out var movement)) continue;
            
            Vector2 exitDirection = GetRandomExitDirection(ref currentState.random);
            movement.ApplyKnockback(knockbackForce, exitDirection);
        }
    }

    public void Finish(PlayerID? winner)
    {
        audioSourceEnd.Play();
        
        if (winner.HasValue)
        {
            if (InstanceHandler.TryGetInstance<CoinManager>(out var coinManager))
            {
                int cost = InstanceHandler.TryGetInstance(out GraffitiManager gm)
                    ? gm.ScaleCost(winner.Value, zoneCost)
                    : zoneCost;
                
                coinManager.RemoveCoin(winner.Value, cost);
            }
            else
            {
                Debug.Log($"{name} could not find coin manager");
            }
            
            string path = $"PlayerSkins/PlayerSkin{winner.Value}";
            PlayerSkinData data = Resources.Load<PlayerSkinData>(path);
            
            if (data)
                cappedSprite.color = data.color;

            uncappedSprite.enabled = false;
            cappedSprite.enabled = true;
            
            graffitiManager?.SetOwner(this.id, winner.Value);
        }
        
        foreach (var player in currentState.PlayersInZone)
        {
            if (!player.objectId.TryGetComponent<PlayerMovement>(predictionManager, out var movement)) continue;
            
            Vector2 exitDirection = GetRandomExitDirection(ref currentState.random);
            movement.ApplyKnockback(knockbackForce, exitDirection);
        }
    }
    
    private Vector2 GetRandomExitDirection(ref PredictedRandom random)
    {
        float offset = random.NextFloat(-exitAngleRange, exitAngleRange);
        Quaternion rotation = Quaternion.AngleAxis(offset, Vector3.forward);
        Vector3 zoneForward = transform.up;
        return rotation * zoneForward;
    }

    public void RegisterPlayer(PredictedComponentID player)
    {
        currentState.PlayersInZone.Add(player);
        audioSourceStart.Play();
    }
    
    public void UnregisterPlayer(PredictedComponentID player)
    {
        currentState.PlayersInZone.Remove(player);
    }

    public struct State : IPredictedData<State>
    {
        public PredictedRandom random;
        public DisposableList<PredictedComponentID> PlayersInZone;
        
        public override string ToString()
        {
            var str = "No Players";
            str = !PlayersInZone.isDisposed ? PlayersInZone.ToString() : str;
            return str;
        }
        
        public void Dispose()
        {
            PlayersInZone.Dispose();
        }
    }
}
