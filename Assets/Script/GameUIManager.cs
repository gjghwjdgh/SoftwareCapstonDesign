using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    [Header("관리자 스크립트 연결")]
    public PathManager pathManager;
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover;

    [Header("설정")]
    public float travelDuration = 2.0f;

    // --- 추가된 부분: 키보드 입력을 매 프레임 감지 ---
    void Update()
    {
        // --- 퍼슈트 시작 키 (숫자 1, 2, 3, 4) ---
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            OnStartPursuitClicked(0); // 첫 번째 타겟 (인덱스 0)
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            OnStartPursuitClicked(1); // 두 번째 타겟 (인덱스 1)
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            OnStartPursuitClicked(2); // 세 번째 타겟 (인덱스 2)
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            OnStartPursuitClicked(3); // 네 번째 타겟 (인덱스 3)
        }

        // --- 뷰 전환 키 (Tab) ---
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            OnSwitchViewClicked();
        }
    }
    // --- 여기까지 추가 ---

    /// <summary>
    /// 퍼슈트 시작 버튼 클릭 시 호출될 함수 (UI 버튼 또는 키보드로 호출)
    /// </summary>
    public void OnStartPursuitClicked(int targetIndex)
    {
        // PathManager에 타겟 개수보다 큰 인덱스가 요청될 경우를 방지
        if (targetIndex >= pathManager.targets.Count)
        {
            Debug.LogWarning($"타겟 인덱스 {targetIndex}가 존재하지 않습니다.");
            return;
        }

        // 1. PathManager에게 경로 데이터 요청
        var pathPoints = pathManager.GetPathPoints(targetIndex);
        if (pathPoints == null) return;

        // 2. PathVisualizer에게 해당 경로를 하이라이트하도록 지시
        pathVisualizer.HighlightPath(targetIndex);

        // 3. PursuitMover에게 새 경로로 이동을 시작하라고 지시
        pursuitMover.StartMovement(pathPoints, travelDuration);
    }

    /// <summary>
    /// 뷰 모드 전환 버튼 클릭 시 호출될 함수 (UI 버튼 또는 키보드로 호출)
    /// </summary>
    public void OnSwitchViewClicked()
    {
        pathVisualizer.SwitchViewMode();
    }
}