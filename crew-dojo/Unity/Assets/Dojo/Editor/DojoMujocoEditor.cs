#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Dojo.Mujoco;

namespace DojoEditor
{
    /// <summary>
    /// Unity editor inspector for \link Dojo.Mujoco.DojoMujoco DojoMujoco \endlink
    /// </summary>
    [CustomEditor(typeof(DojoMujoco))]
    public class DojoMujocoEditor : Editor
    {
        private bool _showNetworkTransform = false;

        /** Render custom inspector GUI */
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var script = (DojoMujoco)target;

            _showNetworkTransform = EditorGUILayout.Foldout(_showNetworkTransform, "NetworkTransform");
            if (_showNetworkTransform)
            {
                if (GUILayout.Button("Inject"))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(script.transform);
                    DojoMujoco.SetNetworkTransform(script.transform, true);
                    EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                }
                if (GUILayout.Button("Remove"))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(script.transform);
                    DojoMujoco.SetNetworkTransform(script.transform, false);
                    EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                }

                if (GUILayout.Button("Enable Interpolation"))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(script.transform);
                    DojoMujoco.SetNetworkTransformInterpolation(script.transform, true);
                    EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                }
                if (GUILayout.Button("Disable Interpolation"))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(script.transform);
                    DojoMujoco.SetNetworkTransformInterpolation(script.transform, false);
                    EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                }
            }
            GUILayout.Space(5f);

            if (GUILayout.Button("Fix MjGeom Renderer"))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(script.transform);
                DojoMujoco.FixMjGeomMeshRenderer(script.transform);
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }
    }
}
#endif
