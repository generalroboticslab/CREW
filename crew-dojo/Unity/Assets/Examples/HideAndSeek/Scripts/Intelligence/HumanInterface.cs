using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using Nakama.TinyJson;
using Dojo;
using Dojo.UI;
using Dojo.UI.Feedback;
using Dojo.Recording;
using System.Linq;

namespace Examples.HideAndSeek
{
    public class HumanInterface : FeedbackInterface
    {
        private const string LOGSCOPE = "HumanInterface";

        [SerializeField]
        private DojoMenu _menu;

        [SerializeField]
        private InputActionAsset _feedbackActions;

        private DojoConnection _connection;
        private InputActionMap _feedbackControl;

        private DojoRecord _record;

        private InputAction _leftMouseClick;
        private InputAction _rightMouseClick;

        private PlayerController _controller;
        private Camera _mainCamera;

        protected override void Awake()
        {
            base.Awake();
            _connection = FindObjectOfType<DojoConnection>();
            _record = FindObjectOfType<DojoRecord>();

            // register callbacks
            _connection.OnJoinedMatch += ToggleUI;
            _connection.OnLeftMatch += ToggleUI;
            _connection.OnRoleChanged += m => ToggleUI();

            OnPositiveButton += OnButtonPositive;
            // OnNeutralButton += OnButtonNeutral;
            OnNegativeButton += OnButtonNegative;

            _feedbackControl = _feedbackActions.actionMaps[0];
            if (_elements.Contains(Elements.DISCRETE))
            {
                _feedbackControl.Enable();
            }

            _leftMouseClick = new(binding: "<Mouse>/leftButton");
            _leftMouseClick.performed += OnLeftMouseClick;
            _leftMouseClick.Enable();

            _rightMouseClick = new(binding: "<Mouse>/rightButton");
            _rightMouseClick.performed += OnRightMouseClick;
            _rightMouseClick.Enable();

            _mainCamera = Camera.main;

            Visible = false;
        }

        private void Update()
        {
            if (Visible)
            {
                if (_feedbackControl["Positive"].WasPressedThisFrame())
                {
                    OnButtonPositive();
                }
                // if (_feedbackControl["Neutral"].WasPressedThisFrame())
                // {
                //     OnButtonNeutral();
                // }
                if (_feedbackControl["Negative"].WasPressedThisFrame())
                {
                    OnButtonNegative();
                }
            }
        }

        private void ToggleUI()
        {
            Visible = _connection.HasJoinedMatch && _connection.Role == DojoNetworkRole.Viewer;
        }

        #region Button Callbacks

        private void OnButtonPositive()
        {
            SendFeedback(1);
        }

        private void OnButtonNegative()
        {
            SendFeedback(-1);
        }

        // private void OnButtonNeutral()
        // {
        //     SendFeedback(0);
        // }

        private void SendFeedback(float val)
        {
            var targets = _menu.SelectedFeedbackAIPlayers;
            var targetAgentIDs = targets.ConvertAll(target => int.Parse(target.Split(char.Parse("-")).ToList().Last()));

            var eventData = new List<object>() { val, targetAgentIDs };

            if (targets.Count > 0)
            {
                _connection.SendStateMessage((long)NetOpCode.Feedback, JsonWriter.ToJson(eventData));
            }
            else
            {
                Debug.LogWarning($"{LOGSCOPE}: Feedback provided but no feedback target client selected");
            }
        }

        #endregion Button Callbacks

        #region Mouse Click Callbacks

        private void OnLeftMouseClick(InputAction.CallbackContext ctx)
        {
            if (_connection.IsViewer && _controller == null)
            {
                var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out var hit))
                {
                    var controller = hit.transform.GetComponentInChildren<PlayerController>();
                    if (controller != null)
                    {
                        _controller = controller;
                        _mainCamera.enabled = false;
                        _controller.CamEye.enabled = true;
                    }
                }
            }
        }

        private void OnRightMouseClick(InputAction.CallbackContext ctx)
        {
            if (_connection.IsViewer && _controller != null)
            {
                _controller.CamEye.enabled = false;
                _mainCamera.enabled = true;
                _controller = null;
            }
        }

        #endregion Mouse Click Callbacks
    }
}
