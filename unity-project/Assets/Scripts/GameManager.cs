using System.Collections;
using System.Collections.Generic;
using LiveKit;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    // Properties
    public float HearDistance = 15f;
    public Player PlayerPrefab;
    public Transform SpawnPositions;
    public UI UI;

    public Dictionary<Participant, int> Scores = new Dictionary<Participant, int>();
    
    [HideInInspector] public Camera ActiveCamera;
    
    private bool m_RoundRestarting;

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

    IEnumerator Start()
    {
        NetworkManager.Instance.Room.ParticipantConnected += participant =>
        {
            Debug.Log($"Participant connected : {participant.Sid}");
        };
        
        NetworkManager.Instance.Room.ParticipantDisconnected += participant =>
        {
            if (Player.Players.TryGetValue(participant.Sid, out var player))
                Destroy(player.gameObject);
        };
        
        NetworkManager.Instance.Room.TrackSubscribed += (track, publication, participant) =>
        {
            if (track.Kind == TrackKind.Audio)
                track.Attach();
        };
        
        NetworkManager.Instance.Room.TrackUnsubscribed += (track, publication, participant) =>
        {
            if (track.Kind == TrackKind.Audio)
                track.Detach();
        };
        
        NetworkManager.Instance.PacketReceived += PacketReceived;
        Debug.Log($"LocalParticipant SID: {NetworkManager.Instance.Room.LocalParticipant.Sid}");

        for (var i = 0; i < 20; i++)
        {
            yield return NetworkManager.Instance.SendPacket(new PaddingPacket(), DataPacketKind.RELIABLE);
            yield return new WaitForSeconds(0.75f / 20f);
        }
        
        Debug.Log("Sending Ready");
        yield return NetworkManager.Instance.SendPacket(new ReadyPacket(), DataPacketKind.RELIABLE);
        
        JoinGame();
        UI.UpdateRanking();
    }
    
    void PacketReceived(RemoteParticipant participant, IPacket p, DataPacketKind kind)
    {
        switch (p)
        {
            case ReadyPacket:
                Debug.Log($"{participant.Sid} is ready");
                
                // Player is ready, send local info
                var lp = Player.LocalPlayer;
                if (lp != null)
                {
                    Debug.Log($"\tCurrently playing, sending JoinPacket to {participant.Sid}");
                    NetworkManager.Instance.SendPacket(new JoinPacket
                    {
                        WorldPos = lp.transform.position,
                        Health = lp.Health,
                        Color = lp.Color
                    }, DataPacketKind.RELIABLE, participant);
                }

                Scores.TryGetValue(NetworkManager.Instance.Room.LocalParticipant, out var score);
                NetworkManager.Instance.SendPacket(new ScorePacket
                {
                    Score = score
                }, DataPacketKind.RELIABLE, participant);
                
                break;
            case ScorePacket packet:
                SetScore(participant, packet.Score + GetScore(participant));
                break;
            case JoinPacket packet:
                Debug.Log($"Received JoinPacket from {participant.Sid}, creating a player");
                CreatePlayer(participant, packet.WorldPos, packet.Color);
                break;
        }
    }

    Player CreatePlayer(Participant participant, Vector3 pos, Color color)
    {
        var p = Instantiate(PlayerPrefab, pos, Quaternion.identity);
        p.Participant = participant;
        p.Color = color;
        return p;
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.Space))
            Cursor.lockState = CursorLockMode.None;
        
        if (Input.GetMouseButtonDown(0))
            Cursor.lockState = CursorLockMode.Locked;
    }

    void FixedUpdate()
    {
        // Really simple spatial audio
        if (ActiveCamera == null)
            return;
        
        foreach (var p in Player.Players)
        {
            var participant = p.Value.Participant;
            if (participant == null || participant is LocalParticipant) 
                continue;

            var track = participant.GetTrack(TrackSource.Microphone)?.Track as RemoteAudioTrack;
            if(track == null)
                continue;
            
            var dist = Vector3.Distance(ActiveCamera.transform.position, p.Value.transform.position);
            var volume = 1f - Mathf.Clamp(dist / HearDistance, 0f, 1f);
            track.SetVolume(volume);
        } 
    }
    
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

    public void JoinGame()
    {
        Debug.Log("Joining the game & tell the others");
        var r = Random.Range(0, SpawnPositions.childCount);
        var startPos = SpawnPositions.GetChild(r).position;
        
        var player = CreatePlayer(NetworkManager.Instance.Room.LocalParticipant, startPos, JoinHandler.SelectedColor);
        NetworkManager.Instance.SendPacket(new JoinPacket
        {
            WorldPos = startPos,
            Health = player.Health,
            Color = player.Color
        }, DataPacketKind.RELIABLE);
    }

    public void SetScore(Participant participant, int score)
    {
        Scores[participant] = score;
        UI.UpdateRanking();
    }

    public int GetScore(Participant participant)
    {
        Scores.TryGetValue(participant, out int score); // Default to 0
        return score;
    }

    public void SpectateKiller(Player killer, int respawnDelay = 8)
    {
        Debug.Log($"Spectating {killer.Participant.Sid}");
        UI.ShowKilled(killer);
        SetCameraStatus(killer.Camera, true);
        StartCoroutine(WaitRespawn(respawnDelay));
    }

    IEnumerator WaitRespawn(int delay)
    {
        yield return new WaitForSeconds(delay);
        UI.HideKilled();
        JoinGame();
    }
}
