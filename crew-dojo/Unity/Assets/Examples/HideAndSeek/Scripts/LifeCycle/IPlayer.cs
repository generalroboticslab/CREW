using UnityEngine;

namespace Examples.HideAndSeek
{
    [RequireComponent(typeof(PlayerController))]
    public abstract class IPlayer : MonoBehaviour
    {
        public int Score { get; protected set; } = 0;
        public abstract bool IsHider { get; }
    }
}
