using UnityEngine;

public class GazeInteractor : MonoBehaviour
{
    public PathVisualizer pathVisualizer;
    private Highlightable currentlyGazingAt;
    private Camera mainCamera;

    void Start() { mainCamera = Camera.main; }

    void Update()
    {
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit hit;
        Highlightable gazedObject = null;
        if (Physics.Raycast(ray, out hit, 100f))
            gazedObject = hit.collider.GetComponent<Highlightable>();

        if (currentlyGazingAt != gazedObject)
        {
            currentlyGazingAt?.ToggleHighlight(false);
            currentlyGazingAt = gazedObject;
            currentlyGazingAt?.ToggleHighlight(true);
        }

        if (currentlyGazingAt != null && Input.GetKeyDown(KeyCode.Return))
            pathVisualizer.GenerateAndShowAllPaths();
    }
}