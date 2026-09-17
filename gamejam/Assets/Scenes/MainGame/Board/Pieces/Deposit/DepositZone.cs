using PurrNet;
using PurrNet.Prediction;
using UnityEngine;

public class DepositZone : StatelessPredictedIdentity
{
    [SerializeField] private PredictedRigidbody2D pRigidbody;
    [SerializeField] private float rotationSpeed = 30f;
    
    protected override void Simulate(float delta)
    {
        float currentAngle = pRigidbody.rotation;
        currentAngle += rotationSpeed * delta;
        pRigidbody.MoveRotation(currentAngle);
    }

    public void WantDeposit(PlayerID player, PredictedComponentID playerCID)
    {
        if (InstanceHandler.TryGetInstance(out CoinManager coinManager))
        {
            coinManager.DunkCoins(player, playerCID, pRigidbody.position);
        }
        else
        {
            Debug.Log("Coin Manager Not Found");
        }
    }
}
