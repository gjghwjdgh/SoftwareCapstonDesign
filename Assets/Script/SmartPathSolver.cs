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
    [Tooltip("이 각도 범위(도) 안에 있어야 검사 대상이 됩니다.")]
    public float AngleThreshold = 15.0f;

    [Tooltip("화면상 거리가 (화면폭 x 이 비율) 이내여야 연결합니다.")]
    public float MaxScreenDistRatio = 0.2f; // 예: 화면폭의 20%

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
        public string visualID;
        public Transform transform;
        public Vector3 worldPos;
        public Vector2 screenPos;
        public float distance3D;
        public float rawAngle;
        public float relativeAngle;
        public int parentID; // Union-Find용
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
        LogToUI("\n──────── [스마트 경로 분석 시작] ────────");

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

            string idStr = (i + 1).ToString();
            var textComponent = targets[i].GetComponentInChildren<TMP_Text>();
            if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
            {
                string txt = textComponent.text.Trim();
                if (txt != "0" && txt != "New Text" && txt.Length > 0) idStr = txt;
            }

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
            meta.parentID = metas.Count;

            metas.Add(meta);
        }

        if (metas.Count == 0) return new List<PathResultData>();

        // 2. 정렬 (각도 순서)
        metas.Sort((a, b) => b.rawAngle.CompareTo(a.rawAngle));

        float centerOffsetAngle = metas[metas.Count / 2].rawAngle;
        foreach (var m in metas)
        {
            m.relativeAngle = Mathf.DeltaAngle(centerOffsetAngle, m.rawAngle);
        }
        metas.Sort((a, b) => b.relativeAngle.CompareTo(a.relativeAngle));

        for (int i = 0; i < metas.Count; i++) metas[i].parentID = i;

        // ---------------------------------------------------------
        // [단계 3] 그룹핑 로직 (상세 로그 & 체인 연결)
        // ---------------------------------------------------------
        float distLimitPx = screenWidth * MaxScreenDistRatio;

        LogToUI($"[그룹핑 기준] 각도범위 {AngleThreshold}° / 거리제한 {distLimitPx:F0}px");
        LogToUI("--- 상세 과정 ---");

        // i: 기준 타겟 (왼쪽부터 차례대로)
        for (int i = 0; i < metas.Count; i++)
        {
            TargetMeta A = metas[i];
            bool foundConnection = false; // 연결했는지 확인용

            // j: 내 오른쪽(뒤)에 있는 후보들 탐색
            for (int j = i + 1; j < metas.Count; j++)
            {
                TargetMeta B = metas[j];

                // 1. 각도 차이 계산
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(A.relativeAngle, B.relativeAngle));

                // [검사 1] 각도 범위 밖인가?
                if (angleDiff > AngleThreshold)
                {
                    // 정렬되어 있으므로, 여기서 각도가 멀어지면 그 뒤는 볼 필요 없음
                    // LogToUI($"[{A.visualID}] 검사 중단 -> [{B.visualID}]부터 각도({angleDiff:F1}°) 벗어남.");
                    break;
                }

                // [검사 2] 거리 계산
                float distPx = Vector2.Distance(A.screenPos, B.screenPos);

                if (distPx <= distLimitPx)
                {
                    // ★ 조건 충족: 연결!
                    UnionGroups(metas, i, j);

                    LogToUI($"[{A.visualID}] -> [{B.visualID}] <color=green>[연결 성공]</color>");
                    LogToUI($"   └ (각도 {angleDiff:F1}° / 거리 {distPx:F0}px)");

                    foundConnection = true;
                    // ★ 하나라도 연결되면 A에 대한 탐색 종료 (다음 인덱스 i+1로 이동)
                    break;
                }
                else
                {
                    // ★ 조건 불충족: 거리가 멂 (건너뛰기)
                    // 연결은 안 하지만, 각도 범위 내에 있으므로 다음 후보(j+1)를 계속 확인해야 함
                    LogToUI($"[{A.visualID}] -> [{B.visualID}] <color=orange>[패스]</color> (거리 멂: {distPx:F0}px)");
                }
            }

            if (!foundConnection)
            {
                // 아무랑도 연결 안 됐으면 (각도 범위 내에 아무도 없거나, 다 거리가 멀었음)
                // LogToUI($"[{A.visualID}] -> 연결 대상 없음 (외톨이 가능성)");
            }
        }

        // 3.5 그룹 정리
        Dictionary<int, List<TargetMeta>> groupDict = new Dictionary<int, List<TargetMeta>>();
        for (int i = 0; i < metas.Count; i++)
        {
            int rootID = FindRoot(metas, i);
            if (!groupDict.ContainsKey(rootID)) groupDict[rootID] = new List<TargetMeta>();
            groupDict[rootID].Add(metas[i]);
        }

        List<List<TargetMeta>> finalGroups = groupDict.Values.ToList();
        debugMetas = metas;

        // 그룹 결과 요약 출력
        LogToUI($"\n[최종 결과] 총 {finalGroups.Count}개 그룹 확정");
        for (int g = 0; g < finalGroups.Count; g++)
        {
            var grp = finalGroups[g];
            string members = string.Join(", ", grp.Select(m => m.visualID));
            LogToUI($"  • 그룹 {g + 1}: [{members}]");
        }

        // 5. 그룹별 처리
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
                res.pathPoints = PathUtilities.GenerateQuadraticBezierCurvePath(
                    startPoint.position, m.assignedControlPoint, m.worldPos, 20);
            }
            results.Add(res);
        }
        return results;
    }

    private int FindRoot(List<TargetMeta> metas, int index)
    {
        if (metas[index].parentID != index)
        {
            metas[index].parentID = FindRoot(metas, metas[index].parentID);
        }
        return metas[index].parentID;
    }

    private void UnionGroups(List<TargetMeta> metas, int indexA, int indexB)
    {
        int rootA = FindRoot(metas, indexA);
        int rootB = FindRoot(metas, indexB);
        if (rootA != rootB)
        {
            metas[rootB].parentID = rootA;
        }
    }

    private void ProcessGroupRules(List<TargetMeta> members, Vector3 startPos, Vector2 startScreenPos, Camera cam)
    {
        int N = members.Count;

        var sortedByLen = members.OrderByDescending(m => m.distance3D).ToList();
        float M = N + 2.0f;
        for (int k = 0; k < N; k++)
        {
            sortedByLen[k].assignedPhase = (float)(N - k) / M;
        }

        float minX = members.Min(m => m.screenPos.x);
        float maxX = members.Max(m => m.screenPos.x);
        float widthRatio = (maxX - minX) / Screen.width;

        if (N >= HighDensityCount && widthRatio < 0.2f)
        {
            for (int i = 0; i < N; i++)
            {
                members[i].isStraight = true;
                members[i].debugColor = Color.HSVToRGB((float)i / N, 1f, 1f);
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