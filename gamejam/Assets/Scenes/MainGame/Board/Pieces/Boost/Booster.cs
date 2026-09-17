using PurrNet.Prediction;
using UnityEngine;

public class Booster : StatelessPredictedIdentity
{
    public float boostForce = 25f;
    public float boostForceMax = 27f;
    [SerializeField] private AudioSource boostSound;

    public void PlayAudio()
    {
        boostSound.Play();
    }
}
