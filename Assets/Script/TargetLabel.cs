using UnityEngine;
using TMPro;

public class TargetLabel : MonoBehaviour
{
    public TextMeshProUGUI numberText;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    // 외부에서 숫자를 변경할 때 호출
    public void SetNumber(int number)
    {
        if (numberText != null)
        {
            numberText.text = number.ToString();
        }
    }

    // 항상 카메라를 바라보게 함 (Billboard)
    void LateUpdate()
    {
        if (mainCamera != null && numberText != null)
        {
            // 캔버스가 아니라 텍스트가 달린 오브젝트 전체를 회전시킴
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}