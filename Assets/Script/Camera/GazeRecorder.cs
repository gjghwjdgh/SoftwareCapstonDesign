using UnityEngine;
using System.Collections.Generic;

public class GazeRecorder : MonoBehaviour
{
    private bool isRecording = false;
    private List<Vector2> gazePath;
    private List<float> timestamps;

    void Update()
    {
        if (isRecording)
        {
            gazePath.Add(new Vector2(Screen.width / 2, Screen.height / 2));
            timestamps.Add(Time.time);
        }
    }

    public void StartRecording()
    {
        gazePath = new List<Vector2>();
        timestamps = new List<float>();
        isRecording = true;
    }

    public (List<Vector2> path, List<float> times) StopRecording()
    {
        isRecording = false;
        return (gazePath, timestamps);
    }
}