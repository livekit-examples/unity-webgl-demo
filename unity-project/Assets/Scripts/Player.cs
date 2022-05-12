using System.Collections;
using System.Collections.Generic;
using LiveKit;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Linq;

public class Player : MonoBehaviour
{
    private static readonly int MovingSpeedAnim = Animator.StringToHash("MovingSpeed");
    private static readonly int RotatingSpeedAnim = Animator.StringToHash("RotatingSpeed");
    private static readonly int RotateStateAnim = Animator.StringToHash("Rotating");
    private static readonly int MoveStateAnim = Animator.StringToHash("Moving");

    public static Dictionary<string, Player> Players = new Dictionary<string, Player>();

    public static Player LocalPlayer { get; private set; }

    // Properties
    public Light SpeakingLight;
    public PlayerExplosion PlayerExplosion;
    public CameraProjection Projection;
    public GameObject Mesh;
    public GameObject CameraOrbit;
    public Camera Camera;
    public Animator Animator;
    public int MaxHealth = 3000;
    public float Speed = 6.5f;
    public float Gravity = -9.81f;
    public float Sensitivity = 1f;
    public float RotationSpeed = 160.0f;
    public HealthBar HealthBar;
    public GameObject Cursor;
    public Minigun Minigun;

    private CharacterController m_Controller;
    private Vector2 m_Rotation;
    private Vector3 m_Velocity;
    private Vector3 m_DefaultOrbitPos;
    private float m_DefaultCameraDistance; // Distance to the orbit

    // Network 
    private float m_LastHorizontal, m_LastVertical;
    private float m_UpdateTime = 1f / 15f;
    private float m_LastMoveTime;
    private MovePacket? m_LastMovePacket;
    private MovePacket? m_MovePacket;
    
    // Synced variables
    public Participant Participant;
    public bool IsLocalPlayer { get; private set; }
    [HideInInspector] public int Health;
    [HideInInspector] public Color Color;
    
    void Awake()
    {
        NetworkManager.Instance.PacketReceived += PacketReceived;
        
        Health = MaxHealth;
        
        m_Controller = GetComponent<CharacterController>();
        m_DefaultOrbitPos = CameraOrbit.transform.localPosition;
        m_DefaultCameraDistance = Vector3.Distance(CameraOrbit.transform.position, Camera.transform.position);
    }

    void Start()
    {
        if (Players.TryGetValue(Participant.Sid, out var oPlayer))
            DestroyImmediate(oPlayer.gameObject);
        
        Players.Add(Participant.Sid, this);

        IsLocalPlayer = Participant == NetworkManager.Instance.Room.LocalParticipant;
        if (IsLocalPlayer)
        {
            LocalPlayer = this;
            Projection.SetBackfaceOpacity(0.2f);
            StartCoroutine(NetworkPosition());

            NetworkManager.Instance.Room.LocalParticipant.SetCameraEnabled(true);
            NetworkManager.Instance.Room.LocalParticipant.SetMicrophoneEnabled(true);
        }
        
        GameManager.Instance.SetCameraStatus(Camera, IsLocalPlayer);
        Projection.SetColor(Color);

        HealthBar.Username.text = Participant.Identity;
        Participant.TrackSubscribed += TrackSubscribed;
        Participant.LocalTrackPublished += LocalTrackPublished;
        Participant.IsSpeakingChanged += SpeakingChanged;

        if (Participant.VideoTracks.Count >= 1)
        {
            var pub = Participant.VideoTracks.First();
            var track = pub.Value.Track;
            if (track != null)
                Projection.UpdateTrack(track);
        }
    }

    void OnDestroy()
    {
        if (IsLocalPlayer)
        {
            NetworkManager.Instance.Room.LocalParticipant.SetCameraEnabled(false);
            NetworkManager.Instance.Room.LocalParticipant.SetMicrophoneEnabled(false);
            LocalPlayer = null;
        }

        NetworkManager.Instance.PacketReceived -= PacketReceived;
        Participant.TrackSubscribed -= TrackSubscribed;
        Participant.LocalTrackPublished -= LocalTrackPublished;
        Participant.IsSpeakingChanged -= SpeakingChanged;
        
        Players.Remove(Participant.Sid);
    }

    void PacketReceived(RemoteParticipant rParticipant, IPacket p, DataPacketKind kind)
    {
        switch (p)
        {
            case MovePacket packet:
                if (rParticipant != Participant)
                    break;
                
                m_LastMoveTime = Time.time;
                m_LastMovePacket = m_MovePacket;
                m_MovePacket = packet;
                break;
            case AnimationPacket packet:
                if (rParticipant != Participant)
                    break;
                
                Animator.SetBool(MoveStateAnim, packet.MovingAnim);
                Animator.SetBool(RotateStateAnim, packet.RotateAnim);
                Animator.SetFloat(RotatingSpeedAnim, packet.RotateSpeed);
                Animator.SetFloat(MovingSpeedAnim, packet.MovingSpeed);
                break;
            case DeathPacket packet:
                {
                    if (rParticipant != Participant)
                        break;
                    
                    Debug.Log($"Received DeathPacket for {Participant.Sid}");
                    
                    Players.TryGetValue(packet.KillerSid, out var killer);
                    Kill(killer);
                }
                break;
            case DamagePacket packet:
                if (packet.Sid == Participant.Sid)
                {
                    Health -= Minigun.Damage;
                    if (Health <= 0 && IsLocalPlayer)
                    {
                        NetworkManager.Instance.SendPacket(new DeathPacket
                        {
                            KillerSid = rParticipant.Sid
                        }, DataPacketKind.RELIABLE);
                        
                        Players.TryGetValue(rParticipant.Sid, out var killer);
                        Kill(killer);
                    }
                }
                break;
        }
    }

    void Update()
    {
        if (m_LastMovePacket != null)
        {
            // Really simple interpolation
            var t = (Time.time - m_LastMoveTime) / m_UpdateTime;
            var last = m_LastMovePacket.Value;
            var now = m_MovePacket.Value;
            
            transform.position = Vector3.LerpUnclamped(last.WorldPos, now.WorldPos, t);
            Mesh.transform.localRotation = Quaternion.LerpUnclamped(last.WorldAngle, now.WorldAngle, t);
        }

        if (IsLocalPlayer)
        {
            var vertical = Input.GetAxisRaw("Vertical");
            var horizontal = Input.GetAxisRaw("Horizontal");
            var moving = vertical != 0;
            var rotating = horizontal != 0;

            Animator.SetFloat(MovingSpeedAnim, vertical * Speed / 5);
            Animator.SetBool(MoveStateAnim, moving);
            Animator.SetBool(RotateStateAnim, false);

            m_Controller.Move(Mesh.transform.right * vertical * Speed * Time.deltaTime);

            if (rotating)
            {
                Mesh.transform.localRotation =
                    Quaternion.AngleAxis(RotationSpeed * horizontal * Time.deltaTime, Vector3.up)
                    * Mesh.transform.localRotation;

                if (!moving)
                {
                    Animator.SetFloat(RotatingSpeedAnim, horizontal);
                    Animator.SetBool(RotateStateAnim, true);
                }
            }

            // Gravity 
            if (m_Controller.isGrounded && m_Velocity.y < 0)
                m_Velocity.y = 0f;

            m_Velocity.y += Gravity;
            m_Controller.Move(m_Velocity * Time.deltaTime * Time.deltaTime / 2f);

            if (vertical != m_LastVertical || horizontal != m_LastHorizontal)
            {
                // Send animation packet
                NetworkManager.Instance.SendPacket(new AnimationPacket
                {
                    MovingAnim = Animator.GetBool(MoveStateAnim),
                    RotateAnim = Animator.GetBool(RotateStateAnim),
                    RotateSpeed = Animator.GetFloat(RotatingSpeedAnim),
                    MovingSpeed = Animator.GetFloat(MovingSpeedAnim)
                }, DataPacketKind.RELIABLE);
            }
        
            m_LastVertical = vertical;
            m_LastHorizontal = horizontal;
        }
    }

    void LateUpdate()
    {
        // Update camera position
        if (GameManager.Instance.ActiveCamera != Camera)
            return;

        var deltaX = Input.GetAxis("Mouse X") * Sensitivity;
        var deltaY = Input.GetAxis("Mouse Y") * Sensitivity;

        m_Rotation.x += deltaX;
        m_Rotation.y += deltaY;
        m_Rotation.y = Mathf.Clamp(m_Rotation.y, -20f, 25f);

        var xQuat = Quaternion.AngleAxis(m_Rotation.x, Vector3.up);
        var yQuat = Quaternion.AngleAxis(m_Rotation.y, Vector3.left);

        CameraOrbit.transform.localRotation = xQuat * yQuat;

        if (Minigun.IsShooting)
            CameraOrbit.transform.localPosition = m_DefaultOrbitPos + Random.insideUnitSphere * 0.07f;
        else
            CameraOrbit.transform.localPosition = m_DefaultOrbitPos;

        // Camera raycast (Avoid the camera to be outside the world)
        var origin = CameraOrbit.transform.position;
        var dir = Camera.transform.position - origin;
        dir.Normalize();
        
        if(dir == Vector3.zero)
            dir = Vector3.forward;
        
        if (Physics.Raycast(origin, dir, out var hit, m_DefaultCameraDistance))
            Camera.transform.position = hit.point - dir * 0.2f;
        else
            Camera.transform.position = origin + dir * m_DefaultCameraDistance;
    }
    
    IEnumerator NetworkPosition()
    {
        while (true)
        {
            NetworkManager.Instance.SendPacket(new MovePacket
            {
                WorldPos = transform.position,
                WorldAngle = Mesh.transform.localRotation
            }, DataPacketKind.LOSSY);

            yield return new WaitForSecondsRealtime(m_UpdateTime);
        }
    }

    void Kill(Player killer)
    {
        Instantiate(PlayerExplosion, Mesh.transform.position, Mesh.transform.rotation);
        Destroy(gameObject);

        if (killer != null)
        {
            GameManager.Instance.SetScore(killer.Participant, GameManager.Instance.GetScore(killer.Participant) + 1);
            
            if (IsLocalPlayer)
                GameManager.Instance.SpectateKiller(killer);
        }
    }
    
    /*
     * Hooks
     */

    void HandleCamTrack(Track track)
    {
        Projection.UpdateTrack(track);
    }

    void LocalTrackPublished(TrackPublication publication)
    {
        if (!(publication.Track is LocalVideoTrack))
            return;

        HandleCamTrack(publication.Track);
    }

    void TrackSubscribed(Track track, TrackPublication publication)
    {
        if (!(track is RemoteVideoTrack))
            return;

        HandleCamTrack(track);
    }

    void SpeakingChanged(bool status)
    {
        SpeakingLight.enabled = status;
    }
}