using Unity.Netcode.Components;
using UnityEngine;

namespace Dojo.Netcode.ClientAuthority
{
    /// <summary>
    /// Client (owner of transform) authorizes transform states
    /// \see <a href="https://docs-multiplayer.unity3d.com/netcode/current/components/networktransform/#clientnetworktransform">Unity Documentation</a>
    /// \see <a href="https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Packages/com.unity.multiplayer.samples.coop/Utilities/Net/ClientAuthority/ClientNetworkTransform.cs">Reference</a>
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
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
