using UnityEngine;

public class Rotate : MonoBehaviour
{
    public float speed = 10f;

    private Vector3 _axis = Vector3.up;

    void Update()
    {
        _axis = Vector3.Slerp(Vector3.up, Vector3.forward, Mathf.Sin(Time.time));
        transform.Rotate(_axis, Time.deltaTime * speed);
    }
}
