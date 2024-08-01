using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dojo
{
    /// <summary>
    /// Dispatcher help class for Unity single threading
    /// </summary>
    public class Dispatcher : MonoBehaviour
    {
        /** Global instance */
        public static Dispatcher Instance { get; private set; }

        /** Is dispatcher instance initialized or not */
        public static bool IsInitialized => Instance != null;

        private static readonly Queue<Action> _queueU = new(); // actions in Update
        private static readonly Queue<Action> _queueFU = new(); // actions in Fixed Update

        /// <summary>
        /// Schedule async function to be executed in coroutine
        /// </summary>
        /// <param name="action">function to be executed</param>
        /// <param name="inFixedUpdate">run in Unity.Update or Unity.FixedUpdate</param>
        public void Enqueue(IEnumerator action, bool inFixedUpdate = false)
        {
            if (inFixedUpdate)
            {
                lock (_queueFU)
                {
                    _queueFU.Enqueue(() =>
                    {
                        StartCoroutine(action);
                    });
                }
            }
            else
            {
                lock (_queueU)
                {
                    _queueU.Enqueue(() =>
                    {
                        StartCoroutine(action);
                    });
                }
            }
        }

        /// <summary>
        /// Schedule function to be executed on Unity main thread
        /// </summary>
        /// <param name="action">function to be executed</param>
        /// <param name="async">function is async or not</param>
        /// <param name="inFixedUpdate">run in Unity.Update or Unity.FixedUpdate</param>
        public void Enqueue(Action action, bool async = false, bool inFixedUpdate = false)
        {
            if (async)
            {
                Enqueue(AsyncWrapper(action), inFixedUpdate);
            }
            else
            {
                if (inFixedUpdate)
                {
                    lock (_queueFU)
                    {
                        _queueFU.Enqueue(action);
                    }
                }
                else
                {
                    lock (_queueU)
                    {
                        _queueU.Enqueue(action);
                    }
                }
            }
        }

        private IEnumerator AsyncWrapper(Action action)
        {
            action();
            yield return null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            lock (_queueU)
            {
                while (_queueU.Count > 0)
                {
                    _queueU.Dequeue().Invoke();
                }
            }
        }

        private void FixedUpdate()
        {
            lock (_queueFU)
            {
                while (_queueFU.Count > 0)
                {
                    _queueFU.Dequeue().Invoke();
                }
            }
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}
