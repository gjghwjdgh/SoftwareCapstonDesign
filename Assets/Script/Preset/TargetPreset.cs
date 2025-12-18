using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewPreset", menuName = "AR/Target Preset")]
public class TargetPreset : ScriptableObject
{
    public string description; // 예: "고밀도 테스트", "수직 배치"
    // 시작점(0,0,0) 기준 상대 좌표들
    public List<Vector3> relativePositions = new List<Vector3>();
}