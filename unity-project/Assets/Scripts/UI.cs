using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public ScoreRanking GoldRank;
    public ScoreRanking SilverRank;
    public ScoreRanking BronzeRank;
    public Image HealthBar;
    public Image MicroImage;
    public TMP_Text KilledText;
    public TMP_Text KillerText;

    void Start()
    {
        NetworkManager.Instance.Room.LocalParticipant.IsSpeakingChanged += (speaking) =>
        {
            MicroImage.gameObject.SetActive(speaking);
        };
    }

    void Update()
    {
        var p = Player.LocalPlayer;
        if (p == null)
            return;
        
        HealthBar.fillAmount = (float) p.Health / p.MaxHealth;
    }

    public void ShowKilled(Player killer)
    {
        KillerText.SetText(killer.Participant.Identity);
        KilledText.gameObject.SetActive(true);
        KillerText.gameObject.SetActive(true);
    }

    public void HideKilled()
    {
        KilledText.gameObject.SetActive(false);
        KillerText.gameObject.SetActive(false);
    }
    
    public void UpdateRanking()
    {
        var top3 = GameManager.Instance.Scores.OrderByDescending(x => x.Value).Take(3).ToList();

        GoldRank.gameObject.SetActive(false);
        SilverRank.gameObject.SetActive(false);
        BronzeRank.gameObject.SetActive(false);

        if (top3.Count >= 1)
        {
            var gold = top3.ElementAt(0);
            if (gold.Value != 0)
            {
                GoldRank.gameObject.SetActive(true);
                GoldRank.Name.SetText(gold.Key.Identity);
                GoldRank.Score.SetText($"{gold.Value} {(gold.Value > 1 ? "kills" : "kill")}");  
            }
        }
        
        if (top3.Count >= 2)
        {
            var silver = top3.ElementAt(1);
            if (silver.Value != 0)
            {
                SilverRank.gameObject.SetActive(true);
                SilverRank.Name.SetText(silver.Key.Identity);
                SilverRank.Score.SetText($"{silver.Value} {(silver.Value > 1 ? "kills" : "kill")}");
            }
        }
        
        if (top3.Count >= 3)
        {
            var bronze = top3.ElementAt(2);
            if (bronze.Value != 0)
            {
                BronzeRank.gameObject.SetActive(true);
                BronzeRank.Name.SetText(bronze.Key.Identity);
                BronzeRank.Score.SetText($"{bronze.Value} {(bronze.Value > 1 ? "kills" : "kill")}");
            }
        }
    }
}
