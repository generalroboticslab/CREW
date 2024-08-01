using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dojo.UI.Feedback
{
    public class _ContinuousFeedbackVisualization: VisualElement
    {
        public List<Vector2> _feedbackTimes = new List<Vector2>();
        public int lineWidth;

        public new class UxmlFactory : UxmlFactory<_ContinuousFeedbackVisualization, UxmlTraits> {}

        public _ContinuousFeedbackVisualization()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private float TransformFeedback(float feedback, Rect contentRect)
        {
            // Convert feedback from [-1, 1] to [0, 1]
            float normalizedFeedback = (float)(feedback * 0.5 + 0.5);
            float transformedFeedback = (1 - normalizedFeedback) * contentRect.height;
            return transformedFeedback;
        }

        private float ConvertTimeAgoToXPos(float timeAgo, Rect contentRect)
        {
            return contentRect.width - ((timeAgo + 1) * lineWidth) + 0.5f;
        }

        private List<List<Vector2>> ConstructFeedbackTimeSegmentGroups()
        {
            // This is used to store feedback in groups of connected segments.
            // Connected segments have lines connecting the feedback and are in the
            // same nested list.
            // Disconnected segments do not have lines connecting them and are stored
            // in separate nested list.
            var feedbackTimeSegmentGroups = new List<List<Vector2>>();

            List<Vector2> currentPointSegment = new List<Vector2>();
            for (int i=0; i<_feedbackTimes.Count; i++)
            {
                var timeAgo = _feedbackTimes[i].x;
                var feedback = _feedbackTimes[i].y;

                // Check if the feedback is connected
                // (the user had mouse over the element continuously).
                // If not connected (time is too big), then separate them.
                if (currentPointSegment.Count != 0 && 
                    timeAgo - currentPointSegment.Last().x > 1.1f)
                {
                    feedbackTimeSegmentGroups.Add(currentPointSegment);
                    currentPointSegment = new List<Vector2>();
                }

                currentPointSegment.Add(new Vector2(timeAgo, feedback));
            }
            if (currentPointSegment.Count > 0)
            {
                feedbackTimeSegmentGroups.Add(currentPointSegment);
            }

            return feedbackTimeSegmentGroups;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect r = contentRect;

            var feedbackTimeSegmentGroups = ConstructFeedbackTimeSegmentGroups();
            var numLineSegments = feedbackTimeSegmentGroups.Aggregate(0, (acc, x) => acc + x.Count() - 1);

            MeshWriteData mesh = ctx.Allocate(4 * numLineSegments, 6 * numLineSegments);
            foreach (var feedbackSegment in feedbackTimeSegmentGroups)
            {
                for (int i=1; i<feedbackSegment.Count; i++)
                {
                    var prevX = ConvertTimeAgoToXPos(feedbackSegment[i-1].x, contentRect);
                    var prevY = TransformFeedback(feedbackSegment[i-1].y, contentRect);

                    var x = ConvertTimeAgoToXPos(feedbackSegment[i].x, contentRect);
                    var y = TransformFeedback(feedbackSegment[i].y, contentRect);

                    var startPos = new Vector3(prevX, prevY, Vertex.nearZ);
                    var endPos = new Vector3(x, y, Vertex.nearZ);

                    Vertex[] vertices = new Vertex[4];
                    Vector3 lineWidthSpacing = new Vector3(0, lineWidth / 2, 0);
                    vertices[0].position = startPos - lineWidthSpacing;
                    vertices[1].position = startPos + lineWidthSpacing;
                    vertices[2].position = endPos + lineWidthSpacing;
                    vertices[3].position = endPos - lineWidthSpacing;

                    for (var index = 0; index < vertices.Length; index++)
                    {
                        vertices[index].tint = Color.black;
                        mesh.SetNextVertex(vertices[index]);
                    }
                    int baseIdx = 4 * (i - 1);
                    mesh.SetNextIndex((ushort)(baseIdx + 0));
                    mesh.SetNextIndex((ushort)(baseIdx + 1));
                    mesh.SetNextIndex((ushort)(baseIdx + 2));
                    mesh.SetNextIndex((ushort)(baseIdx + 2));
                    mesh.SetNextIndex((ushort)(baseIdx + 3));
                    mesh.SetNextIndex((ushort)(baseIdx + 0));
                }
            }
        }
    }

    public class ContinuousFeedback: VisualElement
    {
        public Color focusedBorderColor { get; set; }
        public int lineWidth { get; set; }
        public float visualizationUpdateInterval { get; set; }

        /** Invoked when continuous feedback is given */
        public event Action<float> OnNewContinuousFeedback;

        private List<Vector2> _feedbackTimes = new List<Vector2>();

        private VisualElement _areaElement;
        private _ContinuousFeedbackVisualization _visualization;

        private float _currentFeedback = 0;
        private bool _pointerInElement = false;
        
        public new class UxmlFactory : UxmlFactory<ContinuousFeedback, UxmlTraits> {}

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlColorAttributeDescription m_focused_border_color = new UxmlColorAttributeDescription(){name = "focused-border-color", defaultValue = new Color32(0x75, 0xFB, 0xCA, 0xFF)};
            UxmlIntAttributeDescription m_line_width = new UxmlIntAttributeDescription(){name = "line-width", defaultValue = 5 };
            UxmlFloatAttributeDescription m_visualization_update_interval = new UxmlFloatAttributeDescription(){name = "visualization-update-interval", defaultValue = 0.2f };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var ate = ve as ContinuousFeedback;
                ate.focusedBorderColor = m_focused_border_color.GetValueFromBag(bag, cc);
                ate.lineWidth = m_line_width.GetValueFromBag(bag, cc);
                ate.visualizationUpdateInterval = m_visualization_update_interval.GetValueFromBag(bag, cc);

                ate.Clear();
                VisualTreeAsset vt = Resources.Load<VisualTreeAsset>("ContinuousFeedback");
                VisualElement continuousFeedback = vt.Instantiate();

                ate._areaElement = continuousFeedback.Q<VisualElement>("ContinuousFeedbackArea");

                ate._visualization = continuousFeedback.Q<_ContinuousFeedbackVisualization>("Visualization");
                ate._visualization.lineWidth = ate.lineWidth;

                ate._visualization.RegisterCallback<PointerMoveEvent>(ate.OnPointerMoveEvent);
                ate._visualization.RegisterCallback<PointerEnterEvent>(ate.OnPointerEnterEvent);
                ate._visualization.RegisterCallback<PointerLeaveEvent>(ate.OnPointerLeaveEvent);

                ate.Add(continuousFeedback);
                ate.SetFocusedBorder(false);
            }
        }

        private void OnPointerMoveEvent(PointerMoveEvent evt)
        {
            float normalizedY = 1 - (evt.localPosition.y / _visualization.layout.size.y);
            // Convert feedback from [0, 1] to [-1, 1]
            float feedback = (normalizedY * 2) - 1;
            _currentFeedback = feedback;
        }

        private void OnPointerEnterEvent(PointerEnterEvent evt)
        {
            _pointerInElement = true;
            SetFocusedBorder(true);
        }

        private void OnPointerLeaveEvent(PointerLeaveEvent evt)
        {
            _pointerInElement = false;
            SetFocusedBorder(false);
        }

        private void SetFocusedBorder(bool focused)
        {
            StyleColor borderColor = focused ? new StyleColor(focusedBorderColor) : Color.clear;
            _visualization.style.borderTopColor = borderColor;
            _visualization.style.borderLeftColor = borderColor;
            _visualization.style.borderRightColor = borderColor;
            _visualization.style.borderBottomColor = borderColor;
        }

        private void AddFeedback(float feedback)
        {
            _feedbackTimes.Insert(0, new Vector3(0, feedback));
            int maxFeedbackPlot = (int)_visualization.contentRect.width;
            if (_feedbackTimes.Count > maxFeedbackPlot)
            {
                _feedbackTimes.RemoveAt(maxFeedbackPlot);
            }
        }

        /** Updates the visualization, but does NOT invoke the feedback event */
        public void UpdateVisualization()
        {
            for (int i = 0; i < _feedbackTimes.Count; i++)
            {
                int timeAgo = (int)_feedbackTimes[i].x;
                float feedback = _feedbackTimes[i].y;
                _feedbackTimes[i] += new Vector2(1, 0);
            }

            if (_pointerInElement)
            {
                AddFeedback(_currentFeedback);
            }

            _visualization._feedbackTimes = new List<Vector2>(_feedbackTimes);
            _visualization.MarkDirtyRepaint();
        }

        /** Invokes the feedback event with the current feedback */
        public void UpdateFeedback()
        {
            // Use -9 as a code for no feedback provided.
            var feedback = _pointerInElement ? _currentFeedback : -9;
            OnNewContinuousFeedback?.Invoke(feedback);
        }
    }
}
