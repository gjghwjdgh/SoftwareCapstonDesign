using UnityEngine;
using System.Collections.Generic;

public class PursuitMover : MonoBehaviour
{
    public GameObject helperPrefab;
    private List<ObjectMover3D> activeMovers = new List<ObjectMover3D>();

    public void StartMovementWithPhase(List<Vector3> path, float duration, float startPhase, Color? color, System.Action<List<Vector2>, List<float>> onComplete)
    {
        if (path == null || path.Count == 0) return;

        // 생성: 위치는 일단 path[0]이지만, 아래에서 즉시 옮길 것임.
        GameObject helperInstance = Instantiate(helperPrefab, path[0], Quaternion.identity);

        if (color.HasValue)
        {
            var renderer = helperInstance.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color.Value;
        }

        ObjectMover3D mover = helperInstance.GetComponent<ObjectMover3D>();
        if (mover != null)
        {
            activeMovers.Add(mover);

            // ★★★ 중요: 코루틴 시작 전에 강제로 위치를 잡는 함수를 먼저 호출 ★★★
            // 이렇게 하면 프레임이 그려지기 전에 위치가 수정됩니다.
            mover.ForceSetPosition(path, startPhase);

            StartCoroutine(mover.MoveAlongPathWithPhase(path, duration, startPhase, onComplete));
        }
        else
        {
            Destroy(helperInstance);
        }
    }

    public void StopAllMovements()
    {
        foreach (var mover in activeMovers) if (mover != null) mover.StopMovement();
        activeMovers.Clear();
    }
}