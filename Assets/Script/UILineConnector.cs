using UnityEngine;
using UnityEngine.UI;

public class UILineConnector : MonoBehaviour
{
    private Transform startPoint3D, target3D;
    private Camera mainCamera;
    private RectTransform rectTransform;
    private Image lineImage;
    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;

    // --- ▼ 여기가 추가된 부분입니다 ▼ ---
    // 외부 스크립트가 startPoint3D와 target3D를 읽을 수 있도록 공개합니다.
    public Transform StartPoint3D => startPoint3D;
    public Transform Target3D => target3D;
    // --- ▲ 여기가 추가된 부분입니다 ▲ ---

    public void Initialize(Transform start, Transform target, Camera camera)
    {
        this.startPoint3D = start;
        this.target3D = target;
        this.mainCamera = camera;
        this.rectTransform = GetComponent<RectTransform>();
        this.lineImage = GetComponent<Image>();
        SetHighlight(false);
    }

    // ... (Update, UpdateLinePosition, SetHighlight 함수는 이전과 동일)
    void Update()
    {
        if (startPoint3D == null || target3D == null || mainCamera == null) return;
        UpdateLinePosition();
    }

    void UpdateLinePosition()
    {
        Vector3 startScreenPoint3D = mainCamera.WorldToScreenPoint(startPoint3D.position);
        Vector2 targetScreenPoint = mainCamera.WorldToScreenPoint(target3D.position);
        Vector2 finalStartScreenPoint;
        bool isStartOnScreen = startScreenPoint3D.z > 0 && startScreenPoint3D.x > 0 && startScreenPoint3D.x < Screen.width && startScreenPoint3D.y > 0 && startScreenPoint3D.y < Screen.height;
        if (isStartOnScreen){ finalStartScreenPoint = startScreenPoint3D; }
        else {
            Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
            Vector2 startPointScreen = startScreenPoint3D;
            if (startScreenPoint3D.z < 0) { startPointScreen = screenCenter + (screenCenter - (Vector2)startPointScreen); }
            Vector2 fromCenter = startPointScreen - screenCenter;
            float angleRad = Mathf.Atan2(fromCenter.y, fromCenter.x);
            float tanAngle = Mathf.Tan(angleRad);
            float screenAspect = (float)Screen.width / Screen.height;
            float x, y;
            if (Mathf.Abs(fromCenter.x) > Mathf.Abs(fromCenter.y) * screenAspect){ x = (fromCenter.x > 0) ? Screen.width : 0; y = screenCenter.y + (x - screenCenter.x) * tanAngle; }
            else { y = (fromCenter.y > 0) ? Screen.height : 0; if (Mathf.Approximately(tanAngle, 0)) { x = screenCenter.x; } else { x = screenCenter.x + (y - screenCenter.y) / tanAngle; } }
            finalStartScreenPoint = new Vector2(x, y);
        }
        Vector2 difference = targetScreenPoint - finalStartScreenPoint;
        rectTransform.sizeDelta = new Vector2(difference.magnitude, 5f);
        rectTransform.position = finalStartScreenPoint + (difference / 2);
        rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
    }
    
    public void SetHighlight(bool highlighted) { lineImage.color = highlighted ? highlightColor : normalColor; }
}