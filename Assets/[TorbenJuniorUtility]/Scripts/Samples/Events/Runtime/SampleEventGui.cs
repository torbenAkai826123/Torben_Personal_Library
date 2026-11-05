using UnityEngine;

namespace Torben.Events.Samples
{
    // 範例畫面的 IMGUI 縮放: 以 720p 為基準放大, 高解析度螢幕上也看得清楚
    internal static class SampleEventGui
    {
        internal static void ApplyScale()
        {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        }
    }
}
