using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public LiveKitNetwork LiveKitNetwork { get; private set; }
    
    private bool m_RoundRestarting;
    public Spectator SpectatorPrefab;
    public Camera ActiveCamera;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(this);
        LiveKitNetwork = GetComponent<LiveKitNetwork>();
    }

    [Server]
    public void StartRound()
    {
        var posLength = World.Instance.RedPositions.transform.childCount;
        var redIndex = 0;
        var blueIndex = 0;
        
        foreach (var p in LiveKitNetwork.Connections.Values)
        {
            NetworkServer.RemovePlayerForConnection(p.NetConnection, true);

            var pPrefab = LiveKitNetwork.playerPrefab;

            Transform startPos;
            if (p.Team == Team.Red)
                startPos = World.Instance.RedPositions.transform.GetChild(redIndex++ % posLength);
            else
                startPos = World.Instance.BluePositions.transform.GetChild(blueIndex++ % posLength);
            
            var playerObject = Instantiate(pPrefab, startPos.position, startPos.rotation);
            playerObject.name = $"{pPrefab.name} [connId={p.NetConnection.connectionId}]";
            
            NetworkServer.AddPlayerForConnection(p.NetConnection, playerObject);
        }
    }

    [Server]
    public void UpdateScore(float restartDelay)
    {
        var pCount = Player.Players.Count;
        if (pCount == 0)
            return;
        
        var numBlue = 0;
        foreach (var p in Player.Players.Values)
            if (p.Team == Team.Blue)
                numBlue++;

        if (numBlue >= pCount)
        {
            GameState.Instance.BlueScore++;
            GameState.Instance.RpcShowRoundWin(Team.Blue, restartDelay);
            StartCoroutine(HandleRestart(restartDelay));
        }
        else if (numBlue == 0)
        {
            GameState.Instance.RedScore++;
            GameState.Instance.RpcShowRoundWin(Team.Red, restartDelay);
            StartCoroutine(HandleRestart(restartDelay));
        }
    }

    private IEnumerator HandleRestart(float startDelay)
    {
        if (m_RoundRestarting)
            yield break;
        
        m_RoundRestarting = true;
        yield return new WaitForSeconds(startDelay);
        StartRound();
        m_RoundRestarting = false;
    }
    
    [Client]
    public void SetCameraStatus(Camera camera, bool status)
    {
        if (status)
        {
            var pCam = ActiveCamera;
            ActiveCamera = null;
        
            if(pCam != null)
                SetCameraStatus(pCam, false);
            
            ActiveCamera = camera;
            camera.enabled = true;
            camera.GetComponent<PostProcessLayer>().enabled = true;
            camera.GetComponent<PostProcessVolume>().enabled = true;
            camera.gameObject.AddComponent<AudioListener>();
        }
        else
        {
            camera.enabled = false;
            camera.GetComponent<PostProcessLayer>().enabled = false;
            camera.GetComponent<PostProcessVolume>().enabled = false;
            Destroy(camera.GetComponent<AudioListener>());
        }
    }

    [Server]
    public void ToSpectator(NetworkConnectionToClient client, bool specRandom = true)
    {
        NetworkServer.RemovePlayerForConnection(client, true);
        var spec = Instantiate(SpectatorPrefab);
        NetworkServer.AddPlayerForConnection(client, spec.gameObject);

        if (specRandom)
        {
            var p = Player.Players.First();
            spec.RpcSpectatePlayer(client, p.Value);
        }
    }
}
