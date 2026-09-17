using Unity.Cinemachine;
using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    [SerializeField] private CinemachineCamera camera;

    private void Awake()
    {
        camera.Priority = -1;
    }

    public void Init()
    {
        camera.Priority = 10;
    }
    
    public void SetPriority(int priority)
    {
        camera.Priority = priority;
    }
}
