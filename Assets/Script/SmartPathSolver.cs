using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class PathResultData
{
    public int targetIndex;
    public List<Vector3> pathPoints;
    public float phaseValue;
    public Color? overrideColor;
}

public class SmartPathSolver : MonoBehaviour
{
    [Header("1. 그룹핑 설정")]
    [Tooltip("검사할 각도 범위 (이 범위를 벗어나면 아예 검사 안함)")]
    public float AngleThreshold = 15.0f;

    [Tooltip("화면상 거리 제한 (화면폭 x 비율)")]
    public float MaxScreenDistRatio = 0.2f;

    [Tooltip("그룹 전체 각도가 이 값을 넘으면 오른쪽부터 잘라냄")]
    public float MaxGroupSpanAngle = 45.0f;

    [Header("2. 곡선 및 구역 설정")]
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

    private class TargetMeta
    {
        public int originalIndex;
        public int sortedIndex;
        public string visualID;
        public Transform transform;
        public Vector3 worldPos;
        public Vector2 screenPos;
        public float distance3D;
        public float rawAngle;
        public float relativeAngle;
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
        LogToUI("\n──────── [스마트 경로 분석 V7: 실시간 방출] ────────");

        if (cam == null || startPoint == null || targets == null || targets.Count == 0)
            return new List<PathResultData>();

        List<TargetMeta> metas = new List<TargetMeta>();
        Vector3 startScreenPos3 = cam.WorldToScreenPoint(startPoint.position);
        Vector2 startScreenPos = new Vector2(startScreenPos3.x, startScreenPos3.y);
        debugStartPos = startPoint.position;
        float screenWidth = Screen.width;

        // 1. 데이터 수집
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null) continue;

            string idStr = "";
            var textComponent = targets[i].GetComponentInChildren<TMP_Text>();
            if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
                idStr = textComponent.text.Trim();

            if (cam.WorldToViewportPoint(targets[i].position).z <= 0) continue;

            TargetMeta meta = new TargetMeta();
            meta.originalIndex = i;
            meta.visualID = idStr;
            meta.transform = targets[i];
            meta.worldPos = targets[i].position;
            meta.distance3D = Vector3.Distance(startPoint.position, meta.worldPos);

            Vector3 sPos = cam.WorldToScreenPoint(meta.worldPos);
            meta.screenPos = new Vector2(sPos.x, sPos.y);

            Vector2 dir = meta.screenPos - startScreenPos;
            meta.rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            metas.Add(meta);
        }

        if (metas.Count == 0) return new List<PathResultData>();

        // 2. 정렬 및 인덱스 재부여
        metas.Sort((a, b) => b.rawAngle.CompareTo(a.rawAngle));

        float centerOffsetAngle = metas[metas.Count / 2].rawAngle;
        foreach (var m in metas)
        {
            m.relativeAngle = Mathf.DeltaAngle(centerOffsetAngle, m.rawAngle);
        }
        metas.Sort((a, b) => b.relativeAngle.CompareTo(a.relativeAngle));

        for (int i = 0; i < metas.Count; i++)
        {
            metas[i].sortedIndex = i + 1;
        }

        // ---------------------------------------------------------
        // [단계 3] 그룹핑 로직 (실시간 방출 및 재시작)
        // ---------------------------------------------------------
        float distLimitPx = screenWidth * MaxScreenDistRatio;
        LogToUI($"[조건] 각도 {AngleThreshold}° / 거리 {distLimitPx:F0}px / 그룹폭 {MaxGroupSpanAngle}°");
        LogToUI("--- 상세 과정 ---");

        List<List<TargetMeta>> finalGroups = new List<List<TargetMeta>>();
        List<TargetMeta> loners = new List<TargetMeta>();

        // 아직 그룹이 없는 타겟들의 리스트
        HashSet<TargetMeta> ungroupedMetas = new HashSet<TargetMeta>(metas);

        for (int i = 0; i < metas.Count; i++)
        {
            TargetMeta startNode = metas[i];

            // 이미 다른 그룹에 속했으면 건너뜀
            if (!ungroupedMetas.Contains(startNode)) continue;

            // 새 그룹 시작
            List<TargetMeta> currentGroup = new List<TargetMeta>();
            currentGroup.Add(startNode);
            ungroupedMetas.Remove(startNode);

            LogToUI($"▶ [{startNode.sortedIndex}]부터 새 그룹 탐색 시작");

            // 체인 연결
            for (int j = i + 1; j < metas.Count; j++)
            {
                TargetMeta currentNode = currentGroup.Last();
                TargetMeta nextNode = metas[j];

                if (!ungroupedMetas.Contains(nextNode)) continue;

                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(currentNode.relativeAngle, nextNode.relativeAngle));
                float distPx = Vector2.Distance(currentNode.screenPos, nextNode.screenPos);

                // 일단 연결 조건 (각도 & 거리)
                if (angleDiff <= AngleThreshold && distPx <= distLimitPx)
                {
                    // 연결하기 전에 그룹폭 검사
                    float potentialSpan = Mathf.Abs(Mathf.DeltaAngle(currentGroup.First().relativeAngle, nextNode.relativeAngle));

                    if (potentialSpan <= MaxGroupSpanAngle)
                    {
                        // 연결 성공!
                        currentGroup.Add(nextNode);
                        ungroupedMetas.Remove(nextNode);
                        LogToUI($"   └ [{currentNode.sortedIndex}]→[{nextNode.sortedIndex}] <color=green>[연결]</color>");
                    }
                    else
                    {
                        // 그룹폭 초과로 방출
                        LogToUI($"   └ [{currentNode.sortedIndex}]→[{nextNode.sortedIndex}] <color=red>[방출]</color> (그룹폭 {potentialSpan:F1}° 초과)");
                        // nextNode는 연결 안 되고 외톨이로 남음 (다음 루프에서 처리)
                    }
                }
            }

            // 완성된 그룹 처리
            if (currentGroup.Count > 1) finalGroups.Add(currentGroup);
            else loners.AddRange(currentGroup); // 혼자 남았으면 외톨이
        }

        LogToUI($"[확정] 정규 그룹 {finalGroups.Count}개 / 외톨이 {loners.Count}명");

        // 4. 페이즈 및 곡선 할당
        int colorIdx = 0;
        foreach (var group in finalGroups)
        {
            Color col = debugColors[colorIdx % debugColors.Length];
            colorIdx++;
            foreach (var m in group) m.debugColor = col;
            ProcessGroupRules(group, startPoint.position, startScreenPos, cam);
        }

        if (loners.Count > 0)
        {
            ProcessPhaseOnly(loners);
            foreach (var m in loners)
            {
                m.debugColor = Color.white;
                ProcessSingleLonerCurve(m, startPoint.position, startScreenPos, cam);
            }
        }

        debugMetas = metas;

        // 5. 결과 생성
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
                res.pathPoints = PathUtilities.GenerateQuadraticBezierCurvePath(
                    startPoint.position, m.assignedControlPoint, m.worldPos, 20);
            }
            results.Add(res);
        }
        return results;
    }

    // ---------------------------------------------------------
    // [헬퍼 함수들]
    // ---------------------------------------------------------
    private void ProcessPhaseOnly(List<TargetMeta> members)
    {
        int N = members.Count;
        var sortedByLen = members.OrderByDescending(m => m.distance3D).ToList();
        float M = N + 2.0f;
        for (int k = 0; k < N; k++)
        {
            sortedByLen[k].assignedPhase = (float)(N - k) / M;
        }
    }

    private void ProcessSingleLonerCurve(TargetMeta m, Vector3 startPos, Vector2 startScreenPos, Camera cam)
    {
        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        bool isLeft = m.screenPos.x < screenCenter.x;

        Vector3 dir = (m.worldPos - startPos).normalized;
        Vector3 visualRight = Vector3.ProjectOnPlane(cam.transform.right, dir).normalized;
        Vector3 bendVector = isLeft ? -visualRight : visualRight;

        float offset = m.distance3D * CurveRatioWeak;
        Vector3 mid = (startPos + m.worldPos) * 0.5f;
        m.assignedControlPoint = mid + (bendVector * offset);
    }

    private void ProcessGroupRules(List<TargetMeta> members, Vector3 startPos, Vector2 startScreenPos, Camera cam)
    {
        ProcessPhaseOnly(members);

        int N = members.Count;
        float minX = members.Min(m => m.screenPos.x);
        float maxX = members.Max(m => m.screenPos.x);
        float widthRatio = (maxX - minX) / Screen.width;

        if (N >= HighDensityCount && widthRatio < 0.2f)
        {
            LogToUI($"[고밀도] 그룹 직선화 ({N}개)");
            foreach (var m in members)
            {
                m.isStraight = true;
                m.debugColor = Color.HSVToRGB((float)members.IndexOf(m) / N, 1f, 1f);
            }
            return;
        }

        float avgRelAngle = members.Average(m => m.relativeAngle);
        float zoneFactor = Mathf.Clamp01(Mathf.Abs(avgRelAngle) / 45.0f);
        float zoneMultiplier = Mathf.Lerp(0.5f, 2.5f, zoneFactor);

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
            Vector3 downTwist = (-visualUp * 0.8f + visualRight * 0.2f).normalized;

            Vector3 bendVector = Vector3.zero;
            bool isLeftInGroup = (i < centerIdx);

            if (N % 2 == 1 && i == centerIdx) m.isStraight = true;
            else if (N <= 4)
            {
                bendVector = isLeftInGroup ? -visualRight : visualRight;
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