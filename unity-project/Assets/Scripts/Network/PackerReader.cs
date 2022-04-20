using System;
using System.IO;
using System.Text;
using UnityEngine;

public class PacketReader
{
    private byte[] m_Buffer;
    private int m_Position;

    void Reset()
    {
        m_Position = 0;
    }
    
    void SetBuffer(byte[] data)
    {
        m_Buffer = data;
    }
    
    public IPacket UnserializePacket(byte[] data)
    {
        Reset();
        SetBuffer(data);

        var id = ReadUShort();
        if (Protocol.PacketType.TryGetValue(id, out var type))
        {
            var v = Activator.CreateInstance(type) as IPacket;
            v.Deserialize(this);
            return v;
        }

        return null; // Invalid packet
    }
    
    /*
     * https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types
     */
    private unsafe T Read<T>() where T : unmanaged
    {
        var size = sizeof(T);
        if (m_Position + size > m_Buffer.Length)
            throw new EndOfStreamException("Can't read, end of data reached");
        
        T val;
        fixed (byte* ptr = &m_Buffer[m_Position])
            val = *(T*)ptr;
        
        m_Position += size;
        return val;
    }

    public byte[] ReadBytes(int count)
    {
        if (m_Position + count > m_Buffer.Length)
            throw new EndOfStreamException("Can't read, end of data reached");
        
        var data = new byte[count];
        Array.Copy(m_Buffer, m_Position, data, 0, count);
        m_Position += count;
        return data;
    }
    
    public string ReadString()
    {
        var size = ReadUShort(); 
        if (size == 0)
            return null;
        
        if (m_Position + size > m_Buffer.Length)
            throw new EndOfStreamException("Can't read, end of data reached");
        
        return Encoding.ASCII.GetString(ReadBytes(size));
    }
    
    public byte ReadByte() => Read<byte>();
    public sbyte ReadSByte() => Read<sbyte>();
    public long ReadLong() => Read<long>();
    public ulong ReadULong() => Read<ulong>();
    public double ReadDouble() => Read<double>();
    public int ReadInt() => Read<int>();
    public uint ReadUInt() => Read<uint>();
    public float ReadSingle() => Read<float>();
    public short ReadShort() => Read<short>();
    public ushort ReadUShort() => Read<ushort>();
    public bool ReadBool() => ReadByte() == 1;
    public Vector3 ReadVector3() => Read<Vector3>();
    public Quaternion ReadQuaternion() => Read<Quaternion>();
    public Color ReadColor() => Read<Color>();
}