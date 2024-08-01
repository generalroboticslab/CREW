using System;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using Nakama;
using Nakama.TinyJson;
using Dojo.Nakama;

namespace Dojo
{
    /// <summary>
    /// Manage networking connections, matches, and user presences
    /// </summary>
    public class DojoConnection : MonoBehaviour
    {
        private const string LOGSCOPE = "DojoConnectionManager";

        [Header("General")]
        [SerializeField] private bool _isClient = true;

        /** Whether if current user is Client */
        public bool IsClient => _isClient;

        /**
         * Whether if current user is Server\n
         * \link Dojo.DojoNetworkRole.Server DojoNetworkRole.Server \endlink */
        public bool IsServer => !_isClient;

        /** User current role in %Dojo network */
        public DojoNetworkRole Role { get; private set; } = DojoNetworkRole.Viewer;

        /**
         * Whether if current user is Human Viewer\n
         * \link Dojo.DojoNetworkRole.Viewer DojoNetworkRole.Viewer \endlink */
        public bool IsViewer => Role == DojoNetworkRole.Viewer;

        /**
         * Whether if current user is Human Player\n
         * \link Dojo.DojoNetworkRole.Player DojoNetworkRole.Player \endlink */
        public bool IsPlayer => Role == DojoNetworkRole.Player;

        /** Invoked when \link Dojo.DojoConnection DojoConnection \endlink is ready */
        public event Action OnConnectionManagerReady;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RegisterUnload(string objectName);
#endif

        private void Awake()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            var args = Environment.GetCommandLineArgs();
            if (!IsClient)
            {
                for (var idx = 0; idx < args.Length; ++idx)
                {
                    var arg = args[idx];
                    // get unique ID
                    if (arg.Equals("-NakamaID") && idx < args.Length - 1)
                    {
                        _uniqueServerID = args[idx + 1];
                        ++idx;
                    }
                }
            }
            else
            {
                var screenSize = new Vector2Int(Screen.width, Screen.height);
                var screenPos = Screen.mainWindowPosition;
                var displayID = 0;

                for (var idx = 0; idx < args.Length; ++idx)
                {
                    var arg = args[idx];
                    // check for screen size
                    if (arg.Equals("-DojoScreenSize") && idx < args.Length - 1 && args[idx + 1].Contains("x"))
                    {
                        var widthHeight = args[idx + 1].Split("x");
                        if (widthHeight.Length == 2 && int.TryParse(widthHeight[0], out var width) && int.TryParse(widthHeight[1], out var height))
                        {
                            screenSize.x = width;
                            screenSize.y = height;
                            ++idx;
                        }
                    }
                    // check for screen position
                    else if (arg.Equals("-DojoScreenPos") && idx < args.Length - 1 && args[idx + 1].Contains(","))
                    {
                        var posXY = args[idx + 1].Split(",");
                        if (posXY.Length == 2 && int.TryParse(posXY[0], out var posX) && int.TryParse(posXY[1], out var posY))
                        {
                            screenPos.x = posX;
                            screenPos.y = posY;
                            ++idx;
                        }
                    }
                    // check for display monitor index
                    else if (arg.Equals("-DojoMonitorID") && idx < args.Length - 1 && int.TryParse(args[idx + 1], out var monitorID))
                    {
                        displayID = monitorID;
                        ++idx;
                    }
                }

                var displays = new List<DisplayInfo>();
                Screen.GetDisplayLayout(displays);
                if (!Screen.fullScreen)
                {
                    if (screenSize.x > 0 && screenSize.y > 0)
                    {
                        Screen.SetResolution(screenSize.x, screenSize.y, false);
                    }
                    if (displays.Count > displayID)
                    {
                        Screen.MoveMainWindowTo(displays[displayID], screenPos);
                    }
                }
            }
#endif

            Debug.Assert(FindObjectsOfType<DojoConnection>().Length == 1, "Only one connection manager is allowed!");

            if (!FindObjectOfType<Dispatcher>())
            {
                var dispatcher = new GameObject("[Dispatcher]");
                dispatcher.AddComponent<Dispatcher>();
            }

            if (!IsClient)
            {
                Role = DojoNetworkRole.Server;
            }
        }

        private void Start()
        {
            if (!IsClient)
            {
                Invoke(nameof(PingInstanceServer), 0.0f);
                Invoke(nameof(PingClients), 0.0f);
            }
            OnConnectionManagerReady?.Invoke();

#if UNITY_WEBGL && !UNITY_EDITOR
            RegisterUnload(name);
#endif
        }

        private void Update()
        {
            if (HasJoinedMatch && !IsClient && !IsInvoking(nameof(ReportMatchStateInNakama)))
            {
                Invoke(nameof(ReportMatchStateInNakama), 0.0f);
            }
        }

        private async void OnApplicationQuit()
        {
            await LeaveNakamaMatch(true);
        }

        #region Nakama Connection

        [Header("Nakama Connection")]
        [SerializeField] private NakamaConfig _nakamaConfig;

        [Tooltip("Connection timeout in milliseconds")]
        [SerializeField, Min(1)] private int _nakamaConnectionTimeout = 5000;

        [Tooltip("Maximum retry if joining a match failed")]
        [SerializeField, Min(0)] private int _nakamaJoinMatchMaxRetry = 10;

        [Tooltip("Update match info on Nakama every N seconds")]
        [SerializeField, Min(0.0f)] private float _nakamaReportFrequency = 0.5f;

        private IClient _nakamaClient;
        private ISession _nakamaSession;
        private ISocket _nakamaSocket;
        private IMatch _nakamaMatch;
        private int _joinMatchRetry;

        /** Invoked when current user joined a new match on Nakama */
        public event Action OnJoinedMatch;

        /** Invoked when current user left current match on Nakama */
        public event Action OnLeftMatch;

        /**
         * Invoked a user on %Dojo network has changed its \link Dojo.DojoConnection.Role Role \endlink \n
         * Only valid when current user has joined a Nakama match
         * @param[in] user The user (Nakama.IUserPresence) that has changed
         */
        public event Action<IUserPresence> OnRoleChanged; // callback for role changed

        /** Has connected to Nakama framework? */
        public bool HasConnected => _nakamaSocket?.IsConnected == true;

        /** Has joined a match on Nakama framework? */
        public bool HasJoinedMatch => HasConnected && _nakamaMatch != null;

        /**
         * Invoked when a remote user has joined current Nakama match \n
         * Only valid when current user has joined a Nakama match
         * @param[in] user The user (Nakama.IUserPresence) that has joined
         */
        public event Action<IUserPresence> OnMatchPlayerJoined;

        /**
         * Invoked when a remote user has left current Nakama match \n
         * Only valid when current user has joined a Nakama match
         * @param[in] user The user (Nakama.IUserPresence) that has left
         */
        public event Action<IUserPresence> OnMatchPlayerLeft;

        // mapping from opcode to callback function
        private readonly Dictionary<long, Action<DojoMessage>> _remoteMessages = new();

        /**
         * Current user (Nakama.IUserPresence) in Nakama match\n
         * <code>null</code> if has not joined a match
         */
        public IUserPresence MatchSelf => _nakamaMatch?.Self;

        /**
         * Server user (Nakama.IUserPresence) in Nakama match\n
         * <code>null</code> if has not joined a match
         */
        public IUserPresence MatchServer { get; private set; }

        /**
         * All current users in Nakama match\n
         * Mapping from user (Nakama.IUserPresence) to \link Dojo.DojoNetworkRole DojoNetworkRole \endlink
         */
        public readonly Dictionary<IUserPresence, DojoNetworkRole> MatchClients = new();

        private readonly HashSet<IUserPresence> _matchPresences = new();
        private uint _matchClientsVersion;

        public readonly HashSet<string> MatchAIPlayers = new();

        /**
         * Current Nakama match label\n
         * <code>null</code> if has not joined a match
         */
        public string MatchLabel => _nakamaMatch?.Label;

        /**
         * Current Nakama match unique ID\n
         * <code>null</code> if has not joined a match
         */
        public string MatchID => _nakamaMatch?.Id;

        /// <summary>
        /// Subscribe remote message callback in Nakama match
        /// </summary>
        /// <param name="opCode">message type identifier</param>
        /// <param name="callback">callback function for registering</param>
        /// <param name="append">force only one callback for this message or not</param>
        public void SubscribeRemoteMessages(long opCode, Action<DojoMessage> callback, bool append = true)
        {
            if (_remoteMessages.ContainsKey(opCode) && append)
            {
                _remoteMessages[opCode] += callback;
            }
            else
            {
                _remoteMessages[opCode] = callback;
            }
        }

        /// <summary>
        /// Unsubscribe remote message callback in Nakama match
        /// </summary>
        /// <param name="opCode">message type identifier</param>
        /// <param name="callback">same callback function used in \link Dojo.DojoConnection.SubscribeRemoteMessages SubscribeRemoteMessages \endlink</param>
        public void UnsubscribeRemoteMessages(long opCode, Action<DojoMessage> callback)
        {
            if (_remoteMessages.ContainsKey(opCode))
            {
                _remoteMessages[opCode] -= callback;
            }
        }

        /// <summary>
        /// Connect to Nakama framework and register a new user.\n
        /// If \p address is empty, use IP address in \link Dojo.Nakama.NakamaConfig NakamaConfig \endlink.\n
        /// Empty \p username is only allowed when current role is Server or AI Player
        /// </summary>
        /// <param name="address">running Nakama framework address</param>
        /// <param name="username">username on Nakama framework</param>
        /// <returns>\p Task to be awaited</returns>
        public async Task ConnectNakama(string address = "", string username = "")
        {
            // prepare player ID
            var uniqueID = Guid.NewGuid().ToString();

            // create client
            _nakamaClient = new Client(_nakamaConfig.Scheme, string.IsNullOrEmpty(address) ? _nakamaConfig.Host : address, _nakamaConfig.Port, _nakamaConfig.ServerKey, UnityWebRequestAdapter.Instance);

            // prepare cancellation token
            var token = new CancellationTokenSource(_nakamaConnectionTimeout);

            if (!IsClient)
            {
                username = "Server " + uniqueID[..8];
            }
            else if (string.IsNullOrEmpty(username))
            {
                return;
            }

            // create session
            _nakamaSession = await _nakamaClient.AuthenticateDeviceAsync(uniqueID, canceller: token.Token);

            // establish socket
            _nakamaSocket = _nakamaClient.NewSocket();
            await _nakamaSocket.ConnectAsync(_nakamaSession, appearOnline: true, connectTimeout: _nakamaConnectionTimeout / 1000);


            // update account username
            if (!string.IsNullOrEmpty(username))
            {
                await _nakamaClient.UpdateAccountAsync(_nakamaSession, _nakamaSession.Username, displayName: username);
            }

            // if session is expired, refresh
            if (_nakamaSession.IsExpired)
            {
                // prepare cancellation token
                token = new CancellationTokenSource(_nakamaConnectionTimeout);
                _nakamaSession = await _nakamaClient.SessionRefreshAsync(_nakamaSession, canceller: token.Token);
            }

            _joinMatchRetry = _nakamaJoinMatchMaxRetry;
        }

        /// <summary>
        /// Join a Nakama match.\n
        /// If \p targetMatch is empty, invoke \link Dojo.Nakama.RPCJoinOrNewMatchPayload RPC \endlink to allocate a new server to host a match.\n
        /// If current \link Dojo.DojoConnection.Role Role \endlink is Server, ignore \p targetMatch and create a new match regardless.\n
        /// This function will try to find and join a Nakama match within a maximum number of retries
        /// </summary>
        /// <param name="targetMatch">target match ID</param>
        /// <returns>\p Task to be awaited</returns>
        public async Task JoinNakamaMatch(string targetMatch = "")
        {
            _joinMatchRetry--;
            if (_joinMatchRetry <= 0)
            {
                return;
            }

            _nakamaMatch = null;
            MatchServer = null;
            MatchClients.Clear();
            _matchPresences.Clear();
            _matchClientsVersion = 0;

            if (IsClient)
            {
                // if client mode, request for a match
                if (Role == DojoNetworkRole.Viewer)
                {
                    // if viewer, can join any match
                    if (string.IsNullOrEmpty(targetMatch))
                    {
                        // empty target, request a new match
                        await JoinOrFindMatch(true);
                    }
                    else
                    {
                        _nakamaMatch = await _nakamaSocket.JoinMatchAsync(targetMatch);
                        foreach (var presence in _nakamaMatch.Presences)
                        {
                            _matchPresences.Add(presence);
                        }
                    }
                }
                else
                {
                    await JoinOrFindMatch();
                }
            }
            else
            {
                // if server mode, create new match
                try
                {
                    _nakamaMatch = await _nakamaSocket.CreateMatchAsync($"{_nakamaConfig.GameTag}:{_uniqueServerID}:{Time.time}");
                    // create match storage
                    await SendNakamaRPC<RPCCreateMatchPayload>(
                        NakamaRPC.CreateMatch,
                        new()
                        {
                            ServerId = _nakamaSession.UserId,
                            MatchId = _nakamaMatch.Id,
                            GameTag = _nakamaConfig.GameTag,
                            MaxNumPlayers = _nakamaConfig.MaxNumPlayers
                        }
                    );
                    foreach (var presence in _nakamaMatch.Presences)
                    {
                        _matchPresences.Add(presence);
                    }
                    MatchServer = MatchSelf;
                }
                catch (OperationCanceledException)
                {
                    Debug.LogError($"{LOGSCOPE}: JoinNakama failed to create match!");
                }
            }

            if (HasJoinedMatch)
            {
                var dispatcher = Dispatcher.Instance;

                // subscribe presence update
                _nakamaSocket.ReceivedMatchPresence += m => dispatcher.Enqueue(() => OnNakamaPresence(m));
                // subscribe state messages
                _nakamaSocket.ReceivedMatchState += m => dispatcher.Enqueue(() => OnNakamaMatchState(m));

                SubscribeRemoteMessages((long)NakamaOpCode.HelloFromClient, OnRemoteClient, false);
                SubscribeRemoteMessages((long)NakamaOpCode.HelloFromServer, OnRemoteServer, false);
                SubscribeRemoteMessages((long)NakamaOpCode.UpdateClients, OnRemoteUpdateClients, false);
                SubscribeRemoteMessages((long)NakamaOpCode.SwitchRole, OnRemoteSwitchRole, false);

                SubscribeRemoteMessages((long)NakamaOpCode.RTTSync, OnRTTSync, false);
                SubscribeRemoteMessages((long)NakamaOpCode.RTTAck, OnRTTAck, false);
                SubscribeRemoteMessages((long)NakamaOpCode.RTTAckSync, OnRTTAckSync, false);

                DeclareIdentity();

                if (!IsClient)
                {
                    OnJoinedMatch?.Invoke();
                }
            }
            else
            {
                Debug.LogError($"{LOGSCOPE}: JoinNakama failed to join match!");
            }
        }

        // client join or find match
        private async Task JoinOrFindMatch(bool forceCreate = false)
        {
            if (!IsClient)
            {
                return;
            }

            var matchId = "";
            // if not viewer, need to join or find valid match
            while (string.IsNullOrEmpty(matchId))
            {
                try
                {
                    var result = await SendNakamaRPC<RPCJoinOrNewMatchPayload>(
                        NakamaRPC.JoinOrNewMatch,
                        new()
                        {
                            GameTag = _nakamaConfig.GameTag,
                            MaxNumPlayers = forceCreate ? 0 : _nakamaConfig.MaxNumPlayers,
                        }
                    );
                    matchId = result.Payload;

                    if (string.IsNullOrEmpty(matchId))
                    {
                        // if empty, meaning a new server is going to be allocated
                        // wait for next RPC
                        await Task.Delay(_nakamaConnectionTimeout);
                    }
                    else
                    {
                        // otherwise, join match
                        _nakamaMatch = await _nakamaSocket.JoinMatchAsync(matchId);
                        foreach (var presence in _nakamaMatch.Presences)
                        {
                            _matchPresences.Add(presence);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // a timeout, then break
                    break;
                }

                // reset force create flag
                forceCreate = false;
            }
        }

        /// <summary>
        /// Disconnect and leave a Nakama match.\n
        /// Leave default parameters unchanged when using in public code
        /// </summary>
        /// <param name="removeSelf">delete self presence on Nakama framework or not</param>
        /// <param name="reset">reset internal states or not</param>
        /// <returns>\p Task to be awaited</returns>
        public async Task LeaveNakamaMatch(bool removeSelf = true, bool reset = true)
        {
            if (HasConnected)
            {
                if (!IsClient)
                {
                    foreach (var p in _matchPresences)
                    {
                        RemoveUserAccount(p.UserId);
                    }
                    // clean up match storage
                    _ = SendNakamaRPC<RPCCleanMatchPayload>(
                        NakamaRPC.CleanMatch,
                        new()
                        {
                            ServerId = _nakamaSession.UserId,
                            GameTag = _nakamaConfig.GameTag
                        }
                    );
                }
                if (removeSelf)
                {
                    RemoveUserAccount(_nakamaSession.UserId);
                }
                if (HasJoinedMatch)
                {
                    await _nakamaSocket.LeaveMatchAsync(_nakamaMatch);
                    if (reset)
                    {
                        _nakamaMatch = null;
                        Role = DojoNetworkRole.Viewer;
                        OnLeftMatch?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Send state message in current Nakama match.\n
        /// Ignore if not connected to a match
        /// </summary>
        /// <param name="opCode">message type identifier</param>
        /// <param name="message">message content</param>
        /// <param name="target">target user on Nakama</param>
        /// <returns>\p Task to be awaited</returns>
        public Task SendStateMessage(long opCode, string message, IUserPresence target)
        {
            return SendStateMessage(opCode, Encoding.UTF8.GetBytes(message), target);
        }

        /// <summary>
        /// Send state message in current Nakama match.\n
        /// Ignore if not connected to a match
        /// </summary>
        /// <param name="opCode">message type identifier</param>
        /// <param name="message">message bytes</param>
        /// <param name="target">target user on Nakama</param>
        /// <returns>\p Task to be awaited</returns>
        public Task SendStateMessage(long opCode, ArraySegment<byte> message, IUserPresence target)
        {
            return SendStateMessage(opCode, message, broadcast: false, targets: new[] { target });
        }

        /// <summary>
        /// Send state message in current Nakama match.\n
        /// Ignore if not connected to a match.\n
        /// If \p broadcast, this message will be sent to all users, regardless of \p targets.\n
        /// If \p targets is \p null and \link Dojo.DojoConnection.IsClient IsClient \endlink is \p true,
        /// send this message to current \link Dojo.DojoConnection.MatchServer Server \endlink.\n
        /// If \p targets is \p null and \link Dojo.DojoConnection.IsServer IsServer \endlink is \p true,
        /// send this message to all clients in the match
        /// </summary>
        /// <param name="opCode">message type identifier</param>
        /// <param name="message">message content</param>
        /// <param name="broadcast">broadcast this message or not</param>
        /// <param name="targets">array of target users to send this message to</param>
        /// <returns>\p Task to be awaited</returns>
        public Task SendStateMessage(long opCode, string message, bool broadcast = false, IEnumerable<IUserPresence> targets = null)
        {
            return SendStateMessage(opCode, Encoding.UTF8.GetBytes(message), broadcast, targets);
        }

        /// <summary>
        /// Send state message in current Nakama match.\n
        /// Ignore if not connected to a match.\n
        /// If \p broadcast, this message will be sent to all users, regardless of \p targets.\n
        /// If \p targets is \p null and \link Dojo.DojoConnection.IsClient IsClient \endlink is \p true,
        /// send this message to current \link Dojo.DojoConnection.MatchServer Server \endlink.\n
        /// If \p targets is \p null and \link Dojo.DojoConnection.IsServer IsServer \endlink is \p true,
        /// send this message to all clients in the match
        /// </summary>
        /// <param name="opCode">message type identifier</param>
        /// <param name="message">message bytes</param>
        /// <param name="broadcast">broadcast this message or not</param>
        /// <param name="targets">array of target users to send this message to</param>
        /// <returns>\p Task to be awaited</returns>
        public Task SendStateMessage(long opCode, ArraySegment<byte> message, bool broadcast = false, IEnumerable<IUserPresence> targets = null)
        {
            if (HasJoinedMatch)
            {
                if (broadcast)
                {
                    return _nakamaSocket.SendMatchStateAsync(_nakamaMatch.Id, opCode, message);
                }
                else if (targets != null || MatchServer == null)
                {
                    return _nakamaSocket.SendMatchStateAsync(_nakamaMatch.Id, opCode, message, targets);
                }
                else
                {
                    targets = IsClient ? new List<IUserPresence>() { MatchServer } : MatchClients.Keys.ToList();
                    return _nakamaSocket.SendMatchStateAsync(_nakamaMatch.Id, opCode, message, targets);
                }
            }
            else
            {
                Debug.LogWarning($"{LOGSCOPE}: SendStateMessage new state discarded!");
                return Task.CompletedTask;
            }
        }

        // send RPC to Nakama server
        private async Task<IApiRpc> SendNakamaRPC<T>(string rpc, T payload)
        {
            if (_nakamaSession != null)
            {
                return await _nakamaClient.RpcAsync(_nakamaSession, rpc, JsonWriter.ToJson(payload));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Register number of AI players in the match
        /// </summary>
        /// <param name="aiPlayerNames">name of the AI players</param>
        /// <returns>size of registered AI players</returns>
        public int RegisterAIPlayers(List<string> aiPlayerNames)
        {
            if (IsServer)
            {
                var maxSize = Math.Min(aiPlayerNames.Count, _nakamaConfig.MaxNumPlayers - MatchAIPlayers.Count);
                for (var i = 0; i < maxSize; ++i)
                {
                    MatchAIPlayers.Add(aiPlayerNames[i]);
                }

                // notify the clients
                var toSend = EncodeMatchClientsPayload();
                SendStateMessage((long)NakamaOpCode.UpdateClients, toSend);

                return MatchAIPlayers.Count;
            }
            return 0;
        }

        /// <summary>
        /// Get active matches on Nakama.\n
        /// Used in \link Dojo.UI.DojoMenu DojoMenu \endlink
        /// </summary>
        /// <returns>list of match info</returns>
        public async Task<List<MatchStorageData>> QueryActiveMatches()
        {
            if (HasConnected)
            {
                var response = await SendNakamaRPC<RPCQueryMatchesPayload>(
                    NakamaRPC.QueryMatches,
                    new()
                    {
                        GameTag = _nakamaConfig.GameTag,
                        MaxNumPlayers = -1,
                        MaxNumRecords = 100
                    }
                );
                if (string.IsNullOrEmpty(response.Payload))
                {
                    return null;
                }
                var records = JsonParser.FromJson<List<string>>(response.Payload);
                if (records == null)
                {
                    return null;
                }
                else
                {
                    return records.Select(r => JsonParser.FromJson<MatchStorageData>(r)).ToList();
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Get user profile on Nakama by user presence.\n
        /// Used in \link Dojo.UI.DojoMenu DojoMenu \endlink
        /// </summary>
        /// <param name="users">list of users to query</param>
        /// <returns>a mapping from user presence to user profile info</returns>
        public async Task<Dictionary<IUserPresence, IApiUser>> QueryUserDisplayNames(IEnumerable<IUserPresence> users)
        {
            if (HasConnected)
            {
                var result = await _nakamaClient.GetUsersAsync(_nakamaSession, users.Select(u => u.UserId).ToList());
                // check if output is valid
                if (result == null || result.Users.Select(u => u.Id).Except(users.Select(u => u.UserId)).Count() > 0)
                {
                    Debug.LogWarning($"{LOGSCOPE}: QueryUserDisplayNames failed to query by GetUsersAsync");
                    return null;
                }
                // output order is not the same, build a new dictionary here
                var inputMapping = users.ToDictionary(u => u.UserId, u => u);
                var outputMapping = result.Users.ToDictionary(u => inputMapping[u.Id], u => u);
                return outputMapping;
            }
            else
            {
                return null;
            }
        }

        // callback for match presence update
        private void OnNakamaPresence(IMatchPresenceEvent e)
        {
            foreach (var presence in e.Joins)
            {
                _matchPresences.Add(presence);
                OnMatchPlayerJoined?.Invoke(presence);
            }

            foreach (var presence in e.Leaves)
            {
                OnMatchPlayerLeft?.Invoke(presence);
                _matchPresences.Remove(presence);

                if (!IsClient && MatchClients.ContainsKey(presence))
                {
                    RemoveUserAccount(presence.UserId);
                }
                else if (IsClient && MatchServer.Equals(presence))
                {
                    // if server has left, remove self from match
                    _ = LeaveNakamaMatch();
                }
                MatchClients.Remove(presence);
            }

            // for server, declare its identity to all
            if (!IsClient)
            {
                DeclareIdentity();

                if (e.Joins.Count() > 0)
                {
                    // here we treat all incoming clients as players, to reduce delay caused by server competition at launch time
                    ReportMatchStateInNakama(e.Joins.Count());
                }
            }
        }

        // callback for match state update
        private void OnNakamaMatchState(IMatchState e)
        {
            var message = new DojoMessage(e);
            var opCode = e.OpCode;
            if (_remoteMessages.TryGetValue(opCode, out var action))
            {
                action?.Invoke(message);
            }
        }

        // broadcast identity
        private void DeclareIdentity()
        {
            if (IsClient)
            {
                // send desired role to server for validation
                SendStateMessage((long)NakamaOpCode.HelloFromClient, Role.ToString(), true);
            }
            else
            {
                SendStateMessage((long)NakamaOpCode.HelloFromServer, "Hello", true);
            }
        }

        // callback for NakamaOpCode.HelloFromServer
        private void OnRemoteServer(DojoMessage m)
        {
            if (IsClient && !m.Sender.Equals(MatchServer))
            {
                MatchServer = m.Sender;

                // if local clients not set, send identity to server
                if (MatchClients.Count == 0)
                {
                    DeclareIdentity();
                }
            }
        }

        // callback for NakamaOpCode.HelloFromClient
        private void OnRemoteClient(DojoMessage m)
        {
            // for server recieving, declare identity again
            if (!IsClient && !MatchClients.ContainsKey(m.Sender))
            {
                DeclareIdentity();

                // validate client state
                var currentPlayerCount = MatchClients.Values.Where(role => role != DojoNetworkRole.Viewer).Count() + MatchAIPlayers.Count;
                if (Enum.TryParse<DojoNetworkRole>(m.GetString(), out var role))
                {
                    if (role == DojoNetworkRole.Viewer || currentPlayerCount < _nakamaConfig.MaxNumPlayers)
                    {
                        MatchClients[m.Sender] = role;
                        _matchClientsVersion++;
                        ReportMatchStateInNakama(0); // update match info
                    }
                }

                var toSend = EncodeMatchClientsPayload();

                if (MatchClients.ContainsKey(m.Sender))
                {
                    // notify all
                    SendStateMessage((long)NakamaOpCode.UpdateClients, toSend, broadcast: true);
                }
                else
                {
                    // send to sender only
                    SendStateMessage((long)NakamaOpCode.UpdateClients, toSend, targets: new List<IUserPresence>() { m.Sender });
                }
            }
        }

        // callback for NakamaOpCode.UpdateClients
        private async void OnRemoteUpdateClients(DojoMessage m)
        {
            if (IsClient)
            {
                var playersWithVersions = m.GetDecodedData<List<string>>();
                var newVersion = uint.Parse(playersWithVersions[0]);

                if (newVersion <= _matchClientsVersion)
                {
                    // ignore old versions
                    return;
                }
                else
                {
                    _matchClientsVersion = newVersion;
                }

                var currentPlayers = JsonParser.FromJson<Dictionary<string, DojoNetworkRole>>(playersWithVersions[1]);
                if (currentPlayers.ContainsKey(MatchSelf.SessionId))
                {
                    var currentAIPlayers = JsonParser.FromJson<List<string>>(playersWithVersions[2]);
                    foreach (var player in currentAIPlayers)
                    {
                        MatchAIPlayers.Add(player);
                    }

                    if (!MatchClients.ContainsKey(MatchSelf))
                    {
                        // if current match does not contain self
                        // and new match contains self
                        // means player is authenticated by server
                        OnJoinedMatch?.Invoke();
                    }

                    var role = currentPlayers[MatchSelf.SessionId];

                    MatchClients.Clear();
                    MatchClients[MatchSelf] = role;

                    while (_matchPresences.Count() < currentPlayers.Count())
                    {
                        await Task.Delay(100);
                    }

                    foreach (var presence in _matchPresences)
                    {
                        if (currentPlayers.ContainsKey(presence.SessionId))
                        {
                            MatchClients[presence] = currentPlayers[presence.SessionId];
                        }
                    }

                    // update role
                    if (Role != role)
                    {
                        Role = role;
                        OnRoleChanged?.Invoke(MatchSelf);
                    }
                }
                else
                {
                    // if not found, server does not validate joining the match
                    // usually caused by overflowed number of players in a match
                    // thus try again to create new match
                    await LeaveNakamaMatch(false, false);
                    _ = JoinNakamaMatch();
                }
            }
        }

        // send RPC to delete user account
        private void RemoveUserAccount(string userId)
        {
            _ = SendNakamaRPC<RPCRemoveUserAccountPayload>(
                NakamaRPC.RemoveUserAccount,
                new()
                {
                    UserId = userId,
                }
            );
        }

        // encode current match clients as payload
        private byte[] EncodeMatchClientsPayload()
        {
            Dictionary<string, DojoNetworkRole> toEncode = new();
            foreach (var pair in MatchClients)
            {
                toEncode[pair.Key.SessionId] = pair.Value;
            }

            // broadcast current clients
            string[] playersWithVersion = new string[]
            {
                _matchClientsVersion.ToString(),
                JsonWriter.ToJson(toEncode),
                JsonWriter.ToJson(MatchAIPlayers.ToList()),
            };

            return Encoding.UTF8.GetBytes(JsonWriter.ToJson(playersWithVersion));
        }

        private async void ReportMatchStateInNakama(int fakeIncrease)
        {
            if (HasJoinedMatch && !IsClient)
            {
                await SendNakamaRPC<RPCUpdateMatchPayload>(
                    NakamaRPC.UpdateMatch,
                    new()
                    {
                        ServerId = _nakamaSession.UserId,
                        GameTag = _nakamaConfig.GameTag,
                        NumPlayers = MatchClients.Where(p => p.Value != DojoNetworkRole.Viewer).Count() + fakeIncrease + MatchAIPlayers.Count(),
                        NumClients = MatchClients.Count() + MatchAIPlayers.Count(),
                    }
                );
            }
        }

        // report current match state in nakama storage
        private async void ReportMatchStateInNakama()
        {
            if (HasJoinedMatch && !IsClient)
            {
                await SendNakamaRPC<RPCUpdateMatchPayload>(
                    NakamaRPC.UpdateMatch,
                    new()
                    {
                        ServerId = _nakamaSession.UserId,
                        GameTag = _nakamaConfig.GameTag,
                        NumPlayers = MatchClients.Where(p => p.Value != DojoNetworkRole.Viewer).Count() + MatchAIPlayers.Count(),
                        NumClients = MatchClients.Count() + MatchAIPlayers.Count()
                    }
                );
                Invoke(nameof(ReportMatchStateInNakama), _nakamaReportFrequency);
            }
        }

        #endregion Nakama Connection

        #region Role Management

        private readonly Dictionary<string, Func<DojoNetworkRole, DojoNetworkRole, bool>> _switchRoleCustomRules = new();

        /// <summary>
        /// Register custom switch role rules.\n
        /// The rules get executed when a user tries to switch to another role
        /// </summary>
        /// <param name="checkFunction">check function, given current role and target role, return the switch is allowed or not</param>
        /// <param name="name">check function id, using same name will overwrite previous rule</param>
        public void RegisterSwitchRoleRule(Func<DojoNetworkRole, DojoNetworkRole, bool> checkFunction, string name = "default")
        {
            _switchRoleCustomRules.Add(name, checkFunction);
        }

        /// <summary>
        /// Try to switch current role.\n
        /// Only allowed when \link Dojo.DojoConnection.IsClient IsClient \endlink is \p true.\n
        /// If has not joined a match, the switch is executed immediately.\n
        /// Otherwise, sends a request to \link Dojo.DojoConnection.MatchServer MatchServer \endlink to permit the switch
        /// </summary>
        /// <param name="targetRole">target role to switch to</param>
        public void SwitchRole(DojoNetworkRole targetRole)
        {
            if (IsClient && targetRole != DojoNetworkRole.Server &&
                (_switchRoleCustomRules.Count == 0 || _switchRoleCustomRules.Values.All(f => f(Role, targetRole))))
            {
                if (HasJoinedMatch)
                {
                    SendStateMessage((long)NakamaOpCode.SwitchRole, targetRole.ToString());
                }
                else
                {
                    Role = targetRole;
                }
            }
        }

        // callback for NakamaOpCode.SwitchRole
        private void OnRemoteSwitchRole(DojoMessage m)
        {
            if (!IsClient && MatchClients.ContainsKey(m.Sender))
            {
                if (Enum.TryParse<DojoNetworkRole>(m.GetString(), out var targetRole))
                {
                    var currentRole = MatchClients[m.Sender];
                    // validate roles
                    if (!IsValidRoleSwitch(currentRole, targetRole))
                    {
                        return;
                    }

                    var currentPlayerCount = MatchClients.Values.Where(role => role != DojoNetworkRole.Viewer).Count() - (currentRole != DojoNetworkRole.Viewer ? 1 : 0) + MatchAIPlayers.Count;

                    if (currentRole != targetRole && (targetRole == DojoNetworkRole.Viewer || currentPlayerCount < _nakamaConfig.MaxNumPlayers))
                    {
                        MatchClients[m.Sender] = targetRole;
                        _matchClientsVersion++;

                        var toSend = EncodeMatchClientsPayload();

                        OnRoleChanged?.Invoke(m.Sender);

                        // notify all
                        SendStateMessage((long)NakamaOpCode.UpdateClients, toSend);
                    }
                }
            }
        }

        // validate a role switch
        private bool IsValidRoleSwitch(DojoNetworkRole currentRole, DojoNetworkRole targetRole)
        {
            // cannot be server!
            if (currentRole == DojoNetworkRole.Server || targetRole == DojoNetworkRole.Server)
            {
                return false;
            }
            return true;
        }

        #endregion Role Management

        #region RTT Measure

        [Header("RTT Measurement")]
        [Tooltip("Ping client from server every N seconds")]
        [SerializeField] private float _rttPingFrequency = 1.0f;

        private Dictionary<IUserPresence, NakamaRTT> _rttMeasures = new();

        private async void PingClients()
        {
            if (!IsClient)
            {
                if (HasJoinedMatch)
                {
                    // update current clients
                    _rttMeasures.Keys.Where(p => !_matchPresences.Contains(p)).ToList().ForEach(p => _rttMeasures.Remove(p));

                    foreach (var presence in _matchPresences)
                    {
                        if (!_rttMeasures.ContainsKey(presence))
                        {
                            _rttMeasures[presence] = new NakamaRTT();
                        }
                        _rttMeasures[presence].Ping();
                    }

                    // broadcast sync requests
                    await SendStateMessage((long)NakamaOpCode.RTTSync, "Sync!", broadcast: true);
                }
                Invoke(nameof(PingClients), _rttPingFrequency);
            }
        }

        // callback for NakamaOpCode.RTTSync
        private void OnRTTSync(DojoMessage m)
        {
            if (IsClient && HasJoinedMatch && m.Sender.Equals(MatchServer))
            {
                if (!_rttMeasures.ContainsKey(m.Sender))
                {
                    _rttMeasures[m.Sender] = new NakamaRTT();
                }
                _rttMeasures[m.Sender].Ping();

                // responds ack
                SendStateMessage((long)NakamaOpCode.RTTAck, "Ack!", m.Sender);
            }
        }

        // callback for NakamaOpCode.RTTAck
        private void OnRTTAck(DojoMessage m)
        {
            if (!IsClient && HasJoinedMatch)
            {
                if (_rttMeasures.ContainsKey(m.Sender))
                {
                    _rttMeasures[m.Sender].Pong();

                    // responds acksync
                    SendStateMessage((long)NakamaOpCode.RTTAckSync, "AckSync!", m.Sender);
                }
                else
                {
                    Debug.LogWarning($"{LOGSCOPE}: invalid RTTAck client {m.Sender}");
                }
            }
        }

        // callback for NakamaOpCode.RTTAckSync
        private void OnRTTAckSync(DojoMessage m)
        {
            if (IsClient && HasJoinedMatch && m.Sender.Equals(MatchServer))
            {
                if (_rttMeasures.ContainsKey(m.Sender))
                {
                    _rttMeasures[m.Sender].Pong();
                }
                else
                {
                    Debug.LogWarning($"{LOGSCOPE}: invalid RTTAck server {m.Sender}");
                }
            }
        }

        /// <summary>
        /// Get measured RTT between server and client in Nakama match.\n
        /// Used in \link Dojo.Netcode.DojoTransport DojoTransport \endlink \n
        /// Can be ignored in public code
        /// </summary>
        /// <param name="user">target user</param>
        /// <returns>RTT in milliseconds</returns>
        public ulong GetMeasuredRTT(IUserPresence user = default)
        {
            if (IsClient)
            {
                user = MatchServer;
            }

            if (_rttMeasures.ContainsKey(user))
            {
                return _rttMeasures[user].MeasuredMS;
            }
            else
            {
                return 0;
            }
        }

        #endregion RTT Measure

        #region Instance Connection

        [Header("Instance Server Connection")]
        [SerializeField] private string _instanceHost = "localhost";
        [SerializeField] private int _instancePort = 9000;

        [Tooltip("Ping instance server every N seconds")]
        [SerializeField] private int _instancePingFrequency = 10;

        private string _uniqueServerID = "";

        private async void PingInstanceServer()
        {
            if (!IsClient && !string.IsNullOrEmpty(_uniqueServerID))
            {
                // only ping if clients exist
                if (MatchClients.Count > 0)
                {
                    using var client = new HttpClient();
                    var uri = new Uri($"http://{_instanceHost}:{_instancePort}/instance?query={_uniqueServerID}");
                    await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri), HttpCompletionOption.ResponseHeadersRead);
                    Debug.Log($"Ping {uri}");
                }
                Invoke(nameof(PingInstanceServer), _instancePingFrequency);
            }
        }

        #endregion Instance Connection
    }
}
