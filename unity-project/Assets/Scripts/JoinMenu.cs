using System.Collections;
using LiveKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinMenu : MonoBehaviour
{
    public Button JoinButton;
    public Button HostButton;
    public RawImage CameraPreview;
    public TMP_Text NoCamera;
    public TMP_InputField RoomInput;
    public TMP_InputField NameInput;

    private bool m_Connecting;
    private LocalVideoTrack m_PreviewTrack;    
    
    void Awake()
    {
        JoinButton.onClick.AddListener(() => JoinRoom(false));
        HostButton.onClick.AddListener(() => JoinRoom(true));
        
#if !UNITY_EDITOR && UNITY_WEBGL
        StartCoroutine(StartPreviewCamera());
#endif
    }

    private IEnumerator StartPreviewCamera()
    {
        var f = Client.CreateLocalVideoTrack();
        yield return f;

        if (f.IsError)
            yield break;

        m_PreviewTrack = f.ResolveValue;

        var video = m_PreviewTrack.Attach() as HTMLVideoElement;
        CameraPreview.color = Color.white;
        Destroy(NoCamera);
        
        video.VideoReceived += tex => CameraPreview.texture = tex;
    }
    
    void JoinRoom(bool host)
    {
        if (m_Connecting)
            return;

        var room = RoomInput.text.Trim();
        var username = NameInput.text.Trim();

#if !UNITY_EDITOR && UNITY_WEBGL
        if (username.Length == 0)
            return;

        if (room.Length == 0)
            return;
#endif
        
        m_Connecting = true;
        LiveKitNetwork.Instance.StartCoroutine(JoinRoutine(username, room, host));
    }

    void OnDestroy()
    {
        m_PreviewTrack?.Detach();
    }

    IEnumerator JoinRoutine(string name, string room, bool host)
    {
        yield return LiveKitNetwork.Instance.JoinRoom(room, name, host);
        m_Connecting = false;
    }
}
