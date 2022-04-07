using Mirror;
using UnityEngine;

public class Spectator : NetworkBehaviour
{
    public static Spectator LocalSpectator { get; private set; }
    
    public override void OnStartClient()
    {
        if(isLocalPlayer)
            LocalSpectator = this;
    }

    public override void OnStopClient()
    {
        LocalSpectator = null;
    }

    [TargetRpc]
    public void RpcSpectatePlayer(NetworkConnection client, Player player)
    {
        if (player == null)
            return;
        
        Debug.Log($"Spectating {player}");
        GameManager.Instance.SetCameraStatus(player.Camera, true);
    }
}
