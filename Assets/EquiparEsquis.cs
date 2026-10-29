using System.Collections;
using UnityEngine;
using Oculus.Interaction;

public class EquiparEsquis : MonoBehaviour
{
    [SerializeField] private Collider cuerpoCollider;
    [SerializeField] private Rigidbody cuerpoRigidbody;
    [SerializeField] private PhysicsMaterial materialConEsquis;
    [SerializeField] private GameObject esquisAgarrables;

    private bool equipado;

    private void Reset()
    {
        cuerpoCollider = GetComponent<Collider>();
        cuerpoRigidbody = GetComponent<Rigidbody>();
    }

    // Para el evento "When Select" del Pointable Unity Event Wrapper
    public void PonerEsquisDesdeAgarre(PointerEvent evento)
    {
        PonerEsquis();
    }

    public void PonerEsquis()
    {
        if (equipado) return;
        equipado = true;

        cuerpoCollider.sharedMaterial = materialConEsquis;
        cuerpoRigidbody.WakeUp();

        StartCoroutine(OcultarEsquisTrasUnFrame());

        Debug.Log("Esquís puestos");
    }

    private IEnumerator OcultarEsquisTrasUnFrame()
    {
        yield return null;
        if (esquisAgarrables != null) esquisAgarrables.SetActive(false);
    }
}