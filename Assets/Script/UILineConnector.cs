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

    public Transform StartPoint3D => startPoint3D;
    public Transform Target3D => target3D;

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
        UpdateLinePosition();
    }

    void UpdateLinePosition()
    {
        // 3D 공간의 StartPoint와 Target의 화면 좌표를 계산합니다.
        Vector3 startScreenPoint3D = mainCamera.WorldToScreenPoint(startPoint3D.position);
        Vector2 targetScreenPoint = mainCamera.WorldToScreenPoint(target3D.position);

        // ✨ --- 핵심 수정 --- ✨
        // StartPoint가 카메라 뒤(z <= 0)에 있는지 확인합니다.
        if (startScreenPoint3D.z <= 0)
        {
            // 뒤에 있다면, 선을 그냥 숨겨서 버그를 방지합니다.
            lineImage.enabled = false;
            return; // 여기서 함수를 종료합니다.
        }

        // StartPoint가 카메라 앞에 있다면, 선이 보이도록 합니다.
        lineImage.enabled = true;

        // 이제 선의 시작점은 무조건 StartPoint의 실제 화면 좌표가 됩니다.
        Vector2 finalStartScreenPoint = startScreenPoint3D;

        // 두 점을 잇는 선을 그립니다.
        Vector2 difference = targetScreenPoint - finalStartScreenPoint;
        rectTransform.sizeDelta = new Vector2(difference.magnitude, 5f);
        rectTransform.position = finalStartScreenPoint + (difference / 2);
        rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
    }

    public void SetHighlight(bool highlighted)
    {
        if (lineImage != null) lineImage.color = highlighted ? highlightColor : normalColor;
    }
}