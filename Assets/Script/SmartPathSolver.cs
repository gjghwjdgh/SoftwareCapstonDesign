using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PathResultData
{
    public int targetIndex;
    public List<Vector3> pathPoints;
    public float phaseValue;
    public Color? overrideColor;
}

public class SmartPathSolver : MonoBehaviour
{
    [Header("시스템 매개변수")]
    public float AngleThreshold = 10.0f;       // 그룹 분할 각도 기준
    public float MaxSectorAngle = 45.0f;       // 그룹 최대 허용 각도
    public float CenterZoneRadius = 0.3f;      // 중앙 구역 반지름 비율 (0.0 ~ 0.5)
    public float CurveRatioWeak = 0.03f;       // 약한 곡선 비율 (2~5%)
    public float CurveRatioStrong = 0.15f;     // 강한 곡선 비율 (10~15%)
    public int HighDensityCount = 8;           // 고밀도 판단 개수

    private Color[] debugColors = new Color[] {
        Color.red, Color.blue, Color.green, Color.yellow,
        Color.cyan, Color.magenta, new Color(1, 0.5f, 0)
    };

    private class TargetMeta
    {
        public int originalIndex;
        public Transform transform;
        public Vector3 worldPos;
        public Vector2 screenPos;
        public float distance3D;
        public float screenAngle;
        public float screenDist;
        public int groupID = -1;

        public float assignedPhase;
        public Vector3 assignedControlPoint;
        public bool isStraight = false;
        public Color? debugColor;
    }

    public List<PathResultData> Solve(Transform startPoint, List<Transform> targets, Camera cam)
    {
        if (cam == null || startPoint == null || targets == null || targets.Count == 0) return new List<PathResultData>();

        // [단계 1] 초기화 및 데이터 수집
        List<TargetMeta> metas = new List<TargetMeta>();
        Vector3 startScreenPos3 = cam.WorldToScreenPoint(startPoint.position);
        Vector2 startScreenPos = new Vector2(startScreenPos3.x, startScreenPos3.y);
        Vector3 camForward = cam.transform.forward; camForward.y = 0; camForward.Normalize();

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null) continue;
            if (cam.WorldToViewportPoint(targets[i].position).z <= 0) continue;

            TargetMeta meta = new TargetMeta();
            meta.originalIndex = i;
            meta.transform = targets[i];
            meta.worldPos = targets[i].position;
            meta.distance3D = Vector3.Distance(startPoint.position, meta.worldPos);

            Vector3 sPos = cam.WorldToScreenPoint(meta.worldPos);
            meta.screenPos = new Vector2(sPos.x, sPos.y);

            Vector3 dirToTarget = (meta.worldPos - startPoint.position).normalized;
            dirToTarget.y = 0;
            meta.screenAngle = Vector3.SignedAngle(camForward, dirToTarget, Vector3.up);

            metas.Add(meta);
        }

        if (metas.Count == 0) return new List<PathResultData>();

        // [단계 2] 레이더 스캔 그룹핑
        metas.Sort((a, b) => a.screenAngle.CompareTo(b.screenAngle));

        int currentGroupID = 0;
        metas[0].groupID = currentGroupID;
        for (int i = 1; i < metas.Count; i++)
        {
            if (Mathf.Abs(metas[i].screenAngle - metas[i - 1].screenAngle) > AngleThreshold) currentGroupID++;
            metas[i].groupID = currentGroupID;
        }

        var groupedMetas = metas.GroupBy(m => m.groupID).ToList();
        List<TargetMeta> loners = new List<TargetMeta>();
        List<List<TargetMeta>> regularGroups = new List<List<TargetMeta>>();
        int colorIndex = 0;

        foreach (var group in groupedMetas)
        {
            List<TargetMeta> members = group.ToList();
            if (members.Count == 1) { loners.Add(members[0]); members[0].debugColor = Color.white; }
            else
            {
                regularGroups.Add(members);
                Color col = debugColors[colorIndex % debugColors.Length];
                foreach (var m in members) m.debugColor = col;
                colorIndex++;
            }
        }

        // [단계 3~6] 그룹별 처리 (정규 그룹)
        foreach (var members in regularGroups)
        {
            ProcessGroup(members, startPoint.position, startScreenPos, cam);
        }

        // 외톨이 처리 (가짜 그룹)
        if (loners.Count > 0)
        {
            ProcessGroup(loners, startPoint.position, startScreenPos, cam);
        }

        // 결과 생성
        List<PathResultData> results = new List<PathResultData>();
        foreach (var m in metas)
        {
            PathResultData res = new PathResultData();
            res.targetIndex = m.originalIndex;
            res.phaseValue = m.assignedPhase;
            res.overrideColor = m.debugColor;
            if (m.isStraight)
            {
                res.pathPoints = new List<Vector3>();
                for (int j = 0; j <= 50; j++) res.pathPoints.Add(Vector3.Lerp(startPoint.position, m.worldPos, j / 50f));
            }
            else
            {
                res.pathPoints = PathUtilities.GenerateQuadraticBezierCurvePath(startPoint.position, m.assignedControlPoint, m.worldPos, 50);
            }
            results.Add(res);
        }
        return results;
    }

    // ★★★ 피드백이 반영된 핵심 처리 함수 ★★★
    private void ProcessGroup(List<TargetMeta> members, Vector3 startPos, Vector2 startScreenPos, Camera cam)
    {
        int N = members.Count;

        // --- [1. 그룹 속성 진단] ---
        Vector2 groupCenterScreen = Vector2.zero;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var m in members)
        {
            groupCenterScreen += m.screenPos;
            if (m.screenPos.x < minX) minX = m.screenPos.x;
            if (m.screenPos.x > maxX) maxX = m.screenPos.x;
            if (m.screenPos.y < minY) minY = m.screenPos.y;
            if (m.screenPos.y > maxY) maxY = m.screenPos.y;
        }
        groupCenterScreen /= N;

        float screenDiag = new Vector2(Screen.width, Screen.height).magnitude;
        float groupDiag = new Vector2(maxX - minX, maxY - minY).magnitude;
        float groupSizeRatio = groupDiag / screenDiag;

        // 고밀도 진단: 개수는 많고(8개 이상) 크기는 작을 때(화면의 20% 미만)
        bool isHighDensity = (N >= HighDensityCount) && (groupSizeRatio < 0.2f);

        // 구역 진단: 화면 중앙으로부터의 거리 비율
        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        float distFromCenter = Vector2.Distance(groupCenterScreen, screenCenter);
        float maxDist = screenDiag * 0.5f;
        float normalizedDist = Mathf.Clamp01(distFromCenter / maxDist); // 0.0(중앙) ~ 1.0(구석)

        // --- [2. 페이즈 할당 (규격서 공식)] ---
        // 길이 긴 순서대로 정렬 (0번이 가장 김)
        var sortedByLen = members.OrderByDescending(m => m.distance3D).ToList();
        float M = N + 2.0f;

        for (int k = 0; k < N; k++)
        {
            // 공식: (N - k) / M
            // k=0 (1등, 긴 경로) -> N/M (후반부)
            // k=N-1 (꼴등, 짧은 경로) -> 1/M (초반부)
            sortedByLen[k].assignedPhase = (float)(N - k) / M;
        }

        // --- [3. 예외 처리: 고밀도] ---
        if (isHighDensity)
        {
            for (int i = 0; i < N; i++)
            {
                members[i].isStraight = true;
                members[i].debugColor = Color.HSVToRGB((float)i / N, 1f, 1f);
            }
            return;
        }

        // --- [4. 곡선 할당 (상세 규칙)] ---
        ApplyDetailedCurveRules(members, startPos, cam, normalizedDist);
    }

    private void ApplyDetailedCurveRules(List<TargetMeta> members, Vector3 startPos, Camera cam, float zoneDist)
    {
        int count = members.Count;
        int centerIdx = count / 2;

        // 1. 구역 가중치 (Zone Multiplier)
        // 중앙이면 0.5배(약하게), 외곽이면 최대 2.5배(강하게)
        float zoneMultiplier = 1.0f;
        if (zoneDist < CenterZoneRadius) zoneMultiplier = 0.5f;
        else zoneMultiplier = Mathf.Lerp(1.0f, 2.5f, (zoneDist - CenterZoneRadius) * 2f);

        // 2. 깊이 정보 (Fountain Effect용)
        var sortedByDepth = members.OrderBy(m => m.distance3D).ToList();
        TargetMeta closest = sortedByDepth.First();
        TargetMeta farthest = sortedByDepth.Last();

        for (int i = 0; i < count; i++)
        {
            TargetMeta m = members[i];

            // 카메라 기준 벡터 준비
            Vector3 dir = (m.worldPos - startPos).normalized;
            Vector3 visualRight = Vector3.ProjectOnPlane(cam.transform.right, dir).normalized;
            Vector3 visualUp = Vector3.ProjectOnPlane(cam.transform.up, dir).normalized;

            Vector3 bendVector = Vector3.zero;
            bool isLeft = (i < centerIdx); // 정렬된 리스트 기준 왼쪽 그룹인가?

            // --- [규칙 B: 공간 활용] ---

            // 홀수일 때 정중앙은 직선
            if (count % 2 == 1 && i == centerIdx)
            {
                m.isStraight = true;
            }
            // 4개 이하: 좌우 공간만 사용 (Lookup Table: 2개, 3개, 4개 상황 포괄)
            else if (count <= 4)
            {
                if (isLeft) bendVector = -visualRight; // 왼쪽
                else bendVector = visualRight;         // 오른쪽
            }
            // 5개 이상: 상하 공간(Twist) 혼합 사용
            else
            {
                if (isLeft) // 왼쪽 그룹
                {
                    if (i % 2 == 0) bendVector = -visualRight; // 순수 왼쪽
                    else bendVector = (visualUp * 0.8f - visualRight * 0.2f).normalized; // 위+왼쪽 비틀기
                }
                else // 오른쪽 그룹
                {
                    // 오른쪽 그룹 내에서의 상대 인덱스
                    int rightIdx = i - centerIdx;
                    if (rightIdx % 2 == 0) bendVector = visualRight; // 순수 오른쪽
                    else bendVector = (-visualUp * 0.8f + visualRight * 0.2f).normalized; // 아래+오른쪽 비틀기
                }
            }

            // --- [규칙 D: 깊이 우선 할당 & 강도 결정] ---
            float baseRatio = CurveRatioStrong;

            if (m == closest)
            {
                baseRatio = CurveRatioWeak; // 가까운 건 약하게
                // 홀수 그룹이면 가장 가까운 놈에게 직선 우선권 부여 (중앙이 아니더라도)
                // (단, 위에서 이미 중앙 인덱스에게 직선을 줬으므로 여기선 약한 곡선으로 유지하거나 덮어쓸 수 있음)
                // 규격서: "가장 가까운 타겟: 직선 또는 가장 약한 곡선"
            }
            else if (m == farthest)
            {
                baseRatio = CurveRatioStrong * 1.2f; // 먼 건 더 크게 (Fountain Effect)
            }
            else
            {
                // 안쪽(중앙 인덱스에 가까운) 경로는 약하게, 바깥쪽은 강하게 차등 적용
                float distFromIdxCenter = Mathf.Abs(i - (count - 1) / 2.0f);
                if (distFromIdxCenter < 1.5f) baseRatio = CurveRatioWeak;
            }

            // 최종 Offset 계산
            float finalOffset = m.distance3D * baseRatio * zoneMultiplier;

            // 제어점 할당
            Vector3 mid = (startPos + m.worldPos) * 0.5f;
            m.assignedControlPoint = mid + (bendVector * finalOffset);
        }
    }
}