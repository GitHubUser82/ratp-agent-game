using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 12f, -8f);
    public float followSpeed = 10f;
    public float angle = 70f;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(angle, 0f, 0f);
    }
}