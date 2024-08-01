namespace Dojo.Nakama
{
    /// <summary>
    /// Payload structure for \link Dojo.Nakama.NakamaRPC.JoinOrNewMatch JoinOrNewMatch \endlink
    /// </summary>
    public class RPCJoinOrNewMatchPayload
    {
        /** Game identifier */
        public string GameTag;

        /** Maximum number of players allowed in game */
        public int MaxNumPlayers;
    }

    /// <summary>
    /// Payload structure for \link Dojo.Nakama.NakamaRPC.RemoveUserAccount RemoveUserAccount \endlink
    /// </summary>
    public class RPCRemoveUserAccountPayload
    {
        /** User ID to remove */
        public string UserId;
    }

    /// <summary>
    /// Payload structure for \link Dojo.Nakama.NakamaRPC.CreateMatch CreateMatch \endlink
    /// </summary>
    public class RPCCreateMatchPayload
    {
        /** Server user ID */
        public string ServerId;

        /** Current hosting match ID */
        public string MatchId;

        /** Game identifier */
        public string GameTag;

        /** Maximum number of players allowed in game */
        public int MaxNumPlayers;
    }

    /// <summary>
    /// Payload structure for \link Dojo.Nakama.NakamaRPC.UpdateMatch UpdateMatch \endlink
    /// </summary>
    public class RPCUpdateMatchPayload
    {
        /** Server user ID */
        public string ServerId;

        /** Game identifier */
        public string GameTag;

        /** Number of players in game */
        public int NumPlayers;

        /** Number of users in game */
        public int NumClients;
    }

    /// <summary>
    /// Payload structure for \link Dojo.Nakama.NakamaRPC.CleanMatch CleanMatch \endlink
    /// </summary>
    public class RPCCleanMatchPayload
    {
        /** Server user ID */
        public string ServerId;

        /** Game identifier */
        public string GameTag;
    }

    /// <summary>
    /// Payload structure for \link Dojo.Nakama.NakamaRPC.QueryMatch QueryMatch \endlink
    /// </summary>
    public class RPCQueryMatchPayload
    {
        /** Game identifier */
        public string GameTag;

        /** Server user ID */
        public string ServerId;
    }

    /// <summary>
    /// Payload structure for \link Dojo.Nakama.NakamaRPC.QueryMatches QueryMatches \endlink
    /// </summary>
    public class RPCQueryMatchesPayload
    {
        /** Game identifier */
        public string GameTag;

        /** Maximum number of players allowed in game */
        public int MaxNumPlayers;

        /** Maximum number of records to query */
        public int MaxNumRecords;
    }

    /// <summary>
    /// Match storage data structure on Nakama
    /// </summary>
    public class MatchStorageData
    {
        /** Game identifier */
        public string GameTag;

        /** Server user ID */
        public string ServerId;

        /** Current hosting match ID */
        public string MatchId;

        /** Maximum number of players allowed in game */
        public int MaxNumPlayers;

        /** Number of players in game */
        public int NumPlayers;

        /** Number of users in game */
        public int NumClients;
    }
}
