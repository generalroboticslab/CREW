using System;
using System.Collections;
using UnityEngine;
using Unity.MLAgents;
using UnityEngine.InputSystem;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Dojo;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine.AI;

namespace Examples.HideAndSeek_Single
{
    public class AIAgent : Agent
    {
        [Header("Configs")]

        [SerializeField] private int max_steps = 1000;
        [SerializeField] private bool _written_feedback = true;
        [SerializeField] private float _agentMoveSpeed = 4.0f;

        [SerializeField] private float _agentRotationSpeed = 60.0f;

        [Tooltip("Request decision every N seconds")]
        [SerializeField] private float _decisionRequestFrequency = 1.0f;
        [SerializeField] private bool _repeatActions = false;

        [HideInInspector] public int AgentID = -1;
        private PlayerController _playerController;
        private GameManager _gameManager;

        private bool _imitationLearning = false;

        private DojoConnection _connection;

        private float _feedback = 0;
        private int _fixedUpdateCount = 0;
        private bool _isActive = true;
        private int _trajectoryID = 0;

        private AIAction _lastAction;

        private int buffer = 0;
        private int steps = 0;
        private bool clear_cam_flag = false;

        private NavMeshAgent navmeshagent;


        private Vector2 _prevAction;


        protected override void Awake()
        {
            base.Awake();

            _playerController = GetComponentInChildren<PlayerController>();

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
                if (arg.Equals("-Written_feedback") && idx < args.Length - 1 && float.TryParse(args[idx + 1], out var _Written_feedback))
                {
                    _written_feedback = _Written_feedback == 1 ? true : false;
                    ++idx;
                }
            }
            InvokeRepeating(nameof(DecisionRequestLoop), 0.0f, _decisionRequestFrequency);
#endif
            _connection = FindObjectOfType<DojoConnection>();
            _gameManager = FindObjectOfType<GameManager>();
            navmeshagent = GetComponentInChildren<NavMeshAgent>();
            navmeshagent.enabled = false;

            if (_connection.IsServer)
            {
                _connection.SubscribeRemoteMessages((long)NetOpCode.Feedback, OnRemoteFeedback);
                _connection.SubscribeRemoteMessages((long)NetOpCode.ImitationLearning, OnImitationLearning);
                _playerController.SetMoveSpeed(_agentMoveSpeed);
                _playerController.SetRotationSpeed(_agentRotationSpeed);
            }
            _gameManager.random_seed = UnityEngine.Random.seed;
        }

        private void FixedUpdate()
        {
            if (_connection.IsServer)
            {
                var dis = (transform.Find("Body").position - _gameManager._treasure.transform.position).magnitude;
                if (dis < 2f)
                {
                    Debug.Log("Hider Caught!");
                    AddReward(1);
                    _isActive = false;
                    _gameManager._treasure.GetComponentInChildren<HiderHeuristic>()._isActive = false;
                    EndEpisode();
                    steps = 0;
                    _trajectoryID += 1;
                    _lastAction = 0;

                    if (_written_feedback)
                    {
                        Debug.Log("--Sending Written!");
                        _connection.SendStateMessage((long)NetOpCode.ShowWrittenFeedback, "Show Written Feedback!");
                        Debug.Log("--Sending Written Done!");
                    }
                    else
                    {
                        _gameManager.OnReceiveWrittenFeedback(null);
                    }
                }

                if (steps < max_steps)
                {
                    steps += 1;
                }
                else
                {
                    Debug.Log("Max Steps Reached");
                    _isActive = false;
                    _gameManager._treasure.GetComponentInChildren<HiderHeuristic>()._isActive = false;
                    EndEpisode();
                    steps = 0;
                    _trajectoryID += 1;
                    _lastAction = 0;

                    if (_written_feedback)
                    {
                        Debug.Log("--Sending Written!");
                        _connection.SendStateMessage((long)NetOpCode.ShowWrittenFeedback, "Show Written Feedback!");
                        Debug.Log("--Sending Written Done!");
                    }
                    else
                    {
                        _gameManager.OnReceiveWrittenFeedback(null);
                    }
                }

                _fixedUpdateCount += 1;
            }
        }
        private void DecisionRequestLoop()
        {

            if (_isActive)
            {
                RequestDecision();
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (_connection.IsServer)
            {
                sensor.AddObservation(_feedback);
                _feedback = 0;
                sensor.AddObservation(Time.realtimeSinceStartup);
                sensor.AddObservation(_trajectoryID);
                sensor.AddObservation(_imitationLearning);
                sensor.AddObservation(_playerController.humanAction.Value);
                sensor.AddObservation(_playerController.transform.position);
                sensor.AddObservation(_playerController.transform.rotation.eulerAngles);
                sensor.AddObservation(_gameManager._treasure.transform.position);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var actionZ = actions.ContinuousActions[0];
            var actionX = actions.ContinuousActions[1];
            var action = new Vector2(actionX, actionZ);

            ExecuteAction(action);
        }

        private void ExecuteAction(Vector2 des)
        {
            if (!_connection.IsServer)
                return;

            if (_isActive)
            { _gameManager._treasure.GetComponentInChildren<HiderHeuristic>()._isActive = true; }
            if (!_imitationLearning)
            {
                ActionNavigateToDes(des);
                // ActionSetVelocity(des);
            }
        }


        private void OnRemoteFeedback(DojoMessage m)
        {
            var feedbackMessage = m.GetDecodedData<List<object>>();
            float feedback = Convert.ToSingle(feedbackMessage[0]);
            List<int> targets = (feedbackMessage[1] as IEnumerable<object>).Cast<object>().Cast<int>().ToList();
            if (targets.Contains(AgentID))
            {
                if (feedback != -9)
                    {_feedback = feedback;}
            }
        }

        private void OnImitationLearning(DojoMessage m)
        {
            if (!_connection.IsServer)
                return;
            var imitationLearningMessage = m.GetDecodedData<List<object>>();
            int target = (int)imitationLearningMessage[0];
            _imitationLearning = target == AgentID ? !_imitationLearning : false;
        }

        private void ActionNavigateToDes(Vector2 des)
        {
            if (_connection.IsServer)
            {
                navmeshagent.enabled = true;
                navmeshagent.SetDestination(new Vector3(des.x, 0, des.y));
            }
            else
            {
                ActionNavigateToDesServerRpc(des);
            }
        }

        [ServerRpc]
        private void ActionNavigateToDesServerRpc(Vector2 des)
        {
            ActionNavigateToDes(des);
        }

        private void ActionSetVelocity(Vector2 vel)
        {
            if (_connection.IsServer)
            {
                vel = vel.normalized;

                _playerController._offset.x = vel.x * 0.6f;
                _playerController._offset.z = vel.y * 0.6f;
                // rotate the player along y direction to face the direction of the velocity (x, z).
                _playerController._body.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(vel.x, vel.y) * Mathf.Rad2Deg, 0);
                // _playerController._body.AddForce(new Vector3(vel.x, 0, vel.y) * _agentMoveSpeed, ForceMode.VelocityChange);
            }
            else
            {
                ActionSetVelocityServerRpc(vel);
            }
        }

        [ServerRpc]
        private void ActionSetVelocityServerRpc(Vector2 vel)
        {
            ActionSetVelocity(vel);
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
