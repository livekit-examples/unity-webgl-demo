using System;
using System.Text;
using UnityEngine;

public class PacketWriter
{
    private byte[] m_Buffer = new byte[Protocol.MaxPacketSize];
    private int m_Position;

    void Reset()
    {
        m_Position = 0;
    }

    public ArraySegment<byte> SerializePacket<T>(T packet) where T : IPacket
    {
        var id = Protocol.PacketIds[typeof(T)];
        Reset();
        WriteUShort(id);
        packet.Serialize(this);
        return new ArraySegment<byte>(m_Buffer, 0, m_Position);
    }

    /*
     * https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types
     */
    private unsafe void Write<T>(T val) where T : unmanaged
    {
        var size = sizeof(T);
        if (m_Buffer.Length < m_Position + size)
            throw new ArgumentException("Max packet size exceeded");

        fixed (byte* ptr = &m_Buffer[m_Position])
            *(T*) ptr = val;

        m_Position += size;
    }
    
    public void WriteBytes(byte[] val, int offset, int count)
    {
        if (m_Buffer.Length < m_Position + count)
            throw new ArgumentException("Max packet size exceeded");
        
        Array.ConstrainedCopy(val, offset, m_Buffer, m_Position, count);
        m_Position += count;
    }
    
    public void WriteString(string val)
    {
        if (val == null)
        {
            WriteUShort(0);
            return;
        }
        
        var data = Encoding.ASCII.GetBytes(val);
        WriteUShort(checked((ushort)val.Length)); // null terminated string instead ?
        WriteBytes(data, 0, data.Length);
    }
    
    public void WriteByte(byte val) => Write(val);
    public void WriteSByte(sbyte val) => Write(val);
    public void WriteLong(long val) => Write(val);
    public void WriteULong(ulong val) => Write(val);
    public void WriteDouble(double val) => Write(val);
    public void WriteInt(int val) => Write(val);
    public void WriteUInt(uint val) => Write(val);
    public void WriteSingle(float val) => Write(val);
    public void WriteShort(short val) => Write(val);
    public void WriteUShort(ushort val) => Write(val);
    public void WriteBool(bool val) => WriteByte((byte) (val ? 1 : 0));
    public void WriteVector3(Vector3 val) => Write(val);
    public void WriteQuaternion(Quaternion val) => Write(val);
    public void WriteColor(Color val) => Write(val);
}
