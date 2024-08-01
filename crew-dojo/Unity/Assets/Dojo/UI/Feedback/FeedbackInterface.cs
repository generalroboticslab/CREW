using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dojo.UI.Feedback
{
    /// <summary>
    /// Base interface for user feedback UI
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class FeedbackInterface : MonoBehaviour
    {
        protected VisualElement _combinedFeedback;

        protected ContinuousFeedback _continuousFeedback;
        protected DiscreteFeedback _discreteFeedback;
        protected TakeControl _takeControl;
        protected WrittenFeedback _writtenFeedback;

        /** The list of supported feedback elements to display */
        [SerializeField]
        protected List<Elements> _elements = new List<Elements> { Elements.DISCRETE };

        /** Invoked when continuous feedback is given */
        public event Action<float> OnNewContinuousFeedback;

        /** Invoked when positive button is pressed */
        public event Action OnPositiveButton;

        /** Invoked when neutral button is pressed */
        //public event Action OnNeutralButton;

        /** Invoked when negative button is pressed */
        public event Action OnNegativeButton;

        /** Invoked when take control button is pressed */
        public event Action OnTakeControlButton;

        /** Invoked when written feedback is submitted */
        public event Action<string> OnSubmitWrittenFeedback;

        public bool IsWrittenFeedbackVisible => _writtenFeedback.style.display != DisplayStyle.None;

        public enum Elements
        {
            DISCRETE = 0,
            CONTINUOUS = 1,
            IMITATION_LEARNING = 2,
        }

        protected virtual void Awake()
        {
            _combinedFeedback = GetComponent<UIDocument>().rootVisualElement;
            _continuousFeedback = _combinedFeedback.Q<ContinuousFeedback>("ContinuousFeedback");
            _discreteFeedback = _combinedFeedback.Q<DiscreteFeedback>("DiscreteFeedback");
            _takeControl = _combinedFeedback.Q<TakeControl>("TakeControl");
            _writtenFeedback = _combinedFeedback.Q<WrittenFeedback>("CombinedFeedbackWrittenArea");
            
            _discreteFeedback.style.display = DisplayStyle.None;
            _continuousFeedback.style.display = DisplayStyle.None;
            _takeControl.style.display = DisplayStyle.None;
            _writtenFeedback.style.display = DisplayStyle.None;

            if (_elements.Contains(Elements.CONTINUOUS))
            {
                _continuousFeedback.OnNewContinuousFeedback += (float feedback) => OnNewContinuousFeedback?.Invoke(feedback);
                InvokeRepeating(nameof(UpdateContinuousFeedbackVisualization), 0, _continuousFeedback.visualizationUpdateInterval);
                _continuousFeedback.style.display = DisplayStyle.Flex;
            }
            
            if (_elements.Contains(Elements.DISCRETE))
            {
                _discreteFeedback.OnPositiveButton += () => OnPositiveButton?.Invoke();
                //_discreteFeedback.OnNeutralButton += () => OnNeutralButton?.Invoke();
                _discreteFeedback.OnNegativeButton += () => OnNegativeButton?.Invoke();
                _discreteFeedback.style.display = DisplayStyle.Flex;
            }

            if (_elements.Contains(Elements.IMITATION_LEARNING))
            {
                _takeControl.OnTakeControlButton += () => OnTakeControlButton?.Invoke();
                _takeControl.style.display = DisplayStyle.Flex;
            }

            _writtenFeedback.OnSubmitWrittenFeedback += _OnSubmitWrittenFeedback;
        }

        private void FixedUpdate()
        {
            if (_elements.Contains(Elements.CONTINUOUS))
            {
                _continuousFeedback.UpdateFeedback();
            }
        }

        private void _OnSubmitWrittenFeedback(string writtenFeedback)
        {
            HideWrittenFeedback();
            OnSubmitWrittenFeedback?.Invoke(writtenFeedback);
        }

        /** Shows the written feedback interface */
        public void ShowWrittenFeedback()
        {
            _writtenFeedback.ResetText();
            _writtenFeedback.style.display = DisplayStyle.Flex;
        }

        /** Hides the written feedback interface */
        public void HideWrittenFeedback()
        {
            _writtenFeedback.style.display = DisplayStyle.None;
        }

        private void UpdateContinuousFeedbackVisualization()
        {
            _continuousFeedback.UpdateVisualization();
        }

        /** Is current UI visible? */
        public bool Visible
        {
            get
            {
                var combinedFeedbackArea = _combinedFeedback.Q<VisualElement>("CombinedFeedbackArea");
                return combinedFeedbackArea.style.display != DisplayStyle.None;
            }
            set
            {
                var combinedFeedbackArea = _combinedFeedback.Q<VisualElement>("CombinedFeedbackArea");
                combinedFeedbackArea.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
