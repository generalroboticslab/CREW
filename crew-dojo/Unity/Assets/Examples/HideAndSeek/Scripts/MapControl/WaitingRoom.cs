using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Nakama;
using Dojo;

namespace Examples.HideAndSeek
{
    // Waiting Room handles UI display
    // For each new join button click, GameManager captures and updates information

    [RequireComponent(typeof(UIDocument))]
    public class WaitingRoom : NetworkBehaviour
    {
        private const string LOGSCOPE = "WaitingRoom";

        [SerializeField, Range(1, 100)]
        private int _matchStartNumHiders = 1;

        [SerializeField, Range(1, 100)]
        private int _matchStartNumSeekers = 1;

        private VisualElement _roomUI;

        private Label _hiderCountText;
        private Label _seekerCountText;

        private Button _joinHiderButton;
        private Button _joinSeekerButton;
        private bool _lastSelection = false;

        private readonly NetworkVariable<int> _hiderCount = new(0);
        private readonly NetworkVariable<int> _seekerCount = new(0);
        private readonly NetworkVariable<bool> _waitingRoomLocked = new(false);
        private readonly NetworkVariable<float> _matchRemainingTime = new(0.0f);
        private readonly NetworkVariable<float> _matchTotalTime = new(0.0f);
        private bool _selected = false;

        private DojoConnection _connection;

        public event Action<bool, ulong, bool> OnNewPlayer;
        public event Action<AIAgent, bool, int, bool> OnNewAIPlayer;

        public int HiderCount => _hiderCount.Value;
        public int SeekerCount => _seekerCount.Value;

        private GameManager _gameManager;

        public bool Visible
        {
            get
            {
                return _roomUI.style.display == DisplayStyle.Flex;
            }
            set
            {
                _roomUI.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void Awake()
        {
#if UNITY_STANDALONE // && !UNITY_EDITOR

            var args = Environment.GetCommandLineArgs();

            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];

                if (arg.Equals("-MatchStartNumHiders") && idx < args.Length - 1 && int.TryParse(args[idx + 1], out var numHiders) && numHiders >= 0)
                {
                    _matchStartNumHiders = numHiders;
                    ++idx;
                }

                else if (arg.Equals("-MatchStartNumSeekers") && idx < args.Length - 1 && int.TryParse(args[idx + 1], out var numSeekers) && numSeekers >= 0)
                {
                    _matchStartNumSeekers = numSeekers;
                    ++idx;
                }
            }
#endif

            _connection = FindObjectOfType<DojoConnection>();

            _roomUI = GetComponent<UIDocument>().rootVisualElement;
            _hiderCountText = _roomUI.Q<Label>("HiderCount");
            _seekerCountText = _roomUI.Q<Label>("SeekerCount");

            _joinHiderButton = _roomUI.Q<Button>("Hider");
            _joinSeekerButton = _roomUI.Q<Button>("Seeker");

            _gameManager = GameManager.Instance;
        }

        private void Start()
        {
            if (GameManager.Instance.HostType == GameHostType.WaitingRoom)
            {
                _joinHiderButton.clickable.clicked += () =>
                {
                    JoinWaitingRoom(true);
                };
                _joinSeekerButton.clickable.clicked += () =>
                {
                    JoinWaitingRoom(false);
                };
                _connection.OnRoleChanged += OnRoleChanged;
            }

            Visible = _connection.IsPlayer;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.HostType == GameHostType.WaitingRoom)
            {
                _hiderCountText.text = _hiderCount.Value.ToString();
                _seekerCountText.text = _seekerCount.Value.ToString();

                _joinHiderButton.SetEnabled(!_waitingRoomLocked.Value && !_selected);
                _joinSeekerButton.SetEnabled(!_waitingRoomLocked.Value && !_selected);
            }
        }

        public void JoinWaitingRoom(bool isHider)
        {
            Debug.Log($"{LOGSCOPE}: JoinWaitingRoom ({isHider})");
            if (!_joinHiderButton.enabledSelf || !_joinSeekerButton.enabledSelf)
            {
                return;
            }
            if (NetworkManager.Singleton.IsClient && !_waitingRoomLocked.Value)
            {
                _selected = true;
                _lastSelection = isHider;
                OnJoinButtonServerRPC(isHider, NetworkManager.Singleton.LocalClientId);
            }
        }

        public void JoinWaitingRoomAI(AIAgent agent, bool isHider, int agentID)
        {
            if (_waitingRoomLocked.Value)
            {
                return;
            }

            Debug.Log($"{LOGSCOPE}: JoinWaitingRoomAI ({isHider} {agentID})");
            if (NetworkManager.Singleton.IsServer)
            {
                if (isHider)
                {
                    _hiderCount.Value++;
                }
                else
                {
                    _seekerCount.Value++;
                }
                var canStartGame = _hiderCount.Value >= _matchStartNumHiders && _seekerCount.Value >= _matchStartNumSeekers && (_hiderCount.Value == _seekerCount.Value);
                OnNewAIPlayer?.Invoke(agent, isHider, agentID, canStartGame);
            }
        }

        // lock / unlock waiting room (by server)
        public void ToggleWaitingRoom(bool locked)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                _waitingRoomLocked.Value = locked;
            }
        }

        // if player switch to viewer while waiting
        public void OnPlayerLeaves(bool isHider)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                if (isHider)
                {
                    _hiderCount.Value = Math.Max(_hiderCount.Value - 1, 0);
                }
                else
                {
                    _seekerCount.Value = Math.Max(_seekerCount.Value - 1, 0);
                }
            }
        }

        // prepare for new match and hide UI
        public void PrepareBeforeGame()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                // let client exit UI
                ExitUIClientRPC();
            }
        }

        // after a match, redisplay UI
        public void ResetAfterGame()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                // reset waiting room
                _hiderCount.Value = 0;
                _seekerCount.Value = 0;
                _waitingRoomLocked.Value = false;
                // let client enter UI
                EnterUIClientRPC();
                _selected = false;
            }
        }

        private void OnRoleChanged(IUserPresence user)
        {
            // if user switched from viewer to player
            // or from player to viewer
            if (user.Equals(_connection.MatchSelf))
            {
                if (_connection.IsViewer)
                {
                    Visible = false;
                    _selected = false;
                }
                else
                {
                    Visible = true;
                }
            }
        }

        public void UpdateRemainingTime(float time)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                _matchRemainingTime.Value = time;
                _matchTotalTime.Value = time;
            }
        }

        public void UpdateRemainingTime()
        {
            bool gamePaused = _gameManager.GamePaused;
            if (NetworkManager.Singleton.IsServer && !gamePaused)
            {
                _matchRemainingTime.Value = Mathf.Max(0.0f, _matchRemainingTime.Value - Time.deltaTime);
            }
        }

        public float RemainingTime => _matchRemainingTime.Value;

        public float DurationTime => _matchTotalTime.Value - _matchRemainingTime.Value;

        [ClientRpc]
        private void EnterUIClientRPC()
        {
            if (!_connection.IsViewer)
            {
                Visible = true;
            }
            _selected = false;
        }

        [ClientRpc]
        private void ExitUIClientRPC()
        {
            Visible = false;
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnJoinButtonServerRPC(bool isHider, ulong clientID)
        {
            if (isHider)
            {
                _hiderCount.Value++;
            }
            else
            {
                _seekerCount.Value++;
            }
            var canStartGame = _hiderCount.Value >= _matchStartNumHiders && _seekerCount.Value >= _matchStartNumSeekers && (_hiderCount.Value == _seekerCount.Value);
            OnNewPlayer?.Invoke(isHider, clientID, canStartGame);
        }

        public bool IsHider => _lastSelection;
    }
}
