using System.Collections;
using System.Collections.Generic;
using LiveKit;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class JoinHandler : MonoBehaviour
{
    public static Color SelectedColor { get; private set; }
    
    public Button JoinButton;
    public RawImage CameraPreview;
    public TMP_Text NoCamera;
    public TMP_InputField RoomInput;
    public TMP_InputField NameInput;
    public CameraProjection Projection;
    public RectTransform ColorPicker;

    [ColorUsageAttribute(false,true)] public List<Color> Colors;

    private LocalVideoTrack m_PreviewTrack;
    
    void Start()
    {
        // Random color by default
        SelectedColor = Colors[Random.Range(0, ColorPicker.childCount)];
        Projection.SetColor(SelectedColor);
        
        foreach (Transform c in ColorPicker)
        {
            var btn = c.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                SelectedColor = Colors[c.GetSiblingIndex()];
                Projection.SetColor(SelectedColor);
            });
        }
        
        JoinButton.onClick.AddListener(() =>
        {
            var room = RoomInput.text.Trim();
            var username = NameInput.text.Trim();

            if (username.Length == 0)
                return;

            if (room.Length == 0)
                return;

            StartCoroutine(NetworkManager.Instance.StartNetwork(room, username));
        });

        NetworkManager.Instance.Connected += room =>
        {
            Debug.Log("Connected to the room, changing scene...");
            SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        };

        Projection.UpdateTrack(null);
        StartCoroutine(StartPreviewCamera());
    }

    IEnumerator StartPreviewCamera()
    {
        var f = Client.CreateLocalVideoTrack();
        yield return f;

        if (f.IsError)
            yield break;

        m_PreviewTrack = f.ResolveValue;

        Projection.UpdateTrack(m_PreviewTrack);
        var video = m_PreviewTrack.Attach() as HTMLVideoElement;
        CameraPreview.color = Color.white;
        Destroy(NoCamera);
        
        video.VideoReceived += tex => CameraPreview.texture = tex;
    }
    
    void OnDestroy()
    {
        m_PreviewTrack?.Detach();
    }
}
