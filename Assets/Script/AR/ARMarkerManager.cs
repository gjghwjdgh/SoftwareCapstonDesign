// 파일 이름: ARMarkerManager.cs

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI; // Button 사용을 위해 추가
using Unity.XR.CoreUtils;

public class ARMarkerManager : MonoBehaviour
{
    [Header("관리자 연결")]
    public PathVisualizer pathVisualizer;
    public GameUIManager gameUIManager;

    [Header("UI 연결")]
    public Button startAnalysisButton; // 마커 씬에서도 분석 시작 버튼 제어

    [Header("마커-프리팹 매칭")]
    public XRReferenceImageLibrary referenceImageLibrary;
    public string startMarkerName = "StartMarker";
    public string targetMarkerPrefix = "Target_";
    public GameObject startPointPrefab;
    public GameObject targetPrefab;

    private ARTrackedImageManager trackedImageManager;
    private Transform startPointInstance;
    private Dictionary<string, Transform> targetInstances = new Dictionary<string, Transform>();

    void Awake()
    {
        XROrigin sessionOrigin = FindAnyObjectByType<XROrigin>();
        if (sessionOrigin != null)
        {
            trackedImageManager = sessionOrigin.GetComponent<ARTrackedImageManager>();
        }

        if (trackedImageManager == null)
        {
            Debug.LogError("ARTrackedImageManager를 찾을 수 없습니다. XROrigin에 컴포넌트가 있는지 확인하세요.");
            enabled = false;
            return;
        }

        if (startAnalysisButton != null)
        {
            startAnalysisButton.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
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

        if (needsPathUpdate)
        {
            UpdatePathVisualizerTargets();
        }
    }

    private void UpdateTrackedImage(ARTrackedImage trackedImage, ref bool needsPathUpdate)
    {
        string imageName = trackedImage.referenceImage.name;
        Transform imageTransform = trackedImage.transform;

        if (trackedImage.trackingState == TrackingState.Tracking)
        {
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

        if (startAnalysisButton != null)
        {
            startAnalysisButton.gameObject.SetActive(targetInstances.Count > 0 && startPointInstance != null);
        }
    }

    // --- ISceneStateHandler 인터페이스 구현 ---

    public void EnterAnalysisState()
    {
        // 분석이 시작되면 버튼만 숨깁니다. (마커 추적은 계속되어야 함)
        if (startAnalysisButton != null)
        {
            startAnalysisButton.gameObject.SetActive(false);
        }
    }

    public void EnterIdleState()
    {
        // 유휴 상태가 되면, 조건에 따라 버튼을 다시 보여줍니다.
        if (startAnalysisButton != null)
        {
            startAnalysisButton.gameObject.SetActive(targetInstances.Count > 0 && startPointInstance != null);
        }
    }
}