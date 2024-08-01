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

namespace Examples.HideAndSeek_Single
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

        public bool _isControllingAgent = false;

        protected override void Awake()
        {
            base.Awake();
            _connection = FindObjectOfType<DojoConnection>();

            // register callbacks
            _connection.OnJoinedMatch += ToggleUI;
            _connection.OnLeftMatch += ToggleUI;
            _connection.OnRoleChanged += m => ToggleUI();

            OnNewContinuousFeedback += OnContinuousFeedback;

            OnPositiveButton += OnButtonPositive;
            // OnNeutralButton += OnButtonNeutral;
            OnNegativeButton += OnButtonNegative;

            OnTakeControlButton += OnButtonTakeControl;

            OnSubmitWrittenFeedback += OnWrittenFeedback;

            _feedbackControl = _feedbackActions.actionMaps[0];
            if (_elements.Contains(Elements.DISCRETE))
            {
                _feedbackControl.Enable();
            }

            _connection.SubscribeRemoteMessages((long)NetOpCode.ShowWrittenFeedback, OnShowWrittenFeedback);
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
                if (_feedbackControl["Joystick+"].WasPressedThisFrame())
                {
                    OnButtonPositive();
                }
                if (_feedbackControl["Joystick-"].WasPressedThisFrame())
                {
                    OnButtonNegative();
                }
                
            }

        }

        private void ToggleUI()
        {
            Visible = _connection.HasJoinedMatch && _connection.Role == DojoNetworkRole.Viewer;
        }

        private void OnShowWrittenFeedback(DojoMessage m)
        {
            if (_connection.IsClient && _connection.IsViewer)
            {
                ShowWrittenFeedback();
            }
        }

        #region Button Callbacks

        private void OnButtonTakeControl()
        {
            var targets = _menu.SelectedFeedbackAIPlayers;
            var targetAgentIDs = targets.ConvertAll(target => int.Parse(target.Split(char.Parse("-")).ToList().Last()));

            if (targets.Count != 1)
            {
                Debug.LogWarning($"{LOGSCOPE}: Button clicked but only 1 target client can be selected");
                return;
            }

            var targetAgentID = targetAgentIDs[0];
            var eventData = new List<object>() { targetAgentID };
            _connection.SendStateMessage((long)NetOpCode.ImitationLearning, JsonWriter.ToJson(eventData));
            _isControllingAgent = !_isControllingAgent;
            _takeControl.SetMode(_isControllingAgent ? TakeControl.Mode.ReleaseControl : TakeControl.Mode.TakeControl);
        }

        private void OnContinuousFeedback(float val)
        {
            SendFeedback(val);
        }

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
                // Debug.LogWarning($"{LOGSCOPE}: Feedback provided but no feedback target client selected");
            }
        }

        private void OnWrittenFeedback(string message)
        {
            var eventData = new List<object>() { message };
            _connection.SendStateMessage((long)NetOpCode.ReceiveWrittenFeedback, JsonWriter.ToJson(eventData));
        }

        #endregion Button Callbacks
    }
}
