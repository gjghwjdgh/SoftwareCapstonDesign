using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation; // ARPlane 제어용

public class PresetManager : MonoBehaviour
{
    [Header("프리셋 목록")]
    public List<TargetPreset> presets;

    [Header("필수 연결")]
    public PathVisualizer pathVisualizer;
    public GameObject startPointPrefab;
    public GameObject targetPrefab;

    [Header("생성 설정")]
    public float spawnDistance = 1.2f;

    private int currentIndex = -1;

    public void LoadNextPreset()
    {
        if (presets == null || presets.Count == 0) return;
        currentIndex = (currentIndex + 1) % presets.Count;
        ApplyPreset(presets[currentIndex]);
    }

    private void ApplyPreset(TargetPreset preset)
    {
        // 1. [소프트 리셋] 객체만 지우기 (씬 로드 X)
        ClearSceneManual();

        // 2. [AR 바닥 숨기기] 프리셋 모드에서는 바닥이 거슬리므로 끕니다.
        ToggleARPlanes(false);

        // 3. [생성] 시작점 만들기
        Transform camTr = Camera.main.transform;
        Vector3 startPos = camTr.position + (camTr.forward * spawnDistance);
        startPos.y = -0.5f;

        GameObject startObj = Instantiate(startPointPrefab, startPos, Quaternion.identity);
        if (GameUIManager.Instance != null) startObj.transform.SetParent(GameUIManager.Instance.targetParent);

        // 4. [생성] 타겟 만들기
        List<Transform> newTargets = new List<Transform>();
        int idCounter = 1;

        foreach (Vector3 relPos in preset.relativePositions)
        {
            Vector3 targetPos = startObj.transform.position + relPos;
            GameObject tObj = Instantiate(targetPrefab, targetPos, Quaternion.identity);

            if (GameUIManager.Instance != null)
                tObj.transform.SetParent(GameUIManager.Instance.targetParent);

            // 텍스트 부여
            var label = tObj.GetComponentInChildren<TargetLabel>();
            if (label != null) label.SetNumber(idCounter);
            else
            {
                var tmp = tObj.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp != null) tmp.text = idCounter.ToString();
            }
            tObj.name = $"Target_{idCounter}";
            idCounter++;

            newTargets.Add(tObj.transform);
        }

        // 5. [전권 이양] ARPlacementManager에게 데이터 넘김
        ARPlacementManager placementMgr = FindFirstObjectByType<ARPlacementManager>();
        if (placementMgr != null)
        {
            placementMgr.LoadExternalData(startObj.transform, newTargets);
            Debug.Log($"[Preset] '{preset.description}' 로드 완료.");
        }
    }

    // ★★★ 수동 청소 함수 (씬 로드 없이 객체만 삭제) ★★★
    private void ClearSceneManual()
    {
        // 1. 선 지우기
        var lines = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        foreach (var line in lines) line.positionCount = 0;

        // 2. 타겟들 삭제
        if (GameUIManager.Instance != null && GameUIManager.Instance.targetParent != null)
        {
            foreach (Transform child in GameUIManager.Instance.targetParent) Destroy(child.gameObject);
        }

        // 3. 기존 시작점 삭제 (PathVisualizer가 들고 있는 것)
        if (pathVisualizer.startPoint != null)
        {
            Destroy(pathVisualizer.startPoint.gameObject);
            pathVisualizer.startPoint = null;
        }

        // 4. 리스트 초기화
        pathVisualizer.targets.Clear();
    }

    // ★★★ AR Plane(바닥) 끄기/켜기 함수 ★★★
    private void ToggleARPlanes(bool isOn)
    {
        ARPlaneManager planeManager = FindFirstObjectByType<ARPlaneManager>();
        if (planeManager != null)
        {
            // 1. 매니저 자체를 꺼서 더 이상 인식을 안 하게 함
            planeManager.enabled = isOn;

            // 2. 이미 생성된 바닥 타일들을 싹 찾아서 끄거나 켬
            foreach (var plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(isOn);
            }
        }
    }
    // =========================================================
    // [기능 2] 개발자용: 현재 배치를 파일로 저장하기 (수정됨)
    // =========================================================
#if UNITY_EDITOR
    [ContextMenu("현재 배치를 프리셋으로 저장")]
    public void SaveCurrentLayout()
    {
        // 1. 안전장치: 필수 요소 확인
        if (pathVisualizer == null)
        {
            Debug.LogError("오류: PathVisualizer가 연결되지 않았습니다.");
            return;
        }

        if (pathVisualizer.startPoint == null)
        {
            Debug.LogError("오류: 저장 실패. Start Point(시작점)가 없습니다.");
            return;
        }

        if (pathVisualizer.targets == null)
        {
            // 리스트가 아예 없으면 새로 만듦
            pathVisualizer.targets = new List<Transform>();
        }

        TargetPreset newPreset = ScriptableObject.CreateInstance<TargetPreset>();
        newPreset.description = $"Preset_{System.DateTime.Now:mm_ss}";

        // 2. 리스트 순회하며 저장
        int saveCount = 0;
        foreach (var t in pathVisualizer.targets)
        {
            // ★★★ [수정] 타겟이 없거나 삭제되었으면 건너뜀 (에러 방지 핵심) ★★★
            if (t == null) continue;

            // 시작점 기준 상대 위치 저장
            Vector3 rel = t.position - pathVisualizer.startPoint.position;
            newPreset.relativePositions.Add(rel);
            saveCount++;
        }

        if (saveCount == 0)
        {
            Debug.LogWarning("저장할 유효한 타겟이 하나도 없습니다.");
            return;
        }

        // 3. 파일 생성
        // 폴더가 없으면 에러가 날 수 있으니 폴더 확인
        if (!System.IO.Directory.Exists(Application.dataPath + "/Resources/Presets"))
        {
            System.IO.Directory.CreateDirectory(Application.dataPath + "/Resources/Presets");
        }

        string fileName = $"Preset_{System.DateTime.Now:yyyyMMdd_HHmmss}.asset";
        string path = $"Assets/Resources/Presets/{fileName}";

        UnityEditor.AssetDatabase.CreateAsset(newPreset, path);
        UnityEditor.AssetDatabase.SaveAssets();

        // 리스트에 자동 추가
        presets.Add(newPreset);
        Debug.Log($"✅ 저장 완료! ({saveCount}개) 파일명: {fileName}");
    }
#endif
}