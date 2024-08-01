using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.SideChannels;
using Dojo;

namespace Examples.HideAndSeek
{
    public class AIAgentManager : MonoBehaviour
    {
        [SerializeField]
        private PlayerAssigner _assigner;

        [SerializeField]
        private WaitingRoom _waitingRoom;

        [SerializeField]
        private MapManager _mapManager;

        [Header("Agent Prefabs")]
        [SerializeField]
        private GameObject _hiderAgentPrefab;

        [SerializeField]
        private GameObject _seekerAgentPrefab;

        private DojoConnection _connection;

        private EventChannel _eventChannel;
        private ToggleTimestepChannel _toggleTimestepChannel;

        private int _numHiderAgents = 0;
        private int _numSeekerAgents = 0;

        private readonly List<AIAgent> _hiderAgents = new();
        private readonly List<AIAgent> _seekerAgents = new();

        private GameManager _gameManager;

        private void Awake()
        {
#if UNITY_STANDALONE // && !UNITY_EDITOR

            var args = Environment.GetCommandLineArgs();

            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];

                if (arg.Equals("-NumHiders") && idx < args.Length - 1 && int.TryParse(args[idx + 1], out var numHiders) && numHiders >= 0)
                {
                    _numHiderAgents = numHiders;
                    ++idx;
                }

                else if (arg.Equals("-NumSeekers") && idx < args.Length - 1 && int.TryParse(args[idx + 1], out var numSeekers) && numSeekers >= 0)
                {
                    _numSeekerAgents = numSeekers;
                    ++idx;
                }
            }
#endif
            _connection = FindObjectOfType<DojoConnection>();
            if (_connection.IsServer)
            {
                _mapManager.OnMapReady += OnMapReady;
            }

            _gameManager = GameManager.Instance;
        }

        private void Start()
        {
            if (_connection.IsServer)
            {

                SpawnAgents();
            }
        }

        public void ResetAgents()
        {
            if (_connection.IsServer)
            {
                _hiderAgents.ForEach(agent => agent.RequestDecision());
                _seekerAgents.ForEach(agent => agent.RequestDecision());

                _hiderAgents.ForEach(agent => agent.StartCoroutine(agent.WaitAndStartRequestingDecisions()));
                _seekerAgents.ForEach(agent => agent.StartCoroutine(agent.WaitAndStartRequestingDecisions()));
            }
        }

        public void ResetAgentsEndEpisode(bool iscaught)
        {
            if (iscaught)
            {
                _hiderAgents.ForEach(agent => agent.HiderCaught(true));
                _seekerAgents.ForEach(agent => agent.HiderCaught(false));
            }

            else
            {
                _hiderAgents.ForEach(agent => agent.StepsReached());
                _seekerAgents.ForEach(agent => agent.StepsReached());
            }

            _hiderAgents.ForEach(agent => agent.StartCoroutine(agent.WaitAndStartRequestingDecisions()));
            _seekerAgents.ForEach(agent => agent.StartCoroutine(agent.WaitAndStartRequestingDecisions()));
        }

        public void EndEpisode()
        {
            if (_hiderAgents.Count > 0)
            {
                _hiderAgents.First().EndEpisode();
            }
            else if (_seekerAgents.Count > 0)
            {
                _seekerAgents.First().EndEpisode();
            }
        }

        private void OnMapReady()
        {
            if (GameManager.Instance.HostType != GameHostType.WaitingRoom)
            {
                throw new Exception("Join Leave Mode not supported when there are AI players!");
            }

            JoinPlayers();
        }

        private void SpawnAgents()
        {
            // register AI players
            if (_numHiderAgents > 0)
            {
                var players = Enumerable.Range(0, _numHiderAgents).Select(x => $"Hider-{x}").ToList();
                _numHiderAgents = _connection.RegisterAIPlayers(players);
            }

            if (_numSeekerAgents > 0)
            {
                var players = Enumerable.Range(0, _numSeekerAgents).Select(x => $"Seeker-{x}").ToList();
                _numSeekerAgents = _connection.RegisterAIPlayers(players) - _numHiderAgents;
            }

            for (var i = 0; i < _numHiderAgents; ++i)
            {
                var agent = Instantiate(_hiderAgentPrefab, transform).GetComponent<AIAgent>();
                agent.AgentID = i;
                _hiderAgents.Add(agent);
            }
            for (var i = 0; i < _numSeekerAgents; ++i)
            {
                var agent = Instantiate(_seekerAgentPrefab, transform).GetComponent<AIAgent>();
                // Add _numHiderAgents to make sure all ids are unique between all agents.
                agent.AgentID = _numHiderAgents + i;
                _seekerAgents.Add(agent);
            }
            ConnectAgents();
        }

        private void JoinPlayers()
        {
            // Spawn the rest of the agents.
            for (var i = 0; i < _numHiderAgents; ++i)
            {
                _waitingRoom.JoinWaitingRoomAI(_hiderAgents[i], true, i);
            }
            for (var i = 0; i < _numSeekerAgents; ++i)
            {
                _waitingRoom.JoinWaitingRoomAI(_seekerAgents[i], false, i);
            }
        }

        private void ConnectAgents()
        {
            Debug.Assert(_connection.IsServer);

            // register MLAgent environment
            _eventChannel = new(_connection);
            if (_eventChannel.IsInitialized)
            {
                SideChannelManager.RegisterSideChannel(_eventChannel);
            }
            _toggleTimestepChannel = new(_gameManager);
            if (_toggleTimestepChannel.IsInitialized)
            {
                SideChannelManager.RegisterSideChannel(_toggleTimestepChannel);
            }

            Academy.Instance.OnEnvironmentReset += GameManager.Instance.ResetGameState;
            GameManager.Instance.OnGameStop += _ => JoinPlayers();
            ResetAgents();
        }

        private void OnDestroy()
        {
            if (_eventChannel.IsInitialized)
            {
                SideChannelManager.UnregisterSideChannel(_eventChannel);
            }
        }
    }
}
