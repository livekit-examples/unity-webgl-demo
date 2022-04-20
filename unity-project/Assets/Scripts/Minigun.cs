using System.Collections;
using LiveKit;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Random = UnityEngine.Random;

public class Minigun : MonoBehaviour
{
    private static readonly int ShootingAnim = Animator.StringToHash("Shooting");

    public bool IsShooting { get; private set; } 
    
    public Player Player;
    public ParticleSystem MuzzleEffect;
    public ParticleSystem ImpactEffect;
    public TrailRenderer BulletTracer;
    public GameObject MinigunPoint;
    public int Damage = 25;
    public float FireRate = 1 / 10f;
    public float DamageVignetteDuration = 1.5f;
    
    private Coroutine m_VignetteRoutine;
    private float m_LastFire;

    void Awake()
    {
        NetworkManager.Instance.PacketReceived += PacketReceived;
    }
    
    void OnDestroy()
    {
        NetworkManager.Instance.PacketReceived -= PacketReceived;
    }
    
    void PacketReceived(RemoteParticipant rParticipant, IPacket p, DataPacketKind kind)
    {
        switch (p)
        {
            case DamagePacket packet:
                if (packet.Sid == Player.Participant.Sid)
                {
                    // Display "damage" effect ( Red vignette )
                    var pp = Player.Camera.GetComponent<PostProcessVolume>();
                    var v = pp.profile.GetSetting<Vignette>();

                    if (m_VignetteRoutine != null)
                        StopCoroutine(m_VignetteRoutine);

                    m_VignetteRoutine = StartCoroutine(UpdateVignette(v, Time.time));
                }
                break;
            case ShootingPacket packet:
                if (rParticipant != Player.Participant)
                    return;
                
                OnShootingChanged(packet.IsShooting);
                break;
        }
    }
    
    IEnumerator UpdateVignette(Vignette vignette, float startTime)
    {
        while (true)
        {
            var dt = Time.time - startTime;
            vignette.color.value = Color.Lerp(Color.red, Color.black, dt / DamageVignetteDuration);

            if (dt >= DamageVignetteDuration)
                break;

            yield return new WaitForEndOfFrame();
        }
    }
    
    void Update()
    {
        if (!Player.IsLocalPlayer)
            return;
        
        // Inputs
        var fire = Input.GetButton("Fire1");
        if (fire && !IsShooting)
        {
            NetworkManager.Instance.SendPacket(new ShootingPacket {IsShooting = true}, DataPacketKind.RELIABLE);
            OnShootingChanged(true);
        }
        else if (!fire && IsShooting)
        {
            NetworkManager.Instance.SendPacket(new ShootingPacket {IsShooting = false}, DataPacketKind.RELIABLE);
            OnShootingChanged(false);
        }
    }

    void FixedUpdate()
    {
        if (!IsShooting)
        {
            m_LastFire = Time.time;
            return;
        }

        var startDir = MinigunPoint.transform.position;
        var dir = Player.Cursor.transform.position - startDir;

        while (Time.time - m_LastFire >= FireRate)
        {
            m_LastFire += FireRate;
            dir += Random.insideUnitSphere * 0.02f;

            if (!Physics.Raycast(startDir, dir, out RaycastHit hit))
                return;

            StartCoroutine(ShootEffect(startDir, hit));

            if (Player.IsLocalPlayer)
            {
                var rPlayer = hit.transform.GetComponent<Player>();
                if (rPlayer == null)
                    return;
                
                rPlayer.Health -= Damage;
                NetworkManager.Instance.SendPacket(new DamagePacket
                {
                    Sid = rPlayer.Participant.Sid
                }, DataPacketKind.RELIABLE);
            }
        }
    }
    
    void OnShootingChanged(bool shooting)
    {
        IsShooting = shooting;
        
        if (shooting)
            MuzzleEffect.Play();
        else
            MuzzleEffect.Stop();

        Player.Animator.SetBool(ShootingAnim, shooting);
    }

    IEnumerator ShootEffect(Vector3 start, RaycastHit hit)
    {
        var tracer = Instantiate(BulletTracer, start, Quaternion.identity);
        yield return new WaitForEndOfFrame();
        tracer.transform.position = hit.point;

        var i = Instantiate(ImpactEffect, hit.point, Quaternion.LookRotation(hit.normal));
        i.Play();
    }
}
