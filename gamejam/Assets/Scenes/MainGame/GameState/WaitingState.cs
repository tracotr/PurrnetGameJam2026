using PurrNet.Lobby;
using PurrNet.Prediction;
using PurrNet.Prediction.StateMachine;
using UnityEngine;

public class WaitingState : PredictedStateNode<WaitingState.State>
{

    protected override void StateSimulate(ref State state, float delta)
    {
        if (predictionManager.players.players.Count >= GameOrchestrator.active.activeLobby.players.Count)
        {
            machine.Next();
        }
    }


    public struct State : IPredictedData<State>
    {
        public void Dispose() { }
    }
}
