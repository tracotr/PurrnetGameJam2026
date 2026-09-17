using PurrNet.Prediction;
using PurrNet.Prediction.StateMachine;
using TMPro;
using UnityEngine;

public class RunningState : PredictedStateNode<RunningState.State>
{
    [SerializeField] private float matchTime = 180.0f;
    [SerializeField] private float endMusicThreshold = 30.0f;
    [SerializeField] private AudioSource normalMusic;
    [SerializeField] private AudioSource endMusic;
    [SerializeField] private TMP_Text text;
    
    public override void Enter()
    {
        currentState.MatchTimer = matchTime;
    }

    protected override void UpdateView(State vs, State? verified)
    {
        var seconds = Mathf.CeilToInt(vs.MatchTimer);
        if (seconds != vs.lastShownSecond)
        {
            vs.lastShownSecond = seconds;
            text.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        if (currentState.endMusicStarted || vs.MatchTimer > endMusicThreshold) return;

        currentState.endMusicStarted = true;
        normalMusic.Stop();
        endMusic.Play();
    }

    protected override void StateSimulate(ref State state, float delta)
    {
        state.MatchTimer = Mathf.Max(0, state.MatchTimer - delta);

        if (state.MatchTimer <= 0.0f)
        {
            machine.Next();
        }
    }

    public struct State : IPredictedData<State>
    {
        public float MatchTimer;
        public bool endMusicStarted;
        public int lastShownSecond;

        override public string ToString()
        {
            return $"{MatchTimer}\n" +
                   $"{endMusicStarted}";
        }
        
        public void Dispose() { }
    }
}
