using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // UI 터치 방지용

[RequireComponent(typeof(ARRaycastManager))]
public class ARPlacementManager : MonoBehaviour
{
    [Header("핵심 연결")]
    public PathVisualizer pathVisualizer;
    public GameUIManager gameUIManager;
    public Button startAnalysisButton;
    public TextMeshProUGUI infoText;
    public GameObject startPointPrefab;
    public GameObject targetPrefab;

    private ARRaycastManager arRaycastManager;
    private Transform placedStartPoint;
    private List<Transform> placedTargets = new List<Transform>();
    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Awake()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        if (startAnalysisButton != null) startAnalysisButton.gameObject.SetActive(false);
        if (infoText != null) infoText.text = "바닥을 비추고 시작 지점을 배치하세요.";
    }

    void Update()
    {
        if (!this.enabled) return;

        if (Touchscreen.current == null || !Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return;

        // UI(로그창 등) 위에 손가락이 있으면 터치 무시
        if (EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue())) return;

        Vector2 touchPosition = Touchscreen.current.primaryTouch.position.ReadValue();

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

                // ★★★ [추가된 부분] TargetGroup에 넣고 로그 알림 ★★★
                if (GameUIManager.Instance != null)
                {
                    // 1. 부모 설정 (TargetGroup 밑으로)
                    if (GameUIManager.Instance.targetParent != null)
                    {
                        newTarget.SetParent(GameUIManager.Instance.targetParent);
                    }
                    // 2. 새 타겟 생성 알림 (로그 초기화)
                    GameUIManager.Instance.NotifyTargetSpawned();
                }
                // -----------------------------------------------------

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
        if (gameUIManager != null) gameUIManager.StartAnalysis();
    }

    public void EnterAnalysisState()
    {
        this.enabled = false;
        if (startAnalysisButton != null) startAnalysisButton.gameObject.SetActive(false);
    }

    public void EnterIdleState()
    {
        this.enabled = true;
        if (startAnalysisButton != null) startAnalysisButton.gameObject.SetActive(placedTargets.Count > 0);
    }
    // ★★★ [추가] 외부(프리셋)에서 생성된 데이터를 넘겨받는 함수 ★★★
    public void LoadExternalData(Transform startPoint, List<Transform> targets)
    {
        // 1. 데이터 인수
        this.placedStartPoint = startPoint;
        this.placedTargets = new List<Transform>(targets); // 리스트 복사

        // 2. PathVisualizer 동기화
        if (pathVisualizer != null)
        {
            pathVisualizer.startPoint = startPoint;
            pathVisualizer.targets = this.placedTargets;
            pathVisualizer.GenerateAndShowAllPaths(); // 선 그리기 강제 실행
        }

        // 3. UI 상태 갱신 (버튼 켜기)
        if (infoText != null) infoText.text = $"[프리셋 로드됨] {placedTargets.Count}개 목표";

        if (startAnalysisButton != null)
        {
            startAnalysisButton.gameObject.SetActive(true); // ★ 버튼 강제 활성화
        }

        // 4. 활성화 상태로 전환 (Idle)
        this.enabled = true;
    }
}