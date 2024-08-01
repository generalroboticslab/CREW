using UnityEngine;
using Unity.Netcode;
using Nakama;

namespace Dojo.Netcode
{
    /// <summary>
    /// Helper class for setting up Netcode networking within %Dojo
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class DojoNetcodeHelper : MonoBehaviour
    {
        private DojoConnection _connection;
        private DojoTransport _transport;

        private void Awake()
        {
            _connection = FindObjectOfType<DojoConnection>();
            _connection.OnJoinedMatch += OnJoinedMatch;
            _connection.OnRoleChanged += OnRoleChanged;
        }

        private void OnJoinedMatch()
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

            // initialize network manager
            if (_connection.IsClient)
            {
                NetworkManager.Singleton.StartClient();
            }
            else
            {
                NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApproved;
                NetworkManager.Singleton.StartServer();
            }

            // should use nakama transport!
            _transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as DojoTransport;
            Debug.Assert(_transport != null);
        }

        private void OnRoleChanged(IUserPresence presence)
        {
            if (NetworkManager.Singleton.IsServer &&
                _connection.MatchClients.TryGetValue(presence, out var role) &&
                _transport.GetNetcodeIDByUser(presence, out var clientID) &&
                NetworkManager.Singleton.ConnectedClients.ContainsKey(clientID))
            {
                if (role == DojoNetworkRole.Viewer)
                {
                    var netObj = NetworkManager.Singleton.ConnectedClients[clientID].PlayerObject;
                    if (netObj != null)
                    {
                        netObj.Despawn();
                    }
                }
                else if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null)
                {
                    var playerObj = Instantiate(NetworkManager.Singleton.NetworkConfig.PlayerPrefab);
                    var playerNetObj = playerObj.GetComponent<NetworkObject>();
                    playerNetObj.SpawnAsPlayerObject(clientID);
                }
            }
        }

        private void OnConnectionApproved(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var clientID = request.ClientNetworkId;

            if (_transport.GetUserByNetcodeID(clientID, out var presence) && _connection.MatchClients.TryGetValue(presence, out var role))
            {
                response.Approved = true;
                response.CreatePlayerObject = NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null && role != DojoNetworkRole.Viewer;
            }
            else
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
            }

            response.Pending = false;
        }
    }
}
