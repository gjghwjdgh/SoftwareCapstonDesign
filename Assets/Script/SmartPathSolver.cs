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
    public float AngleThreshold = 10.0f;
    public float MaxSectorAngle = 45.0f;
    public float CenterZoneRatio = 0.3f;
    public float CurveRatioWeak = 0.05f;
    public float CurveRatioStrong = 0.15f;
    public int HighDensityCount = 8;

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

        foreach (var members in regularGroups) AssignPhaseAndPattern(members, startPoint.position, startScreenPos, false, Vector2.zero, cam);

        if (loners.Count > 0)
        {
            Vector2 globalCentroid = Vector2.zero;
            foreach (var m in metas) globalCentroid += m.screenPos;
            globalCentroid /= metas.Count;
            AssignPhaseAndPattern(loners, startPoint.position, startScreenPos, true, globalCentroid, cam);
        }

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

    private void AssignPhaseAndPattern(List<TargetMeta> members, Vector3 startPos, Vector2 startScreenPos, bool isLonerGroup, Vector2 globalCentroid, Camera cam)
    {
        int N = members.Count;
        float M = N + 2.0f;

        // ★★★ [규격서 공식 적용] 길이 기준 내림차순 정렬 (0번이 가장 긴 놈) ★★★
        var sortedByLen = members.OrderByDescending(m => m.distance3D).ToList();

        for (int k = 0; k < N; k++)
        {
            // 공식: (N - 등수 + 1) / (N + 2)
            // 코드 k는 0부터 시작하므로 '등수'는 k+1
            // 식: (N - (k+1) + 1) / M  => (N - k) / M

            float phase = (float)(N - k) / M;
            sortedByLen[k].assignedPhase = phase;
        }

        if (!isLonerGroup && N >= HighDensityCount)
        {
            for (int i = 0; i < N; i++)
            {
                sortedByLen[i].isStraight = true;
                sortedByLen[i].debugColor = Color.HSVToRGB((float)i / N, 1f, 1f);
            }
            return;
        }

        Vector2 groupCentroid = Vector2.zero;
        foreach (var m in members) groupCentroid += m.screenPos;
        groupCentroid /= N;
        float screenDiag = new Vector2(Screen.width, Screen.height).magnitude;
        Vector2 refCentroid = isLonerGroup ? globalCentroid : groupCentroid;
        bool isCenterZone = (Vector2.Distance(startScreenPos, refCentroid) / (screenDiag * 0.5f)) < CenterZoneRatio;

        if (isLonerGroup) ApplyLonerRules(members, startPos, startScreenPos, globalCentroid, cam);
        else ApplyGroupRules(members, startPos, isCenterZone, cam);
    }

    // (ApplyGroupRules, ApplyLonerRules는 이전과 동일하므로 생략 없이 유지)
    private void ApplyGroupRules(List<TargetMeta> members, Vector3 startPos, bool isCenterZone, Camera cam)
    {
        int count = members.Count;
        var sortedByDepth = members.OrderBy(m => m.distance3D).ToList();
        TargetMeta closest = sortedByDepth.First();
        TargetMeta farthest = sortedByDepth.Last();
        for (int i = 0; i < count; i++)
        {
            TargetMeta m = members[i];
            Vector3 dir = (m.worldPos - startPos).normalized;
            Vector3 visualRight = Vector3.ProjectOnPlane(cam.transform.right, dir).normalized;
            Vector3 visualUp = Vector3.ProjectOnPlane(cam.transform.up, dir).normalized;
            Vector3 left = -visualRight; Vector3 right = visualRight;
            Vector3 upTwist = (visualUp * 0.8f + visualRight * 0.2f).normalized;
            Vector3 downTwist = (-visualUp * 0.8f + visualRight * 0.2f).normalized;
            Vector3 bendDir = Vector3.zero;
            float strength = CurveRatioStrong;

            int centerIdx = count / 2;
            if (count >= 3 && i == centerIdx) m.isStraight = true;
            else
            {
                if (i < centerIdx) bendDir = (i % 2 == 0) ? left : upTwist;
                else bendDir = (i % 2 == 0) ? right : downTwist;
            }
            if (m == farthest && !m.isStraight) strength *= 1.2f;
            if (m == closest && !m.isStraight) strength = CurveRatioWeak;
            if (isCenterZone) strength *= 0.5f; else strength *= 2.0f;
            Vector3 mid = (startPos + m.worldPos) * 0.5f;
            m.assignedControlPoint = mid + (bendDir * m.distance3D * strength);
        }
    }

    private void ApplyLonerRules(List<TargetMeta> loners, Vector3 startPos, Vector2 startScreenPos, Vector2 globalCentroid, Camera cam)
    {
        foreach (var m in loners)
        {
            Vector2 centerDir = globalCentroid - startScreenPos;
            Vector2 myDir = m.screenPos - startScreenPos;
            float cross = (centerDir.x * myDir.y) - (centerDir.y * myDir.x);
            Vector3 dir = (m.worldPos - startPos).normalized;
            Vector3 visualRight = Vector3.ProjectOnPlane(cam.transform.right, dir).normalized;
            Vector3 visualUp = Vector3.ProjectOnPlane(cam.transform.up, dir).normalized;
            Vector3 left = -visualRight; Vector3 right = visualRight;
            Vector3 upTwist = (visualUp * 0.8f + visualRight * 0.2f).normalized;
            Vector3 bendDir;
            float strength = CurveRatioStrong;
            if (Mathf.Abs(cross) < 50f) { bendDir = upTwist; strength = CurveRatioWeak; }
            else if (cross > 0) bendDir = left;
            else bendDir = right;
            Vector3 mid = (startPos + m.worldPos) * 0.5f;
            m.assignedControlPoint = mid + (bendDir * m.distance3D * strength);
        }
    }
}