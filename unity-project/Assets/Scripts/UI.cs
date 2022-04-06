using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    private Image m_HealthImage; 
    
    void Awake()
    {
        m_HealthImage = GetComponent<Image>();
    }

    void Update()
    {
        var p = Player.LocalPlayer;
        if (p == null)
            return;
        
        m_HealthImage.fillAmount = (float) p.Health / p.MaxHealth;
    }
}
