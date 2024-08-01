using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Dojo;

namespace Examples.HideAndSeek
{
    // joinleave displays UI for each individual player

    [RequireComponent(typeof(UIDocument))]
    public class JoinLeave : NetworkBehaviour
    {
        private const string LOGSCOPE = "JoinLeave";

        private VisualElement _selectInterface;
        private Button _joinHiderButton;
        private Button _joinSeekerButton;

        private DojoConnection _connection;
        public event Action<bool> OnNewPlayer;

        private float _matchRemainingTime = 0.0f;
        private float _matchTotalTime = 0.0f;

        private readonly NetworkVariable<int> _hiderCount = new(0);
        private readonly NetworkVariable<int> _seekerCount = new(0);

        public int HiderCount => _hiderCount.Value;
        public int SeekerCount => _seekerCount.Value;
        private bool _lastSelection = false;

        private GameManager _gameManager;

        public bool Visible
        {
            get
            {
                return _selectInterface.style.display == DisplayStyle.Flex;
            }
            set
            {
                _selectInterface.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void Awake()
        {
            _connection = FindObjectOfType<DojoConnection>();
            _selectInterface = GetComponent<UIDocument>().rootVisualElement;

            _joinHiderButton = _selectInterface.Q<Button>("Hider");
            _joinSeekerButton = _selectInterface.Q<Button>("Seeker");

            _gameManager = GameManager.Instance;
        }

        private void Start()
        {
            if (GameManager.Instance.HostType == GameHostType.JoinLeave)
            {
                _joinHiderButton.clickable.clicked += () =>
                {
                    JoinGame(true);
                };
                _joinSeekerButton.clickable.clicked += () =>
                {
                    JoinGame(false);
                };
            }

            Visible = _connection.IsPlayer;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.HostType == GameHostType.JoinLeave)
            {
                Visible = _connection.IsPlayer && GameManager.Instance.Stage == GameStage.IDLE;
            }
        }

        public void UpdateRemainingTime(float time)
        {
            if (NetworkManager.Singleton.IsClient)
            {
                _matchRemainingTime = time;
                _matchTotalTime = time;
            }
        }

        public void UpdateRemainingTime()
        {
            bool gamePaused = _gameManager.GamePaused;
            if (NetworkManager.Singleton.IsClient && !gamePaused)
            {
                _matchRemainingTime = Mathf.Max(0.0f, _matchRemainingTime - Time.deltaTime);
            }
        }

        public float RemainingTime => _matchRemainingTime;

        public float DurationTime => _matchTotalTime - _matchRemainingTime;

        public void JoinGame(bool isHider)
        {
            Debug.Log($"{LOGSCOPE}: JoinGame ({isHider})");
            if (!_joinHiderButton.enabledSelf || !_joinSeekerButton.enabledSelf)
            {
                return;
            }
            if (NetworkManager.Singleton.IsClient)
            {
                OnNewPlayer?.Invoke(isHider);
                UpdatePlayerCountServerRpc(isHider, 1);
                _lastSelection = isHider;
                _joinHiderButton.SetEnabled(false);
                _joinSeekerButton.SetEnabled(false);
            }
        }

        public void ResetAfterGame()
        {
            if (NetworkManager.Singleton.IsClient)
            {
                _joinHiderButton.SetEnabled(true);
                _joinSeekerButton.SetEnabled(true);
                UpdatePlayerCountServerRpc(_lastSelection, -1);
            }
        }

        public bool IsHider => _lastSelection;

        [ServerRpc(RequireOwnership = false)]
        private void UpdatePlayerCountServerRpc(bool isHider, int offset)
        {
            if (isHider)
            {
                _hiderCount.Value = Math.Max(0, _hiderCount.Value + offset);
            }
            else
            {
                _seekerCount.Value = Math.Max(0, _seekerCount.Value + offset);
            }
        }
    }
}
