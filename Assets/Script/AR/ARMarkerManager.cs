using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public class ARMarkerManager : MonoBehaviour
{
    public PathVisualizer pathVisualizer;
    public GameUIManager gameUIManager;
    public Button startAnalysisButton;
    public XRReferenceImageLibrary referenceImageLibrary;
    public string startMarkerName = "StartMarker";
    public string targetMarkerPrefix = "Target_";
    public GameObject startPointPrefab;
    public GameObject targetPrefab;

    private ARTrackedImageManager trackedImageManager;
    private Transform startPointInstance;
    private Dictionary<string, Transform> targetInstances = new Dictionary<string, Transform>();

    private bool isAnalysisActive = false;

    void Awake()
    {
        // FindAnyObjectByType은 최신 유니티 버전 호환용 (구버전이면 FindObjectOfType 사용)
        XROrigin sessionOrigin = FindFirstObjectByType<XROrigin>();
        if (sessionOrigin != null) trackedImageManager = sessionOrigin.GetComponent<ARTrackedImageManager>();

        if (trackedImageManager == null) { Debug.LogError("ARTrackedImageManager Missing"); enabled = false; return; }
        if (startAnalysisButton != null) startAnalysisButton.gameObject.SetActive(false);
    }

    void OnEnable() { if (trackedImageManager != null) trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged; }
    void OnDisable() { if (trackedImageManager != null) trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged; }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        if (isAnalysisActive) return;

        bool needsPathUpdate = false;
        foreach (var trackedImage in eventArgs.added) { UpdateTrackedImage(trackedImage, ref needsPathUpdate); }
        foreach (var trackedImage in eventArgs.updated) { UpdateTrackedImage(trackedImage, ref needsPathUpdate); }
        foreach (var trackedImage in eventArgs.removed)
        {
            string imageName = trackedImage.referenceImage.name;
            if (targetInstances.ContainsKey(imageName))
            {
                Destroy(targetInstances[imageName].gameObject);
                targetInstances.Remove(imageName);
                needsPathUpdate = true;
            }
        }
        if (needsPathUpdate) UpdatePathVisualizerTargets();
    }

    private void UpdateTrackedImage(ARTrackedImage trackedImage, ref bool needsPathUpdate)
    {
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            string imageName = trackedImage.referenceImage.name;
            Transform imageTransform = trackedImage.transform;

            if (imageName == startMarkerName)
            {
                if (startPointInstance == null)
                {
                    startPointInstance = Instantiate(startPointPrefab, imageTransform.position, imageTransform.rotation).transform;
                    pathVisualizer.startPoint = startPointInstance;
                    needsPathUpdate = true;
                }
                startPointInstance.SetPositionAndRotation(imageTransform.position, imageTransform.rotation);
            }
            else if (imageName.StartsWith(targetMarkerPrefix))
            {
                if (!targetInstances.ContainsKey(imageName))
                {
                    Transform newTarget = Instantiate(targetPrefab, imageTransform.position, imageTransform.rotation).transform;

                    // ★★★ [추가된 부분] TargetGroup에 넣고 로그 알림 ★★★
                    if (GameUIManager.Instance != null)
                    {
                        // 1. 부모 설정
                        if (GameUIManager.Instance.targetParent != null)
                        {
                            newTarget.SetParent(GameUIManager.Instance.targetParent);
                        }
                        // 2. 알림
                        GameUIManager.Instance.NotifyTargetSpawned();
                    }
                    // -----------------------------------------------------

                    targetInstances[imageName] = newTarget;
                    needsPathUpdate = true;
                }
                targetInstances[imageName].SetPositionAndRotation(imageTransform.position, imageTransform.rotation);
            }
        }
    }

    private void UpdatePathVisualizerTargets()
    {
        var sortedTargets = targetInstances.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
        pathVisualizer.targets = sortedTargets;
        pathVisualizer.GenerateAndShowAllPaths();
        if (startAnalysisButton != null) startAnalysisButton.gameObject.SetActive(targetInstances.Count > 0 && startPointInstance != null);
    }

    public void OnStartAnalysisButtonClicked() { if (gameUIManager != null) gameUIManager.StartAnalysis(); }

    public void EnterAnalysisState()
    {
        isAnalysisActive = true;
        if (startAnalysisButton != null) startAnalysisButton.gameObject.SetActive(false);
    }

    public void EnterIdleState()
    {
        isAnalysisActive = false;
        if (startAnalysisButton != null) startAnalysisButton.gameObject.SetActive(targetInstances.Count > 0 && startPointInstance != null);
    }
}