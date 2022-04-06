using UnityEngine;

public class PlayerExplosion : MonoBehaviour
{
    public float MinForce;
    public float MaxForce;
    public float Radius;
    
    void Start()
    {
        foreach (Transform c in transform)
        {
            var rb = c.GetComponent<Rigidbody>();

            if (rb != null)
                rb.AddExplosionForce(Random.Range(MinForce, MaxForce), transform.position, Radius);
            
            // Remove destroyed pieces after some time.
        }
    }
}
