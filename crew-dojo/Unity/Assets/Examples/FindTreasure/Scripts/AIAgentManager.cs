using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.SideChannels;
using Unity.MLAgents.Sensors;
using Dojo;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Examples.FindTreasure
{
    public class AIAgentManager : MonoBehaviour
    {
        [SerializeField]
        private MapManager _map;

        [SerializeField]
        private GameObject _agentPrefab;

        [SerializeField]
        private Camera _aiAgentCamera;

        private DojoConnection _connection;

        private GameManager _gameManager;

        private EventChannel _eventChannel;
        private WrittenFeedbackChannel _writtenFeedbackChannel;

        private Vector3 spawnpos;


        [HideInInspector]
        public AIAgent agent;

        private void Awake()
        {
            _connection = FindObjectOfType<DojoConnection>();
            _gameManager = FindObjectOfType<GameManager>();

            var cameraSensorComponent = _agentPrefab.GetComponent<CameraSensorComponent>();
            // _aiAgentCamera.enabled = true;
            var cams = _agentPrefab.transform.Find("AccCam_Sensor").GetComponent<AccumuCamera>().GetComponent<Camera>();

            cameraSensorComponent.Camera = cams;
            // cameraSensorComponent.Camera = _aiAgentCamera; // fully observable
        }

        public void SpawnAgent()
        {
            if (!_connection.IsServer)
                throw new NotServerException("You must spawn agents on the server for server ownership");
            _connection.RegisterAIPlayers(new List<string> { "FindTreasure-0" });
            var netObj = Instantiate(_agentPrefab).GetComponent<NetworkObject>();
            agent = netObj.GetComponentInChildren<AIAgent>();
            agent.AgentID = 0;
            ResetAgent();
            netObj.Spawn();
            Initialize();

        }

        private void Initialize()
        {
            if (Academy.IsInitialized)
            {
                // register MLAgent environment
                _eventChannel = new(_connection);
                _writtenFeedbackChannel = new(_connection);
                if (_eventChannel.IsInitialized)
                    SideChannelManager.RegisterSideChannel(_eventChannel);
                if (_writtenFeedbackChannel.IsInitialized)
                    SideChannelManager.RegisterSideChannel(_writtenFeedbackChannel);

                Academy.Instance.OnEnvironmentReset += _gameManager.ResetGame;
            }
        }

        public void ResetAgent()
        {
            if (_connection.IsServer)
            {
                var spawnPoint = _map.FindSpawnPointForPlayer();
                var success = agent.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>().Warp(spawnPoint.center);
                agent.GetComponentInChildren<Rigidbody>().velocity = Vector3.zero;
                agent.GetComponentInChildren<Rigidbody>().angularVelocity = Vector3.zero;

                agent.StartRequestingDecisions();
            }

            agent.GetComponentInChildren<PlayerController>().CamAcc.ClearAccumulation();
            agent.GetComponentInChildren<PlayerController>().CamAccSens.ClearAccumulation();
            agent.GetComponentInChildren<PlayerController>().clear_cam_flag.Value = true;
        }


        private void OnDestroy()
        {
            if (Academy.IsInitialized)
            {
                if (_eventChannel.IsInitialized)
                    SideChannelManager.UnregisterSideChannel(_eventChannel);
                if (_writtenFeedbackChannel.IsInitialized)
                    SideChannelManager.UnregisterSideChannel(_writtenFeedbackChannel);
            }
        }



    }
}
