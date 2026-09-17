using PurrNet.Prediction;
using UnityEngine;

public class Pinball : PredictedIdentity<Pinball.State>
{
    [SerializeField] private PredictedRigidbody2D pRigidbody;
    [SerializeField] private float maxSpeed = 25f;

    protected override void LateAwake()
    {
        pRigidbody.onPredictedCollisionEnter += PRCollisionEnter;
        pRigidbody.onPredictedTriggerEnter += PRTriggerEnter;
    }

    protected override void OnDestroy()
    {
        pRigidbody.onPredictedCollisionEnter -= PRCollisionEnter;
        pRigidbody.onPredictedTriggerEnter -= PRTriggerEnter;
    }

    protected override void Simulate(ref State state, float delta)
    {
        if (state.HasPendingImpulse)
        {
            pRigidbody.velocity = state.PendingImpulse;
            state.PendingImpulse = Vector2.zero;
            state.HasPendingImpulse = false;
        }

        Vector2 v = pRigidbody.velocity;
        if (v.sqrMagnitude > maxSpeed * maxSpeed)
            pRigidbody.velocity = v.normalized * maxSpeed;
    }

    private void PRCollisionEnter(PredictedCollision2D collision)
    {
        if (!collision.other.TryGetComponent<Bumper>(out var bumper)) return;

        Vector2 normal = collision.contacts[0].normal;
        
        var knockbackForce = currentState.random.NextFloat(bumper.knockbackForce, bumper.knockbackForceMax);
        Queue(normal * bumper.knockbackForce);
    }

    private void PRTriggerEnter(PredictedTrigger trigger)
    {
        if (trigger.other.TryGetComponent<Booster>(out var booster))
        {
            Vector2 dir = booster.transform.up;
            var boostForce = currentState.random.NextFloat(booster.boostForce, booster.boostForceMax);
            Queue(dir.normalized * boostForce);
            booster.PlayAudio();
        }

        if (trigger.other.TryGetComponent<PinballReset>(out var _))
        {
            predictionManager.hierarchy.Delete(this);
        }
    }

    private void Queue(Vector2 velocity)
    {
        currentState.PendingImpulse += velocity;
        currentState.HasPendingImpulse = true;
    }

    public struct State : IPredictedData<State>
    {
        public PredictedRandom random;
        
        public Vector2 PendingImpulse;
        public bool HasPendingImpulse;

        public void Dispose() { }
    }
}