using UnityEngine;

namespace Dojo.Nakama
{
    /// <summary>
    /// Nakama configuration object
    /// </summary>
    [CreateAssetMenu(fileName = "NakamaConfig", menuName = "Dojo/Nakama Match Configurations")]
    public class NakamaConfig : ScriptableObject
    {
        [Header("Game")]
        /** Game identifier */
        [Tooltip("The game tag for instancing server")]
        public string GameTag = "DefaultGame";

        [Tooltip("Maximum number of players except for server")]
        /** Maximum number of players allowed in game */
        public int MaxNumPlayers = 2;

        [Header("Connection")]
        /** Connection scheme (\p http or \p https) */
        public string Scheme = "http";

        /** Nakama server IP address */
        public string Host = "localhost";

        /** Nakama server port */
        public int Port = 7350;

        /** Nakama server authentication key */
        public string ServerKey = "defaultkey";
    }
}
