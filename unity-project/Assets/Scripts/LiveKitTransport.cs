using System;
using System.Collections;
using System.Collections.Generic;
using LiveKit;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class LiveKitTransport : Transport
{
    private static int NetId = 0;

    public Room Room;
    public Participant Host;

    private readonly Dictionary<int, Participant> m_Participants = new Dictionary<int, Participant>();
    private readonly Dictionary<Participant, int> m_ConnectionIds = new Dictionary<Participant, int>();

    private void HandleRoom()
    {
        Room.DataReceived += (data, p, channel) =>
        {
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

    public Participant GetParticipant(int id)
    {
        if (!m_Participants.ContainsKey(id))
            return null;

        return m_Participants[id];
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
        Room.LocalParticipant.PublishData(segment.ToArray(), channelKind, new[] { Host as RemoteParticipant });
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
        HandleNewParticipant(Host); // Host always have the ID 0

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
        Room.LocalParticipant.PublishData(segment.ToArray(), channelKind, new[] { GetParticipant(connId) as RemoteParticipant });
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
}
