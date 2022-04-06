using LiveKit;
using UnityEngine;

public class CameraProjection : MonoBehaviour
{
    private static readonly int CameraTexture = Shader.PropertyToID("_CameraTexture");
    private static readonly int Color = Shader.PropertyToID("_MainColor");
    private static readonly int BackfaceOpacity = Shader.PropertyToID("_BackfaceOpacity");
    
    private MeshRenderer m_MeshRenderer;
    private HTMLVideoElement m_Video;

    public Track Track { get; private set; }

    private void Awake()
    {
        m_MeshRenderer = GetComponent<MeshRenderer>();
        enabled = false;
    }

    public void SetColor(Color color)
    {
        m_MeshRenderer.material.SetColor(Color, color);
    }

    public void SetBackfaceOpacity(float opacity)
    {
        m_MeshRenderer.material.SetFloat(BackfaceOpacity, opacity);
    }

    public void UpdateTrack(Track nTrack)
    {
        if (nTrack == null)
        {
            enabled = false;
            return;
        }
        
        if (!(nTrack is LocalVideoTrack) && !(nTrack is RemoteVideoTrack))
            return;

        if (Track == nTrack)
            return;

        Track?.Detach();
        Track = nTrack;

        var video = Track.Attach() as HTMLVideoElement;
        video.VideoReceived += tex =>
        {
            m_MeshRenderer.material.SetTexture(CameraTexture, tex);
            enabled = true;
        };
    }
}
