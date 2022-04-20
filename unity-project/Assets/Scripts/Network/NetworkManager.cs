using System.Collections;
using LiveKit;
using Twirp;
using UnityEngine;
using UnityProtocol;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    
    public delegate void PacketReceivedDelegate(RemoteParticipant participant, IPacket packet, DataPacketKind kind);
    public event PacketReceivedDelegate PacketReceived;

    public delegate void ConnectedDelegate(Room room);
    public event ConnectedDelegate Connected;
    
    public delegate void ConnectionFailedDelegate();
    public event ConnectionFailedDelegate ConnectionFailed;
    
    public string ServiceURL = "http://localhost:8080";
    public string LiveKitURL = "ws://localhost:7880";
    
    public Room Room { get; private set; }
    public UnityServiceClient UnityService { get; private set; }

    private bool m_Connecting;
    private PacketReader m_PacketReader;
    private PacketWriter m_PacketWriter;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(this);
        Instance = this;
        
        UnityService = new UnityServiceClient(this, ServiceURL, 5);
    }

    public IEnumerator StartNetwork(string room, string username)
    {
        if (m_Connecting)
            yield break;

        m_Connecting = true;
        var req = new JoinTokenRequest
        {
            ParticipantName = username,
            RoomName = room
        };

        var tokenOp = UnityService.RequestJoinToken(req);
        yield return tokenOp;

        if (tokenOp.IsError)
        {
            Debug.LogError("An error occurred while getting a join token");
            m_Connecting = false;
            ConnectionFailed?.Invoke();
            yield break;
        }
        
        Room = new Room();
        m_PacketReader = new PacketReader();
        m_PacketWriter = new PacketWriter();
        
        Room.DataReceived += DataReceived;
        
        var joinOp = Room.Connect(LiveKitURL, tokenOp.Resp.JoinToken);
        yield return joinOp;

        if (joinOp.IsError)
        {
            Debug.LogError("An error occurred while joining the room");
            m_Connecting = false;
            ConnectionFailed?.Invoke();
            yield break;
        }
        
        Connected?.Invoke(Room);
        m_Connecting = false;
    }

    private void DataReceived(byte[] data, RemoteParticipant participant, DataPacketKind? kind)
    {
        if (participant == null)
            return; // Ignore packets coming from the Server API

        var packet = m_PacketReader.UnserializePacket(data);
        if (packet == null)
            return;
        
        PacketReceived?.Invoke(participant, packet, (DataPacketKind) kind);
    }

    public IEnumerator SendPacket<T>(T packet, DataPacketKind kind, params RemoteParticipant[] participants) where T : IPacket
    {
        var data = m_PacketWriter.SerializePacket(packet);
        return Room.LocalParticipant.PublishData(data.Array, data.Offset, data.Count, kind, participants);
    }
} 