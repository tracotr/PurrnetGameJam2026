using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

public class Flipper : PredictedIdentity<Flipper.State>
{
    [SerializeField] private PredictedRigidbody2D rightFlipper;
    [SerializeField] private PredictedRigidbody2D leftFlipper;

    [SerializeField] private float leftFlipperDefaultAngle = -25.0f;
    [SerializeField] private float rightFlipperDefaultAngle = 25.0f;
    
    [SerializeField] private float flipHoldDuration = 0.12f;

    [SerializeField] private float flipSpeed = 360f; // degrees per second
    [SerializeField] private float hitForceMultiplier = 1.5f;

    [SerializeField] private AudioSource sourceL;
    [SerializeField] private AudioSource sourceR;
    
    protected override void LateAwake()
    {
        leftFlipper.onPredictedCollisionEnter += (collision) => OnFlipperHit(true, collision.other, collision.contacts[0]);
        leftFlipper.onPredictedCollisionStay += (collision) => OnFlipperHit(true, collision.other, collision.contacts[0]);
        
        rightFlipper.onPredictedCollisionEnter += (collision) => OnFlipperHit(false, collision.other, collision.contacts[0]);
        rightFlipper.onPredictedCollisionStay += (collision) => OnFlipperHit(false, collision.other, collision.contacts[0]);
    }

    protected override void OnDestroy()
    {
        leftFlipper.onPredictedCollisionEnter -= (collision) => OnFlipperHit(true, collision.other, collision.contacts[0]);
        leftFlipper.onPredictedCollisionStay -= (collision) => OnFlipperHit(true, collision.other, collision.contacts[0]);
        
        rightFlipper.onPredictedCollisionEnter -= (collision) => OnFlipperHit(false, collision.other, collision.contacts[0]);
        rightFlipper.onPredictedCollisionStay -= (collision) => OnFlipperHit(false, collision.other, collision.contacts[0]);
    }

    protected override State GetInitialState()
    {
        return new State
        {
            LeftAngle = leftFlipperDefaultAngle,
            RightAngle = rightFlipperDefaultAngle,
        };
    }
    
    protected override void Simulate(ref State state, float delta)
    {
        bool leftPressed = false;
        bool rightPressed = false;

        foreach (var movement in PlayerMovement.all)
        {
            leftPressed  |= movement.currentInput.WantFlipLeft;
            rightPressed |= movement.currentInput.WantFlipRight;
        }

        if (leftPressed && !state.PrevLeftPressed)
            state.LeftHoldTimer = flipHoldDuration;

        if (rightPressed && !state.PrevRightPressed)
            state.RightHoldTimer = flipHoldDuration;

        state.PrevLeftPressed = leftPressed;
        state.PrevRightPressed = rightPressed;

        state.LeftHoldTimer = Mathf.Max(0f, state.LeftHoldTimer - delta);
        state.RightHoldTimer = Mathf.Max(0f, state.RightHoldTimer - delta);

        bool left = state.LeftHoldTimer > 0f;
        bool right = state.RightHoldTimer > 0f;

        float leftTarget  = left  ? -leftFlipperDefaultAngle  : leftFlipperDefaultAngle;
        float rightTarget = right ? -rightFlipperDefaultAngle : rightFlipperDefaultAngle;

        float previousLeft = state.LeftAngle;
        state.LeftAngle = Mathf.MoveTowardsAngle(state.LeftAngle, leftTarget, flipSpeed * delta);
        state.LeftAngularVelocity = Mathf.DeltaAngle(previousLeft, state.LeftAngle) / delta;

        float previousRight = state.RightAngle;
        state.RightAngle = Mathf.MoveTowardsAngle(state.RightAngle, rightTarget, flipSpeed * delta);
        state.RightAngularVelocity = Mathf.DeltaAngle(previousRight, state.RightAngle) / delta;

        leftFlipper.MoveRotation(state.LeftAngle);
        rightFlipper.MoveRotation(state.RightAngle);
    }

    private void OnFlipperHit(bool isLeft, GameObject other, Physics2DContactPoint contact)
    {
        float angularVelocity = isLeft ? currentState.LeftAngularVelocity : currentState.RightAngularVelocity;

        if (Mathf.Abs(angularVelocity) < 1f)
        {
            return;
        }

        Vector2 contactPoint = contact.point;

        var flipper = isLeft ? leftFlipper : rightFlipper;
        Vector2 pivot = flipper.position;
        Vector2 radius = contactPoint - pivot;

        float angularRad = angularVelocity * Mathf.Deg2Rad;
        Vector2 tangentVelocity = new Vector2(-radius.y, radius.x) * angularRad;
        Vector2 impulse = tangentVelocity * hitForceMultiplier;

        
        if (isLeft)
        { 
            sourceL.Play();
        }
        else
        {
            sourceR.Play();
        }
        
        if (other.TryGetComponent<PlayerMovement>(out var playerMovement))
        {
            playerMovement.ApplyKnockback(impulse.magnitude, impulse);
            return;
        }
        
        if (other.TryGetComponent<PredictedRigidbody2D>(out var otherBody))
        {
            otherBody.AddForce(impulse, ForceMode2D.Impulse);
        }
    }
    

    public struct State : IPredictedData<State>
    {
        public float LeftAngle;
        public float RightAngle;
        public float LeftAngularVelocity;
        public float RightAngularVelocity;
        
        public bool PrevLeftPressed;
        public bool PrevRightPressed;
        public float LeftHoldTimer;
        public float RightHoldTimer;

        public void Dispose() { }
    }
}