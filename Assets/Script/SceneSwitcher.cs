// SceneSwitcher.cs (전체 코드)
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    // 터치 기반 씬의 이름 또는 빌드 인덱스
    public string touchSceneName = "AR_Scene";

    // 마커 기반 씬의 이름 또는 빌드 인덱스
    public string markerSceneName = "AR_Marker_Scene";

    public void SwitchToTouchScene()
    {
        SceneManager.LoadScene(touchSceneName);
    }

    public void SwitchToMarkerScene()
    {
        SceneManager.LoadScene(markerSceneName);
    }

    // 현재 씬에 따라 다른 씬으로 전환하는 토글 함수
    public void SwapMode()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        if (currentSceneName == touchSceneName)
        {
            SwitchToMarkerScene();
        }
        else
        {
            SwitchToTouchScene();
        }
    }
}