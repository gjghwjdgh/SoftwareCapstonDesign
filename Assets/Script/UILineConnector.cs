using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UILineConnector : MonoBehaviour
{
    [Header("UI 설정")]
    public Image helperPrefab; // 2D 퍼슈트 도우미로 사용할 UI Image 프리팹

    private Transform startPoint3D;
    private Transform target3D;
    private Camera mainCamera;
    private RectTransform rectTransform;
    private Image lineImage;

    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;

    private Coroutine pursuitCoroutine;

    public void Initialize(Transform start, Transform target, Camera camera)
    {
        this.startPoint3D = start;
        this.target3D = target;
        this.mainCamera = camera;
        this.rectTransform = GetComponent<RectTransform>();
        this.lineImage = GetComponent<Image>();
        SetHighlight(false);
    }

    void Update()
    {
        if (startPoint3D == null || target3D == null || mainCamera == null) return;

        Vector3 targetViewport = mainCamera.WorldToViewportPoint(target3D.position);
        bool isTargetVisible = targetViewport.z > 0 && targetViewport.x > 0 && targetViewport.x < 1 && targetViewport.y > 0 && targetViewport.y < 1;
        
        lineImage.enabled = isTargetVisible;

        if (isTargetVisible)
        {
            UpdateLinePosition();
        }
    }

    void UpdateLinePosition()
    {
        Vector3 startScreenPoint3D = mainCamera.WorldToScreenPoint(startPoint3D.position);
        Vector2 targetScreenPoint = mainCamera.WorldToScreenPoint(target3D.position);
        Vector2 finalStartScreenPoint;

        bool isStartOnScreen = startScreenPoint3D.z > 0 && startScreenPoint3D.x > 0 && startScreenPoint3D.x < Screen.width && startScreenPoint3D.y > 0 && startScreenPoint3D.y < Screen.height;

        if (isStartOnScreen)
        {
            finalStartScreenPoint = startScreenPoint3D;
        }
        else
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
            Vector2 startPointScreen = startScreenPoint3D;
            if (startScreenPoint3D.z < 0)
            {
                startPointScreen = screenCenter + (screenCenter - (Vector2)startScreenPoint3D);
            }
            Vector2 fromCenter = startPointScreen - screenCenter;
            float angleRad = Mathf.Atan2(fromCenter.y, fromCenter.x);
            float tanAngle = Mathf.Tan(angleRad);
            float screenAspect = (float)Screen.width / Screen.height;
            float x, y;
            if (Mathf.Abs(fromCenter.x) > Mathf.Abs(fromCenter.y) * screenAspect)
            { 
                x = (fromCenter.x > 0) ? Screen.width : 0;
                y = screenCenter.y + (x - screenCenter.x) * tanAngle;
            }
            else
            { 
                y = (fromCenter.y > 0) ? Screen.height : 0;
                if (Mathf.Approximately(tanAngle, 0)) // tan 0으로 나누는 것 방지
                {
                    x = screenCenter.x;
                }
                else
                {
                    x = screenCenter.x + (y - screenCenter.y) / tanAngle;
                }
            }
            finalStartScreenPoint = new Vector2(x, y);
        }

        Vector2 difference = targetScreenPoint - finalStartScreenPoint;
        rectTransform.sizeDelta = new Vector2(difference.magnitude, 5f);
        rectTransform.position = finalStartScreenPoint + (difference / 2);
        rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
    }

    public void StartPursuit(float duration)
    {
        if (pursuitCoroutine != null) StopCoroutine(pursuitCoroutine);
        pursuitCoroutine = StartCoroutine(AnimateHelper(duration));
    }

    private IEnumerator AnimateHelper(float duration)
    {
        Image helper = Instantiate(helperPrefab, transform.parent);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float progress = elapsedTime / duration;
            
            // 현재 라인의 양 끝점을 계산
            Vector2 startPos = rectTransform.position - (Vector3)rectTransform.right * (rectTransform.sizeDelta.x / 2f);
            Vector2 endPos = rectTransform.position + (Vector3)rectTransform.right * (rectTransform.sizeDelta.x / 2f);
            
            helper.rectTransform.position = Vector2.Lerp(startPos, endPos, progress);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        Destroy(helper.gameObject);
    }
    
    public void SetHighlight(bool highlighted)
    {
        lineImage.color = highlighted ? highlightColor : normalColor;
    }
}