using UnityEngine;

public class RendererReturnProxy : MonoBehaviour
{
    public AutoReturnToSpawner target;

    void OnBecameInvisible()
    {
        if (target != null && target.returnWhenInvisible)
            target.DoReturn();
    }
}
