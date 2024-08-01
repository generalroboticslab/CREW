using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.SideChannels;
using Unity.MLAgents.Sensors;
using Dojo;

namespace Examples.Bowling
{
    public class AIAgentManager : MonoBehaviour
    {
        [SerializeField]
        private GameBoard _board;

        [SerializeField]
        private GameObject _agentPrefab;

        [SerializeField]
        private Camera _aiAgentCamera;

        private DojoConnection _connection;

        private EventChannel _eventChannel;

        private int _numAgents = 0;

        [HideInInspector]
        public readonly List<AIAgent> agents = new();

        private void Awake()
        {
#if UNITY_STANDALONE // && !UNITY_EDITOR

            var args = Environment.GetCommandLineArgs();

            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];

                if (arg.Equals("-NumAgents") && idx < args.Length - 1 && int.TryParse(args[idx + 1], out var num) && num >= 0)
                {
                    _numAgents = num;
                    ++idx;
                }
            }
#endif

            _connection = FindObjectOfType<DojoConnection>();
            _connection.OnJoinedMatch += OnJoinedMatch;

            CameraSensorComponent cameraSensorComponent = _agentPrefab.GetComponent<CameraSensorComponent>();
            cameraSensorComponent.Camera = _aiAgentCamera;
        }

        private void OnJoinedMatch()
        {
            if (_connection.IsServer)
            {
                // register AI players
                if (_numAgents > 0)
                {
                    var players = Enumerable.Range(0, _numAgents).Select(x => $"Bowling-{x}").ToList();
                    _numAgents = _connection.RegisterAIPlayers(players);
                }

                if (_numAgents > 0)
                {
                    // spawn AI players
                    for (var i = 0; i < _numAgents; ++i)
                    {
                        var agent = Instantiate(_agentPrefab, transform).GetComponent<AIAgent>();
                        agents.Add(agent);
                    }
                    Initialize();
                }
            }
        }

        private void Initialize()
        {
            Debug.Assert(_connection.IsServer);

            if (Academy.IsInitialized)
            {
                // register MLAgent environment
                _eventChannel = new(_connection);
                if (_eventChannel.IsInitialized)
                {
                    SideChannelManager.RegisterSideChannel(_eventChannel);
                }

                Academy.Instance.OnEnvironmentReset += _board.ResetGameState;
            }
        }

        private void OnDestroy()
        {
            if (Academy.IsInitialized)
            {
                if (_eventChannel.IsInitialized)
                {
                    SideChannelManager.UnregisterSideChannel(_eventChannel);
                }
            }
        }
    }
}
