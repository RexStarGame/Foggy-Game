using UnityEngine;

public class KillZoneReturn : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        other.GetComponentInParent<AutoReturnToSpawner>()?.DoReturn();
    }
}
