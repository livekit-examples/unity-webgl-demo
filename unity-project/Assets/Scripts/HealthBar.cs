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
        if (Player.isLocalPlayer)
            Destroy(gameObject);

        m_RedSpeed = Player.MaxHealth / 8f;
        Red.fillAmount = 1;
    }

    void Update()
    {
        var health = (float) Player.Health / Player.MaxHealth;
        Blue.fillAmount = health;
        
        if (Red.fillAmount > Blue.fillAmount)
            Red.fillAmount = Mathf.Max(0, Red.fillAmount - m_RedSpeed / Player.MaxHealth * Time.deltaTime);

        if (Camera.current != null)
        {
            transform.LookAt(Camera.current.transform.position);
            transform.rotation = Quaternion.AngleAxis(180, Vector3.up) * transform.rotation;            
        }
    }
}
