using UnityEngine;

namespace PassAR
{
    /// <summary>Keeps its children inside the screen's safe area (notches, rounded corners, gesture bar).</summary>
    [ExecuteAlways]
    public class SafeArea : MonoBehaviour
    {
        private Rect applied;

        private void Update()
        {
            Rect safe = Screen.safeArea;
            if (safe == applied || Screen.width == 0) return;
            applied = safe;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
