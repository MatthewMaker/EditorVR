using Unity.EditorXR.Modules;
using Unity.EditorXR.Utilities;
using Unity.XRTools.Utils.GUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.EditorXR.UI
{
    sealed class RaySlider : Slider, ISelectionFlags
    {
        public SelectionFlags selectionFlags
        {
            get { return m_SelectionFlags; }
            set { m_SelectionFlags = value; }
        }

        [SerializeField]
        [FlagsProperty]
        private SelectionFlags m_SelectionFlags = SelectionFlags.Ray | SelectionFlags.Direct;

        public override void OnPointerEnter(PointerEventData eventData)
        {
            var rayEventData = eventData as RayEventData;
            if (rayEventData == null || UIUtils.IsValidEvent(rayEventData, selectionFlags))
                base.OnPointerEnter(eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            var rayEventData = eventData as RayEventData;
            if (rayEventData == null || UIUtils.IsValidEvent(rayEventData, selectionFlags))
                base.OnPointerExit(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            var rayEventData = eventData as RayEventData;
            if (rayEventData == null || UIUtils.IsValidEvent(rayEventData, selectionFlags))
                base.OnPointerDown(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            var rayEventData = eventData as RayEventData;
            if (rayEventData == null || UIUtils.IsValidEvent(rayEventData, selectionFlags))
                base.OnPointerUp(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            var rayEventData = eventData as RayEventData;
            if (rayEventData == null || UIUtils.IsValidEvent(rayEventData, selectionFlags))
                base.OnDrag(eventData);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            //Not selectable
        }
    }
}
