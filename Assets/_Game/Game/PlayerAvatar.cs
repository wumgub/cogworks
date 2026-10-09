using System;
using Cogworks.View;
using Unity.Netcode;
using UnityEngine;

namespace Cogworks.Game
{
    /// <summary>
    /// Networked avatar. The owner moves it locally and its NetworkTransform (owner authority) replicates
    /// the position; everyone else just displays it. Also works un-spawned for single-player.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(SpriteRenderer))]
    public sealed class PlayerAvatar : NetworkBehaviour
    {
        /// <summary>Raised when the avatar this machine controls appears.</summary>
        public static event Action<PlayerAvatar> LocalSpawned;

        // Fast Play mode keeps statics between sessions; drop handlers from the previous session's objects.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => LocalSpawned = null;

        public PlayerController Controller => GetComponent<PlayerController>();

        public override void OnNetworkSpawn()
        {
            Setup(isLocal: IsOwner, slot: (int)(OwnerClientId % 4));
        }

        /// <summary>Single-player: no NetworkManager is running, so configure directly.</summary>
        public void SetupOffline() => Setup(isLocal: true, slot: 0);

        void Setup(bool isLocal, int slot)
        {
            Controller.IsLocal = isLocal;
            GetComponent<SpriteRenderer>().sprite = GameArt.PlayerSprite(slot);
            if (TryGetComponent<PlayerSpriteAnimator>(out var animator)) animator.Slot = slot;
            // Local avatar draws above others so you can always find yourself.
            GetComponent<SpriteRenderer>().sortingOrder = isLocal ? 16 : 15;
            if (isLocal) LocalSpawned?.Invoke(this);
        }
    }
}
