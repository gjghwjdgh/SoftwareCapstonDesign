using UnityEngine;

public class GazeInteractor : MonoBehaviour
{
    public PathVisualizer pathVisualizer;
    private Transform currentlyGazingAt;

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f) && hit.transform.CompareTag("StartButton"))
        {
            if (currentlyGazingAt != hit.transform)
            {
                if(currentlyGazingAt != null) currentlyGazingAt.GetComponent<Highlightable>()?.ToggleHighlight(false);

                currentlyGazingAt = hit.transform;
                currentlyGazingAt.GetComponent<Highlightable>()?.ToggleHighlight(true);
            }
        }
        else
        {
            if(currentlyGazingAt != null)
            {
                currentlyGazingAt.GetComponent<Highlightable>()?.ToggleHighlight(false);
                currentlyGazingAt = null;
            }
        }

        if (currentlyGazingAt != null && Input.GetKeyDown(KeyCode.Return)) // Enter 키
        {
            Debug.Log("선을 생성합니다!");
            pathVisualizer.ShowAllPaths();
        }
    }
}