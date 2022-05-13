using System;
using System.Collections.Generic;
using UnityEngine;

public class Protocol
{
    public const int MaxPacketSize = 512; // We shouldn't need more for the demo ( header is included and there's no buffer resizing )

    public static readonly Dictionary<Type, ushort> PacketIds = new Dictionary<Type, ushort>();
    public static readonly Dictionary<ushort, Type> PacketType = new Dictionary<ushort, Type>();

    public static void RegisterPacket(ushort id, Type type)
    {
        PacketIds.Add(type, id);
        PacketType.Add(id, type);
    }

    static Protocol()
    {
        RegisterPacket(ReadyPacket.Id, typeof(ReadyPacket));
        RegisterPacket(ScorePacket.Id, typeof(ScorePacket));
        RegisterPacket(JoinPacket.Id, typeof(JoinPacket));
        RegisterPacket(MovePacket.Id, typeof(MovePacket));
        RegisterPacket(DamagePacket.Id, typeof(DamagePacket));
        RegisterPacket(DeathPacket.Id, typeof(DeathPacket));
        RegisterPacket(PaddingPacket.Id, typeof(PaddingPacket));
        RegisterPacket(ShootingPacket.Id, typeof(ShootingPacket));
        RegisterPacket(AnimationPacket.Id, typeof(AnimationPacket));
    }
}

public interface IPacket
{
    // In reality, this isn't required for blittable structs but needed for more complex structs ( Containing strings ... ) + endianness
    void Serialize(PacketWriter writer) { }
    void Deserialize(PacketReader reader) { }
}

// TODO Remove
public struct PaddingPacket : IPacket
{
    public const ushort Id = 999;
}

public struct ReadyPacket : IPacket
{
    public const ushort Id = 0;
}

public struct ScorePacket : IPacket
{
    public const ushort Id = 16;
    public int Score;
    
    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(Score);
    }

    public void Deserialize(PacketReader reader)
    {
        Score = reader.ReadInt();
    }
}

public struct JoinPacket : IPacket
{
    public const ushort Id = 1;
    public Vector3 WorldPos;
    public int Health;
    public Color Color;
    
    public void Serialize(PacketWriter writer)
    {
        writer.WriteVector3(WorldPos);
        writer.WriteInt(Health);
        writer.WriteColor(Color);
    }

    public void Deserialize(PacketReader reader)
    {
        WorldPos = reader.ReadVector3();
        Health = reader.ReadInt();
        Color = reader.ReadColor();
    }
}

public struct MovePacket : IPacket
{
    public const ushort Id = 2;
    public Vector3 WorldPos;
    public Quaternion WorldAngle;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteVector3(WorldPos);
        writer.WriteQuaternion(WorldAngle);
    }

    public void Deserialize(PacketReader reader)
    {
        WorldPos = reader.ReadVector3();
        WorldAngle = reader.ReadQuaternion();
    }
}

public struct AnimationPacket : IPacket
{
    public const ushort Id = 8;
    public bool MovingAnim;
    public float MovingSpeed;
    public bool RotateAnim;
    public float RotateSpeed;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteBool(MovingAnim);
        writer.WriteSingle(MovingSpeed);
        writer.WriteBool(RotateAnim);
        writer.WriteSingle(RotateSpeed);
    }

    public void Deserialize(PacketReader reader)
    {
        MovingAnim = reader.ReadBool();
        MovingSpeed = reader.ReadSingle();
        RotateAnim = reader.ReadBool();
        RotateSpeed = reader.ReadSingle();
    }
}

public struct DamagePacket : IPacket
{
    public const ushort Id = 4;
    public string Sid;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(Sid);
    }

    public void Deserialize(PacketReader reader)
    {
        Sid = reader.ReadString();
    }
}

public struct DeathPacket : IPacket
{
    public const ushort Id = 5;
    public string KillerSid;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(KillerSid);
    }

    public void Deserialize(PacketReader reader)
    {
        KillerSid = reader.ReadString();
    }
}

public struct ShootingPacket : IPacket
{
    public const ushort Id = 7;
    public bool IsShooting;
    
    public void Serialize(PacketWriter writer)
    {
        writer.WriteBool(IsShooting);
    }

    public void Deserialize(PacketReader reader)
    {
        IsShooting = reader.ReadBool();
    }
}
