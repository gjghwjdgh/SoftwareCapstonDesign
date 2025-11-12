// 파일 이름: ARPlacementManager.cs

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ARRaycastManager))]
public class ARPlacementManager : MonoBehaviour
{
    [Header("핵심 연결")]
    public PathVisualizer pathVisualizer;
    public GameUIManager gameUIManager;

    [Header("UI 연결")]
    public Button startAnalysisButton;
    public TextMeshProUGUI infoText;

    [Header("생성할 프리팹")]
    public GameObject startPointPrefab;
    public GameObject targetPrefab;

    private ARRaycastManager arRaycastManager;
    private Transform placedStartPoint;
    private List<Transform> placedTargets = new List<Transform>();
    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Awake()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();

        if (startAnalysisButton != null)
        {
            startAnalysisButton.gameObject.SetActive(false);
        }
        if (infoText != null)
        {
            infoText.text = "바닥을 비추고 시작 지점을 배치하세요.";
        }
    }

    void Update()
    {
        if (Touchscreen.current == null || !Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return;
        }

        Vector2 touchPosition = Touchscreen.current.primaryTouch.position.ReadValue();

        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (arRaycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            var hitPose = hits[0].pose;
            if (placedStartPoint == null)
            {
                placedStartPoint = Instantiate(startPointPrefab, hitPose.position, hitPose.rotation).transform;
                if (infoText != null) infoText.text = "목표 지점을 배치하세요.";
                pathVisualizer.startPoint = placedStartPoint;
            }
            else
            {
                Transform newTarget = Instantiate(targetPrefab, hitPose.position, hitPose.rotation).transform;
                placedTargets.Add(newTarget);
                if (infoText != null) infoText.text = $"{placedTargets.Count}개의 목표 지점 배치 완료.";
                if (startAnalysisButton != null) startAnalysisButton.gameObject.SetActive(true);

                pathVisualizer.targets = placedTargets;
                pathVisualizer.GenerateAndShowAllPaths();
            }
        }
    }

    public void OnStartAnalysisButtonClicked()
    {
        if (gameUIManager != null)
        {
            gameUIManager.StartAnalysis();
        }
    }

    // --- ISceneStateHandler 인터페이스 구현 ---

    public void EnterAnalysisState()
    {
        // 분석이 시작되면, 터치 입력을 막고 버튼을 숨깁니다.
        this.enabled = false;
        if (startAnalysisButton != null)
        {
            startAnalysisButton.gameObject.SetActive(false);
        }
    }

    public void EnterIdleState()
    {
        // 유휴 상태가 되면, 다시 터치 입력을 허용하고 버튼을 보여줍니다.
        this.enabled = true;
        if (startAnalysisButton != null)
        {
            startAnalysisButton.gameObject.SetActive(placedTargets.Count > 0);
        }
    }
}