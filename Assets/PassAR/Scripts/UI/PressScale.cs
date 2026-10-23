using UnityEngine;
using UnityEngine.EventSystems;

namespace PassAR
{
    /// <summary>Small press feedback so buttons feel responsive on a touch screen.</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.96f;
        private bool pressed;

        public void OnPointerDown(PointerEventData eventData) { pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }
        public void OnPointerExit(PointerEventData eventData) { pressed = false; }

        private void OnDisable()
        {
            pressed = false;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float target = pressed ? PressedScale : 1f;
            float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 3f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
