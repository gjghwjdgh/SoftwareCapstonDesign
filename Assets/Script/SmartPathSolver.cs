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
    public float AngleThreshold = 15.0f;
    public float MaxScreenDistRatio = 0.2f;
    public float MaxGroupSpanAngle = 45.0f; // 추가된 기능: 그룹 최대 각도

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
        public int parentID;
        public float assignedPhase;
        public Vector3 assignedControlPoint;
        public bool isStraight = false;
        public Color? debugColor;
    }

    private List<TargetMeta> debugMetas = new List<TargetMeta>();
    private Vector3 debugStartPos;

    // 메인 함수
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

            // [수정됨] sPos 변수 정의 추가
            Vector3 sPos = cam.WorldToScreenPoint(meta.worldPos);
            meta.screenPos = new Vector2(sPos.x, sPos.y);

            Vector2 dir = meta.screenPos - startScreenPos;
            meta.rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            meta.parentID = metas.Count;

            metas.Add(meta);
        }

        if (metas.Count == 0)
        {
            LogToUI("결과: 타겟 없음.");
            return new List<PathResultData>();
        }

        // 2. 정렬 (각도 순서)
        metas.Sort((a, b) => b.rawAngle.CompareTo(a.rawAngle));

        float centerOffsetAngle = metas[metas.Count / 2].rawAngle;
        foreach (var m in metas)
        {
            m.relativeAngle = Mathf.DeltaAngle(centerOffsetAngle, m.rawAngle);
        }
        metas.Sort((a, b) => b.relativeAngle.CompareTo(a.relativeAngle));

        for (int i = 0; i < metas.Count; i++) metas[i].parentID = i;

        // 3. 그룹핑 로직 (Greedy Chain + Span검사 + 원형연결)
        float distLimitPx = screenWidth * MaxScreenDistRatio;
        LogToUI($"[그룹핑 기준] 각도 {AngleThreshold}° / 거리 {distLimitPx:F0}px / 최대폭 {MaxGroupSpanAngle}°");

        for (int i = 0; i < metas.Count; i++)
        {
            TargetMeta A = metas[i];

            for (int j = i + 1; j < metas.Count; j++)
            {
                TargetMeta B = metas[j];

                // A. 인접 각도 검사
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(A.relativeAngle, B.relativeAngle));
                if (angleDiff > AngleThreshold) break;

                // B. 그룹 전체 폭(Span) 검사
                float spanDiff = Mathf.Abs(Mathf.DeltaAngle(A.relativeAngle, B.relativeAngle));
                if (spanDiff > MaxGroupSpanAngle)
                {
                    LogToUI($"[{A.visualID}] vs [{B.visualID}] -> [중단] 그룹폭 초과 ({spanDiff:F1}°)");
                    break;
                }

                // C. 거리 검사
                float distPx = Vector2.Distance(A.screenPos, B.screenPos);
                string logMsg = $"[{A.visualID}] vs [{B.visualID}] : 각도 {angleDiff:F1}° / 거리 {distPx:F0}px";

                if (distPx <= distLimitPx)
                {
                    UnionGroups(metas, i, j);
                    logMsg += " -> <color=green>[연결 성공]</color>";
                    LogToUI(logMsg);
                    break;
                }
                else
                {
                    logMsg += " -> <color=orange>[패스: 거리 멂]</color>";
                    LogToUI(logMsg);
                }
            }
        }

        // 3.5 원형 연결 검사 (Circular Check)
        if (metas.Count > 1)
        {
            TargetMeta First = metas[0];
            TargetMeta Last = metas[metas.Count - 1];

            float circleAngleDiff = 360f - Mathf.Abs(First.rawAngle - Last.rawAngle);
            float circleDistPx = Vector2.Distance(First.screenPos, Last.screenPos);

            if (circleAngleDiff <= AngleThreshold && circleDistPx <= distLimitPx)
            {
                UnionGroups(metas, 0, metas.Count - 1);
                LogToUI($"[원형 연결] [{Last.visualID}] <-> [{First.visualID}] 연결됨 (각도 {circleAngleDiff:F1}°)");
            }
        }

        // 4. 그룹 정리
        Dictionary<int, List<TargetMeta>> groupDict = new Dictionary<int, List<TargetMeta>>();
        for (int i = 0; i < metas.Count; i++)
        {
            int rootID = FindRoot(metas, i);
            if (!groupDict.ContainsKey(rootID)) groupDict[rootID] = new List<TargetMeta>();
            groupDict[rootID].Add(metas[i]);
        }

        List<List<TargetMeta>> finalGroups = groupDict.Values.ToList();
        debugMetas = metas;

        LogToUI($"\n[최종 결과] 총 {finalGroups.Count}개 그룹 확정");
        for (int g = 0; g < finalGroups.Count; g++)
        {
            string members = string.Join(", ", finalGroups[g].Select(m => m.visualID));
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
                // PathUtilities 클래스가 프로젝트에 포함되어 있어야 합니다.
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
            metas[index].parentID = FindRoot(metas, metas[index].parentID);
        return metas[index].parentID;
    }

    private void UnionGroups(List<TargetMeta> metas, int indexA, int indexB)
    {
        int rootA = FindRoot(metas, indexA);
        int rootB = FindRoot(metas, indexB);
        if (rootA != rootB) metas[rootB].parentID = rootA;
    }

    private void ProcessGroupRules(List<TargetMeta> members, Vector3 startPos, Vector2 startScreenPos, Camera cam)
    {
        int N = members.Count;

        // 1. 페이즈 할당
        var sortedByLen = members.OrderByDescending(m => m.distance3D).ToList();
        float M = N + 2.0f;
        for (int k = 0; k < N; k++) sortedByLen[k].assignedPhase = (float)(N - k) / M;

        // 2. 고밀도 예외
        float minX = members.Min(m => m.screenPos.x);
        float maxX = members.Max(m => m.screenPos.x);
        float widthRatio = (maxX - minX) / Screen.width;

        if (N >= HighDensityCount && widthRatio < 0.2f)
        {
            LogToUI($"  >> [고밀도] {N}개 밀집 -> 직선화");
            for (int i = 0; i < N; i++)
            {
                members[i].isStraight = true;
                members[i].debugColor = Color.HSVToRGB((float)i / N, 1f, 1f);
            }
            return;
        }

        // 3. 구역 가중치
        float avgRelAngle = members.Average(m => m.relativeAngle);
        float zoneFactor = Mathf.Clamp01(Mathf.Abs(avgRelAngle) / 45.0f);
        float zoneMultiplier = Mathf.Lerp(0.5f, 2.5f, zoneFactor);

        // 4. 깊이 정보
        var sortedByDepth = members.OrderBy(m => m.distance3D).ToList();
        TargetMeta closest = sortedByDepth.First();
        TargetMeta farthest = sortedByDepth.Last();

        // 5. 배치 시나리오
        for (int i = 0; i < N; i++)
        {
            TargetMeta m = members[i];

            Vector3 dir = (m.worldPos - startPos).normalized;
            Vector3 vRight = Vector3.ProjectOnPlane(cam.transform.right, dir).normalized;
            Vector3 vUp = Vector3.ProjectOnPlane(cam.transform.up, dir).normalized;

            Vector3 left = -vRight;
            Vector3 right = vRight;
            Vector3 upTwist = (vUp * 0.8f + vRight * 0.2f).normalized;
            Vector3 downTwist = (-vUp * 0.8f + vRight * 0.2f).normalized;

            Vector3 bendDir = Vector3.zero;
            bool forceStraight = false;
            float strength = CurveRatioStrong;

            if (N == 2)
            {
                if (i == 0) bendDir = left;
                else bendDir = right;
            }
            else if (N == 3)
            {
                if (i == 0) bendDir = left;
                else if (i == 1)
                {
                    if (m == closest) forceStraight = true;
                    else forceStraight = true;
                }
                else bendDir = right;
            }
            else if (N == 4)
            {
                if (i == 0) { bendDir = left; strength *= 1.2f; }
                else if (i == 1) { bendDir = left; strength *= 0.5f; }
                else if (i == 2) { bendDir = right; strength *= 0.5f; }
                else { bendDir = right; strength *= 1.2f; }
            }
            else
            {
                int centerIdx = N / 2;
                if (i == centerIdx)
                {
                    if (N % 2 == 1) forceStraight = true;
                    else bendDir = (i % 2 == 0) ? left : right;
                }
                else if (i < centerIdx)
                {
                    if (i % 2 == 0) bendDir = left;
                    else bendDir = (vUp * 0.8f - vRight * 0.2f).normalized;
                }
                else
                {
                    if (i % 2 == 0) bendDir = right;
                    else bendDir = downTwist;
                }
            }

            if (m == closest && !forceStraight)
            {
                strength = CurveRatioWeak;
                if (N % 2 == 1) forceStraight = true;
            }
            else if (m == farthest && !forceStraight)
            {
                strength = CurveRatioStrong * 1.5f;
            }

            if (forceStraight)
            {
                m.isStraight = true;
            }
            else
            {
                float finalOffset = m.distance3D * strength * zoneMultiplier;
                Vector3 mid = (startPos + m.worldPos) * 0.5f;
                m.assignedControlPoint = mid + (bendDir * finalOffset);
            }
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