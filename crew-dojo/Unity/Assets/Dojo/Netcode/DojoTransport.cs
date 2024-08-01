using System;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Nakama;
using Dojo.Nakama;

namespace Dojo.Netcode
{
    /// <summary>
    /// The transport that enables Unity netcode synchronization through Nakama framework
    /// \see <a href="https://forum.unity.com/threads/clientid-to-transportid-in-networkmanager.1377021/#post-8678538">Discussion about client IDs in NetworkManager</a>
    /// </summary>
    public class DojoTransport : NetworkTransport
    {
        private readonly string LOGSCOPE = "DojoNakamaTransport";

        private DojoConnection _connection;
        private NetworkManager _manager;
        private bool IsClient => _connection.IsClient;

        // IMPORTANT: client ID here is transportID, not clientID assigned by NetworkManager!
        private ulong _clientIDCounter = 1;
        private readonly Queue<ulong> _releasedClientIDs = new();
        private readonly Dictionary<IUserPresence, ulong> _userToClientID = new();

        private readonly Dictionary<ulong, ulong> _clientToNetcodeID = new();
        private readonly Dictionary<ulong, ulong> _netcodeToClientID = new();

        private readonly Dictionary<ulong, PendingClient> _replicatedManagerPending = new();

        private void Awake()
        {
            _connection = FindObjectOfType<DojoConnection>();

            _connection.OnJoinedMatch += OnJoinMatch;
            _connection.OnLeftMatch += OnLeaveMatch;

            _connection.OnMatchPlayerJoined += OnMatchPresenceJoined;
            _connection.OnMatchPlayerLeft += OnMatchPresenceLeft;

            // register transport messages callback
            _connection.SubscribeRemoteMessages((long)NakamaOpCode.TransportMessages, OnTransportMessages, false);
        }

        /// <summary>
        /// Send \p payload to client with \p clientId
        /// </summary>
        /// <param name="clientId">client identifier</param>
        /// <param name="payload">message in bytes</param>
        /// <param name="networkDelivery">delivery type</param>
        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            if (clientId == ServerClientId)
            {
                _connection.SendStateMessage((long)NakamaOpCode.TransportMessages, payload, _connection.MatchServer);
            }
            else if (GetUserByClientID(clientId, out var target))
            {
                _connection.SendStateMessage((long)NakamaOpCode.TransportMessages, payload, target);
            }
            else
            {
                Debug.LogWarning($"{LOGSCOPE}: failed to send transport message!");
            }
        }

        /// <summary>
        /// Poll event override (not used)
        /// </summary>
        /// <param name="clientId">client identifier</param>
        /// <param name="payload">message in bytes</param>
        /// <param name="receiveTime">message received time</param>
        /// <returns>\p NetworkEvent data</returns>
        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            // we use event based instead of poll based
            clientId = default;
            payload = default;
            receiveTime = default;
            return NetworkEvent.Nothing;
        }

        /// <summary>
        /// Transport starts local client connection
        /// </summary>
        /// <returns>success or not</returns>
        public override bool StartClient()
        {
            return _connection.HasJoinedMatch && IsClient;
        }

        /// <summary>
        /// Transport starts local server
        /// </summary>
        /// <returns>success or not</returns>
        public override bool StartServer()
        {
            return _connection.HasJoinedMatch && !IsClient;
        }

        /// <summary>
        /// Disconnect remote client from server\n
        /// Not enabled in %Dojo
        /// </summary>
        /// <param name="clientId">remote client identifier</param>
        public override void DisconnectRemoteClient(ulong clientId)
        {
        }

        /// <summary>
        /// Disconnect local client from server\n
        /// Not enabled in %Dojo
        /// </summary>
        public override void DisconnectLocalClient()
        {
        }

        /// <summary>
        /// Get current RTT between server and client with \p clientId
        /// </summary>
        /// <param name="clientId">client identifier</param>
        /// <returns>RTT in milliseconds</returns>
        public override ulong GetCurrentRtt(ulong clientId)
        {
            if (IsClient)
            {
                return _connection.GetMeasuredRTT();
            }
            else
            {
                foreach (var p in _userToClientID)
                {
                    if (p.Value.Equals(clientId))
                    {
                        return _connection.GetMeasuredRTT(p.Key);
                    }
                }

                return 0;
            }
        }

        /// <summary>
        /// Shutdown transport and clean up
        /// </summary>
        public override void Shutdown()
        {
            if (IsClient)
            {
                _ = _connection.LeaveNakamaMatch();
            }
        }

        /// <summary>
        /// Initialize transport and internal states
        /// </summary>
        /// <param name="networkManager">current \p NetworkManager</param>
        public override void Initialize(NetworkManager networkManager = null)
        {
            Debug.Assert(_connection.HasJoinedMatch, "Transport available only after connection manager has joined match!");

            _manager = networkManager;

            _clientIDCounter = 1;
            _releasedClientIDs.Clear();

            _clientToNetcodeID.Clear();
            _netcodeToClientID.Clear();
        }

        /// <summary>
        /// Default server clientId in Netcode transport
        /// </summary>
        public override ulong ServerClientId => 0;

        #region Nakama Callbacks

        // on match presence joined callback
        private void OnMatchPresenceJoined(IUserPresence presence)
        {
            if (!IsClient)
            {
                var pID = GetNextClientID();
                _userToClientID[presence] = pID;

                PrePendingClients();
                InvokeOnTransportEvent(NetworkEvent.Connect, pID, default, Time.realtimeSinceStartup);
                PostPendingClients(pID);
            }
        }

        // on match presence left callback
        private void OnMatchPresenceLeft(IUserPresence presence)
        {
            if (!IsClient)
            {
                if (_userToClientID.TryGetValue(presence, out var pID))
                {
                    ReleaseClientID(pID);
                    _userToClientID.Remove(presence);

                    InvokeOnTransportEvent(NetworkEvent.Disconnect, pID, default, Time.realtimeSinceStartup);
                    ClearNetcodeMapping(pID);
                }
            }
        }

        // callback for NakamaOpCode.TransportMessages
        private void OnTransportMessages(DojoMessage m)
        {
            if (IsClient)
            {
                InvokeOnTransportEvent(NetworkEvent.Data, ServerClientId, m.RawData, Time.realtimeSinceStartup);
            }
            else if (_userToClientID.TryGetValue(m.Sender, out var pID))
            {
                InvokeOnTransportEvent(NetworkEvent.Data, pID, m.RawData, Time.realtimeSinceStartup);
            }
            else
            {
                Debug.LogError($"{LOGSCOPE}: message sender {m.Sender} not found in current presences!");
            }
        }

        // player joined match callback
        private void OnJoinMatch()
        {
            if (IsClient)
            {
                InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);
            }
        }

        // player left match callback
        private void OnLeaveMatch()
        {
            if (IsClient)
            {
                InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);
            }
        }

        #endregion Nakama Callbacks

        #region ID Management

        /// <summary>
        /// Get user presence by \p netcodeId \n
        /// \p netcodeId is assigned by NetworkManager
        /// </summary>
        /// <param name="netcodeId">unique user identifier</param>
        /// <param name="presence">return user presence in Nakama</param>
        /// <returns>user is found or not</returns>
        public bool GetUserByNetcodeID(ulong netcodeId, out IUserPresence presence)
        {
            if (_netcodeToClientID.ContainsKey(netcodeId))
            {
                return GetUserByClientID(_netcodeToClientID[netcodeId], out presence);
            }

            presence = default;
            return false;
        }

        /// <summary>
        /// Get \p netcodeId by user presence\n
        /// \p netcodeId is assigned by NetworkManager
        /// </summary>
        /// <param name="presence">user presence in Nakama</param>
        /// <param name="netcodeId">return unique user identifier</param>
        /// <returns>user is found or not</returns>
        public bool GetNetcodeIDByUser(IUserPresence presence, out ulong netcodeId)
        {
            if (_userToClientID.TryGetValue(presence, out var clientId))
            {
                return _clientToNetcodeID.TryGetValue(clientId, out netcodeId);
            }

            netcodeId = default;
            return false;
        }

        // get user presence by ID
        private bool GetUserByClientID(ulong clientId, out IUserPresence presence)
        {
            foreach (var user in _userToClientID)
            {
                if (user.Value.Equals(clientId))
                {
                    presence = user.Key;
                    return true;
                }
            }

            presence = default;
            return false;
        }

        // get next available client ID
        private ulong GetNextClientID()
        {
            if (_releasedClientIDs.Count > 0)
            {
                return _releasedClientIDs.Dequeue();
            }
            else
            {
                return _clientIDCounter++;
            }
        }

        // release client ID
        private void ReleaseClientID(ulong clientID)
        {
            _releasedClientIDs.Enqueue(clientID);
        }

        #endregion ID Management

        #region NetworkManager ID <-> Transport ID mapping

        private void PrePendingClients()
        {
            // replicate the pending clients dictionary
            if (_manager != null && !IsClient)
            {
                _replicatedManagerPending.Clear();
                foreach (var entry in _manager.PendingClients)
                {
                    _replicatedManagerPending[entry.Key] = entry.Value;
                }
            }
        }

        private void PostPendingClients(ulong clientID)
        {
            // compare pending clients and get assigned netcode ID
            if (_manager != null && !IsClient)
            {
                var pending = _manager.PendingClients;
                var diffs = pending.Keys.Where(id => !_replicatedManagerPending.ContainsKey(id));

                if (diffs.Count() == 1)
                {
                    // build mappings
                    var netcodeID = diffs.First();
                    _netcodeToClientID[netcodeID] = clientID;
                    _clientToNetcodeID[clientID] = netcodeID;
                }
                else
                {
                    Debug.LogWarning($"{LOGSCOPE}: Invalid number of new pending clients!");
                }
            }
        }

        private void ClearNetcodeMapping(ulong clientID)
        {
            if (_clientToNetcodeID.TryGetValue(clientID, out var netcodeID))
            {
                _clientToNetcodeID.Remove(clientID);
                if (_netcodeToClientID.ContainsKey(netcodeID))
                {
                    _netcodeToClientID.Remove(netcodeID);
                }
            }
        }

        #endregion NetworkManager ID <-> Transport ID mapping
    }
}
