using System.Collections.Generic;
using PurrNet;
using PurrNet.Lobby;
using PurrNet.Prediction;
using PurrNet.Prediction.StateMachine;
using TMPro;
using UnityEngine;

public class EndedState : PredictedStateNode<EndedState.State>
{
    [SerializeField] private TMP_Text winText;
    [SerializeField] private float waitTime = 6.0f;
    [SerializeField] private GameOverBroadcaster broadcaster;

    private static readonly List<(PlayerID player, int dunked)> _standings = new();

    private void Awake()
    {
        winText.enabled = false;
    }

    public override void Enter()
    {
        currentState.WaitTimer = waitTime;
        currentState.Winner = ResolveWinner();
    }

    public override void ViewEnter(bool isVerified)
    {
        if (isVerified) return;

        winText.enabled = true;
        RefreshText();
        PlayerNameRegistry.onNamesChanged += RefreshText;
    }

    public override void ViewExit(bool isVerified)
    {
        if (isVerified) return;

        PlayerNameRegistry.onNamesChanged -= RefreshText;
        winText.enabled = false;
    }

    private void OnDisable()
    {
        PlayerNameRegistry.onNamesChanged -= RefreshText;
    }

    private void RefreshText()
    {
        winText.text = currentState.Winner == default
            ? "It's a draw! Thanks for playing!"
            : $"{PlayerNameRegistry.Get(currentState.Winner)} has won! Thanks for playing!";
    }

    private PlayerID ResolveWinner()
    {
        if (!InstanceHandler.TryGetInstance(out CoinManager coinManager))
            return default;

        coinManager.GetStandings(_standings);
        if (_standings.Count == 0) return default;

        int top = _standings[0].dunked;

        // tie at the top means no single winner
        if (_standings.Count > 1 && _standings[1].dunked == top)
            return default;

        return _standings[0].player;
    }

    protected override void StateSimulate(ref State state, float delta)
    {
        state.WaitTimer = Mathf.Max(0.0f, state.WaitTimer - delta);

        if (state.WaitTimer <= 0.0f && !state.Ended)
        {
            state.Ended = true;
            broadcaster.EndGame();
        }
    }

    public struct State : IPredictedData<State>
    {
        public float WaitTimer;
        public bool Ended;
        public PlayerID Winner;
        public void Dispose() { }
    }
}