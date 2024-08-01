using UnityEngine;
using Unity.Netcode.Components;
using Mujoco;

namespace Dojo.Mujoco
{
    /// <summary>
    /// %Dojo helper component for Mujoco plugin
    /// </summary>
    public class DojoMujoco : MonoBehaviour
    {
        private DojoConnection _connection;

        private void Awake()
        {
            _connection = FindObjectOfType<DojoConnection>();
        }

        private void Start()
        {
            if (_connection.IsClient)
            {
                var scene = FindObjectOfType<MjScene>();
                if (scene != null)
                {
                    scene.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Disable component \p T in \p go
        /// </summary>
        /// <typeparam name="T">target component type</typeparam>
        /// <param name="go">game object to disable with</param>
        public static void DisableComponent<T>(GameObject go) where T : MonoBehaviour
        {
            if (go.TryGetComponent<T>(out var comp))
            {
                comp.enabled = false;
            }
        }

#if UNITY_EDITOR

        /// <summary>
        /// Attach or remove \p NetworkTransform on \p transform
        /// </summary>
        /// <param name="transform">target Unity transform</param>
        /// <param name="inject">attach or remove</param>
        public static void SetNetworkTransform(Transform transform, bool inject)
        {
            // set on MjBaseBody
            if (transform.GetComponent<MjBaseBody>() != null)
            {
                if (inject)
                {
                    InjectCustomScript<NetworkTransform>(transform);
                    // sync position and rotation only
                    var net = transform.GetComponent<NetworkTransform>();
                    net.SyncPositionX = net.SyncPositionY = net.SyncPositionZ = true;
                    net.SyncRotAngleX = net.SyncRotAngleY = net.SyncRotAngleZ = true;
                    net.SyncScaleX = net.SyncScaleY = net.SyncScaleZ = false;
                }
                else
                {
                    RemoteCustomScript<NetworkTransform>(transform);
                }
            }

            // set children
            foreach (Transform child in transform)
            {
                SetNetworkTransform(child, inject);
            }
        }

        /// <summary>
        /// Enable or disable \p NetworkTransform interpolation on \p transform
        /// </summary>
        /// <param name="transform">target Unity transform</param>
        /// <param name="enabled">enable or disable</param>
        public static void SetNetworkTransformInterpolation(Transform transform, bool enabled)
        {
            if (transform.TryGetComponent<NetworkTransform>(out var t))
            {
                t.Interpolate = enabled;
            }

            foreach (Transform child in transform)
            {
                SetNetworkTransformInterpolation(child, enabled);
            }
        }

        /// <summary>
        /// Fix \p MjGeom mesh renderer
        /// </summary>
        /// <param name="transform">target Unity transform</param>
        public static void FixMjGeomMeshRenderer(Transform transform)
        {
            if (transform.TryGetComponent<MjGeom>(out var geom) && geom.Settings.Filtering.Group == 3)
            {
                // please refer to https://github.com/deepmind/mujoco/issues/503
                // this helps clean up all mesh renderer overlapping issues
                if (transform.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.enabled = false;
                }
            }

            // fix children
            foreach (Transform child in transform)
            {
                FixMjGeomMeshRenderer(child);
            }
        }

        private static void InjectCustomScript<T>(Transform transform) where T : MonoBehaviour
        {
            // ensure only once
            if (transform.gameObject.GetComponent<T>() == null)
            {
                transform.gameObject.AddComponent<T>();
            }
        }

        private static void RemoteCustomScript<T>(Transform transform) where T : MonoBehaviour
        {
            if (transform.gameObject.TryGetComponent<T>(out var comp))
            {
                DestroyImmediate(comp);
            }
        }
#endif
    }
}
