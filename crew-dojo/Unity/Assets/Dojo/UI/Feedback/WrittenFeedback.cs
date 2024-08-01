using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dojo.UI.Feedback
{
    public class WrittenFeedback : VisualElement
    {
        /** Invoked when written feedback is given */
        public event Action<String> OnSubmitWrittenFeedback;
        
        private TextField _textFieldWrittenFeedback;
        private Button _btnOnSubmit;

        public new class UxmlFactory : UxmlFactory<WrittenFeedback, UxmlTraits> {}

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var ate = ve as WrittenFeedback;

                ate.Clear();
                VisualTreeAsset vt = Resources.Load<VisualTreeAsset>("WrittenFeedback");
                VisualElement writtenFeedback = vt.Instantiate();
                writtenFeedback.StretchToParentSize();

                ate._textFieldWrittenFeedback = writtenFeedback.Q<TextField>("WrittenFeedbackInput");

                ate._btnOnSubmit = writtenFeedback.Q<Button>("Submit");
                ate._btnOnSubmit.clicked += () => ate.OnSubmitWrittenFeedback?.Invoke(ate._textFieldWrittenFeedback.value.Replace("\\n", "\n"));

                ate.Add(writtenFeedback);
            }
        }

        public void ResetText()
        {
            _textFieldWrittenFeedback.value = "Overall Feedback:\n\n\nImprovement:\n\n\nWhat went well?:\n\n\nRating (1-10):\n";
        }
    }
}
