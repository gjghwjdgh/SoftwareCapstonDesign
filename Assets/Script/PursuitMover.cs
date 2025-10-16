using UnityEngine;
using System.Collections.Generic;

public class PursuitMover : MonoBehaviour
{
    public GameObject helperPrefab;

    // ✨ 약속을 통일: 경로 데이터를 받는 콜백(Action)을 받도록 수정
    public void StartMovement(List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        GameObject helperInstance = Instantiate(helperPrefab, path[0], Quaternion.identity);
        ObjectMover3D mover = helperInstance.GetComponent<ObjectMover3D>();
        if (mover != null)
        {
            // 받은 콜백을 그대로 ObjectMover3D에게 전달
            StartCoroutine(mover.MoveAlongPath(path, duration, onComplete));
        }
        else
        {
            Debug.LogError("Helper Prefab에 ObjectMover3D 스크립트가 없습니다!");
            onComplete?.Invoke(null, null);
            Destroy(helperInstance);
        }
    }
}