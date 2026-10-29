using System.Collections;
using UnityEngine;
using Oculus.Interaction;

public class EquiparEsquis : MonoBehaviour
{
    [SerializeField] private Collider cuerpoCollider;
    [SerializeField] private Rigidbody cuerpoRigidbody;
    [SerializeField] private PhysicsMaterial materialConEsquis;
    [SerializeField] private GameObject esquisAgarrables;
    [SerializeField] private NavegacionEsqui navegacion;
    [SerializeField] private bool congelarHastaTenerEsquis = true;

    private bool equipado;
    private RigidbodyConstraints constraintsOriginales;

    private void Reset()
    {
        cuerpoCollider = GetComponent<Collider>();
        cuerpoRigidbody = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (cuerpoRigidbody == null) return;

        // Guardamos las restricciones de siempre (rotación X y Z congeladas)
        constraintsOriginales = cuerpoRigidbody.constraints;

        // Hasta tener los esquís, el cuerpo no se puede mover
        if (congelarHastaTenerEsquis)
            cuerpoRigidbody.constraints = RigidbodyConstraints.FreezeAll;
    }

    // Para el evento "When Select" del Pointable Unity Event Wrapper
    public void PonerEsquisDesdeAgarre(PointerEvent evento)
    {
        PonerEsquis();
    }

    [ContextMenu("Poner esquís (prueba)")]
    public void PonerEsquis()
    {
        if (equipado) return;
        equipado = true;

        cuerpoCollider.sharedMaterial = materialConEsquis;
        cuerpoRigidbody.constraints = constraintsOriginales;
        cuerpoRigidbody.WakeUp();
        if (navegacion != null) navegacion.enabled = true;

        StartCoroutine(OcultarEsquisTrasUnFrame());
        Debug.Log("Esquís puestos");
    }

    private IEnumerator OcultarEsquisTrasUnFrame()
    {
        yield return null;
        if (esquisAgarrables != null) esquisAgarrables.SetActive(false);
    }
}