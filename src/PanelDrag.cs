using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BagOHeim
{
    /// <summary>
    /// Lets a panel be dragged by any empty part of it (the frame or the background; the buttons and text boxes still work as
    /// buttons and text boxes). Where it was left is reported so it can be remembered, and a double-click puts it back.
    /// </summary>
    internal class PanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public RectTransform Target;
        /// <summary>Called with the amount the panel was moved by (in its parent's units), as it moves.</summary>
        public Action<Vector2> Moved;
        /// <summary>Called when a drag ends, so the position can be saved.</summary>
        public Action Dropped;
        /// <summary>Called on a double-click, to reset the position.</summary>
        public Action Reset;

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null || Target.parent == null) return;
            // The pointer moves in screen pixels; the panel's position is in its parent's units.
            float scale = Mathf.Max(0.0001f, Target.parent.lossyScale.x);
            Moved?.Invoke(eventData.delta / scale);
        }

        public void OnEndDrag(PointerEventData eventData) => Dropped?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount >= 2) Reset?.Invoke();
        }
    }
}
