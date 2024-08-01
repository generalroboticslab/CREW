using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


namespace Dojo.UI.Feedback
{
    public class TakeControl : VisualElement
    {
        public Color focusedBorderColor { get; set; }

        /** Invoked when take control button is pressed */
        public event Action OnTakeControlButton;
        
        private VisualElement _areaElement;
        private Button _btnOnControl;
        private Mode _mode = Mode.TakeControl;

        public new class UxmlFactory : UxmlFactory<TakeControl, UxmlTraits> {}

        public enum Mode {
            TakeControl = 0,
            ReleaseControl = 1,
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlColorAttributeDescription m_focused_border_color = new UxmlColorAttributeDescription(){name = "focused-border-color", defaultValue = new Color32(0x75, 0xFB, 0xCA, 0xFF)};

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var ate = ve as TakeControl;
                ate.focusedBorderColor = m_focused_border_color.GetValueFromBag(bag, cc);

                ate.Clear();
                VisualTreeAsset vt = Resources.Load<VisualTreeAsset>("TakeControl");
                VisualElement takeControl = vt.Instantiate();
                
                ate._areaElement = takeControl.Q<VisualElement>("TakeControlArea");
                ate._btnOnControl = takeControl.Q<Button>("TakeControl");
                ate._btnOnControl.clicked += () => ate.OnTakeControlButton?.Invoke();
                
                ate._areaElement.RegisterCallback<PointerEnterEvent>(ate.OnPointerEnterEvent);
                ate._areaElement.RegisterCallback<PointerLeaveEvent>(ate.OnPointerLeaveEvent);

                ate.Add(takeControl);
            }
        }

        /** Sets the mode of the take control button */
        public void SetMode(Mode mode)
        {
            _mode = mode;
            _btnOnControl.text = _mode == Mode.TakeControl ? "Take Control" : "Release Control";
        }

        private void OnPointerEnterEvent(PointerEnterEvent evt)
        {
            SetFocusedBorder(true);
        }

        private void OnPointerLeaveEvent(PointerLeaveEvent evt)
        {
            SetFocusedBorder(false);
        }

        private void SetFocusedBorder(bool focused)
        {
            StyleColor borderColor = focused ? new StyleColor(focusedBorderColor) : Color.clear;
            _areaElement.style.borderTopColor = borderColor;
            _areaElement.style.borderLeftColor = borderColor;
            _areaElement.style.borderRightColor = borderColor;
            _areaElement.style.borderBottomColor = borderColor;
        }
    }
}
