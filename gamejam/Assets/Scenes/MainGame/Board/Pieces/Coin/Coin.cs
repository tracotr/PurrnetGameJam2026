using PurrNet;
using PurrNet.Prediction;
using UnityEngine;

public class Coin : PredictedIdentity<Coin.State>
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private AudioSource audioSource;
    
    public float RespawnTime = 20.0f;
    private int coinWorth = 1;
    
    protected override void LateAwake()
    {
        particles.Play();
    }
    
    protected override State GetInitialState()
    {
        return new State()
        {
            IsActive = true,
        };
    }

    protected override void Simulate(ref State state, float delta)
    {
        state.RespawnTimer = Mathf.Max(state.RespawnTimer - delta, 0);

        if (state.RespawnTimer <= 0.0f)
        {
            Respawn();
        }
    }

    protected override void UpdateView(State viewState, State? verified)
    {
        
        spriteRenderer.enabled = viewState.IsActive;
        if (viewState.IsActive && verified.HasValue)
        {
            particles.Play();
        }
        else
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void PickUp(PlayerID playerID)
    {
        if (!currentState.IsActive) return;
        
        if (!InstanceHandler.TryGetInstance(out CoinManager coinManager))
        {
            Debug.LogError("Coin Manager not found");
            return;
        }
        
        coinManager.AddCoin(playerID, coinWorth);
        audioSource.Play();

        Hide();
    }

    private void Hide()
    {
        currentState.RespawnTimer = RespawnTime;
        currentState.IsActive = false;
    }

    private void Respawn()
    {
        currentState.IsActive = true;
    }
    
    
    public struct State : IPredictedData<State>
    {
        public float RespawnTimer;
        public bool IsActive;
        
        public void Dispose() { }
    }
}
