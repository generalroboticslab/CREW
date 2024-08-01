using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Netcode;

namespace Dojo.Netcode
{
    /// <summary>
    /// Netcode object pool implementation for %Dojo
    /// \see <a href="https://github.com/Unity-Technologies/com.unity.multiplayer.samples.bitesize/blob/main/Basic/2DSpaceShooter/Assets/Scripts/NetworkObjectPool.cs">Reference</a>
    /// </summary>
    public class DojoNetcodeObjectPool : MonoBehaviour
    {
        [SerializeField]
        private List<PoolConfigObject> _pooledList;

        private readonly HashSet<GameObject> _prefabs = new();
        private readonly Dictionary<GameObject, Queue<NetworkObject>> _pooledObjects = new();

        /// <summary>
        /// Unity \p Start method
        /// </summary>
        public void Start()
        {
            NetworkManager.Singleton.OnServerStarted += InitializePool;
        }

        /// <summary>
        /// Unity \p OnValidate method
        /// </summary>
        public void OnValidate()
        {
            if (_pooledList == null)
            {
                return;
            }

            for (var i = 0; i < _pooledList.Count; i++)
            {
                var prefab = _pooledList[i].Prefab;
                if (prefab != null)
                {
                    Assert.IsNotNull(prefab.GetComponent<NetworkObject>(), $"{nameof(DojoNetcodeObjectPool)}: Pooled prefab \"{prefab.name}\" at index {i} has no {nameof(NetworkObject)} component.");
                }
            }
        }

        /// <summary>
        /// Get prefab game object by \p idx\n
        /// Prefab must be registered in pool
        /// </summary>
        /// <param name="idx">index</param>
        /// <returns>prefab game object</returns>
        public GameObject GetPrefabAt(int idx)
        {
            if (idx < 0 || idx >= _pooledList.Count)
            {
                return null;
            }
            return _pooledList[idx].Prefab;
        }

        /// <summary>
        /// Get an instance of the given prefab from pool with default pose.\n
        /// The prefab must be registered in pool.
        /// </summary>
        /// <param name="prefab">prefab game object</param>
        /// <returns>instantiated \p NetworkObject from prefab</returns>
        public NetworkObject GetNetworkObject(GameObject prefab)
        {
            return GetNetworkObjectInternal(prefab, prefab.transform.localPosition, prefab.transform.localRotation);
        }

        /// <summary>
        /// Get an instance of the given prefab from pool with given pose.\n
        /// The prefab must be registered in pool.
        /// </summary>
        /// <param name="prefab">prefab game object</param>
        /// <param name="position">target position</param>
        /// <param name="rotation">target rotation</param>
        /// <returns>instantiated \p NetworkObject from prefab with target pose</returns>
        public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return GetNetworkObjectInternal(prefab, position, rotation);
        }

        /// <summary>
        /// Return an object to the pool and reset it.
        /// </summary>
        /// <param name="networkObject">object to return</param>
        /// <param name="prefab">corresponding prefab game object</param>
        public void ReturnNetworkObject(NetworkObject networkObject, GameObject prefab)
        {
            if (!networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }
            var go = networkObject.gameObject;
            go.SetActive(false);
            go.transform.SetParent(transform);
            _pooledObjects[prefab].Enqueue(networkObject);
        }

        /// <summary>
        /// Register a prefab game object to pool
        /// </summary>
        /// <param name="prefab">prefab game object</param>
        /// <param name="prewarmCount">number of pre-instantiated objects</param>
        public void AddPrefab(GameObject prefab, int prewarmCount = 0)
        {
            var networkObject = prefab.GetComponent<NetworkObject>();

            Assert.IsNotNull(networkObject, $"{nameof(prefab)} must have {nameof(networkObject)} component.");
            Assert.IsFalse(_prefabs.Contains(prefab), $"Prefab {prefab.name} is already registered in the pool.");

            RegisterPrefabInternal(prefab, prewarmCount);
        }

        // Builds up the cache for a prefab.
        private void RegisterPrefabInternal(GameObject prefab, int prewarmCount)
        {
            _prefabs.Add(prefab);
            _pooledObjects[prefab] = new();

            for (int i = 0; i < prewarmCount; i++)
            {
                var go = Instantiate(prefab);
                ReturnNetworkObject(go.GetComponent<NetworkObject>(), prefab);
            }

            // Register Netcode Spawn handlers
            NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, new DummyPrefabInstanceHandler(prefab, this));
        }

        private NetworkObject GetNetworkObjectInternal(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!_pooledObjects.ContainsKey(prefab))
            {
                RegisterPrefabInternal(prefab, 1);
            }

            var queue = _pooledObjects[prefab];

            if (queue.Count == 0)
            {
                var obj = Instantiate(prefab);
                ReturnNetworkObject(obj.GetComponent<NetworkObject>(), prefab);
            }
            var networkObject = queue.Dequeue();

            // Here we must reverse the logic in ReturnNetworkObject.
            var go = networkObject.gameObject;
            go.transform.SetParent(transform);
            go.SetActive(true);
            go.transform.SetPositionAndRotation(position, rotation);

            return networkObject;
        }

        /// Registers all objects to the cache.
        private void InitializePool()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                foreach (var configObject in _pooledList)
                {
                    RegisterPrefabInternal(configObject.Prefab, configObject.PrewarmCount);
                }
            }
        }
    }

    /// <summary>
    /// A configuration object for \link Dojo.Netcode.DojoNetcodeObjectPool DojoNetcodeObjectPool \endlink
    /// </summary>
    [Serializable]
    struct PoolConfigObject
    {
        /** Prefab game object */
        public GameObject Prefab;

        /** Number of instantiated objects from prefab at start */
        public int PrewarmCount;
    }

    /// <summary>
    /// A simple prefab handler for \link Dojo.Netcode.DojoNetcodeObjectPool DojoNetcodeObjectPool \endlink
    /// </summary>
    class DummyPrefabInstanceHandler : INetworkPrefabInstanceHandler
    {
        private readonly GameObject _prefab;
        private readonly DojoNetcodeObjectPool _pool;

        public DummyPrefabInstanceHandler(GameObject prefab, DojoNetcodeObjectPool pool)
        {
            _prefab = prefab;
            _pool = pool;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return _pool.GetNetworkObject(_prefab, position, rotation);
        }

        public void Destroy(NetworkObject networkObject)
        {
            _pool.ReturnNetworkObject(networkObject, _prefab);
        }
    }
}
