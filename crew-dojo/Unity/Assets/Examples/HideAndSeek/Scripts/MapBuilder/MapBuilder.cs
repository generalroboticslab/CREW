using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Examples.HideAndSeek.MapBuilder
{
    [RequireComponent(typeof(UIDocument))]
    public class MapBuilder : MonoBehaviour
    {
        private TextField _widthInput;
        private TextField _heightInput;
        private TextField _typeInput;
        private Button _copyButton;

        private int width = 10;
        private int height = 10;
        private int type = 1;

        private Texture2D mapTex;

        private InputAction leftMouseClick;
        private InputAction midMouseClick;
        private InputAction rightMouseClick;

        [SerializeField]
        private Renderer _mapView;

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _widthInput = root.Q<TextField>("Width");
            _heightInput = root.Q<TextField>("Height");
            _typeInput = root.Q<TextField>("Type");
            _copyButton = root.Q<Button>("Copy");

            leftMouseClick = new InputAction(binding: "<Mouse>/leftButton");
            leftMouseClick.Enable();
            rightMouseClick = new InputAction(binding: "<Mouse>/rightButton");
            rightMouseClick.Enable();
            midMouseClick = new InputAction(binding: "<Mouse>/middleButton");
            midMouseClick.Enable();
        }

        private void Start()
        {
            _copyButton.clickable.clicked += CopyMapToClipboard;
            _widthInput.RegisterValueChangedCallback(_ => ResetMap());
            _heightInput.RegisterValueChangedCallback(_ => ResetMap());
            _typeInput.RegisterValueChangedCallback(_ => ResetMap());

            ResetMap();
        }

        private void Update()
        {
            if (leftMouseClick.WasPerformedThisFrame())
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 100))
                {
                    SetHitColor(hit, Color.green);
                }
            }
            if (rightMouseClick.WasPerformedThisFrame())
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 100))
                {
                    SetHitColor(hit, Color.white);
                }
            }
            if (midMouseClick.WasPerformedThisFrame())
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 100))
                {
                    SetHitColor(hit, Color.cyan);
                }
            }
        }

        private void SetHitColor(RaycastHit hit, Color color)
        {
            var point = hit.point;
            var posX = Mathf.Clamp(Mathf.FloorToInt((point.x + 5f) / 10f * width), 0, width - 1);
            var posY = Mathf.Clamp(Mathf.FloorToInt((-point.y + 5f) / 10f * height), 0, height - 1);
            mapTex.SetPixel(posX, posY, color);
            mapTex.Apply();
        }

        private void CopyMapToClipboard()
        {
            var pixels = mapTex.GetPixels32();

            GUIUtility.systemCopyBuffer = string.Join(",", pixels.Select((p, idx) =>
            {
                var prefix = idx % width == 0 ? "\n" : "";
                if (p.r > 0)
                {
                    return $"{prefix}0";
                }
                else if (p.b > 0)
                {
                    return $"{prefix}x";
                }
                else
                {
                    return $"{prefix}{type}";
                }
            }).ToArray())[1..] + ",";

            Debug.Log("Map copied to clipboard!");
        }

        private void ResetMap()
        {
            if (int.TryParse(_widthInput.text, out width) &&
                int.TryParse(_heightInput.text, out height) &&
                int.TryParse(_typeInput.text, out type))
            {
                width = Mathf.Min(Mathf.Max(width, 1), 100);
                height = Mathf.Min(Mathf.Max(height, 1), 100);
                type = Mathf.Min(Mathf.Max(type, 1), 100);

                if (mapTex != null)
                {
                    Destroy(mapTex);
                }
                mapTex = new(width, height, TextureFormat.ARGB32, false)
                {
                    filterMode = FilterMode.Point
                };
                mapTex.SetPixels32(Enumerable.Repeat(new Color32(255, 255, 255, 255), width * height).ToArray());
                mapTex.Apply();

                _mapView.material.mainTexture = mapTex;
            }
        }
    }
}
