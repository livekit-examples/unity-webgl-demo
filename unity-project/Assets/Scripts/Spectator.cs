using System;
using System.Linq;
using Mirror;
using UnityEngine;

public class Spectator : NetworkBehaviour
{
    public static Spectator LocalSpectator { get; private set; }
    private Player m_Player;
    
    public override void OnStartClient()
    {
        if (isLocalPlayer)
        {
            LocalSpectator = this;
            Player.PlayerRemoved += PlayerRemoved;
        }
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer)
        {
            LocalSpectator = null;
            Player.PlayerRemoved -= PlayerRemoved;
        }
    }
    
    void PlayerRemoved(Player player)
    {
        if (m_Player == player)
        {
            var p = Player.Players.Values.FirstOrDefault();
            if(p != null)
                SpectatePlayer(player);
        }
    }
    
    public void SpectatePlayer(Player player)
    {
        Debug.Log($"Spectating {player.Sid}");
        
        m_Player = player;
        GameManager.Instance.SetCameraStatus(player.Camera, true);
    }

    [TargetRpc]
    public void RpcSpectatePlayer(NetworkConnection client, Player player)
    {
        if (player == null)
            return;
        
        SpectatePlayer(player);
    }
}
