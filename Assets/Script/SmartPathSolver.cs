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
    // [평가 모드]
    public enum EvaluationMode
    {
        Full,       // 페이즈 O, 곡선 O
        PhaseOnly,  // 페이즈 O, 곡선 X (직선)
        CurveOnly,  // 페이즈 X (0), 곡선 O
        None        // 페이즈 X (0), 곡선 X (직선)
    }

    [Header("0. 평가 모드")]
    public EvaluationMode currentMode = EvaluationMode.Full;

    [Header("1. 그룹핑 설정")]
    public float AngleThreshold = 15.0f;
    public float MaxScreenDistRatio = 0.2f;
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

        // 결과값
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
        LogToUI($"\n──────── [스마트 경로 분석] 모드: {currentMode} ────────");

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

            // ★ [모드 초기화] 모드에 따라 기본값 강제 설정
            if (currentMode == EvaluationMode.CurveOnly || currentMode == EvaluationMode.None)
                meta.assignedPhase = 0.0f; // 페이즈 끄기 (시작점)

            if (currentMode == EvaluationMode.PhaseOnly || currentMode == EvaluationMode.None)
                meta.isStraight = true;    // 곡선 끄기 (직선)

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
        // [단계 3] 그룹핑 로직 (상세 로그 & 체인 연결 & 가지치기)
        // ---------------------------------------------------------
        float distLimitPx = screenWidth * MaxScreenDistRatio;
        LogToUI($"[조건] 각도 {AngleThreshold}° / 거리 {distLimitPx:F0}px");
        LogToUI("--- 상세 과정 ---");

        List<List<TargetMeta>> finalGroups = new List<List<TargetMeta>>();
        List<TargetMeta> loners = new List<TargetMeta>();

        // 아직 그룹 없는 애들
        HashSet<TargetMeta> ungroupedMetas = new HashSet<TargetMeta>(metas);

        for (int i = 0; i < metas.Count; i++)
        {
            TargetMeta startNode = metas[i];
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

                // 각도 범위 밖이면 중단 (정렬되어 있으므로)
                if (angleDiff > AngleThreshold) break;

                // 거리 조건 확인
                if (distPx <= distLimitPx)
                {
                    // 그룹폭 검사 (Pruning)
                    float potentialSpan = Mathf.Abs(Mathf.DeltaAngle(currentGroup.First().relativeAngle, nextNode.relativeAngle));

                    if (potentialSpan <= MaxGroupSpanAngle)
                    {
                        // 연결 성공
                        currentGroup.Add(nextNode);
                        ungroupedMetas.Remove(nextNode);
                        LogToUI($"   └ [{currentNode.sortedIndex}]→[{nextNode.sortedIndex}] <color=green>[연결]</color>");
                    }
                    else
                    {
                        // 그룹폭 초과 -> 방출 (연결 안 함)
                        LogToUI($"   └ [{currentNode.sortedIndex}]→[{nextNode.sortedIndex}] <color=red>[방출]</color> (폭 {potentialSpan:F1}° 초과)");
                    }
                }
                else
                {
                    // 거리가 멀어서 패스
                    LogToUI($"   └ [{currentNode.sortedIndex}]→[{nextNode.sortedIndex}] <color=orange>[패스]</color> (거리 멂)");
                }
            }

            if (currentGroup.Count > 1) finalGroups.Add(currentGroup);
            else loners.AddRange(currentGroup);
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

            // ★ [최종 확인] 모드가 직선 강제면 여기서도 확실히 직선화
            if (currentMode == EvaluationMode.PhaseOnly || currentMode == EvaluationMode.None)
            {
                m.isStraight = true;
            }

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
        // ★ [모드 체크] 페이즈 끄는 모드면 0.0 (시작점) 고정
        if (currentMode == EvaluationMode.CurveOnly || currentMode == EvaluationMode.None)
        {
            foreach (var m in members) m.assignedPhase = 0.0f;
            return;
        }

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
        // ★ [모드 체크] 직선 모드면 스킵
        if (currentMode == EvaluationMode.PhaseOnly || currentMode == EvaluationMode.None)
        {
            m.isStraight = true;
            return;
        }

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
        ProcessPhaseOnly(members); // 페이즈 할당

        // [모드 체크] 직선 모드면 중단
        if (currentMode == EvaluationMode.PhaseOnly || currentMode == EvaluationMode.None)
        {
            foreach (var m in members) m.isStraight = true;
            return;
        }

        int N = members.Count;

        // 1. 그룹의 형태(가로/세로) 및 중심 파악
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float sumX = 0;

        foreach (var m in members)
        {
            if (m.screenPos.x < minX) minX = m.screenPos.x;
            if (m.screenPos.x > maxX) maxX = m.screenPos.x;
            if (m.screenPos.y < minY) minY = m.screenPos.y;
            if (m.screenPos.y > maxY) maxY = m.screenPos.y;
            sumX += m.screenPos.x;
        }

        float groupWidth = Mathf.Abs(maxX - minX);
        float groupHeight = Mathf.Abs(maxY - minY);
        float groupCenterX = sumX / N; // 그룹의 무게중심 X
        float screenWidth = Screen.width;

        // 2. 고밀도 예외 (너무 좁은 영역에 많이 뭉침)
        if (currentMode == EvaluationMode.Full && N >= HighDensityCount && (groupWidth / screenWidth) < 0.2f)
        {
            LogToUI($"[고밀도] 그룹 직선화 ({N}개)");
            foreach (var m in members)
            {
                m.isStraight = true;
                m.debugColor = Color.HSVToRGB((float)members.IndexOf(m) / N, 1f, 1f);
            }
            return;
        }

        // ★★★ [추가된 로직] 세로 그룹 판별 ★★★
        // 높이가 너비보다 1.5배 이상 크면 '세로 그룹'으로 간주
        bool isVerticalGroup = groupHeight > (groupWidth * 1.5f);

        // 3. 구역 진단 (중앙/외곽)
        float avgRelAngle = members.Average(m => m.relativeAngle);
        float zoneFactor = Mathf.Clamp01(Mathf.Abs(avgRelAngle) / 45.0f);
        float zoneMultiplier = Mathf.Lerp(0.5f, 2.5f, zoneFactor);

        // 4. 곡선 계산
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

            // ★★★ [수정된 좌/우 판단 로직] ★★★
            bool isLeftInGroup;

            if (isVerticalGroup)
            {
                // 세로 그룹이라면: 순서(Index) 무시하고, 실제 화면 좌표(X)로 판단
                // 그룹 중심보다 왼쪽에 있으면 왼쪽 곡선, 오른쪽에 있으면 오른쪽 곡선
                isLeftInGroup = m.screenPos.x < groupCenterX;
            }
            else
            {
                // 가로 그룹(일반)이라면: 정렬된 순서(Index)를 따름
                isLeftInGroup = (i < centerIdx);
            }

            // --- 패턴 할당 (기존과 동일) ---
            if (N % 2 == 1 && i == centerIdx && !isVerticalGroup)
            {
                // 홀수 그룹의 정중앙 (세로 그룹일 때는 제외, 확실히 갈라주기 위해)
                m.isStraight = true;
            }
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
                    // 세로 그룹이거나 일반적인 경우
                    // 만약 세로 그룹이라 인덱스 순서가 꼬였다면 여기서 i값 기반 분산이 
                    // 약간 랜덤하게 보일 순 있지만, 방향(좌/우)은 확실히 맞게 됨.
                    int rIdx = i - centerIdx;
                    if (rIdx % 2 == 0) bendVector = visualRight;
                    else bendVector = downTwist;
                }
            }

            float ratio = CurveRatioStrong;
            if (m == closest) { ratio = CurveRatioWeak; if (N % 2 == 1 && !isVerticalGroup) m.isStraight = true; }
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