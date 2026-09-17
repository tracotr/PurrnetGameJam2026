using PurrNet;
using PurrNet.Prediction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGraffiti : PredictedIdentity<PlayerGraffiti.Input, PlayerGraffiti.State>
{
    [Header("References")]
    [SerializeField] private PredictedRigidbody2D pRigidbody;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PredictedRigidbody2D gameRigidbody;
    [SerializeField] private SpriteRenderer gameSprite;
    [SerializeField] private Transform targetMarker;

    [SerializeField] private TopDownCamera minigameCamera;
    [SerializeField] private GameObject minigameDisplay;
    [SerializeField] private Transform minigameTransform;
    [SerializeField] private InputActionReference moveAction;

    [SerializeField] private TextMeshPro coinText;
    [SerializeField] private TextMeshPro zoneText;
    [SerializeField] private TextMeshPro hintText;

    [SerializeField] private AudioSource audioSourceLoop;
    [SerializeField] private AudioSource audioSourceComplete;

    [Header("Trail")]
    [SerializeField] private LineRenderer trail;
    [SerializeField] private float minPointDistance = 0.15f;
    [SerializeField] private int maxPoints = 2048;

    [Header("Gameplay settings")]
    [SerializeField] private int targetsRequired = 5;
    [SerializeField] private float gameMoveSpeed = 20f;
    [SerializeField] private float reachRadius = 0.8f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float gameTime = 30.0f;
    [SerializeField] private Vector2 maxTargetSpawn = new Vector2(6f, 6f);
    [SerializeField] private float startGameMinSpeed = 2.0f;

    private PredictedEvent _onTargetHit;
    private Vector2 _lastTrailPoint;
    private bool _trailActive;

    protected override void LateAwake()
    {
        pRigidbody.onPredictedTriggerEnter += PRigidbodyTriggerEnter;
        pRigidbody.onPredictedTriggerStay += PRigidbodyTriggerStay;
        pRigidbody.onPredictedTriggerExit += PRigidbodyTriggerExit;

        minigameDisplay.SetActive(false);
        gameSprite.enabled = false;
        targetMarker.gameObject.SetActive(false);

        if (InstanceHandler.TryGetInstance(out CoinManager coinManager))
        {
            coinManager.OnCoinUpdate += UpdateCoinDisplay;
        }

        if (InstanceHandler.TryGetInstance(out GraffitiManager graffitiManager))
        {
            graffitiManager.OnZoneUpdate += UpdateCoinDisplay;
            graffitiManager.OnZoneUpdate += UpdateZoneCountDisplay;
        }

        _onTargetHit = new PredictedEvent(predictionManager, this);
        _onTargetHit.AddListener(PlayComplete);

        hintText.enabled = isOwner;

        if (!isOwner) return;

        moveAction.action.Enable();
    }

    private void PlayComplete()
    {
        if (!isOwner) return;
        audioSourceComplete.pitch = 1f + Mathf.Min(currentState.TargetsHit, 5) * 0.06f;
        audioSourceComplete.Play();
    }

    // Presentation is derived from view state, never driven from the simulate path.
    private void ApplyPresentation(bool active)
    {
        if (!isOwner) return;
        if (minigameDisplay.activeSelf == active) return;

        minigameDisplay.SetActive(active);
        minigameCamera.SetPriority(active ? 11 : -11);
    }

    private void UpdateTrail(State viewState)
    {
        if (!viewState.GameActive)
        {
            if (!_trailActive) return;
            trail.positionCount = 0;
            _trailActive = false;
            return;
        }

        Vector2 local = minigameTransform.InverseTransformPoint(gameRigidbody.transform.position);

        if (!_trailActive)
        {
            _trailActive = true;
            trail.positionCount = 1;
            trail.SetPosition(0, local);
            _lastTrailPoint = local;
            return;
        }

        if ((local - _lastTrailPoint).sqrMagnitude < minPointDistance * minPointDistance) return;
        if (trail.positionCount >= maxPoints) return;

        trail.positionCount++;
        trail.SetPosition(trail.positionCount - 1, local);
        _lastTrailPoint = local;
    }

    private void UpdateCoinDisplay()
    {
        if (InstanceHandler.TryGetInstance(out CoinManager coinManager))
        {
            if (!owner.HasValue)
            {
                Debug.Log($"No id found for {name}>?????");
                return;
            }

            var val = owner.Value;
            coinText.text = $"{coinManager.GetCurrentCoins(val)}";
        }
    }

    private void UpdateZoneCountDisplay()
    {
        if (InstanceHandler.TryGetInstance(out GraffitiManager gManager))
        {
            if (!owner.HasValue)
            {
                Debug.Log($"No id found for {name}>?????");
                return;
            }

            var val = owner.Value;
            zoneText.text = $"{gManager.GetOwnedCount(val)}";
        }
    }

    protected override State GetInitialState()
    {
        return new State()
        {
            GameActive = false,
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

    protected override void SetUnityState(State state)
    {
        if (!isOwner) return;

        gameSprite.enabled = state.GameActive;
        targetMarker.gameObject.SetActive(state.GameActive);
        targetMarker.position = state.CurrentTarget;
    }

    protected override void UpdateView(State viewState, State? verified)
    {
        if (!isOwner) return;

        UpdateTrail(viewState);
        ApplyPresentation(viewState.GameActive);

        gameSprite.enabled = viewState.GameActive;
        targetMarker.gameObject.SetActive(viewState.GameActive);
        targetMarker.position = viewState.CurrentTarget;

        float speed = viewState.GameActive ? gameRigidbody.velocity.magnitude : 0f;
        float target = Mathf.Clamp01(speed / gameMoveSpeed);

        if (target > 0.05f && !audioSourceLoop.isPlaying)
            audioSourceLoop.Play();

        audioSourceLoop.volume = Mathf.MoveTowards(audioSourceLoop.volume, target, Time.unscaledDeltaTime * 6f);
        audioSourceLoop.pitch = Mathf.Lerp(0.85f, 1.2f, target);

        if (audioSourceLoop.volume <= 0.001f && audioSourceLoop.isPlaying)
            audioSourceLoop.Stop();
    }

    protected override void Simulate(Input input, ref State state, float delta)
    {
        if (!state.GameActive) return;

        state.GameTimer = Mathf.Max(0.0f, state.GameTimer - delta);
        if (state.GameTimer <= 0.0f)
        {
            EndGame();
            return;
        }

        if (!CanAfford(state.CurrentZone))
        {
            AbandonGame(state.CurrentZone);
            return;
        }

        Vector2 origin = minigameTransform.position;
        Vector2 originDelta = origin - state.LastOrigin;
        state.LastOrigin = origin;

        gameRigidbody.position += originDelta;
        state.CurrentTarget += originDelta;

        Vector2 desiredVelocity = input.MovementDirection * gameMoveSpeed;
        gameRigidbody.velocity = Vector2.MoveTowards(gameRigidbody.velocity, desiredVelocity, acceleration * delta);
        gameRigidbody.position = ClampToPlayArea(gameRigidbody.position, origin);

        if (!((gameRigidbody.position - state.CurrentTarget).sqrMagnitude <= reachRadius * reachRadius)) return;

        state.TargetsHit++;
        _onTargetHit.Invoke();

        if (state.TargetsHit >= targetsRequired)
        {
            if (predictionManager.TryGetIdentity(state.CurrentZone, out var identity))
            {
                if (identity.TryGetComponent<GraffitiZone>(out var zone))
                {
                    if (!owner.HasValue) return;

                    zone.Finish(owner.Value);
                    zone.UnregisterPlayer(playerMovement.id);
                }
            }

            EndGame();
            return;
        }

        state.CurrentTarget = GetRandomSpawn(ref state.random, origin);
    }

    private bool CanAfford(PredictedComponentID zoneID)
    {
        if (!owner.HasValue) return false;
        if (!InstanceHandler.TryGetInstance(out CoinManager coinManager)) return true;
        if (!predictionManager.TryGetIdentity(zoneID, out var identity)) return true;
        if (!identity.TryGetComponent<GraffitiZone>(out var zone)) return true;

        int cost = InstanceHandler.TryGetInstance(out GraffitiManager gm)
            ? gm.ScaleCost(owner.Value, zone.zoneCost)
            : zone.zoneCost;

        return coinManager.GetCurrentCoins(owner.Value) >= cost;
    }

    private void AbandonGame(PredictedComponentID zoneID)
    {
        if (predictionManager.TryGetIdentity(zoneID, out var identity)
            && identity.TryGetComponent<GraffitiZone>(out var zone))
        {
            zone.FinishNoWinner();
            zone.UnregisterPlayer(playerMovement.id);
        }

        EndGame();
    }

    private void PRigidbodyTriggerEnter(PredictedTrigger trigger)
    {
        if (predictionManager.isVerifiedAndReplaying) return;
        if (trigger.other.TryGetComponent<GraffitiZone>(out var gZone))
        {
            gZone.RegisterPlayer(playerMovement.id);
        }

        if (trigger.other.TryGetComponent<Coin>(out var coin))
        {
            if (!owner.HasValue) return;

            coin.PickUp(owner.Value);
        }

        if (trigger.other.TryGetComponent<DepositZone>(out var dZone))
        {
            if (!owner.HasValue) return;

            dZone.WantDeposit(owner.Value, playerMovement.id);
        }
    }

    private void PRigidbodyTriggerStay(PredictedTrigger trigger)
    {
        if (predictionManager.isVerifiedAndReplaying) return;
        if (trigger.other.TryGetComponent<GraffitiZone>(out var gZone) && InstanceHandler.TryGetInstance<CoinManager>(out var cManager))
        {
            if (!owner.HasValue) return;
            bool goingToFast = pRigidbody.velocity.magnitude > startGameMinSpeed;

            int cost = InstanceHandler.TryGetInstance(out GraffitiManager gMan)
                ? gMan.ScaleCost(owner.Value, gZone.zoneCost)
                : gZone.zoneCost;

            bool notEnoughDough = cManager.GetCurrentCoins(owner.Value) < cost;

            if (goingToFast)
            {
                if (isOwner) hintText.text = "im too fast!!";
                return;
            }

            if (notEnoughDough)
            {
                if (isOwner) hintText.text = $"im so broke... ({cost} coins)";
                return;
            }

            if (isOwner) hintText.text = "";
            StartGame(gZone.id);
        }
    }

    private void PRigidbodyTriggerExit(PredictedTrigger trigger)
    {
        if (predictionManager.isVerifiedAndReplaying) return;

        if (isOwner) hintText.text = "";

        if (trigger.other.TryGetComponent<GraffitiZone>(out var gZone))
        {
            EndGame();
            gZone.UnregisterPlayer(playerMovement.id);
        }
    }

    private void StartGame(PredictedComponentID zoneID)
    {
        if (currentState.GameActive) return;

        if (InstanceHandler.TryGetInstance<GraffitiManager>(out var manager))
        {
            if (manager.IsOwner(zoneID, owner.GetValueOrDefault()))
            {
                return;
            }
        }

        currentState.CurrentZone = zoneID;
        currentState.GameActive = true;
        currentState.TargetsHit = 0;
        currentState.LastOrigin = pRigidbody.position;
        currentState.CurrentTarget = GetRandomSpawn(ref currentState.random, currentState.LastOrigin);

        playerMovement.AddLockout(gameTime);
        currentState.GameTimer = gameTime;

        gameRigidbody.velocity = Vector2.zero;
        gameRigidbody.MovePosition(currentState.LastOrigin);
        gameRigidbody.currentState.isSleeping = false;

        SetUnityState(currentState);
    }

    private Vector2 ClampToPlayArea(Vector2 position, Vector2 origin)
    {
        float x = Mathf.Clamp(position.x, origin.x - maxTargetSpawn.x, origin.x + maxTargetSpawn.x);
        float y = Mathf.Clamp(position.y, origin.y - maxTargetSpawn.y, origin.y + maxTargetSpawn.y);
        return new Vector2(x, y);
    }

    private Vector2 GetRandomSpawn(ref PredictedRandom random, Vector2 origin)
    {
        float x = random.NextFloat(-maxTargetSpawn.x, maxTargetSpawn.x);
        float y = random.NextFloat(-maxTargetSpawn.y, maxTargetSpawn.y);
        return origin + new Vector2(x, y);
    }

    private void EndGame()
    {
        if (!currentState.GameActive) return;

        currentState.CurrentZone = default;
        currentState.GameActive = false;
        currentState.TargetsHit = 0;
        currentState.GameTimer = 0f;

        playerMovement.RemoveLockout();

        gameRigidbody.velocity = Vector2.zero;
        gameRigidbody.MovePosition(minigameTransform.position);
        gameRigidbody.currentState.isSleeping = true;

        SetUnityState(currentState);
    }

    public struct Input : IPredictedData<Input>
    {
        public Vector3 MovementDirection;

        public void Dispose() { }
    }

    public struct State : IPredictedData<State>
    {
        public PredictedComponentID CurrentZone;
        public float GameTimer;
        public bool GameActive;

        public PredictedRandom random;

        public int TargetsHit;
        public Vector2 CurrentTarget;
        public Vector2 LastOrigin;

        public void Dispose() { }
    }
}