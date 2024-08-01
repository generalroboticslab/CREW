#if UNITY_EDITOR
using UnityEngine;

namespace DojoEditor
{
    /// <summary>
    /// Configuration file for building Dojo example projects
    /// </summary>
    [CreateAssetMenu(fileName = "DojoBuildConfig", menuName = "Dojo/Builder Config")]
    public class DojoBuilderConfig : ScriptableObject
    {
        /** Scene path to build from */
        public string ScenePath;

        /** Scene name for output sub-folder */
        public string SceneName;

        /** Output folder */
        public string OutputPath;

        /** Component name for \link Dojo.DojoConnection DojoConnection \endlink in server mode */
        public string DojoServerName;

        /** Component name for \link Dojo.DojoConnection DojoConnection \endlink in client mode */
        public string DojoClientName;

        /** Enable WebGL build or not (can be slow) */
        public bool EnableWebGLBuild;

        /** Enable Linux build or not (can be slow) */
        public bool EnableLinuxBuild;
    }
}

#endif
