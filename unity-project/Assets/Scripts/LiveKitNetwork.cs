using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LiveKit;
using UnityEngine;
using Mirror;
using Twirp;
using UnityProtocol;

public class ClientConnection
{
    public readonly NetworkConnectionToClient NetConnection;
    public Team Team;

    public ClientConnection(NetworkConnectionToClient conn)
    {
        NetConnection = conn;
    }
}

public class LiveKitNetwork : NetworkManager
{
    public readonly Dictionary<int, ClientConnection> Connections = new Dictionary<int, ClientConnection>();
    
    [Header("LiveKit")]
    public string ServiceURL = "http://localhost:8080";
    public string LiveKitURL = "ws://localhost:7880";
    
    public LiveKitTransport Transport { get; private set; }
    public UnityServiceClient UnityService { get; private set; }
    public Room Room { get; private set; }

    public override void Awake()
    {
        base.Awake();
        UnityService = new UnityServiceClient(this, ServiceURL, 10);
        Transport = GetComponent<LiveKitTransport>();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // Do nothing
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"New player joined {conn.connectionId}");
    }

    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);
        
        Debug.Log($"Player ready {conn.connectionId}");
        var client = new ClientConnection(conn);
        Connections.Add(conn.connectionId, client);
        
        // Find a team
        client.Team = Team.Blue; // Blue by default

        var numBlue = 0;
        foreach (var p in Connections)
            if (p.Value.Team == Team.Blue)
                numBlue++;

        if (numBlue > Connections.Count - numBlue)
            client.Team = Team.Red;

        if (Connections.Count <= 2)
            GameManager.Instance.StartRound();
        else
            GameManager.Instance.AddSpectator(conn);
    }
    
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Connections.Remove(conn.connectionId);
    }

    public override void OnClientConnect()
    {
        if (!NetworkClient.ready)
            NetworkClient.Ready();
    }

    public override void OnClientSceneChanged()
    {
        
    }
    
    public IEnumerator JoinRoom(string room, string username, bool host)
    {
#if !UNITY_EDITOR && UNITY_WEBGL
        Room = new Room();

        Room.TrackSubscribed += (track, publication, participant) =>
        {
            if (track.Kind == TrackKind.Audio)
                track.Attach();
        };
        
        Room.TrackUnsubscribed += (track, publication, participant) =>
        {
            track.Detach();
        };

        var req = new JoinTokenRequest
        {
            ParticipantName = username,
            RoomName = room,
            Host = host
        };

        var tokenOp = UnityService.RequestJoinToken(req);
        yield return tokenOp;

        if (tokenOp.IsError)
        {
            Debug.Log("An error occurred while getting a join token");
            yield break;
        }

        var conOptions = new RoomConnectOptions()
        {
            AutoSubscribe = true
        };

        var joinOp = Room.Connect(LiveKitURL, tokenOp.Resp.JoinToken, conOptions);
        yield return joinOp;


        Debug.Log(joinOp);
        Debug.Log(joinOp.IsError);
        if (joinOp.IsError)
        {
            Debug.Log("An error occurred while joining the room");
            yield break;
        }

        Debug.Log("Connected to the room");

        Transport.Room = Room;

        if (host)
        {
            Transport.Host = Room.LocalParticipant;
        }
        else
        {
            foreach (var p in Room.Participants.Values)
            {
                if (p.Metadata == null)
                    continue;

                try
                {
                    var m = ParticipantMetadata.Parser.ParseJson(p.Metadata);
                    if (!m.IsHost) 
                        continue;

                    Debug.Log("Found host");
                    Transport.Host = p; // Found
                    break;
                }
                catch
                {
                    // ignored
                }
            }

            if (Transport.Host == null)
            {
                Room.Disconnect();
                Debug.Log("Host wasn't found !");
                yield break;
            }
        }

        Room.ParticipantDisconnected += p =>
        {
            if (p == Transport.Host)
            {
                Room.Disconnect();
            }
        };

        yield return Room.LocalParticipant.EnableCameraAndMicrophone();
#endif
        ServerChangeScene("GameScene");
        
        if (host)
            StartHost();
        else
            StartClient();

        yield break;
    }
}
