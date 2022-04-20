using System;
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

    void Awake()
    {
        m_MeshRenderer = GetComponent<MeshRenderer>();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (m_Video != null)
            m_Video.VideoReceived -= VideoReceived;
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
            gameObject.SetActive(false);
            return;
        }

        if (Track == nTrack)
            return;

        Track?.Detach();
        Track = nTrack;

        if (m_Video != null)
            m_Video.VideoReceived -= VideoReceived;

        m_Video = Track.Attach() as HTMLVideoElement;
        m_Video.VideoReceived += VideoReceived;
    }

    void VideoReceived(Texture2D tex)
    {
        m_MeshRenderer.material.SetTexture(CameraTexture, tex);
        gameObject.SetActive(true);
    }
}
