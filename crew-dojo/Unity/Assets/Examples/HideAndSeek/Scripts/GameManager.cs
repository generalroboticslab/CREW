using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Nakama;
using Nakama.TinyJson;
using Dojo;
using Dojo.Netcode;
using Dojo.Recording;
using Unity.MLAgents;
using System.IO;

namespace Examples.HideAndSeek
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

        public static GameManager Instance { get; private set; }

        public GameHostType HostType = GameHostType.WaitingRoom;

        [SerializeField, Min(0.1f)]
        private float _matchMaxDuration = 120.0f; // duration in seconds

        [SerializeField]
        private MapManager _map;

        private DojoConnection _connection;
        private DojoTransport _transport;

        private WaitingRoom _waitingRoom;
        private JoinLeave _joinLeave;
        private PlayerAssigner _assigner;
        private AIAgentManager _agentManager;

        private DojoRecord _record;

        // maintaining the list of active players in the match (alive and playing)
        // can be modified by PlayerAssigner class
        public readonly Dictionary<ulong, bool> ActivePlayers = new();
        public readonly HashSet<Tuple<int, bool>> ActiveAIPlayers = new();

        public GameStage Stage { get; private set; } = GameStage.IDLE;
        public event Action<DojoMessage> OnGameStart;
        public event Action<DojoMessage> OnGameStop;
        public event Action<DojoMessage> OnHiderDied;
        public event Action<DojoMessage> OnSeekerCaught;

        private bool _winnerIsHider = true;
        private bool _gameRunning = false;

        public bool GameRunning => _gameRunning;

        private bool _gamePaused = false;
        public bool GamePaused => _gamePaused;

        // [SerializeField]
        private int _maxSteps;

        private int step = 0;
        static string jsonString;
        StringsList maps;

        public int random_seed = 0;
        private bool rand_maze = false;


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
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            _connection = FindObjectOfType<DojoConnection>();
            _connection.OnMatchPlayerJoined += OnMatchPlayerJoined;
            _connection.OnMatchPlayerLeft += OnMatchPlayerLeft;
            _connection.OnRoleChanged += OnMatchRoleSwitch;

            _connection.OnRoleChanged += OnRoleChanged;
            _connection.OnMatchPlayerLeft += OnPlayerLeft;

            _waitingRoom = GetComponentInChildren<WaitingRoom>();
            _joinLeave = GetComponentInChildren<JoinLeave>();
            _assigner = GetComponentInChildren<PlayerAssigner>();
            _record = GetComponentInChildren<DojoRecord>();
            _agentManager = GetComponentInChildren<AIAgentManager>();

            UnityEngine.Random.seed = random_seed;

            _maxSteps = (int)(_matchMaxDuration * 50);
        }

        private void Start()
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            if (HostType == GameHostType.WaitingRoom)
            {
                _waitingRoom.Visible = true;
                _joinLeave.Visible = false;
            }
            else
            {
                _joinLeave.Visible = true;
                _waitingRoom.Visible = false;
            }

            // prevent player to switch a role during an ongoing game
            _connection.RegisterSwitchRoleRule((currentRole, targetRole) =>
            {
                return !(HostType == GameHostType.WaitingRoom && Stage == GameStage.PlAY);
            });

            var dispatcher = Dispatcher.Instance;

            _waitingRoom.OnNewPlayer += WaitingRoomOnNewPlayer;
            _waitingRoom.OnNewAIPlayer += (a, b, c, d) => dispatcher.Enqueue(() => WaitingRoomOnNewAIPlayer(a, b, c, d), inFixedUpdate: true);
            _joinLeave.OnNewPlayer += JoinLeaveOnNewPlayer;

        }

        private void Update()
        {
            if (NetworkManager.Singleton != null)
            {
                if (HostType == GameHostType.WaitingRoom && NetworkManager.Singleton.IsServer)
                {
                    if (step >= _maxSteps)
                    {
                        EpisodeEnded(false);
                    }
                    step += 1;

                    if (Stage == GameStage.PlAY)
                    {
                        _waitingRoom.UpdateRemainingTime();
                        if (ActivePlayers.Values.Count(m => m) + ActiveAIPlayers.Count(m => m.Item2) == 0)
                        {
                            StopGameEpisode();
                        }
                    }
                }
                else if (HostType == GameHostType.JoinLeave && NetworkManager.Singleton.IsClient)
                {
                    if (Stage == GameStage.PlAY)
                    {
                        _joinLeave.UpdateRemainingTime();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            Instance = null;
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

                if (_map.SpawnPoints.Count < 2)
                    throw new InvalidOperationException("Both a hider and seeker must be spawned at the beginning of the game for MLAgents to work properly. This cannot happen if the number of spawn points is < 2.");
            }
        }

        private void OnRoleChanged(IUserPresence user)
        {
            OnRoleChangedOrPlayerLeft(user, false);
        }

        private void OnPlayerLeft(IUserPresence user)
        {
            OnRoleChangedOrPlayerLeft(user, true);
        }

        private void OnRoleChangedOrPlayerLeft(IUserPresence user, bool playerLeft)
        {
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsServer && HostType == GameHostType.WaitingRoom)
                {
                    // if a player joined in waiting room but now switched to role other than viewer
                    // or player left
                    // we release its player object and update UI
                    if (_transport.GetNetcodeIDByUser(user, out var clientID) && ActivePlayers.TryGetValue(clientID, out var isHider))
                    {
                        _connection.MatchClients.TryGetValue(user, out var test);
                        Debug.Log(test);
                        if (playerLeft || (_connection.MatchClients.TryGetValue(user, out var role) && role != DojoNetworkRole.Player))
                        {
                            _waitingRoom.OnPlayerLeaves(isHider);
                        }
                    }
                }
                else if (NetworkManager.Singleton.IsClient && user.Equals(_connection.MatchSelf) && HostType == GameHostType.JoinLeave)
                {
                    StopGameEpisode();
                }
            }
        }

        private async void OnMatchPlayerJoined(IUserPresence user)
        {
            if (_connection.IsServer && _connection.MatchClients.TryGetValue(user, out var role))
            {
                await _record.DispatchEvent(RecordEvent.ClientJoin, "joined", user, role);
            }
        }

        private async void OnMatchPlayerLeft(IUserPresence user)
        {
            if (_connection.IsServer && _connection.MatchClients.TryGetValue(user, out var role))
            {
                await _record.DispatchEvent(RecordEvent.ClientLeave, "left", user, role);
            }
        }

        private async void OnMatchRoleSwitch(IUserPresence user)
        {
            if (_connection.IsServer && _connection.MatchClients.TryGetValue(user, out var role))
            {
                await _record.DispatchEvent(RecordEvent.ClientRoleSwitch, "switched role", user, role);
            }
        }

        // assign a new player object to the client
        private async void WaitingRoomOnNewPlayer(bool isHider, ulong clientID, bool startGame)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                _assigner.RequestRole(isHider, clientID);

                if (_transport.GetUserByNetcodeID(clientID, out var user) && _connection.MatchClients.TryGetValue(user, out var role))
                {
                    var selection = isHider ? "Hider" : "Seeker";
                    await _record.DispatchEvent(RecordEvent.PlayerSelection, $"{selection}", user, role);
                }

                if (startGame)
                {
                    _waitingRoom.ToggleWaitingRoom(true);
                    _waitingRoom.PrepareBeforeGame();
                    Invoke(nameof(StartGameEpisode), 0.0f);
                }
            }
        }

        private async void WaitingRoomOnNewAIPlayer(AIAgent agent, bool isHider, int agentID, bool startGame)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                _assigner.RequestAIRole(agent, isHider, agentID);

                var selection = isHider ? "Hider" : "Seeker";
                await _record.DispatchEvent(RecordEvent.PlayerSelection, $"AI {agentID} {selection}");

                if (startGame)
                {
                    _waitingRoom.ToggleWaitingRoom(true);
                    _waitingRoom.PrepareBeforeGame();
                    Invoke(nameof(StartGameEpisode), 0.0f);
                }
            }
        }

        // assgin a new player object to the client
        private async void JoinLeaveOnNewPlayer(bool isHider)
        {
            if (NetworkManager.Singleton.IsClient)
            {
                Invoke(nameof(StartGameEpisode), 0.0f);
                _assigner.RequestRole(isHider);

                var selection = isHider ? "Hider" : "Seeker";
                await _record.DispatchEvent(RecordEvent.PlayerSelection, $"{selection}");
            }
        }

        #endregion Callbacks


        #region Episode Management

        // start a game episode
        private async void StartGameEpisode()
        {
            CancelInvoke(nameof(StartGameEpisode));
            if (_gameRunning)
            {
                return;
            }

            Debug.Log($"{LOGSCOPE}: StartGameEpisode");

            _gameRunning = true;
            _agentManager.ResetAgents();

            Stage = GameStage.PlAY;
            if (NetworkManager.Singleton.IsServer && HostType == GameHostType.WaitingRoom)
            {
                // start time
                _waitingRoom.UpdateRemainingTime(_matchMaxDuration);
                Invoke(nameof(StopGameEpisode), _matchMaxDuration);
                await _connection.SendStateMessage((long)NetOpCode.GameEpisodeStart, "GameStarted");
            }
            else if (NetworkManager.Singleton.IsClient && HostType == GameHostType.JoinLeave)
            {
                // count down
                _winnerIsHider = true;
                _joinLeave.UpdateRemainingTime(_matchMaxDuration);
                Invoke(nameof(StopGameEpisode), _matchMaxDuration);
                OnGameStart?.Invoke(default);
            }

            var numHiders = ActivePlayers.Values.Count(p => p) + ActiveAIPlayers.Count(p => p.Item2);
            var numSeekers = ActivePlayers.Values.Count(p => !p) + ActiveAIPlayers.Count(p => !p.Item2);
            await _record.DispatchEvent(RecordEvent.EpisodeBegin, $"{HostType} {numHiders} {numSeekers}");
        }

        // end a game episode and reset
        private async void StopGameEpisode()
        {
            if (!_gameRunning)
            {
                return;
            }

            Debug.Log($"{LOGSCOPE}: StopGameEpisode");

            _gameRunning = false;
            _agentManager.EndEpisode();

            CancelInvoke(nameof(StopGameEpisode));
            Stage = GameStage.IDLE;

            if (NetworkManager.Singleton.IsServer && HostType == GameHostType.WaitingRoom)
            {
                // compute winner, if at least one hider survived, hider wins
                var numHiders = ActivePlayers.Values.Count(p => p) + ActiveAIPlayers.Count(p => p.Item2);
                var numSeekers = ActivePlayers.Values.Count(p => !p) + ActiveAIPlayers.Count(p => !p.Item2);
                var winner = numHiders > 0 ? "Hider" : "Seeker";
                await _record.DispatchEvent(RecordEvent.EpisodeEnd, $"{HostType} {numHiders} {numSeekers} {winner} {_waitingRoom.DurationTime}");

                var eventData = new string[] { winner, _waitingRoom.DurationTime.ToString() };
                await _connection.SendStateMessage((long)NetOpCode.GameEpisodeStop, JsonWriter.ToJson(eventData));
                ActivePlayers.Keys.ToList().ForEach(clientID => _assigner.ReleaseRole(clientID));
                ActiveAIPlayers.ToList().ForEach(agent => _assigner.ReleaseAIRole(agent.Item2, agent.Item1));

                // stop and reset
                _waitingRoom.ResetAfterGame();

                OnGameStop?.Invoke(new(JsonWriter.ToJson(eventData)));
            }
            else if (NetworkManager.Singleton.IsClient && HostType == GameHostType.JoinLeave)
            {
                var numHiders = ActivePlayers.Values.Count(p => p) + ActiveAIPlayers.Count(p => p.Item2);
                var numSeekers = ActivePlayers.Values.Count(p => !p) + ActiveAIPlayers.Count(p => !p.Item2);
                var winner = _winnerIsHider ? "Hider" : "Seeker";
                await _record.DispatchEvent(RecordEvent.EpisodeEnd, $"{HostType} {numHiders} {numSeekers} {winner} {_joinLeave.DurationTime}");

                var eventData = new string[] { winner, _joinLeave.DurationTime.ToString() };
                OnGameStop?.Invoke(new(JsonWriter.ToJson(eventData)));
                _assigner.ReleaseRole();
                _joinLeave.ResetAfterGame();
            }
        }


        public void ResetGameState()
        {
            //if (!Academy.IsInitialized)
            Debug.Log("Reset Game State");
            StopGameEpisode();
        }

        #endregion Episode Management


        #region Public Properties

        public void EpisodeEnded(bool iscaught)
        {
            // Debug.Log("GameManager: Hider Caught");
            if (rand_maze)
            {
                var idx = UnityEngine.Random.Range(0, maps.strings.Count);
                _map.LoadMap(maps.strings[idx]);
            }
            else
            {
                _map.LoadMap(MapManager.DEFAULT_MAP);
            }
            _agentManager.ResetAgentsEndEpisode(iscaught);
            foreach (var playercont in FindObjectsOfType<PlayerController>())
            {
                _assigner.ResetPlayerPosition(playercont);
            }
            step = 0;

        }


        // player has died after collision, waiting room version
        public async void HiderHasDied(ulong clientID)
        {
            if (HostType == GameHostType.WaitingRoom)
            {
                if (ActivePlayers.TryGetValue(clientID, out var isHider))
                {
                    _waitingRoom.OnPlayerLeaves(isHider);
                }
                _assigner.ReleaseRole(clientID);

                await _connection.SendStateMessage((long)NetOpCode.HiderHasDied, "HiderHasDied");

                if (_transport.GetUserByNetcodeID(clientID, out var user) && _connection.MatchClients.TryGetValue(user, out var role))
                {
                    await _record.DispatchEvent(RecordEvent.PlayerDied, "Player Died", user, role);
                }
            }
        }

        // join & leave version
        public async void HiderHasDied()
        {
            if (HostType == GameHostType.JoinLeave)
            {
                _winnerIsHider = false;
                OnHiderDied?.Invoke(default);
                // StopGameEpisode();

                await _record.DispatchEvent(RecordEvent.PlayerDied, "Player Died");
            }
        }

        public async void HiderHasDied(int agentID)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                await _record.DispatchEvent(RecordEvent.PlayerDied, $"AI Player {agentID} Died");

                _assigner.ReleaseAIRole(true, agentID);

                OnHiderDied?.Invoke(new(agentID.ToString()));
            }
        }

        public async void SeekerHasCaught(int seekerID, int hiderID)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                await _record.DispatchEvent(RecordEvent.PlayerDied, $"AI Player {seekerID} Has Caught {hiderID}");

                var data = new int[] { seekerID, hiderID };

                OnSeekerCaught?.Invoke(new(JsonWriter.ToJson(data)));
            }
        }

        public float GetMatchTimeout()
        {
            if (HostType == GameHostType.WaitingRoom)
            {
                return _waitingRoom.RemainingTime;
            }
            else
            {
                return _joinLeave.RemainingTime;
            }
        }

        public int HiderCount
        {
            get
            {
                if (HostType == GameHostType.WaitingRoom)
                {
                    return _waitingRoom.HiderCount;
                }
                else
                {
                    return _joinLeave.HiderCount;
                }
            }
        }

        public int SeekerCount
        {
            get
            {
                if (HostType == GameHostType.WaitingRoom)
                {
                    return _waitingRoom.SeekerCount;
                }
                else
                {
                    return _joinLeave.SeekerCount;
                }
            }
        }

        public bool IsHider
        {
            get
            {
                if (HostType == GameHostType.WaitingRoom)
                {
                    return _waitingRoom.IsHider;
                }
                else
                {
                    return _joinLeave.IsHider;
                }
            }
        }

        #endregion Public Properties

        #region PauseGame
        public void PauseGame()
        {
            _gamePaused = !_gamePaused;

            if (_gamePaused)
            {
                CancelInvoke(nameof(StopGameEpisode));
            }
            else
            {
                if ((NetworkManager.Singleton.IsServer && HostType == GameHostType.WaitingRoom) ||
                    (NetworkManager.Singleton.IsClient && HostType == GameHostType.JoinLeave))
                {
                    float remainingTime = (HostType == GameHostType.WaitingRoom) ? _waitingRoom.RemainingTime : _joinLeave.RemainingTime;

                    if (remainingTime > 0)
                    {
                        Invoke(nameof(StopGameEpisode), remainingTime);
                    }
                    else
                    {
                        StopGameEpisode();
                    }
                }
            }
        }
        #endregion PauseGame
    }
}
