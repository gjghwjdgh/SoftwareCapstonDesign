using UnityEngine;
using System.Collections.Generic;

public class PursuitMover : MonoBehaviour
{
    public GameObject helperPrefab;
    private List<ObjectMover3D> activeMovers = new List<ObjectMover3D>();

    public void StartMovement(List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        GameObject helperInstance = Instantiate(helperPrefab, path[0], Quaternion.identity);
        ObjectMover3D mover = helperInstance.GetComponent<ObjectMover3D>();
        if (mover != null)
        {
            // 생성된 mover를 리스트에 추가하여 기억합니다.
            activeMovers.Add(mover);
            StartCoroutine(mover.MoveAlongPath(path, duration, onComplete));
        }
        else
        {
            Debug.LogError("Helper Prefab에 ObjectMover3D 스크립트가 없습니다!");
            onComplete?.Invoke(null, null);
            Destroy(helperInstance);
        }
    }

    // GameUIManager가 호출할 "모두 정지!" 명령
    public void StopAllMovements()
    {
        foreach (var mover in activeMovers)
        {
            if (mover != null)
            {
                mover.StopMovement();
            }
        }
        // 리스트를 깨끗하게 비워 다음 분석을 준비합니다.
        activeMovers.Clear();
    }
}