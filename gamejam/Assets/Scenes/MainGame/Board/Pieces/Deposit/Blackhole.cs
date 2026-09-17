using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;

public class Blackhole : StatelessPredictedIdentity
{
    [SerializeField] private float pullStrength = 15f;
    [SerializeField] private float maxPullRadius = 15f;
    [SerializeField] private float minPullRadius = 0.5f;
    [SerializeField] private LayerMask playerLayer;
    
    protected override void Simulate(float delta)
    {
        Collider2D[] results = Physics2D.OverlapCircleAll(transform.position, maxPullRadius, playerLayer);

        foreach (var col in results)
        {
            if (!col.TryGetComponent<PlayerMovement>(out var movement))
                continue;

            Vector2 toCenter = (Vector2)transform.position - (Vector2)movement.transform.position;
            float distance = toCenter.magnitude;

            if (distance <= minPullRadius)
                continue;

            Vector2 direction = toCenter / distance;
            float strength = pullStrength * (1f - distance / maxPullRadius);

            movement.ApplyExternalForce(direction * strength);
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxPullRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, minPullRadius);
    }
}
