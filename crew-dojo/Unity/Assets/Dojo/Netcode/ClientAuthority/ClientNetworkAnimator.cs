using Unity.Netcode.Components;
using UnityEngine;

namespace Dojo.Netcode.ClientAuthority
{
    /// <summary>
    /// Client (owner of animator) authorizes animator states
    /// \see <a href="https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Packages/com.unity.multiplayer.samples.coop/Utilities/Net/ClientAuthority/ClientNetworkAnimator.cs">Reference</a>
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkAnimator : NetworkAnimator
    {
        /// <summary>
        /// Overwrite to enable client authoritative
        /// </summary>
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
