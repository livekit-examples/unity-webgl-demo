using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LiveKit;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class JoinHandler : MonoBehaviour
{
    public static Color SelectedColor { get; private set; }
    public static LocalVideoTrack SelectedCamera { get; private set; }
    public static string RoomName { get; private set; }
    public static string Username { get; private set; }
    
    public Button JoinButton;
    public RawImage CameraPreview;
    public TMP_Text NoCamera;
    public TMP_InputField RoomInput;
    public TMP_InputField NameInput;
    public CameraProjection Projection;
    public RectTransform ColorPicker;
    public TMP_Dropdown CamDropdown;
    [ColorUsageAttribute(false, true)] public List<Color> Colors;

    private JSArray<MediaDeviceInfo> m_Devices;

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
            RoomName = RoomInput.text.Trim();
            Username = NameInput.text.Trim();

            if (Username.Length == 0)
                return;

            if (RoomName.Length == 0)
                return;

            SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        });

        Projection.UpdateTrack(null);
        StartCoroutine(StartPreviewCamera());
    }

    private void SetDeviceId(string deviceId)
    {
        StartCoroutine(SetDeviceIdHandler(deviceId));
    }
    
    // Update the LocalVideoTrack
    IEnumerator SetDeviceIdHandler(string deviceId)
    {
        SelectedCamera?.Stop();
        SelectedCamera?.Detach();
        
        var f = Client.CreateLocalVideoTrack(new VideoCaptureOptions()
        {
            DeviceId = deviceId
        });
        
        yield return f;

        if (f.IsError)
            yield break;

        SelectedCamera = f.ResolveValue;
        
        Projection.UpdateTrack(SelectedCamera);
        NoCamera.gameObject.SetActive(false);

        var video = SelectedCamera.Attach() as HTMLVideoElement;
        video.VideoReceived += tex =>
        {
            CameraPreview.color = Color.white;
            CameraPreview.texture = tex;
        };
    }
    
    IEnumerator StartPreviewCamera()
    {
        CamDropdown.ClearOptions();

        var devicesOp = Room.GetLocalDevices(MediaDeviceKind.VideoInput);
        yield return devicesOp;
        
        if (devicesOp.IsError)
            yield break;

        m_Devices = devicesOp.ResolveValue;
        foreach (var d in m_Devices)
        {
            CamDropdown.options.Add(new TMP_Dropdown.OptionData()
            {
                text = d.Label
            });
        }
        
        CamDropdown.onValueChanged.AddListener((value) =>
        {
            if (value >= 0 && value < m_Devices.Count)
                SetDeviceId(m_Devices[value].DeviceId);
        });

        if (m_Devices.Count >= 1)
        {
            SetDeviceId(m_Devices.First().DeviceId);
            CamDropdown.value = 0;
            CamDropdown.RefreshShownValue();
        }
    }
    
    void OnDestroy()
    {
        SelectedCamera?.Detach();
    }
}
