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
        public int originalIndex;   // 설치된 순서
        public int sortedIndex;     // ★ 화면 왼쪽부터 1,2,3...
        public string visualID;
        public Transform transform;
        public Vector3 worldPos;
        public Vector2 screenPos;
        public float distance3D;
        public float rawAngle;
        public float relativeAngle;

        public int parentID; // Union-Find 루트
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
        LogToUI("\n──────── [스마트 경로 분석 V5: 체인확장] ────────");

        if (cam == null || startPoint == null || targets == null || targets.Count == 0)
            return new List<PathResultData>();

        List<TargetMeta> metas = new List<TargetMeta>();
        Vector3 startScreenPos3 = cam.WorldToScreenPoint(startPoint.position);
        Vector2 startScreenPos = new Vector2(startScreenPos3.x, startScreenPos3.y);
        debugStartPos = startPoint.position;
        float screenWidth = Screen.width;

        // ---------------------------------------------------------
        // [1] 데이터 수집
        // ---------------------------------------------------------
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

        // ---------------------------------------------------------
        // [2] 정렬 (왼쪽 -> 오른쪽) 및 인덱스 재부여
        // ---------------------------------------------------------
        metas.Sort((a, b) => b.rawAngle.CompareTo(a.rawAngle));

        float centerOffsetAngle = metas[metas.Count / 2].rawAngle;
        foreach (var m in metas)
        {
            m.relativeAngle = Mathf.DeltaAngle(centerOffsetAngle, m.rawAngle);
        }
        metas.Sort((a, b) => b.relativeAngle.CompareTo(a.relativeAngle));

        for (int i = 0; i < metas.Count; i++)
        {
            metas[i].sortedIndex = i + 1; // 1부터 시작
            metas[i].parentID = i;        // Union-Find 초기화
        }

        // ---------------------------------------------------------
        // [3] 그룹핑 (전체 탐색 & 체인 연결)
        // ---------------------------------------------------------
        float distLimitPx = screenWidth * MaxScreenDistRatio;
        LogToUI($"[조건] 각도 {AngleThreshold}° / 거리 {distLimitPx:F0}px");

        // i: 기준 타겟
        for (int i = 0; i < metas.Count; i++)
        {
            TargetMeta A = metas[i];

            // j: 내 뒤에 있는 후보들 (끝까지 검사)
            for (int j = i + 1; j < metas.Count; j++)
            {
                TargetMeta B = metas[j];

                // 1. 각도 검사
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(A.relativeAngle, B.relativeAngle));

                // 정렬되어 있으므로 각도 범위를 벗어나면, 그 뒤에 있는 애들도 다 벗어난 것임.
                // 따라서 여기서 break 하는 것은 안전하며 효율적입니다.
                if (angleDiff > AngleThreshold) break;

                // 2. 거리 검사
                float distPx = Vector2.Distance(A.screenPos, B.screenPos);

                string logHeader = $"[{A.sortedIndex}]→[{B.sortedIndex}]";

                if (distPx <= distLimitPx)
                {
                    // ★ 연결 성공 ★
                    UnionGroups(metas, i, j);
                    LogToUI($"{logHeader} <color=green>[연결]</color> (거리 {distPx:F0}px OK)");

                    // ※ 수정 사항: 여기서 break 하지 않음! 
                    // 1번이 3번이랑 연결돼도, 1번은 4번이랑도 연결될 수 있음 (문어발 연결 허용)
                    // 그래야 그룹이 최대한 커질 수 있음.
                }
                else
                {
                    LogToUI($"{logHeader} <color=orange>[패스]</color> (거리 멂: {distPx:F0}px)");
                }
            }
        }

        // ---------------------------------------------------------
        // [3.5] 그룹 정리 및 가지치기 (Pruning)
        // ---------------------------------------------------------
        Dictionary<int, List<TargetMeta>> tempGroups = new Dictionary<int, List<TargetMeta>>();
        for (int i = 0; i < metas.Count; i++)
        {
            int root = FindRoot(metas, i);
            if (!tempGroups.ContainsKey(root)) tempGroups[root] = new List<TargetMeta>();
            tempGroups[root].Add(metas[i]);
        }

        List<List<TargetMeta>> finalRealGroups = new List<List<TargetMeta>>();
        List<TargetMeta> loners = new List<TargetMeta>(); // 쫓겨나거나 외톨이인 애들

        foreach (var kvp in tempGroups)
        {
            List<TargetMeta> group = kvp.Value;

            // 1명짜리 그룹은 바로 외톨이
            if (group.Count <= 1)
            {
                loners.AddRange(group);
                continue;
            }

            // ★ 오른쪽 끝부터 가지치기 반복 ★
            while (group.Count > 1)
            {
                // 그룹의 양끝 각도 차이 계산
                float totalSpan = Mathf.Abs(Mathf.DeltaAngle(group[0].relativeAngle, group.Last().relativeAngle));

                if (totalSpan > MaxGroupSpanAngle)
                {
                    // 범위 초과! 맨 오른쪽(Last) 퇴출
                    TargetMeta removed = group.Last();
                    group.RemoveAt(group.Count - 1);

                    loners.Add(removed); // 퇴출된 녀석은 외톨이 연합으로

                    // ★ 퇴출 로그 ★
                    LogToUI($"<color=red>[퇴출]</color> 타겟 [{removed.sortedIndex}] -> 그룹 과부하(폭 {totalSpan:F1}°)로 방출됨.");
                }
                else
                {
                    break; // 통과
                }
            }

            // 가지치기 후 남은 인원 확인
            if (group.Count > 1) finalRealGroups.Add(group);
            else loners.AddRange(group); // 다 짤리고 혼자 남았으면 외톨이
        }

        LogToUI($"[확정] 정규 그룹 {finalRealGroups.Count}개 / 외톨이 연합 {loners.Count}명");

        // ---------------------------------------------------------
        // [4] 페이즈 및 곡선 할당
        // ---------------------------------------------------------

        // A. 정규 그룹 처리
        int colorIdx = 0;
        foreach (var group in finalRealGroups)
        {
            Color col = debugColors[colorIdx % debugColors.Length];
            colorIdx++;
            foreach (var m in group) m.debugColor = col;

            ProcessGroupRules(group, startPoint.position, startScreenPos, cam);
        }

        // B. 외톨이 연합 처리 (페이즈 그룹화 + 개별 곡선)
        if (loners.Count > 0)
        {
            // 외톨이끼리도 길이순으로 페이즈 배정 (요청사항)
            ProcessPhaseOnly(loners);

            foreach (var m in loners)
            {
                m.debugColor = Color.white;
                // 외톨이는 굳이 강하게 휠 필요 없이 중앙 기준으로 살짝만 휨 (혹은 직선)
                ProcessSingleLonerCurve(m, startPoint.position, startScreenPos, cam);
            }
        }

        debugMetas = metas;

        // [5] 결과 생성
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

    private int FindRoot(List<TargetMeta> metas, int index)
    {
        if (metas[index].parentID != index)
            metas[index].parentID = FindRoot(metas, metas[index].parentID);
        return metas[index].parentID;
    }

    private void UnionGroups(List<TargetMeta> metas, int indexA, int indexB)
    {
        int rootA = FindRoot(metas, indexA);
        int rootB = FindRoot(metas, indexB);
        if (rootA != rootB) metas[rootB].parentID = rootA;
    }

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
        // 외톨이는 화면 중심 기준 반대 방향으로 살짝 휨 (충돌 회피용)
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
        // 1. 페이즈 할당
        ProcessPhaseOnly(members);

        // 2. 고밀도 예외
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

        // 3. 구역 기반 강도
        float avgRelAngle = members.Average(m => m.relativeAngle);
        float zoneFactor = Mathf.Clamp01(Mathf.Abs(avgRelAngle) / 45.0f);
        float zoneMultiplier = Mathf.Lerp(0.5f, 2.5f, zoneFactor);

        // 4. 곡선 패턴
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