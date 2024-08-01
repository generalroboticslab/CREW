using System;
using UnityEngine;
using Dojo;
using System.Collections.Generic;
using Nakama.TinyJson;

namespace Examples.Bowling
{
    [DefaultExecutionOrder(-1)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField]
        private GameBoard _board;

        [SerializeField]
        private DojoConnection _connection;

        private bool IsClient => _connection.IsClient;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Debug.Assert(FindObjectsOfType<GameManager>().Length == 1, "Only one game manager is allowed!");
        }

        private void Start()
        {
            _board.Connection = _connection;
            _board.OnNewAction += NewClientAction;
            _board.OnNewState += NewServerState;

            _board.OnFrameEnded += FrameEndedEvent;

            _connection.SubscribeRemoteMessages((long)NetOpCode.ClientAction, OnClientAction);
            _connection.SubscribeRemoteMessages((long)NetOpCode.ServerState, OnServerState);
            _connection.SubscribeRemoteMessages((long)NetOpCode.GameEvent, OnGameEvent);
        }

        #region State Action Updates

        private void NewClientAction(NetCommand command)
        {
            if (IsClient)
            {
                var action = command.ToString();
                _connection.SendStateMessage((long)NetOpCode.ClientAction, action);
            }
        }

        private void NewServerState(byte[] state)
        {
            if (!IsClient)
            {
                _connection.SendStateMessage((long)NetOpCode.ServerState, state);
            }
        }

        private void OnClientAction(DojoMessage m)
        {
            if (!IsClient)
            {
                var action = m.GetString();
                if (Enum.TryParse(typeof(NetCommand), action, out var command))
                {
                    _board.HandleClientControl((NetCommand)command);
                }
                else
                {
                    Debug.LogWarning($"Invalid remote action: {action}");
                }
            }
        }

        private void OnServerState(DojoMessage m)
        {
            if (IsClient)
            {
                var state = m.RawData;
                _board.DecodeState(state);
            }
        }

        #endregion State Action Updates

        #region Game Events

        private void FrameEndedEvent(int frameCount, int score)
        {
            if (!IsClient)
            {
                var message = new List<string>() { "FrameEnded", frameCount.ToString(), score.ToString() };
                _connection.SendStateMessage((long)NetOpCode.GameEvent, JsonWriter.ToJson(message));
            }
        }

        private void OnGameEvent(DojoMessage m)
        {
            if (IsClient)
            {
                _board.HandleEvents(m.GetDecodedData<List<string>>());
            }
        }

        #endregion Game Events
    }

}
