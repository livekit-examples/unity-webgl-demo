using System.Collections;
using System.Collections.Generic;
using LiveKit;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Random = UnityEngine.Random;
using UnityEditor;
using System.Linq;

public enum Team : byte
{
    Red = 0,
    Blue = 1
}

public class Player : NetworkBehaviour
{
    private static readonly int MovingSpeedAnim = Animator.StringToHash("MovingSpeed");
    private static readonly int RotatingSpeedAnim = Animator.StringToHash("RotatingSpeed");
    private static readonly int RotateStateAnim = Animator.StringToHash("Rotating");
    private static readonly int MoveStateAnim = Animator.StringToHash("Moving");
    private static readonly int ShootingAnim = Animator.StringToHash("Shooting");

    public static Dictionary<string, Player> Players = new Dictionary<string, Player>();
    public static Player LocalPlayer { get; private set; }
    
    public delegate void PlayerDelagete(Player player);
    public static event PlayerDelagete PlayerAdded;
    public static event PlayerDelagete PlayerRemoved;

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
    public float Sensitivity = 2.0f;
    public float RotationSpeed = 160.0f;
    public float DamageVignetteDuration = 1.5f;
    public HealthBar HealthBar;

    [ColorUsage(true, true)] public Color RedColor;
    [ColorUsage(true, true)] public Color BlueColor;

    // Minigun
    public ParticleSystem MuzzleEffect;
    public ParticleSystem ImpactEffect;
    public TrailRenderer BulletTracer;
    public GameObject MinigunPoint;
    public GameObject Cursor;
    public int MinigunDamage = 10;
    public float MinigunRate = 1/10f;

    // Synced variables
    [HideInInspector] [SyncVar] public Team Team;
    [HideInInspector] [SyncVar] public string Sid; // Sid of the LiveKit Participant
    [HideInInspector] [SyncVar(hook = nameof(OnShootingChanged))] public bool IsShooting;
    [HideInInspector] [SyncVar(hook = nameof(OnHealthChanged))] public int Health;

    private CharacterController m_Controller;
    private float m_Horizontal, m_Vertical;
    private Vector2 m_Rotation;
    private Vector3 m_Velocity;
    private Coroutine m_VignetteRoutine;
    private float m_LastFire;
    
    // Properties are get in the Start() method 
    private Vector3 m_DefaultOrbitPos;
    private float m_DefaultCameraDistance; // Distance to the orbit

    public Participant Participant { get; private set; } // LiveKit instance

    private static void RegisterPlayer(Player player)
    {
        Players[player.Sid] = player;
        PlayerAdded?.Invoke(player);
    }
    
    private static void UnregisterPlayer(Player player)
    {
        Players.Remove(player.Sid);
        PlayerRemoved?.Invoke(player);
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();

        var connId = connectionToClient.connectionId;
        Team = LiveKitNetwork.Instance.Connections[connId].Team;
        
#if !UNITY_EDITOR && UNITY_WEBGL
        Sid = LiveKitNetwork.Instance.Transport.GetParticipant(connId).Sid;
#else
        Sid = GUID.Generate().ToString();
#endif

        RegisterPlayer(this);
    }
    
    public override void OnStartClient()
    {
        RegisterPlayer(this);

#if !UNITY_EDITOR && UNITY_WEBGL
        if (isLocalPlayer)
        {
            LiveKitNetwork.Instance.Room.LocalParticipant.SetCameraEnabled(true);
            LiveKitNetwork.Instance.Room.LocalParticipant.SetMicrophoneEnabled(true);
        }
#endif
    }

    public override void OnStopClient()
    {
        UnregisterPlayer(this);
        
#if !UNITY_EDITOR && UNITY_WEBGL
        if (isLocalPlayer)
        {
            LiveKitNetwork.Instance.Room.LocalParticipant.SetCameraEnabled(false);
            LiveKitNetwork.Instance.Room.LocalParticipant.SetMicrophoneEnabled(false);
        }
#endif
    }

    public override void OnStopServer()
    {
        UnregisterPlayer(this);
    }
    
    void Start()
    {
        Health = MaxHealth;
        m_Controller = GetComponent<CharacterController>();
        if (isLocalPlayer)
        {
            LocalPlayer = this;
            Projection.SetBackfaceOpacity(0.2f);
        }
        
        if(Spectator.LocalSpectator == null)
            GameManager.Instance.SetCameraStatus(Camera, isLocalPlayer);
        
        Projection.SetColor(Team == Team.Red ? RedColor : BlueColor);
        
        m_DefaultOrbitPos = CameraOrbit.transform.localPosition;
        m_DefaultCameraDistance = Vector3.Distance(CameraOrbit.transform.position, Camera.transform.position);

#if !UNITY_EDITOR && UNITY_WEBGL
        var room = LiveKitNetwork.Instance.Room;
        if (Sid == room.LocalParticipant.Sid)
            Participant = room.LocalParticipant;
        else if (room.Participants.TryGetValue(Sid, out RemoteParticipant p))
            Participant = p;
        else
            Debug.LogError($"Player spawned without corresponding LiveKit Participant (Sid: {Sid})");

        HealthBar.Username.text = Participant.Identity;

        Participant.TrackSubscribed += TrackSubscribed;
        Participant.IsSpeakingChanged += SpeakingChanged;

        if (Participant.VideoTracks.Count >= 1)
        {
            var pub = Participant.VideoTracks.First();
            var track = pub.Value.Track;
            if (track != null)
                Projection.UpdateTrack(track);
        }
#endif

    }

    void Update()
    {
        if (!isLocalPlayer)
            return;

        // Inputs
        m_Vertical = Input.GetAxisRaw("Vertical");
        m_Horizontal = Input.GetAxisRaw("Horizontal");

        var fire = Input.GetButton("Fire1");
        if (fire && !IsShooting)
            CmdUpdateShooting(true);
        else if (!fire && IsShooting)
            CmdUpdateShooting(false);
    }

    void FixedUpdate()
    {
        UpdateMinigun();
        UpdateMovement();
    }

    void UpdateMinigun()
    {
        if (!IsShooting)
        {
            m_LastFire = Time.time;
            return;
        }

        var startDir = MinigunPoint.transform.position;
        var dir = Cursor.transform.position - startDir;

        while (Time.time - m_LastFire >= MinigunRate)
        {
            m_LastFire += MinigunRate;
            
            dir += Random.insideUnitSphere * 0.02f;

            if (!Physics.Raycast(startDir, dir, out RaycastHit hit)) 
                return;
        
            StartCoroutine(ShootEffect(startDir, hit));

            // Apply damage
            if (isServer)
            {
                var rPlayer = hit.transform.GetComponent<Player>();
                if (rPlayer == null || rPlayer.Team == Team)
                    return;
            
                rPlayer.Health -= MinigunDamage;
                if (rPlayer.Health <= 0)
                {
                    var rTransform = rPlayer.transform;
                    RpcExplode(rTransform.position, rTransform.rotation);
                    NetworkServer.Destroy(rPlayer.gameObject);
                    GameManager.Instance.ToSpectator(rPlayer.connectionToClient);
                }
            }
        }
    }

    void UpdateMovement()
    {
        if (!isLocalPlayer) 
            return;
        
        var moving = m_Vertical != 0;
        var rotating = m_Horizontal != 0;

        Animator.SetFloat(MovingSpeedAnim, m_Vertical * Speed / 5);
        Animator.SetBool(MoveStateAnim, moving);
        Animator.SetBool(RotateStateAnim, false);

        m_Controller.Move(Mesh.transform.right * m_Vertical * Speed * Time.deltaTime);

        if (rotating)
        {
            Mesh.transform.localRotation =
                Quaternion.AngleAxis(RotationSpeed * m_Horizontal * Time.deltaTime, Vector3.up)
                * Mesh.transform.localRotation;

            if (!moving)
            {
                Animator.SetFloat(RotatingSpeedAnim, m_Horizontal);
                Animator.SetBool(RotateStateAnim, true);
            }
        }

        // Gravity 
        if (m_Controller.isGrounded && m_Velocity.y < 0)
            m_Velocity.y = 0f;

        m_Velocity.y += Gravity;
        m_Controller.Move(m_Velocity * Time.deltaTime * Time.deltaTime / 2f);
    }

    void LateUpdate()
    {
        if (!isLocalPlayer)
            return;

        // Update camera position
        var deltaX = Input.GetAxis("Mouse X") * Sensitivity;
        var deltaY = Input.GetAxis("Mouse Y") * Sensitivity;

        m_Rotation.x += deltaX;
        m_Rotation.y += deltaY;
        m_Rotation.y = Mathf.Clamp(m_Rotation.y, -20f, 25f);

        var xQuat = Quaternion.AngleAxis(m_Rotation.x, Vector3.up);
        var yQuat = Quaternion.AngleAxis(m_Rotation.y, Vector3.left);

        CameraOrbit.transform.localRotation = xQuat * yQuat;

        if (IsShooting)
            CameraOrbit.transform.localPosition = m_DefaultOrbitPos + Random.insideUnitSphere * 0.07f;
        else
            CameraOrbit.transform.localPosition = m_DefaultOrbitPos;
        
        // Camera raycast (Avoid the camera to be outside the world)
        var origin = CameraOrbit.transform.position;
        var dir = Camera.transform.position - origin;
        dir.Normalize();
        if (Physics.Raycast(origin, dir, out var hit, m_DefaultCameraDistance))
            Camera.transform.position = hit.point - dir * 0.2f;
        else
            Camera.transform.position = origin + dir * m_DefaultCameraDistance;
    }

    IEnumerator ShootEffect(Vector3 start, RaycastHit hit)
    {
        var tracer = Instantiate(BulletTracer, start, Quaternion.identity);
        yield return new WaitForEndOfFrame();
        tracer.transform.position = hit.point;

        var i = Instantiate(ImpactEffect, hit.point, Quaternion.LookRotation(hit.normal));
        i.Play();
    }

    void OnDestroy()
    {
        if(isServer)
            GameManager.Instance.UpdateScore(5f);
        
#if !UNITY_EDITOR && UNITY_WEBGL
        Participant.TrackSubscribed -= TrackSubscribed;
        Participant.IsSpeakingChanged -= SpeakingChanged;
#endif
    }

    [ClientRpc]
    void RpcExplode(Vector3 pos, Quaternion rot)
    {
        Instantiate(PlayerExplosion, pos, rot);
    }
    
    /*
     * Hooks
     */
    
    void TrackSubscribed(Track track, TrackPublication publication)
    {
        if (!(track is RemoteVideoTrack))
            return;
            
        Projection.UpdateTrack(track);
    }

    void SpeakingChanged(bool status)
    {
        SpeakingLight.enabled = status;
    }

    [Command]
    void CmdUpdateShooting(bool status)
    {
        IsShooting = status;
    }

    void OnShootingChanged(bool oldValue, bool newValue)
    {
        if (newValue)
            MuzzleEffect.Play();
        else
            MuzzleEffect.Stop();

        if (hasAuthority)
            Animator.SetBool(ShootingAnim, newValue);
    }

    void OnHealthChanged(int oldValue, int newValue)
    {
        if (newValue < oldValue)
        {
            // Display "damage" effect.
            var pp = Camera.GetComponent<PostProcessVolume>();
            var v = pp.profile.GetSetting<Vignette>();
    
            if(m_VignetteRoutine != null)
                StopCoroutine(m_VignetteRoutine);
            
            m_VignetteRoutine = StartCoroutine(UpdateVignette(v, Time.time));
        }
    }

    IEnumerator UpdateVignette(Vignette vignette, float startTime)
    {
        while(true)
        {
            var dt = Time.time - startTime;
            vignette.color.value = Color.Lerp(Color.red, Color.black, dt/DamageVignetteDuration);

            if (dt >= DamageVignetteDuration)
                break;
                
            yield return new WaitForEndOfFrame();
        }
    }
}