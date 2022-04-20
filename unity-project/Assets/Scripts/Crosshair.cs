using UnityEngine;

public class Crosshair : MonoBehaviour
{
    public Player Player;

    // isLocalPlayer isn't ready on Awake() event
    void Start()
    {
        if (!Player.IsLocalPlayer)
            Destroy(gameObject);
    }

    void Update()
    {
        var start = Player.Minigun.MinigunPoint.transform.position;
        var dir = Player.Cursor.transform.position - start;
        dir.Normalize();

        if (Physics.Raycast(start, dir, out RaycastHit hit))
            transform.position = hit.point - dir * 0.4f;
        else
            transform.position = start + dir * 4f;

        var len = (transform.position - start).magnitude;
        transform.localScale = Vector3.one * Mathf.Max(1f, len / 10f);
        transform.LookAt(Player.Camera.transform.position);
    }
}
