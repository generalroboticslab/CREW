using System;
using System.Collections;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Dojo;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine.AI;

namespace Examples.HideAndSeek
{
    public class AIAgent : Agent
    {
        [Header("Configs")]
        [SerializeField] private float _agentMoveSpeed = 4.0f;

        [SerializeField] private float _agentRotationSpeed = 60.0f;

        [Tooltip("Request decision every N seconds")]
        [SerializeField] private float _decisionRequestFrequency = 1.0f;
        [SerializeField] private bool _repeatActions = false;

        [HideInInspector] public int AgentID = -1;
        public PlayerController _playerController;
        private GameManager _gameManager;

        private DojoConnection _connection;

        public bool IsPlayerAlive => _playerController != null;

        private float _feedbackReceived = 0;

        private bool _isActive = true;
        private int _trajectoryID = 0;

        private AIAction _lastAction;


        private NavMeshAgent navmeshagent;



        protected override void Awake()
        {
            base.Awake();


#if UNITY_STANDALONE // && !UNITY_EDITOR
            var args = Environment.GetCommandLineArgs();

            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];

                if (arg.Equals("-MoveSpeed") && idx < args.Length - 1 && float.TryParse(args[idx + 1], out var moveSpeed))
                {
                    _agentMoveSpeed = moveSpeed;
                    ++idx;
                }

                if (arg.Equals("-RotationSpeed") && idx < args.Length - 1 && float.TryParse(args[idx + 1], out var rotSpeed))
                {
                    _agentRotationSpeed = rotSpeed;
                    ++idx;
                }
                if (arg.Equals("-DecisionRequestFrequency") && idx < args.Length - 1 && float.TryParse(args[idx + 1], out var requestFreq))
                {
                    _decisionRequestFrequency = requestFreq;
                    ++idx;
                }
                if (arg.Equals("-Seed") && idx < args.Length - 1 && float.TryParse(args[idx + 1], out var random_seed))
                {
                    UnityEngine.Random.seed = (int)random_seed;
                    ++idx;
                }
            }
#endif
            var sensors = GetComponents<CameraSensorComponent>();
            foreach (var sensor in sensors)
            {
                sensor.Camera = Camera.main;
            }

            _connection = FindObjectOfType<DojoConnection>();
            _gameManager = GameManager.Instance;

            _connection.SubscribeRemoteMessages((long)NetOpCode.Feedback, OnRemoteFeedback);

            _gameManager.random_seed = UnityEngine.Random.seed;

        }

        private void FixedUpdate()
        {
            // if (_repeatActions && IsPlayerAlive)
            // {
            //     ExecuteAction(_lastAction);
            // }
        }

        private void DecisionRequestLoop()
        {
            bool gamePaused = _gameManager.GamePaused;
            if (IsPlayerAlive && !gamePaused && _isActive)
            {
                RequestDecision();
            }
        }

        public void SubscribeController(PlayerController controller)
        {
            _playerController = controller;
            _playerController.AgentID = AgentID;
            _playerController.SetMoveSpeed(_agentMoveSpeed);
            _playerController.SetRotationSpeed(_agentRotationSpeed);

            var sensors = GetComponents<CameraSensorComponent>();
            foreach (var sensor in sensors)
            {
                if (sensor.SensorName.Contains("FirstPerson"))
                {
                    sensor.Camera = _playerController.CamEye;
                    sensor.enabled = _playerController.EnableFirstCamera;
                }
                else if (sensor.SensorName.Contains("Masked"))
                {
                    sensor.Camera = _playerController.CamMasked;
                    sensor.enabled = _playerController.EnableMaskedCamera;
                }
                else if (sensor.SensorName.Contains("Accumulative"))
                {
                    sensor.Camera = _playerController.CamAcc;
                    sensor.enabled = _playerController.EnableAccumuCamera;
                }
            }

            InvokeRepeating(nameof(DecisionRequestLoop), 0.0f, _decisionRequestFrequency);
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (_connection.IsServer)
            {
                sensor.AddObservation(_feedbackReceived);
                _feedbackReceived = 0;
                sensor.AddObservation(Time.realtimeSinceStartup);
                sensor.AddObservation(_trajectoryID);
                sensor.AddObservation(0);
                sensor.AddObservation(_playerController.transform.position);
                sensor.AddObservation(_playerController.transform.rotation.eulerAngles);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var actionZ = actions.ContinuousActions[0] * 10.0f;
            var actionX = actions.ContinuousActions[1] * 10.0f;
            var action = new Vector2(actionX, actionZ);

            ExecuteAction(action);
        }

        private void ExecuteAction(Vector2 des)
        {
            bool gamePaused = _gameManager.GamePaused;
            if (!GameManager.Instance.GameRunning || !IsPlayerAlive || gamePaused)
                return;

            _playerController.ActionNavigateToDes(des);
        }


        private void OnRemoteFeedback(DojoMessage m)
        {
            var feedbackMessage = m.GetDecodedData<List<object>>();
            float feedback = Convert.ToSingle(feedbackMessage[0]);
            List<int> targets = (feedbackMessage[1] as IEnumerable<object>).Cast<object>().Cast<int>().ToList();
            if (targets.Contains(AgentID))
            {
                if (feedback != -9)
                    {_feedbackReceived = feedback;}
            }
        }

        public void HiderCaught(bool ishider)
        {
            if (ishider)
            {
                AddReward(-1.0f);
            }
            else
            {
                AddReward(1.0f);
            }
            _isActive = false;
            EndEpisode();
            _trajectoryID += 1;
        }

        public void StepsReached()
        {
            _isActive = false;
            EndEpisode();
            _trajectoryID += 1;
        }

        public void StartRequestingDecisions()
        {

            if (!_connection.IsServer)
                return;
            _isActive = true;
        }

        public IEnumerator WaitAndStartRequestingDecisions()
        {
            yield return null; // waits one frame
            if (!_connection.IsServer)
                yield return null;
            _isActive = true;
        }

    }
}
