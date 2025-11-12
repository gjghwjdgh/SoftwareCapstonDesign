// 파일 이름: ISceneStateHandler.cs

/// <summary>
/// 각 씬의 상태(분석, 유휴) 전환을 담당하는 관리자들이 구현해야 할 인터페이스.
/// GameUIManager는 이 인터페이스를 통해 현재 씬의 관리자와 소통한다.
/// </summary>
public interface ISceneStateHandler
{
    /// <summary>
    /// GameUIManager가 분석을 시작할 때 호출합니다.
    /// 이 상태에서는 버튼을 숨기는 등의 UI 처리를 합니다.
    /// </summary>
    void EnterAnalysisState();

    /// <summary>
    /// GameUIManager가 분석을 마치고 유휴 상태로 돌아갈 때 호출합니다.
    /// 이 상태에서는 버튼을 다시 표시하는 등의 UI 처리를 합니다.
    /// </summary>
    void EnterIdleState();
}