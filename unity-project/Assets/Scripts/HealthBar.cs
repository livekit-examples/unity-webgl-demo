using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Image Blue;
    public Image Red;
    public TMP_Text Username;
    public Player Player;
    
    private float m_RedSpeed;
    
    void Start()
    {
        if (Player.IsLocalPlayer)
            Destroy(gameObject);

        m_RedSpeed = Player.MaxHealth / 8f;
        Red.fillAmount = 1f;
    }

    void Update()
    {
        var health = (float) Player.Health / Player.MaxHealth;
        Blue.fillAmount = health;
        
        if (Red.fillAmount > Blue.fillAmount)
            Red.fillAmount = Mathf.Max(0, Red.fillAmount - m_RedSpeed / Player.MaxHealth * Time.deltaTime);

        var cam = GameManager.Instance.ActiveCamera;
        if (cam != null)
        {
            transform.LookAt(cam.transform.position);
            transform.rotation = Quaternion.AngleAxis(180, Vector3.up) * transform.rotation;            
        }
    }
}
