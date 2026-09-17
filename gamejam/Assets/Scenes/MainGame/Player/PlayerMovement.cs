using System.Collections.Generic;
using PurrNet;
using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : PredictedIdentity<PlayerMovement.Input, PlayerMovement.State>
{
    [Header("References")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference stopAction;
    [SerializeField] private InputActionReference rightClickAction;
    [SerializeField] private InputActionReference leftClickAction;

    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    
    [SerializeField] private TopDownCamera camera;
    [SerializeField] private PredictedTransform pTransform;
    public PredictedRigidbody2D pRigidbody;

    public static readonly List<PlayerMovement> all = new();
    
    [Header("Movement")]
    public float maxSpeed = 9f;
    public float acceleration = 22f;
    public float deceleration = 9f; 
    public float turnSpeed = 720f;
    public float velocityTurnSpeed = 680f; 

    [Header("Turn Momentum")]
    public float momentumPerDegree = 0.01f; 
    public float maxMomentumPerReversal = 0.065f;
    public float maxSpeedMomentum = 0.6f;
    public float momentumDecayRate = 0.3f; 

    [Header("Knockback")]
    public float knockbackDecay = 17f;
    public float knockbackLockDuration = 0.25f;
    
    [Header("Wall Friction")]
    public float wallFrictionPerSec = 20f;
    
    [Header("Braking")]
    public float brakeDeceleration = 20f;
    
    protected override void LateAwake()
    {
        pRigidbody.onPredictedCollisionEnter += PRCollisionEnter;
        pRigidbody.onPredictedCollisionStay += PRCollisionStay;
        pRigidbody.onPredictedTriggerEnter += PRTriggerEnter;
        
        all.Add(this);
        
        if (!isOwner) return;

        camera.Init();
        moveAction.action.Enable();
        stopAction.action.Enable();

        leftClickAction.action.Enable();
        rightClickAction.action.Enable();
    }

    protected override void OnDestroy()
    {
        pRigidbody.onPredictedCollisionEnter -= PRCollisionEnter;
        pRigidbody.onPredictedCollisionStay -= PRCollisionStay;
        pRigidbody.onPredictedTriggerEnter -= PRTriggerEnter;
        
        if (!isOwner) return;
        all.Remove(this);
    }

    protected override State GetInitialState()
    {
        return new State()
        {
            FacingAngle = 90f,
            VelocityAngle = 90f,
            SpeedMomentum = 0f,
            HasPendingKnockback = false,
            LockTimer = 0f,
        };
    }

    protected override void GetFinalInput(ref Input input)
    {
        input.MovementDirection = moveAction.action.ReadValue<Vector2>();
    }
    
    protected override void SanitizeInput(ref Input input)
    {
        Vector2 clamped = Vector2.ClampMagnitude(input.MovementDirection, 1f);
        input.MovementDirection = clamped;
    }

    protected override void UpdateView(State viewState, State? verified)
    {
        float rad = viewState.VelocityAngle * Mathf.Deg2Rad;
        float x = Mathf.Cos(rad);
        
        animator.SetFloat(SpeedHash, viewState.CurrentSpeed / maxSpeed, 0.05f, Time.deltaTime);
        
        if (viewState.CurrentSpeed > 0.01f && Mathf.Abs(x) > 0.15f)
            sprite.flipX = x < 0f;
    }

    protected override void UpdateInput(ref Input input)
    {
        input.WantStop = stopAction.action.IsPressed();
        input.WantFlipLeft = leftClickAction.action.IsPressed();
        input.WantFlipRight = rightClickAction.action.IsPressed();
    }

    protected override void Simulate(Input input, ref State state, float delta)
    {
        if (state.HasPendingKnockback)
        {
            state.VelocityAngle = Mathf.Atan2(state.PendingKnockback.y, state.PendingKnockback.x) * Mathf.Rad2Deg;
            state.FacingAngle = state.VelocityAngle;
            state.CurrentSpeed = Mathf.Max(state.CurrentSpeed, state.PendingKnockback.magnitude);
            state.LockTimer = knockbackLockDuration;

            state.PendingKnockback = Vector2.zero;
            state.HasPendingKnockback = false;
        }

        if (state.HasPendingBoost)
        {
            state.VelocityAngle = Mathf.Atan2(state.PendingBoost.y, state.PendingBoost.x) * Mathf.Rad2Deg;
            state.FacingAngle = state.VelocityAngle;
            state.CurrentSpeed = Mathf.Max(state.CurrentSpeed, state.PendingBoost.magnitude);
            state.LockTimer = knockbackLockDuration / 2;

            state.PendingBoost = Vector2.zero;
            state.HasPendingBoost = false;
        }

        bool knockbackActive = state.LockTimer > 0f;
        bool braking = input.WantStop && !knockbackActive;

        bool againstWall = state.IsAgainstWall;
        state.IsAgainstWall = false;

        Vector2 inputDir = input.MovementDirection;
        bool isInput = inputDir.sqrMagnitude > 0.01f;

        float previousFacing = state.FacingAngle;
        if (isInput && !knockbackActive)
        {
            float targetAngle = Mathf.Atan2(inputDir.y, inputDir.x) * Mathf.Rad2Deg;
            state.FacingAngle = Mathf.MoveTowardsAngle(state.FacingAngle, targetAngle, turnSpeed * delta);
        }
        float turnDelta = Mathf.DeltaAngle(previousFacing, state.FacingAngle);
        UpdateTurnMomentum(ref state, turnDelta, delta);

        if (knockbackActive)
        {
            state.CurrentSpeed = Mathf.MoveTowards(state.CurrentSpeed, 0f, knockbackDecay * delta);
            state.LockTimer = Mathf.Max(0f, state.LockTimer - delta);
        }
        else if (braking)
        {
            state.CurrentSpeed = Mathf.MoveTowards(state.CurrentSpeed, 0f, brakeDeceleration * delta);
            state.VelocityAngle = Mathf.MoveTowardsAngle(state.VelocityAngle, state.FacingAngle, velocityTurnSpeed * delta);
        }
        else
        {
            float targetSpeed = isInput ? maxSpeed * (1f + state.SpeedMomentum) : 0f;
            float accel = targetSpeed > state.CurrentSpeed ? acceleration : deceleration;
            state.CurrentSpeed = Mathf.MoveTowards(state.CurrentSpeed, targetSpeed, accel * delta);
            state.VelocityAngle = Mathf.MoveTowardsAngle(state.VelocityAngle, state.FacingAngle, velocityTurnSpeed * delta);

            if (againstWall)
            {
                state.CurrentSpeed = Mathf.Max(0f, state.CurrentSpeed - wallFrictionPerSec * delta);
            }
        }

        pRigidbody.velocity = VelocityFromAngleSpeed(state.VelocityAngle, state.CurrentSpeed) + state.PendingExternalForce;
        state.PendingExternalForce = Vector2.zero;
    }
    
    public void Respawn(Vector2 position, float facingAngle = 90f)
    {
        ref State state = ref currentState;
        state.CurrentSpeed = 0f;
        state.FacingAngle = facingAngle;
        state.VelocityAngle = facingAngle;
        state.SpeedMomentum = 0f;
        state.TurnSign = 0f;
        state.SweepAccum = 0f;
        state.PendingExternalForce = Vector2.zero;
        state.PendingKnockback = Vector2.zero;
        state.HasPendingKnockback = false;
        state.PendingBoost = Vector2.zero;
        state.HasPendingBoost = false;
        state.LockTimer = 0f;
        state.IsAgainstWall = false;

        pRigidbody.position = position;
        pRigidbody.velocity = Vector2.zero;
        pTransform.ResetInterpolation();
    }

    private static Vector2 VelocityFromAngleSpeed(float angleDeg, float speed)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
    }
    
    private void UpdateTurnMomentum(ref State state, float turnDelta, float delta)
    {
        float sign = Mathf.Abs(turnDelta) > 0.01f ? Mathf.Sign(turnDelta) : 0f;

        if (sign != 0f && state.TurnSign != 0f && !Mathf.Approximately(sign, state.TurnSign))
        {
            float gain = Mathf.Min(state.SweepAccum * momentumPerDegree, maxMomentumPerReversal);
            state.SpeedMomentum = Mathf.Min(state.SpeedMomentum + gain, maxSpeedMomentum);
            state.SweepAccum = 0f;
        }

        if (sign != 0f)
        {
            state.SweepAccum += Mathf.Abs(turnDelta);
            state.TurnSign = sign;
        }

        state.SpeedMomentum = Mathf.MoveTowards(state.SpeedMomentum, 0f, momentumDecayRate * delta);
    }


    public void ApplyKnockback(float force, Vector3 direction)
    {
        Vector2 dir2D = new Vector2(direction.x, direction.y);
        if (dir2D.sqrMagnitude < 0.0001f) return;
        dir2D.Normalize();

        ref State state = ref currentState;
        state.PendingKnockback += dir2D * force;
        state.HasPendingKnockback = true;
    }

    private void ApplyBoost(float force, Vector3 direction)
    {
        Vector2 dir2D = new Vector2(direction.x, direction.y);
        if (dir2D.sqrMagnitude < 0.0001f) return;
        dir2D.Normalize();

        ref State state = ref currentState;
        state.PendingBoost += dir2D * force;
        state.HasPendingBoost = true;
    }
    
    private void PRCollisionEnter(PredictedCollision2D collision)
    {
        Vector2 normal = collision.contacts[0].normal;

        if (collision.other.TryGetComponent<Bumper>(out var bumper))
        {
            if (currentState.LockTimer > 0f) return; // dont reapply knockback if we still cant move
            
            var boostForce = currentState.random.NextFloat(bumper.knockbackForce, bumper.knockbackForceMax);
            ApplyKnockback(boostForce, normal);
            return;
        }

        if (collision.other.TryGetComponent<Pinball>(out _))
        {
            if (owner.HasValue && InstanceHandler.TryGetInstance<CoinManager>(out var cM))
            {
                int current = cM.GetCurrentCoins(owner.Value);
                int loss = Mathf.CeilToInt(current * 0.25f);
                if (loss > 0)
                    cM.RemoveCoin(owner.Value, loss);
            }
        }

        EvaluateCollisions(normal);
    }

    private void PRCollisionStay(PredictedCollision2D collision)
    {        
        Vector2 normal = collision.contacts[0].normal;
        
        if (collision.other.TryGetComponent<Bumper>(out _))
            return;

        EvaluateCollisions(normal);
    }
    
    private void PRTriggerEnter(PredictedTrigger trigger)
    {
        if (trigger.other.TryGetComponent<Booster>(out var booster))
        {
            var boostForce = currentState.random.NextFloat(booster.boostForce, booster.boostForceMax);
            
            ApplyBoost(boostForce, booster.transform.up);
            booster.PlayAudio();
            
            Vector2 direction = booster.transform.up;
            currentState.VelocityAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
    }


    private void EvaluateCollisions(Vector2 normal)
    {
        ref State state = ref currentState;

        Vector2 currentDir = new Vector2(Mathf.Cos(state.VelocityAngle * Mathf.Deg2Rad), Mathf.Sin(state.VelocityAngle * Mathf.Deg2Rad));
        
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        if (Vector2.Dot(tangent, currentDir) < 0f)
            tangent = -tangent;

        float slideAngle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        state.VelocityAngle = slideAngle;
        state.FacingAngle = slideAngle;

        state.IsAgainstWall = true;
    }

    public void AddLockout(float timer)
    {
        currentState.LockTimer = timer;
    }

    public void RemoveLockout()
    {
        currentState.LockTimer = 0.0f;
    }

    public void ApplyExternalForce(Vector2 force)
    {
        currentState.PendingExternalForce += force;
    }
    
    public struct Input : IPredictedData<Input>
    {
        public Vector2 MovementDirection;
        public bool WantStop;
        
        public bool WantFlipLeft;
        public bool WantFlipRight;

        public void Dispose() { }
    }

    public struct State : IPredictedData<State>
    {
        public PredictedRandom random;
        
        public float CurrentSpeed;
        public float FacingAngle;
        public float VelocityAngle;
        public float SpeedMomentum;
        public float TurnSign;
        public float SweepAccum;
        
        public Vector2 PendingExternalForce;
        
        public Vector2 PendingKnockback;
        public bool HasPendingKnockback;

        public Vector2 PendingBoost;
        public bool HasPendingBoost;
        
        public float LockTimer;
        public bool IsAgainstWall;
        
        public void Dispose() { }
    }
}