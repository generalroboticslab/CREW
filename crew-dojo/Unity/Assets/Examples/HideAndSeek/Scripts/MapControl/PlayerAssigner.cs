using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Examples.HideAndSeek
{
    // assign player in the map

    public class PlayerAssigner : NetworkBehaviour
    {
        private const string LOGSCOPE = "PlayerAssigner";

        [SerializeField]
        private MapManager _map;

        [SerializeField]
        private GameObject _prefabHider;

        [SerializeField]
        private GameObject _prefabSeeker;

        // event callbacks for client side (AI)
        public event Action OnPlayerSpawned;
        public event Action OnPlayerDestroyed;

        private readonly List<Tuple<NetworkObject, AIAgent, bool, int>> _aiAgentObjects = new();

        private List<Vector2Int> _spawnPoints = new();

        private void FixedUpdate()
        {
            _spawnPoints = new List<Vector2Int>(_map.SpawnPoints);
        }

        IEnumerator FindSpawnPointForPlayer(Action<Bounds> onSpawnPointFound)
        {
            int toTry = 50;
            while (toTry > 0)
            {
                if (_spawnPoints.Count == 0)
                {
                    break;
                }

                var rndIdx = UnityEngine.Random.Range(0, _spawnPoints.Count);
                var rndPos = _spawnPoints[rndIdx];
                _spawnPoints.RemoveAt(rndIdx);
                var bounding = new Bounds(
                    new(
                        (rndPos.y - _map.NumColsHalf + 0.5f) * _map.GroundScale.x,
                        0.5f,
                        (_map.NumRowsHalf - rndPos.x - 0.5f) * _map.GroundScale.z
                    ),
                    Vector3.one
                );

                // if no intersection, spawn it here!
                if (Physics.OverlapBox(bounding.center, bounding.extents).Length == 0 && check_all_agent_position(bounding.center,_map.agent_positions, 2f))
                {
                    _map.agent_positions.Add(bounding.center);
                    //Log agent positions
                    Debug.Log("Agent positions: ");
                    foreach (Vector3 point in _map.agent_positions)
                    {
                        Debug.Log(point);
                    }
                    onSpawnPointFound(bounding);
                    yield break;
                }
                toTry--;
            }
            yield return new WaitForFixedUpdate();
            StartCoroutine(FindSpawnPointForPlayer(onSpawnPointFound));
        }

        // called when human user selects a role
        public void RequestRole(bool isHider)
        {
            if (NetworkManager.Singleton.IsClient)
            {
                RequestRoleServerRPC(isHider, NetworkManager.Singleton.LocalClientId);
                GameManager.Instance.ActivePlayers[NetworkManager.Singleton.LocalClientId] = isHider;
            }
        }

        public void RequestRole(bool isHider, ulong clientID)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                return;
            }
            StartCoroutine(FindSpawnPointForPlayer(spawnPoint =>
            {
                var prefab = isHider ? _prefabHider : _prefabSeeker;
                var netObj = Instantiate(prefab).GetComponent<NetworkObject>();
                netObj.transform.Find("Body").SetPositionAndRotation(spawnPoint.center, prefab.transform.localRotation);
                netObj.SpawnAsPlayerObject(clientID);
                GameManager.Instance.ActivePlayers[clientID] = isHider;
            }));
        }

        public void RequestAIRole(AIAgent agent, bool isHider, int agentID)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                return;
            }
            StartCoroutine(FindSpawnPointForPlayer(spawnPoint =>
            {
                var prefab = isHider ? _prefabHider : _prefabSeeker;
                var netObj = Instantiate(prefab).GetComponent<NetworkObject>();
                netObj.transform.Find("Body").SetPositionAndRotation(spawnPoint.center, prefab.transform.localRotation);
                netObj.Spawn();
                GameManager.Instance.ActiveAIPlayers.Add(Tuple.Create(agentID, isHider));

                var controller = netObj.GetComponentInChildren<PlayerController>();
                agent.SubscribeController(controller);
                _aiAgentObjects.Add(Tuple.Create(netObj, agent, isHider, agentID));
            }));
        }

        public void ResetPlayerPosition(PlayerController _controller)
        {

            Debug.Log("ResetPlayerPosition");

            if (!NetworkManager.Singleton.IsServer)
            {
                return;
            }
            _map.agent_positions.Clear();
            StartCoroutine(FindSpawnPointForPlayer(spawnPoint =>
            {
                _controller.Teleport(spawnPoint.center);
            }));
        }

        // called when player has died
        public void ReleaseRole()
        {
            if (NetworkManager.Singleton.IsClient)
            {
                ReleaseRoleServerRPC(NetworkManager.Singleton.LocalClientId);
                GameManager.Instance.ActivePlayers.Remove(NetworkManager.Singleton.LocalClientId);
            }
        }

        public bool ReleaseRole(ulong clientID)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                GameManager.Instance.ActivePlayers.Remove(clientID);
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientID, out var client) && client.PlayerObject != null)
                {
                    client.PlayerObject.Despawn();
                    return true;
                }
            }
            return false;
        }

        public bool ReleaseAIRole(bool isHider, int agentID)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                var agents = _aiAgentObjects.Where(x => x.Item3 == isHider && x.Item4 == agentID).ToList();
                var aiPlayer = GameManager.Instance.ActiveAIPlayers.Where(x => x.Item1 == agentID && x.Item2 == isHider).ToList();
                if (agents.Count > 0)
                {
                    agents.ForEach(agent =>
                    {
                        agent.Item1.Despawn();
                        _aiAgentObjects.Remove(agent);
                    });

                    aiPlayer.ForEach(agent =>
                    {
                        GameManager.Instance.ActiveAIPlayers.Remove(agent);
                    });
                }
            }
            return false;
        }

        [ClientRpc]
        private void RequestRoleResultClientRPC(bool success, ClientRpcParams rpcParams = default)
        {
            if (success)
            {
                OnPlayerSpawned?.Invoke();
            }
        }

        [ClientRpc]
        private void ReleaseRoleResultClientRPC(bool success, ClientRpcParams rpcParams = default)
        {
            if (success)
            {
                OnPlayerDestroyed?.Invoke();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRoleServerRPC(bool isHider, ulong clientID)
        {
            var replyParams = new ClientRpcParams()
            {
                Send = new()
                {
                    TargetClientIds = new[] { clientID },
                }
            };

            RequestRole(isHider, clientID); // assuming spawned
            RequestRoleResultClientRPC(true, replyParams);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReleaseRoleServerRPC(ulong clientID)
        {
            var replyParams = new ClientRpcParams()
            {
                Send = new()
                {
                    TargetClientIds = new[] { clientID },
                }
            };

            ReleaseRoleResultClientRPC(ReleaseRole(clientID), replyParams);
        }

        bool check_all_agent_position(Vector3 potential_position,List<Vector3> agent_positions,float criteria)
        {
            bool result = true;
            if (agent_positions.Count > 0)
            {
                foreach (Vector3 point in agent_positions)
                {
                    if (Vector3.Distance(point,potential_position) < criteria)
                    {
                        result = false;
                        break;
                    }
                }
            }
            return result;
        }
    }
}
