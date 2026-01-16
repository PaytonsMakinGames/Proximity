using UnityEngine;

public class ActivationPadWorldAnchor : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] Vector2 anchor01 = new Vector2(0.5f, 0.18f);
    [SerializeField] float zDepthFromCamera = 10f; // for ortho, just needs to be in front of cam

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        Vector3 p = new Vector3(anchor01.x, anchor01.y, zDepthFromCamera);
        transform.position = cam.ViewportToWorldPoint(p);
    }
}