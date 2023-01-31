using LiveKit;
using Twirp;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    public delegate void PacketReceivedDelegate(RemoteParticipant participant, IPacket packet, DataPacketKind kind);
    public event PacketReceivedDelegate PacketReceived;

    public delegate void RoomCreatedDelegate(Room room);
    public event RoomCreatedDelegate RoomCreated;

    public string ServiceURL = "http://localhost:8080";
    public string LiveKitURL = "ws://localhost:7880";

    public Room Room { get; private set; }
    public UnityServiceClient UnityService { get; private set; }

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

    public ConnectOperation StartNetwork(string token)
    {
        Room = new Room();
        RoomCreated?.Invoke(Room);

        m_PacketReader = new PacketReader();
        m_PacketWriter = new PacketWriter();
        Room.DataReceived += DataReceived;

        return Room.Connect(LiveKitURL, token);
    }

    private void DataReceived(byte[] data, RemoteParticipant participant, DataPacketKind? kind)
    {
        if (participant == null)
        {
            Debug.Log("Received a packet coming from the Server API ? ( Ignoring ..) ");
            return;
        }

        var packet = m_PacketReader.UnserializePacket(data);
        if (packet == null)
        {
            Debug.LogError($"Failed to unserialize incoming packet from {participant.Sid}");
            return;
        }

        PacketReceived?.Invoke(participant, packet, (DataPacketKind)kind);
    }

    public JSPromise SendPacket<T>(T packet, DataPacketKind kind, params RemoteParticipant[] participants) where T : IPacket
    {
        var data = m_PacketWriter.SerializePacket(packet);
        return Room.LocalParticipant.PublishData(data.Array, data.Offset, data.Count, kind, participants);
    }
}
