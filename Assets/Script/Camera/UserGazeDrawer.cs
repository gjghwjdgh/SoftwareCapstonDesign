using UnityEngine;
using System.Collections.Generic;

public class UserGazeDrawer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private bool isDrawing = false;
    private List<Vector2> gazePath;
    private List<float> timestamps;
    private Camera mainCamera;
    private float projectionDepth = 10f;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        mainCamera = Camera.main;
        lineRenderer.enabled = false;
        lineRenderer.positionCount = 0;
    }

    void Update()
    {
        if (isDrawing)
        {
            Vector2 currentGazePoint = new Vector2(Screen.width / 2, Screen.height / 2);
            gazePath.Add(currentGazePoint);
            timestamps.Add(Time.time);

            lineRenderer.positionCount = gazePath.Count;
            Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(currentGazePoint.x, currentGazePoint.y, projectionDepth));
            lineRenderer.SetPosition(gazePath.Count - 1, worldPoint);
        }
    }

    public void StartDrawing()
    {
        gazePath = new List<Vector2>();
        timestamps = new List<float>();
        lineRenderer.positionCount = 0;
        lineRenderer.enabled = true;
        isDrawing = true;
    }

    public (List<Vector2> path, List<float> times) StopDrawing()
    {
        isDrawing = false;
        lineRenderer.enabled = false;
        return (gazePath, timestamps);
    }
}