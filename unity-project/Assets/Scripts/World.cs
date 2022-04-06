using System;
using UnityEngine;

public class World : MonoBehaviour
{
    public static World Instance { get; private set; }
    
    public GameObject BluePositions;
    public GameObject RedPositions;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
