using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dojo.UI.Feedback
{
    public class DiscreteFeedback : VisualElement
    {
        public Color focusedBorderColor { get; set; }

        private VisualElement _areaElement;
        private Button _btnOnPositive;
        //private Button _btnOnNeutral;
        private Button _btnOnNegative;

        /** Invoked when positive button is pressed */
        public event Action OnPositiveButton;

        /** Invoked when neutral button is pressed */
        //public event Action OnNeutralButton;

        /** Invoked when negative button is pressed */
        public event Action OnNegativeButton;
        
        public new class UxmlFactory : UxmlFactory<DiscreteFeedback, UxmlTraits> {}

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
                var ate = ve as DiscreteFeedback;
                ate.focusedBorderColor = m_focused_border_color.GetValueFromBag(bag, cc);

                ate.Clear();
                VisualTreeAsset vt = Resources.Load<VisualTreeAsset>("DiscreteFeedback");
                VisualElement discreteFeedback = vt.Instantiate();

                ate._areaElement = discreteFeedback.Q<VisualElement>("DiscreteFeedbackArea");

                ate._btnOnPositive = discreteFeedback.Q<Button>("Positive");
                ate._btnOnPositive.clicked += () => ate.OnPositiveButton?.Invoke();

                //ate._btnOnNeutral = discreteFeedback.Q<Button>("Neutral");
                //ate._btnOnNeutral.clicked += () => ate.OnNeutralButton?.Invoke();

                ate._btnOnNegative = discreteFeedback.Q<Button>("Negative");
                ate._btnOnNegative.clicked += () => ate.OnNegativeButton?.Invoke();

                ate._areaElement.RegisterCallback<PointerEnterEvent>(ate.OnPointerEnterEvent);
                ate._areaElement.RegisterCallback<PointerLeaveEvent>(ate.OnPointerLeaveEvent);

                ate.Add(discreteFeedback);
            }
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
