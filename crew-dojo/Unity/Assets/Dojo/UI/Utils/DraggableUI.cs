using UnityEngine;
using UnityEngine.UIElements;

namespace Dojo.UI.Utils
{
    /// <summary>
    /// Helper component to enable Unity UI to be draggable
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DraggableUI : MonoBehaviour
    {
        [SerializeField]
        private string _nameToDrag = "Feedback";

        [SerializeField]
        private string _nameOfArea = "FeedbackArea";

        [SerializeField]
        private bool _restrictInArea = true;

        private Vector2 _position = Vector2.zero;
        private VisualElement _dragArea;
        private VisualElement _toDrag;
        private bool _pressed = false;

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _dragArea = root.Q<VisualElement>(_nameOfArea);
            _toDrag = root.Q<VisualElement>(_nameToDrag);

            Debug.Assert(_toDrag != null, $"{_nameToDrag} not found in UI Document!");
            Debug.Assert(_dragArea != null, $"{_nameOfArea} not found in UI Document!");

            _toDrag.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _toDrag.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _dragArea.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _dragArea.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            _position = e.position;
            _pressed = true;
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!_pressed)
            {
                return;
            }

            Vector2 panelPos = e.position;
            var diff = panelPos - _position;
            var uiPos = _toDrag.transform.position;

            var rect = new Rect(_toDrag.contentRect);
            rect.position -= new Vector2(uiPos.x, uiPos.y) + new Vector2(diff.x, diff.y);

            if (!_restrictInArea || IsRectInArea(rect))
            {
                _toDrag.transform.position += new Vector3(diff.x, diff.y);
            }
            _position = panelPos;
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            _position = Vector2.zero;
            _pressed = false;
        }

        private bool IsRectInArea(Rect rect)
        {
            var areaRect = _dragArea.contentRect;
            return areaRect.Contains(rect.max) && areaRect.Contains(rect.min);
        }
    }
}
