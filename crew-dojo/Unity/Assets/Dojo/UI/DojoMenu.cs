using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using Unity.Netcode;
using Nakama;
using Dojo.UI.Components;

namespace Dojo.UI
{
    /// <summary>
    /// Menu UI for %Dojo networking flow
    /// </summary>
    [DefaultExecutionOrder(10000), RequireComponent(typeof(DojoMenuStatus)), RequireComponent(typeof(DojoMenuClients)), RequireComponent(typeof(DojoMenuConnect)), RequireComponent(typeof(DojoMenuMatches))]
    public class DojoMenu : MonoBehaviour
    {
        [SerializeField] private InputAction _toggleMenuAction;

        [Tooltip("Frequency (s) of client list refreshing")]
        [SerializeField] private float _clientsRefreshFreq = 2.0f;

        [Tooltip("Frequency (s) of match list refreshing")]
        [SerializeField] private float _matchesRefreshFreq = 4.0f;

        private DojoConnection _connection;
        private VisualElement _UI_Root;

        private DojoMenuStatus _statusUI;
        private DojoMenuClients _clientsUI;
        private DojoMenuConnect _connectUI;
        private DojoMenuMatches _matchesUI;

        private readonly Dictionary<IUserPresence, string> _cachedDisplayNames = new();

        /**
         * Selected AI players for feedback
         * \see \link Dojo.UI.Feedback.FeedbackInterface FeedbackInterface \endlink
         */
        public List<string> SelectedFeedbackAIPlayers => _clientsUI.SelectedAIPlayers;

        private void Awake()
        {
            _connection = FindObjectOfType<DojoConnection>();

            // dynamically add event system
            if (!FindObjectOfType<EventSystem>())
            {
                var obj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                obj.transform.parent = transform;
            }

            // load all UI components
            var document = GetComponent<UIDocument>();
            _UI_Root = document.rootVisualElement;

            _statusUI = GetComponent<DojoMenuStatus>();
            _clientsUI = GetComponent<DojoMenuClients>();
            _connectUI = GetComponent<DojoMenuConnect>();
            _matchesUI = GetComponent<DojoMenuMatches>();

            // register callbacks
            _connection.OnConnectionManagerReady += OnConnectionManagerReady;
            _connection.OnJoinedMatch += OnJoinedMatch;
            _connection.OnLeftMatch += OnLeftMatch;
            _connection.OnRoleChanged += OnRoleChanged;

            _statusUI.OnIndicatorClicked += OnIndicatorClicked;
            _statusUI.OnRoleSwitchRequested += OnRoleSwitchRequested;
            _connectUI.OnConnectButtonClicked += OnConnectButtonClicked;
            _matchesUI.OnMatchesButtonClicked += OnMatchesButtonClicked;
        }

        private void Start()
        {
            // get current IP
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                var URL = new Uri(Application.absoluteURL);
                _connectUI.IPAddress = URL.Host;
            }
            else
            {
                _connectUI.IPAddress = "127.0.0.1";
            }

            _toggleMenuAction.Enable();
        }

        private void Update()
        {
            if (_toggleMenuAction.WasPerformedThisFrame())
            {
                Visible = !Visible;
            }

            if (Visible && _connection.HasJoinedMatch && !IsInvoking(nameof(RefreshActiveClients)))
            {
                Invoke(nameof(RefreshActiveClients), _clientsRefreshFreq);
            }
            if (!_connection.HasJoinedMatch && _matchesUI.Visible && !IsInvoking(nameof(RefreshActiveMatches)))
            {
                Invoke(nameof(RefreshActiveMatches), _matchesRefreshFreq);
            }
        }

        /** Is current UI visible? */
        public bool Visible
        {
            get
            {
                return _UI_Root.style.display == DisplayStyle.Flex;
            }
            set
            {
                _UI_Root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        #region Dojo Callbacks

        private async void OnConnectionManagerReady()
        {
            // initialize UI styles
            Visible = true;
            _statusUI.Visible = true;
            _clientsUI.Visible = true;
            _connectUI.Visible = false;
            _matchesUI.Visible = false;

            _statusUI.UserName = "";
            _statusUI.MatchID = "";
            _statusUI.Connected = false;

            OnRoleChanged(_connection.MatchSelf);

            if (_connection.IsServer)
            {
                // hidden for AI agents, visible for server
                Visible = _connection.Role == DojoNetworkRole.Server;
                _connectUI.Visible = false;
                _matchesUI.Visible = false;

                try
                {
                    await _connection.ConnectNakama();
                    await _connection.JoinNakamaMatch();
                }
                catch (OperationCanceledException e)
                {
                    Debug.LogError($"Failed to join Nakama! ({e})");
                }
            }
            else
            {
                Visible = true;
                _connectUI.Visible = true;
                _matchesUI.Visible = false;
            }
        }

        private async void OnJoinedMatch()
        {
            var displayName = await _connection.QueryUserDisplayNames(new[] { _connection.MatchSelf });
            if (displayName != null && displayName.Values.Count > 0)
            {
                _statusUI.UserName = displayName.Values.First().DisplayName;
                _statusUI.MatchID = _connection.MatchID;
                _statusUI.Connected = true;
            }
        }

        private void OnLeftMatch()
        {
            try
            {
                _statusUI.UserName = "";
                _statusUI.MatchID = "";
                _statusUI.Connected = false;
                _statusUI.Role = _connection.Role;
                _connectUI.Visible = true;
                _matchesUI.Visible = false;
                _clientsUI.ResetClientsList();
                _matchesUI.ResetMatchesList();
            }
            catch { }
        }

        private void OnRoleChanged(IUserPresence p)
        {
            _statusUI.Role = _connection.Role;
        }

        #endregion Dojo Callbacks

        #region UI Callbacks

        private async void OnConnectButtonClicked()
        {
            if (string.IsNullOrEmpty(_connectUI.UserName))
            {
                return;
            }

            _connectUI.Readonly = true;

            try
            {
                await _connection.ConnectNakama(_connectUI.IPAddress, _connectUI.UserName);
                _connectUI.Visible = false;

                if (_connection.Role == DojoNetworkRole.Player)
                {
                    // join any match
                    await _connection.JoinNakamaMatch();
                }
                else if (_connection.Role == DojoNetworkRole.Viewer)
                {
                    // select match to join
                    RefreshActiveMatches();
                    _matchesUI.Visible = true;
                }
            }
            catch (OperationCanceledException e)
            {
                Debug.LogError($"Failed to join Nakama! ({e})");
            }
            finally
            {
                _connectUI.Readonly = false;
            }
        }

        private void OnRoleSwitchRequested(DojoNetworkRole role)
        {
            _connection.SwitchRole(role);
        }

        private async void OnMatchesButtonClicked()
        {
            _matchesUI.Readonly = true;

            try
            {
                await _connection.JoinNakamaMatch(_matchesUI.SelectedMatchID);
                _matchesUI.Visible = false;
            }
            catch (OperationCanceledException e)
            {
                Debug.LogError($"Failed to join Nakama! ({e})");
            }
            finally
            {
                _matchesUI.Readonly = false;
            }
        }

        private async void OnIndicatorClicked()
        {
            // server is not allowed to leave match this way
            if (_connection.HasJoinedMatch && (_connection.Role != DojoNetworkRole.Server))
            {
                if (NetworkManager.Singleton != null &&
                    (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient))
                {
                    NetworkManager.Singleton.Shutdown();
                }
                else
                {
                    await _connection.LeaveNakamaMatch();
                }
            }
        }

        #endregion UI Callbacks

        #region Refresh Loops

        private async void RefreshActiveClients()
        {
            if (_connection.HasJoinedMatch)
            {
                var matchClients = _connection.MatchClients;

                // remove left player names
                _cachedDisplayNames.Keys
                    .Where(p => !matchClients.ContainsKey(p)).ToList()
                    .ForEach(p => _cachedDisplayNames.Remove(p));

                // query joined player names
                var toQuery = matchClients.Keys.Where(p => !_cachedDisplayNames.ContainsKey(p)).ToList();
                if (toQuery.Count > 0)
                {
                    var result = await _connection.QueryUserDisplayNames(toQuery);
                    if (result != null)
                    {
                        toQuery.ForEach(p => _cachedDisplayNames[p] = result[p].DisplayName);
                    }
                }

                // create and sort users (AI first, then humans)
                var clients = matchClients
                    .Where(pair => _cachedDisplayNames.ContainsKey(pair.Key))
                    .Select(pair => Tuple.Create(pair.Key, _cachedDisplayNames[pair.Key], pair.Value))
                    .ToList();

                _clientsUI.UpdateActiveClients(clients, _connection.MatchAIPlayers.ToList());

                Invoke(nameof(RefreshActiveClients), _clientsRefreshFreq);
            }
        }

        private async void RefreshActiveMatches()
        {
            if (!_connection.HasJoinedMatch)
            {
                var activeMatches = await _connection.QueryActiveMatches();

                if (activeMatches != null)
                {
                    // create and sort matches (by player numbers)
                    var matches = activeMatches.OrderBy(m => m.NumPlayers).ToList();

                    _matchesUI.UpdateActiveMatches(matches);
                }

                Invoke(nameof(RefreshActiveMatches), _matchesRefreshFreq);
            }
        }

        #endregion Refresh Loops
    }
}
