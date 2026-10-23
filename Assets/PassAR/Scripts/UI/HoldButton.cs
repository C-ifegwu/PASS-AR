using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PassAR
{
    /// <summary>A button that reports when it is pressed and when it is let go (used for Squeeze).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event Action Pressed;
        public event Action Released;

        private bool isDown;

        public void OnPointerDown(PointerEventData eventData)
        {
            isDown = true;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void OnDisable()
        {
            Release();
        }

        private void Release()
        {
            if (!isDown) return;
            isDown = false;
            Released?.Invoke();
        }
    }
}
