using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LiveKit;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class LiveKitTransport : Transport
{
    struct DataInfo
    {
        public byte[] Data;
        public RemoteParticipant Destination;
        public DataPacketKind Channel;
    }
    
    private static int NetId = 1; // 0 is for LocalPlayer in Mirror
    private readonly Dictionary<int, Participant> m_Participants = new Dictionary<int, Participant>();
    private readonly Dictionary<Participant, int> m_ConnectionIds = new Dictionary<Participant, int>();
    private Queue<DataInfo> m_DataQueue = new Queue<DataInfo>();
    
    public Room Room;
    public Participant Host;

    void Awake()
    {
        StartCoroutine(HandleData());
    }

    IEnumerator HandleData()
    {
        while (true)
        {
            while (m_DataQueue.Any())
            {
                var info = m_DataQueue.Dequeue();
                yield return Room.LocalParticipant.PublishData(info.Data, info.Channel, new[] { info.Destination });
            }

            yield return new WaitForEndOfFrame();
        }
    }

    void HandleRoom()
    {
        Room.DataReceived += (data, p, channel) =>
        {
            if (p == null)
                return; // Ignore

            var channelId = channel == DataPacketKind.RELIABLE ? Channels.Reliable : Channels.Unreliable;
            if (p == Host)
                OnClientDataReceived.Invoke(new ArraySegment<byte>(data), channelId);
            else
                OnServerDataReceived.Invoke(m_ConnectionIds[p], new ArraySegment<byte>(data), channelId);
        };
    }
    
    public override bool Available()
    {
        return Application.platform == RuntimePlatform.WebGLPlayer;
    }

    public override bool ClientConnected()
    {
        return Room.State == RoomState.Connected;
    }
    
    public override void ClientConnect(string address)
    {
        HandleRoom();
        StartCoroutine(FakeConnect());
    }

    // We can't connect directly, otherwise Mirror doesn't work..
    private IEnumerator FakeConnect()
    {
        yield return new WaitForEndOfFrame();
        OnClientConnected.Invoke();
    }

    public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable)
    {
        var channelKind = channelId == Channels.Reliable ? DataPacketKind.RELIABLE : DataPacketKind.LOSSY;
        m_DataQueue.Enqueue(new DataInfo()
        {
            Data = segment.ToArray(),
            Channel = channelKind,
            Destination = Host as RemoteParticipant
        });
    }

    public override void ClientDisconnect()
    {
        Room.Disconnect();
    }

    public override Uri ServerUri()
    {
        return null;
    }

    public override bool ServerActive()
    {
        return true;
    }

    public override void ServerStart()
    {
        HandleRoom();

        foreach (var p in Room.Participants.Values)
            HandleNewParticipant(p);

        Room.ParticipantConnected += HandleNewParticipant;
        Room.ParticipantDisconnected += p =>
        {
            var i = m_ConnectionIds[p];
            OnServerDisconnected.Invoke(i);
            m_Participants.Remove(i);
            m_ConnectionIds.Remove(p);
        };
    }

    private void HandleNewParticipant(Participant p)
    {
        var id = NetId++;
        m_ConnectionIds.Add(p, id);
        m_Participants.Add(id, p);
        OnServerConnected.Invoke(id);
    }

    public override void ServerSend(int connId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
    {
        var channelKind = channelId == Channels.Reliable ? DataPacketKind.RELIABLE : DataPacketKind.LOSSY;
        m_DataQueue.Enqueue(new DataInfo()
        {
            Data = segment.ToArray(),
            Channel = channelKind,
            Destination = GetParticipant(connId) as RemoteParticipant
        });
    }

    public override void ServerDisconnect(int connectionId)
    {
        // TODO We should kick someone using the roomAdmin permission in the HostToken
    }

    public override string ServerGetClientAddress(int connectionId)
    {
        return null;
    }

    public override void ServerStop()
    {
        Room.Disconnect();
    }

    public override int GetMaxPacketSize(int channelId = Channels.Reliable)
    {
        return 16000;
    }

    public override void Shutdown()
    {

    }
    
    public Participant GetParticipant(int id)
    {
        return !m_Participants.ContainsKey(id) ? null : m_Participants[id];
    }
}
