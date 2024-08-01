namespace Dojo.Nakama
{
    /// <summary>
    /// Static class for Nakama RPC functions
    /// </summary>
    public class NakamaRPC
    {
        /** Join or request new match */
        public static readonly string JoinOrNewMatch = "RPCJoinOrNewMatch";

        /** Remove user account on Nakama */
        public static readonly string RemoveUserAccount = "RPCRemoveUserAccount";

        /** Create match storage on Nakama */
        public static readonly string CreateMatch = "RPCCreateMatch";

        /** Update match storage on Nakama */
        public static readonly string UpdateMatch = "RPCUpdateMatch";

        /** Clean up match storage */
        public static readonly string CleanMatch = "RPCCleanMatch";

        /** Query single match state */
        public static readonly string QueryMatch = "RPCQueryMatch";

        /** Query all active matches */
        public static readonly string QueryMatches = "RPCQueryMatches";
    }
}
