using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro; // 텍스트 읽기 위해 필수

public class PathResultData
{
    public int targetIndex;
    public List<Vector3> pathPoints;
    public float phaseValue;
    public Color? overrideColor;
}

public class SmartPathSolver : MonoBehaviour
{
    [Header("1. 그룹핑 설정 (레이더 스캔)")]
    public float AngleThreshold = 15.0f;
    public float MaxGroupSpanAngle = 45.0f;

    [Header("2. 곡선 강도 및 구역 설정")]
    public float CenterZoneRadius = 0.3f;
    public float CurveRatioWeak = 0.05f;
    public float CurveRatioStrong = 0.15f;

    [Header("3. 예외 처리")]
    public int HighDensityCount = 8;

    [Header("디버그")]
    public bool showDebugGizmos = true;

    private Color[] debugColors = new Color[] {
        Color.red, Color.blue, Color.green, Color.yellow,
        Color.cyan, Color.magenta, new Color(1, 0.5f, 0)
    };

    // 내부 데이터 클래스
    private class TargetMeta
    {
        public int originalIndex;
        public string visualID; // ★ 화면에 적힌 번호 (텍스트)
        public Transform transform;
        public Vector3 worldPos;
        public Vector2 screenPos;
        public float distance3D;
        public float rawAngle;
        public float relativeAngle;
        public int groupID = -1;
        public float assignedPhase;
        public Vector3 assignedControlPoint;
        public bool isStraight = false;
        public Color? debugColor;
    }

    private List<TargetMeta> debugMetas = new List<TargetMeta>();
    private Vector3 debugStartPos;

    // =================================================================================
    // [메인 함수] Solve
    // =================================================================================
    public List<PathResultData> Solve(Transform startPoint, List<Transform> targets, Camera cam)
    {
        // 1. 로그 시작
        LogToUI("\n==================================");
        LogToUI($"[알고리즘 상세 분석 시작] 타겟 {targets.Count}개");

        if (cam == null || startPoint == null || targets == null || targets.Count == 0)
            return new List<PathResultData>();

        List<TargetMeta> metas = new List<TargetMeta>();
        Vector3 startScreenPos3 = cam.WorldToScreenPoint(startPoint.position);
        Vector2 startScreenPos = new Vector2(startScreenPos3.x, startScreenPos3.y);
        debugStartPos = startPoint.position;

        // 2. 데이터 수집 및 ID 식별
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null) continue;

            // ★★★ [ID 식별 로직] 타겟에 적힌 텍스트 읽어오기 ★★★
            string idStr = targets[i].name; // 기본값은 오브젝트 이름

            // TargetLabel이나 TextMeshPro 컴포넌트를 찾아서 텍스트 내용을 가져옴
            var textComponent = targets[i].GetComponentInChildren<TMP_Text>();
            if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
            {
                idStr = textComponent.text; // 예: "1", "2"
            }
            // -------------------------------------------------------

            // 입구 컷 (카메라 뒤쪽 제외)
            if (cam.WorldToViewportPoint(targets[i].position).z <= 0)
            {
                LogToUI($"[제외] 타겟 [{idStr}]: 카메라 뒤쪽에 있음.");
                continue;
            }

            TargetMeta meta = new TargetMeta();
            meta.originalIndex = i;
            meta.visualID = idStr; // 식별된 ID 저장
            meta.transform = targets[i];
            meta.worldPos = targets[i].position;
            meta.distance3D = Vector3.Distance(startPoint.position, meta.worldPos);

            Vector3 sPos = cam.WorldToScreenPoint(meta.worldPos);
            meta.screenPos = new Vector2(sPos.x, sPos.y);

            // 절대 각도 계산
            Vector2 dir = meta.screenPos - startScreenPos;
            meta.rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            metas.Add(meta);
        }

        if (metas.Count == 0)
        {
            LogToUI("[종료] 유효한 타겟이 하나도 없습니다.");
            return new List<PathResultData>();
        }

        // 3. 정렬 및 동적 중심 설정
        // 1차 정렬: 절대 각도 기준 내림차순 (화면 왼쪽 180도 -> 오른쪽 0도)
        metas.Sort((a, b) => b.rawAngle.CompareTo(a.rawAngle));

        float centerOffsetAngle = 0f;
        if (metas.Count > 0)
        {
            int midIndex = metas.Count / 2;
            centerOffsetAngle = metas[midIndex].rawAngle;
            LogToUI($"[기준점 설정] 중간 타겟([{metas[midIndex].visualID}])의 각도 {centerOffsetAngle:F1}도를 0도로 보정.");
        }

        // 상대 각도 계산 및 2차 정렬 (왼쪽 -> 오른쪽 순서 보장)
        foreach (var m in metas)
        {
            m.relativeAngle = Mathf.DeltaAngle(centerOffsetAngle, m.rawAngle);
        }
        metas.Sort((a, b) => b.relativeAngle.CompareTo(a.relativeAngle));

        // 4. [핵심] 상세 그룹핑 로그 출력
        LogToUI("--- [그룹핑 판별 로직] ---");

        List<List<TargetMeta>> finalGroups = new List<List<TargetMeta>>();

        if (metas.Count > 0)
        {
            List<TargetMeta> currentGroup = new List<TargetMeta>();
            currentGroup.Add(metas[0]);

            // 첫 타겟 로그
            LogToUI($"▶ 그룹 시작: 타겟 [{metas[0].visualID}] (상대각: {metas[0].relativeAngle:F1}도)");

            for (int i = 1; i < metas.Count; i++)
            {
                TargetMeta current = metas[i];
                TargetMeta prev = currentGroup.Last();

                float gap = Mathf.Abs(Mathf.DeltaAngle(prev.relativeAngle, current.relativeAngle));
                float totalSpan = Mathf.Abs(Mathf.DeltaAngle(currentGroup[0].relativeAngle, current.relativeAngle));

                string logMsg = $"   └ 타겟 [{current.visualID}] ({current.relativeAngle:F1}도)";

                bool isConnected = false;
                // 연결 조건: 간격이 좁고(Threshold) && 전체가 너무 크지 않을 때(Span)
                if (gap <= AngleThreshold && totalSpan <= MaxGroupSpanAngle)
                {
                    isConnected = true;
                    logMsg += $" -> [연결] (Gap: {gap:F1} <= {AngleThreshold})";
                    currentGroup.Add(current);
                }
                else
                {
                    isConnected = false;
                    if (gap > AngleThreshold)
                        logMsg += $" -> [분리] 사유: 간격이 너무 멂 (Gap: {gap:F1})";
                    else
                        logMsg += $" -> [분리] 사유: 그룹이 너무 커짐 (Span: {totalSpan:F1})";

                    LogToUI(logMsg);

                    finalGroups.Add(currentGroup);
                    currentGroup = new List<TargetMeta>();
                    currentGroup.Add(current);
                    LogToUI($"▶ 새 그룹 시작: 타겟 [{current.visualID}]");
                }

                if (isConnected) LogToUI(logMsg);
            }
            if (currentGroup.Count > 0) finalGroups.Add(currentGroup);
        }

        debugMetas = metas;
        LogToUI($"[결과] 총 {finalGroups.Count}개의 그룹이 생성되었습니다.\n");

        // 5. 그룹별 곡선 처리
        int colorIdx = 0;
        foreach (var group in finalGroups)
        {
            Color col = (group.Count == 1) ? Color.white : debugColors[colorIdx % debugColors.Length];
            if (group.Count > 1) colorIdx++;
            foreach (var m in group) m.debugColor = col;

            ProcessGroupRules(group, startPoint.position, startScreenPos, cam);
        }

        // 6. 결과 생성
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
                // PathUtilities 사용 (이미 프로젝트에 있는 스크립트 사용)
                res.pathPoints = PathUtilities.GenerateQuadraticBezierCurvePath(
                    startPoint.position, m.assignedControlPoint, m.worldPos, 20);
            }
            results.Add(res);
        }
        return results;
    }

    private void ProcessGroupRules(List<TargetMeta> members, Vector3 startPos, Vector2 startScreenPos, Camera cam)
    {
        int N = members.Count;

        // 페이즈 할당
        var sortedByLen = members.OrderByDescending(m => m.distance3D).ToList();
        float M = N + 2.0f;
        for (int k = 0; k < N; k++)
        {
            sortedByLen[k].assignedPhase = (float)(N - k) / M;
        }

        // 고밀도 예외
        float minX = members.Min(m => m.screenPos.x);
        float maxX = members.Max(m => m.screenPos.x);
        float widthRatio = (maxX - minX) / Screen.width;

        if (N >= HighDensityCount && widthRatio < 0.2f)
        {
            LogToUI($"  >> 경고: 고밀도 그룹(멤버 {N}명) 감지. 전원 직선화.");
            for (int i = 0; i < N; i++)
            {
                members[i].isStraight = true;
                members[i].debugColor = Color.HSVToRGB((float)i / N, 1f, 1f);
            }
            return;
        }

        // 구역 진단 로그
        float avgRelAngle = members.Average(m => m.relativeAngle);
        float zoneFactor = Mathf.Clamp01(Mathf.Abs(avgRelAngle) / 45.0f);
        float zoneMultiplier = Mathf.Lerp(0.5f, 2.5f, zoneFactor);

        string zoneName = (zoneMultiplier < 0.8f) ? "중앙" : "외곽";
        string memberIDs = string.Join(", ", members.Select(m => $"[{m.visualID}]"));

        LogToUI($"[그룹상세] 멤버: {memberIDs}");
        LogToUI($"   ㄴ 위치: {zoneName} (중심각: {avgRelAngle:F1}) | 곡선강도: {zoneMultiplier:F1}배");

        // 곡선 계산
        var sortedByDepth = members.OrderBy(m => m.distance3D).ToList();
        TargetMeta closest = sortedByDepth.First();
        TargetMeta farthest = sortedByDepth.Last();
        int centerIdx = N / 2;

        for (int i = 0; i < N; i++)
        {
            TargetMeta m = members[i];
            Vector3 dir = (m.worldPos - startPos).normalized;
            Vector3 visualRight = Vector3.ProjectOnPlane(cam.transform.right, dir).normalized;
            Vector3 visualUp = Vector3.ProjectOnPlane(cam.transform.up, dir).normalized;
            Vector3 upTwist = (visualUp * 0.8f + visualRight * 0.2f).normalized;
            Vector3 downTwist = (-visualUp * 0.8f + visualRight * 0.2f).normalized;

            Vector3 bendVector = Vector3.zero;
            bool isLeftInGroup = (i < centerIdx);

            if (N % 2 == 1 && i == centerIdx) m.isStraight = true;
            else if (N <= 4)
            {
                if (isLeftInGroup) bendVector = -visualRight;
                else bendVector = visualRight;
            }
            else
            {
                if (isLeftInGroup)
                {
                    if (i % 2 == 0) bendVector = -visualRight;
                    else bendVector = (visualUp * 0.8f - visualRight * 0.2f).normalized;
                }
                else
                {
                    int rIdx = i - centerIdx;
                    if (rIdx % 2 == 0) bendVector = visualRight;
                    else bendVector = downTwist;
                }
            }

            float ratio = CurveRatioStrong;
            if (m == closest) { ratio = CurveRatioWeak; if (N % 2 == 1) m.isStraight = true; }
            else if (m == farthest) ratio = CurveRatioStrong * 1.2f;

            float finalOffset = m.distance3D * ratio * zoneMultiplier;
            Vector3 mid = (startPos + m.worldPos) * 0.5f;
            m.assignedControlPoint = mid + (bendVector * finalOffset);
        }
    }

    private void LogToUI(string msg)
    {
        if (GameUIManager.Instance != null)
            GameUIManager.Instance.Log(msg);
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