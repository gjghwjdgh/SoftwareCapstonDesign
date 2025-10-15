using UnityEngine;

public class Highlightable : MonoBehaviour
{
    public Color highlightColor = Color.yellow;
    private Color originalColor;
    private Renderer objectRenderer;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        originalColor = objectRenderer.material.color;
    }

    public void ToggleHighlight(bool state)
    {
        if(objectRenderer != null)
        {
            objectRenderer.material.color = state ? highlightColor : originalColor;
        }
    }
}