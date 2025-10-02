using UnityEngine;
using System.Collections.Generic;

public class PathVisualizer : MonoBehaviour
{
    [Header("���� ����")]
    [Tooltip("��� �����͸� �����ϴ� PathManager")]
    public PathManager pathManager;
    [Tooltip("���� ��θ� �׸� ������ (PathDrawer.cs ����)")]
    public GameObject lineDrawerPrefab;

    [Header("2D ���� ����")]
    public float projectionDepth = 10.0f;

    private List<PathDrawer> pathDrawers = new List<PathDrawer>();
    private bool is3DMode = true;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        InstantiateAndDrawAllPaths();
    }

    void InstantiateAndDrawAllPaths()
    {
        int totalTargets = pathManager.targets.Count;
        for (int i = 0; i < totalTargets; i++)
        {
            GameObject drawerInstance = Instantiate(lineDrawerPrefab, transform);
            drawerInstance.name = $"Path_To_Target_{i}";

            PathDrawer drawer = drawerInstance.GetComponent<PathDrawer>();
            if (drawer != null)
            {
                // [������] PathDrawer�� �� ���� ������ �����մϴ�.
                var pathPoints3D = pathManager.GetPathPoints(i);
                var startPos3D = pathManager.startPoint.position;
                var endPos3D = pathManager.targets[i].position;
                var spreadFactor = pathManager.spreadFactor;
                var resolution = pathManager.pathResolution;

                drawer.DrawPath(pathPoints3D, startPos3D, endPos3D, i, totalTargets, spreadFactor, mainCamera, projectionDepth, resolution);
                drawer.SetViewMode(is3DMode);
                pathDrawers.Add(drawer);
            }
        }
    }

    public void SwitchViewMode()
    {
        is3DMode = !is3DMode;
        foreach (var drawer in pathDrawers)
        {
            drawer.SetViewMode(is3DMode);
        }
    }

    public void HighlightPath(int targetIndex)
    {
        for (int i = 0; i < pathDrawers.Count; i++)
        {
            pathDrawers[i].SetHighlight(i == targetIndex);
        }
    }
}