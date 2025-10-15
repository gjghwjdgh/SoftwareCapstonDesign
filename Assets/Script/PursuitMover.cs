using UnityEngine;
using System.Collections.Generic;

public class PursuitMover : MonoBehaviour
{
    public GameObject helperPrefab; // ObjectMover3D.cs가 붙어있는 프리팹

    // 도우미 객체를 생성하고, 이동 코루틴을 시작시킨 후 아무것도 하지 않음 (자율적으로 움직이게 됨)
    public void StartMovement(List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        GameObject helperInstance = Instantiate(helperPrefab, path[0], Quaternion.identity);
        ObjectMover3D mover = helperInstance.GetComponent<ObjectMover3D>();
        if (mover != null)
        {
            StartCoroutine(mover.MoveAlongPath(path, duration, onComplete));
        }
        else
        {
            Debug.LogError("Helper Prefab에 ObjectMover3D 스크립트가 없습니다!");
            Destroy(helperInstance);
        }
    }
}