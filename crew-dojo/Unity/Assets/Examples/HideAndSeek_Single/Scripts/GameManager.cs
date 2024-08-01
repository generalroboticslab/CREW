using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Dojo;
using Dojo.Netcode;
using Nakama.TinyJson;
using Unity.MLAgents;
using UnityEngine.AI;
using Unity.Netcode.Components;
// using System.Text.Json;

using System.IO;

namespace Examples.HideAndSeek_Single
{
    [Serializable]
    public class StringsList
    {
        public List<string> strings;
    }
    [DefaultExecutionOrder(-1)]
    public class GameManager : MonoBehaviour
    {
        private const string LOGSCOPE = "GameManager";

        [SerializeField]
        private MapManager _map;

        private DojoConnection _connection;
        private DojoTransport _transport;

        private bool IsClient => _connection.IsClient;

        private AIAgentManager _agentManager;

        [SerializeField]
        private GameObject _treasurePrefab;

        public GameObject _treasure;

        private bool _isFirstGame = true;

        private PlayerController _playerController;
        private NetworkTransform _networkTransform;

        public int random_seed = 0;
        private bool rand_maze = false;


        static string jsonString;
        StringsList maps;


        #region Unity Lifecycle

        private void Awake()
        {
#if UNITY_EDITOR
            string filePath = Path.Combine(Application.dataPath, "StreamingAssets/mazes.json");;
#else
            string filePath = Path.Combine(Application.streamingAssetsPath, "mazes.json");

            var args = Environment.GetCommandLineArgs();
            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];

                if (arg.Equals("-RandMaze") && idx < args.Length - 1 && float.TryParse(args[idx + 1], out var RandMaze))
                {
                    rand_maze = RandMaze == 1 ? true : false;
                    
                }
            
            }
#endif

            jsonString = File.ReadAllText(filePath);
            maps = JsonUtility.FromJson<StringsList>(jsonString);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            _connection = FindObjectOfType<DojoConnection>();
            _agentManager = GetComponentInChildren<AIAgentManager>();
            _networkTransform = _treasurePrefab.GetComponent<NetworkTransform>();


            _connection.SubscribeRemoteMessages((long)NetOpCode.ReceiveWrittenFeedback, OnReceiveWrittenFeedback);
            UnityEngine.Random.seed = random_seed;
        }

        private void Start()
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }

        #endregion Unity Lifecycle


        #region Callbacks

        private void OnServerStarted()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                _transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as DojoTransport;


                if (rand_maze)
                {
                    var idx = UnityEngine.Random.Range(0, maps.strings.Count);
                    _map.LoadMap(maps.strings[idx]);
                }
                else
                {
                    _map.LoadMap(MapManager.DEFAULT_MAP);
                }

                _treasure = Instantiate(_treasurePrefab);
                _treasure.GetComponent<NetworkObject>().Spawn();
                ResetTreasure();
                _agentManager.SpawnAgent();
            }
        }

        #endregion Callbacks

        public void ResetGame()
        {

        }

        public void ResetTreasure()
        {
            var spawnPoint = _map.FindSpawnPointForTreasure();

            if (NetworkManager.Singleton.IsServer)
            {
                _treasure.transform.SetPositionAndRotation(spawnPoint.center, _treasurePrefab.transform.rotation);
            }
        }

        public void OnReceiveWrittenFeedback(DojoMessage m)
        {

            // _agentManager.ClearScreen();
            if (!_connection.IsServer)
                return;

            bool successful = false;
            while (!successful)
            {
                try
                {
                    if (rand_maze)
                    {
                        var idx = UnityEngine.Random.Range(0, maps.strings.Count);
                        _map.LoadMap(maps.strings[idx]);
                    }
                    else
                    {
                        _map.LoadMap(MapManager.DEFAULT_MAP);
                    }
                    ResetTreasure();
                    _agentManager.ResetAgent();
                    successful = true; // Set to true if operation succeeds
                }
                catch (Exception ex)
                {
                    Debug.Log(ex.Message);
                }
            }

        }

    }
}
