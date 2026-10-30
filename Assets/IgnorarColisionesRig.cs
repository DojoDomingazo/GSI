using UnityEngine;

public class IgnorarColisionesRig : MonoBehaviour
{
    [SerializeField] private Transform rig;
    [SerializeField] private Collider cuerpoCollider;
    [SerializeField] private float repasarCadaSegundos = 1f;

    private float proximoRepaso;

    private void Reset()
    {
        cuerpoCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        Repasar();
    }

    private void Update()
    {
        if (Time.time < proximoRepaso) return;
        proximoRepaso = Time.time + repasarCadaSegundos;
        Repasar();
    }

    // Por si el rig crea o reactiva colliders más tarde (manos, mandos...)
    private void OnCollisionEnter(Collision c)
    {
        if (rig != null && cuerpoCollider != null && c.collider.transform.IsChildOf(rig))
            Physics.IgnoreCollision(cuerpoCollider, c.collider, true);
    }

    private void Repasar()
    {
        if (rig == null || cuerpoCollider == null) return;
        foreach (Collider c in rig.GetComponentsInChildren<Collider>(true))
            Physics.IgnoreCollision(cuerpoCollider, c, true);
    }
}