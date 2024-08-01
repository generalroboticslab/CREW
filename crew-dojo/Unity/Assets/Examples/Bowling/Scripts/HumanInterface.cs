using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using Nakama.TinyJson;
using Dojo;
using Dojo.UI;
using Dojo.UI.Feedback;
using System.Linq;

namespace Examples.Bowling
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

            _feedbackControl = _feedbackActions.actionMaps[0];
            if (_elements.Contains(Elements.DISCRETE))
            {
                _feedbackControl.Enable();
            }

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
                Debug.LogWarning($"{LOGSCOPE}: Feedback provided but no feedback target client selected");
            }
        }

        #endregion Button Callbacks
    }
}
