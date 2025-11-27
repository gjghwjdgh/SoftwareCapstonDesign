using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 결과 데이터 구조체
public class PathResultData
{
    public int targetIndex;
    public List<Vector3> pathPoints; // 라인 렌더러에 넣을 점들
    public float phaseValue;         // 쉐이더 등에 넣을 페이즈 값 (0.0~1.0)
    public Color? overrideColor;     // 고밀도 그룹용 색상 (없으면 null)
}

public class SmartPathSolver : MonoBehaviour
{
    [Header("1. 그룹핑 설정 (레이더 스캔)")]
    [Tooltip("옆 타겟과의 각도 차이가 이 값보다 크면 그룹을 끊습니다.")]
    public float AngleThreshold = 15.0f;
    [Tooltip("한 그룹의 전체 각도가 이 값을 넘으면 오른쪽부터 잘라냅니다.")]
    public float MaxGroupSpanAngle = 45.0f;

    [Header("2. 곡선 강도 및 구역 설정")]
    [Tooltip("화면 중앙 구역의 반지름 비율 (0.0 ~ 0.5)")]
    public float CenterZoneRadius = 0.3f;
    [Tooltip("중앙 구역에서의 약한 곡률 비율 (길이 비례)")]
    public float CurveRatioWeak = 0.05f;
    [Tooltip("외곽 구역에서의 강한 곡률 비율 (길이 비례)")]
    public float CurveRatioStrong = 0.15f;

    [Header("3. 예외 처리")]
    [Tooltip("이 개수 이상 뭉치면 고밀도(직선화)로 처리합니다.")]
    public int HighDensityCount = 8;

    [Header("디버그")]
    public bool showDebugGizmos = true;

    // 디버그용 색상 팔레트
    private Color[] debugColors = new Color[] {
        Color.red, Color.blue, Color.green, Color.yellow,
        Color.cyan, Color.magenta, new Color(1, 0.5f, 0)
    };

    // 내부 연산용 데이터 클래스
    private class TargetMeta
    {
        public int originalIndex;
        public Transform transform;
        public Vector3 worldPos;
        public Vector2 screenPos;
        public float distance3D;

        public float rawAngle;      // 화면 절대 각도
        public float relativeAngle; // 동적 중심 기준 상대 각도

        public int groupID = -1;
        public float assignedPhase;
        public Vector3 assignedControlPoint;
        public bool isStraight = false;
        public Color? debugColor;
    }

    private List<TargetMeta> debugMetas = new List<TargetMeta>();
    private Vector3 debugStartPos;

    // --- [마스터 알고리즘 함수] ---
    public List<PathResultData> Solve(Transform startPoint, List<Transform> targets, Camera cam)
    {
        if (cam == null || startPoint == null || targets == null || targets.Count == 0)
            return new List<PathResultData>();

        if (GameUIManager.Instance != null)
            GameUIManager.Instance.Log($"계산 시작: 타겟 {targets.Count}개");

        List<TargetMeta> metas = new List<TargetMeta>();
        Vector3 startScreenPos3 = cam.WorldToScreenPoint(startPoint.position);
        Vector2 startScreenPos = new Vector2(startScreenPos3.x, startScreenPos3.y);
        debugStartPos = startPoint.position;

        // ---------------------------------------------------------
        // [단계 1] 입구 컷 (Filtering) & 데이터 수집
        // ---------------------------------------------------------
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null) continue;

            // 카메라 뒤쪽(Z <= 0)은 무조건 제외
            if (cam.WorldToViewportPoint(targets[i].position).z <= 0) continue;

            TargetMeta meta = new TargetMeta();
            meta.originalIndex = i;
            meta.transform = targets[i];
            meta.worldPos = targets[i].position;
            meta.distance3D = Vector3.Distance(startPoint.position, meta.worldPos);

            Vector3 sPos = cam.WorldToScreenPoint(meta.worldPos);
            meta.screenPos = new Vector2(sPos.x, sPos.y);

            // Raw Angle (Atan2) 계산
            Vector2 dir = meta.screenPos - startScreenPos;
            meta.rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            metas.Add(meta);
        }

        if (metas.Count == 0) return new List<PathResultData>();

        // ---------------------------------------------------------
        // [단계 2] 동적 중심 설정 및 정렬 (Dynamic Centering)
        // ---------------------------------------------------------

        // 1. 절대 각도 기준 내림차순 정렬 (왼쪽이 180도, 오른쪽이 0도에 가까우므로)
        // 주의: Atan2는 180(좌) ~ 0(우) ~ -180(좌하단) 범위임.
        // 여기서는 단순하게 상대값 계산을 위해 정렬합니다.
        metas.Sort((a, b) => b.rawAngle.CompareTo(a.rawAngle));

        // 2. 중간 객체(Median) 찾기
        float centerOffsetAngle = 0f;
        if (metas.Count > 0)
        {
            int midIndex = metas.Count / 2;
            centerOffsetAngle = metas[midIndex].rawAngle;
        }

        // 3. 상대 각도 계산 및 재정렬 (왼쪽 -> 오른쪽 순서 보장)
        foreach (var m in metas)
        {
            m.relativeAngle = Mathf.DeltaAngle(centerOffsetAngle, m.rawAngle);
        }
        // 왼쪽(큰 양수) -> 오른쪽(음수) 순서로 정렬 (내림차순)
        metas.Sort((a, b) => b.relativeAngle.CompareTo(a.relativeAngle));


        // ---------------------------------------------------------
        // [단계 3] 순차적 그룹핑 및 가지치기 (Sequential Grouping)
        // ---------------------------------------------------------
        List<List<TargetMeta>> finalGroups = new List<List<TargetMeta>>();

        if (metas.Count > 0)
        {
            List<TargetMeta> currentGroup = new List<TargetMeta>();
            currentGroup.Add(metas[0]);

            for (int i = 1; i < metas.Count; i++)
            {
                TargetMeta current = metas[i];
                TargetMeta prev = currentGroup.Last();

                // 조건 A: 인접 간격 확인
                float gap = Mathf.Abs(Mathf.DeltaAngle(prev.relativeAngle, current.relativeAngle));

                // 조건 B: 그룹 전체 크기 확인 (비대해짐 방지)
                float totalSpan = Mathf.Abs(Mathf.DeltaAngle(currentGroup[0].relativeAngle, current.relativeAngle));

                if (gap <= AngleThreshold && totalSpan <= MaxGroupSpanAngle)
                {
                    currentGroup.Add(current);
                }
                else
                {
                    // 그룹 끊기 (가지치기 완료)
                    finalGroups.Add(currentGroup);
                    currentGroup = new List<TargetMeta>();
                    currentGroup.Add(current);
                }
            }
            if (currentGroup.Count > 0) finalGroups.Add(currentGroup);
        }

        // 디버깅용 데이터 저장
        debugMetas = metas;
        if (GameUIManager.Instance != null)
            GameUIManager.Instance.Log($"그룹핑 완료: 총 {finalGroups.Count}개 그룹");

        // ---------------------------------------------------------
        // [단계 4, 5, 6] 그룹별 규칙 적용 (페이즈, 곡선, 예외처리)
        // ---------------------------------------------------------
        int colorIdx = 0;
        foreach (var group in finalGroups)
        {
            // 디버그 색상 지정 (외톨이는 흰색)
            Color col = (group.Count == 1) ? Color.white : debugColors[colorIdx % debugColors.Length];
            if (group.Count > 1) colorIdx++;
            foreach (var m in group) m.debugColor = col;

            ProcessGroupRules(group, startPoint.position, startScreenPos, cam);
        }

        // ---------------------------------------------------------
        // [단계 7] 최종 결과 생성
        // ---------------------------------------------------------
        List<PathResultData> results = new List<PathResultData>();
        foreach (var m in metas)
        {
            PathResultData res = new PathResultData();
            res.targetIndex = m.originalIndex;
            res.phaseValue = m.assignedPhase;
            res.overrideColor = m.debugColor;

            if (m.isStraight)
            {
                res.pathPoints = new List<Vector3> { startPoint.position, m.worldPos };
            }
            else
            {
                // PathUtilities 사용 (이미 프로젝트에 있는 스크립트)
                res.pathPoints = PathUtilities.GenerateQuadraticBezierCurvePath(
                    startPoint.position, m.assignedControlPoint, m.worldPos, 20);
            }
            results.Add(res);
        }
        return results;
    }

    // 그룹 내부 로직 처리
    private void ProcessGroupRules(List<TargetMeta> members, Vector3 startPos, Vector2 startScreenPos, Camera cam)
    {
        int N = members.Count;

        // =========================================================
        // A. 지능형 페이즈 할당 (길이 기반)
        // =========================================================
        var sortedByLen = members.OrderByDescending(m => m.distance3D).ToList();
        float M = N + 2.0f;
        for (int k = 0; k < N; k++)
        {
            // 4, 3, 2, 1 순서로 할당 (도착점 앞 2칸 비움)
            sortedByLen[k].assignedPhase = (float)(N - k) / M;
        }

        // =========================================================
        // B. 예외 처리: 고밀도 배치 (적극적 단순화)
        // =========================================================
        // 화면상 크기 비율 계산
        float minX = members.Min(m => m.screenPos.x);
        float maxX = members.Max(m => m.screenPos.x);
        float widthRatio = (maxX - minX) / Screen.width;

        // 개수가 많고(8개 이상) 좁은 영역(20% 미만)에 모여있다면
        if (N >= HighDensityCount && widthRatio < 0.2f)
        {
            if (GameUIManager.Instance != null)
                GameUIManager.Instance.Log($"고밀도 그룹 감지(N={N}). 직선화 처리.");

            for (int i = 0; i < N; i++)
            {
                members[i].isStraight = true;
                members[i].debugColor = Color.HSVToRGB((float)i / N, 1f, 1f); // 무지개색
            }
            return;
        }

        // =========================================================
        // C. 곡선 강도 및 패턴 결정 (구역 기반 + 상세 배치)
        // =========================================================

        // 1. 구역 진단 (Zone Analysis)
        float avgRelAngle = members.Average(m => m.relativeAngle);
        // 중앙에서 45도 이상 벗어나면 완전 외곽으로 간주 (0.0 ~ 1.0)
        float zoneFactor = Mathf.Clamp01(Mathf.Abs(avgRelAngle) / 45.0f);

        // 구역 가중치: 중앙(0.5배) ~ 외곽(2.5배)
        float zoneMultiplier = Mathf.Lerp(0.5f, 2.5f, zoneFactor);

        // 2. 깊이 정보 (Depth Priority)
        var sortedByDepth = members.OrderBy(m => m.distance3D).ToList();
        TargetMeta closest = sortedByDepth.First();
        TargetMeta farthest = sortedByDepth.Last();

        int centerIdx = N / 2;

        for (int i = 0; i < N; i++)
        {
            TargetMeta m = members[i];

            // 카메라 기준 벡터
            Vector3 dir = (m.worldPos - startPos).normalized;
            Vector3 visualRight = Vector3.ProjectOnPlane(cam.transform.right, dir).normalized;
            Vector3 visualUp = Vector3.ProjectOnPlane(cam.transform.up, dir).normalized;

            // 비틀기 벡터 (Twist)
            Vector3 upTwist = (visualUp * 0.8f + visualRight * 0.2f).normalized;
            Vector3 downTwist = (-visualUp * 0.8f + visualRight * 0.2f).normalized;

            Vector3 bendVector = Vector3.zero;
            bool isLeftInGroup = (i < centerIdx);

            // --- 패턴 결정 (인원수별 시나리오) ---

            // 홀수일 때 정중앙은 직선
            if (N % 2 == 1 && i == centerIdx)
            {
                m.isStraight = true;
            }
            // 4개 이하 (좌우 공간만 사용)
            else if (N <= 4)
            {
                if (isLeftInGroup) bendVector = -visualRight;
                else bendVector = visualRight;
            }
            // 5개 이상 (위아래 혼합)
            else
            {
                if (isLeftInGroup)
                {
                    if (i % 2 == 0) bendVector = -visualRight; // 왼쪽
                    else bendVector = (visualUp * 0.8f - visualRight * 0.2f).normalized; // 좌상단
                }
                else
                {
                    int rIdx = i - centerIdx;
                    if (rIdx % 2 == 0) bendVector = visualRight; // 오른쪽
                    else bendVector = downTwist; // 우하단
                }
            }

            // --- 강도 결정 ---
            float ratio = CurveRatioStrong;

            // 깊이 우선 규칙
            if (m == closest)
            {
                ratio = CurveRatioWeak;
                if (N % 2 == 1) m.isStraight = true; // 가장 가까운 놈 직선 우선
            }
            else if (m == farthest)
            {
                ratio = CurveRatioStrong * 1.2f; // 먼 놈은 더 크게
            }

            // 최종 Offset = (3D 길이) * (비율) * (구역 가중치)
            float finalOffset = m.distance3D * ratio * zoneMultiplier;

            // 제어점 설정
            Vector3 mid = (startPos + m.worldPos) * 0.5f;
            m.assignedControlPoint = mid + (bendVector * finalOffset);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || debugMetas == null) return;
        foreach (var m in debugMetas)
        {
            if (m.debugColor != null) Gizmos.color = m.debugColor.Value;
            Gizmos.DrawSphere(m.worldPos, 0.05f);
            if (!m.isStraight)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(m.worldPos, m.assignedControlPoint);
            }
        }
    }
}