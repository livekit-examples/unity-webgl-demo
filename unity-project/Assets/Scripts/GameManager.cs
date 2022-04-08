using System.Collections;
using System.Linq;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public TMP_Text BlueText;
    public TMP_Text RedText;
    public TMP_Text RedWon;
    public TMP_Text BlueWon;
    public Image MicroImage;
    public Spectator SpectatorPrefab;

    [SyncVar(hook=nameof(OnBlueScoreChanged))] [HideInInspector] public int BlueScore;
    [SyncVar(hook=nameof(OnRedScoreChanged))] [HideInInspector] public int RedScore;
    
    private bool m_RoundRestarting;
    
    [HideInInspector] public Camera ActiveCamera;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(this);
    }
    
    void Start()
    {
#if !UNITY_EDITOR && UNITY_WEBGL
        if(isClient)
            LiveKitNetwork.Instance.Room.LocalParticipant.IsSpeakingChanged += OnLocalSpeakingChanged;
#endif

    }
    
    void OnDestroy()
    {
#if !UNITY_EDITOR && UNITY_WEBGL
        if(isClient)
            LiveKitNetwork.Instance.Room.LocalParticipant.IsSpeakingChanged -= OnLocalSpeakingChanged;
#endif
    }

    [Server]
    public void StartRound()
    {
        var posLength = World.Instance.RedPositions.transform.childCount;
        var redIndex = 0;
        var blueIndex = 0;
        
        foreach (var p in LiveKitNetwork.Instance.Connections.Values)
        {
            NetworkServer.RemovePlayerForConnection(p.NetConnection, true);

            var pPrefab = LiveKitNetwork.Instance.playerPrefab;

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
            BlueScore++;
            RpcShowRoundWin(Team.Blue, restartDelay);
            StartCoroutine(HandleRestart(restartDelay));
        }
        else if (numBlue == 0)
        {
            RedScore++;
            RpcShowRoundWin(Team.Red, restartDelay);
            StartCoroutine(HandleRestart(restartDelay));
        }
    }

    [Server]
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
    
    [ClientRpc]
    void RpcShowRoundWin(Team team, float time)
    {
        StartCoroutine(ShowRoundWin(team, time));
    }

    [Client]
    IEnumerator ShowRoundWin(Team team, float time)
    {
        var text = team == Team.Red ? RedWon : BlueWon;
        text.gameObject.SetActive(true);

        yield return new WaitForSeconds(time);
        text.gameObject.SetActive(false);
    }
    
    void OnBlueScoreChanged(int old, int neww)
    {
        BlueText.text = neww.ToString();
    }

    void OnRedScoreChanged(int old, int neww)
    {
        RedText.text = neww.ToString();
    }
    
    void OnLocalSpeakingChanged(bool speaking)
    {
        MicroImage.gameObject.SetActive(speaking);
    }
}
