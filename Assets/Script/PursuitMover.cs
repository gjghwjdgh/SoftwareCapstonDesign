using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PursuitMover : MonoBehaviour
{
    [Header("오브젝트 설정")]
    public GameObject helperPrefab;
    
    private GameObject currentHelperInstance;
    private Coroutine movementCoroutine;

    // System.Action onComplete 콜백 제거
    public void StartMovement(List<Vector3> path, float duration)
    {
        if (movementCoroutine != null) StopCoroutine(movementCoroutine);
        if (currentHelperInstance != null) Destroy(currentHelperInstance);

        currentHelperInstance = Instantiate(helperPrefab, path[0], Quaternion.identity);
        movementCoroutine = StartCoroutine(MoveAlongPath(path, duration));
    }

    private IEnumerator MoveAlongPath(List<Vector3> path, float duration)
    {
        float elapsedTime = 0f;
        Vector3 startPos = path[0];
        Vector3 endPos = path[path.Count - 1];
        
        while (elapsedTime < duration)
        {
            float progress = elapsedTime / duration;
            currentHelperInstance.transform.position = Vector3.Lerp(startPos, endPos, progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        currentHelperInstance.transform.position = endPos;
        yield return new WaitForSeconds(1.0f);
        Destroy(currentHelperInstance);
        
        // onComplete 호출 제거
    }
}