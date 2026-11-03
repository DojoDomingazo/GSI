using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

public class EquiparEsquis : MonoBehaviour
{
    [Header("Cuerpo")]
    [SerializeField] private Collider cuerpoCollider;
    [SerializeField] private Rigidbody cuerpoRigidbody;
    [SerializeField] private PhysicsMaterial materialConEsquis;
    [SerializeField] private NavegacionEsqui navegacion;
    [SerializeField] private bool congelarHastaTenerEsquis = true;

    [Header("Esquís")]
    [SerializeField] private GameObject esquisAgarrables;                    // skier_1
    [SerializeField] private float distanciaHorizontalParaPonerse = 1.2f;    // metros, en horizontal, al centro del cuerpo
    [SerializeField] private float retrasoActivarAgarre = 4f;                // segundos sin poder agarrar los esquís al empezar

    [Header("Solo lectura")]
    [SerializeField] private string estado = "ESPERANDO";
    [SerializeField] private float distanciaEsquis;

    private bool equipado, armado;
    private float ultimoLog;
    private readonly List<GameObject> interaccionesActivas = new List<GameObject>();
    private RigidbodyConstraints constraintsOriginales;

    private void Reset()
    {
        cuerpoCollider = GetComponent<Collider>();
        cuerpoRigidbody = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        enabled = true; // Update tiene que correr aunque la casilla del componente estuviera desmarcada

        // Al empezar los esquís no se pueden agarrar: evita conexiones fantasma del agarre a distancia
        if (esquisAgarrables != null)
        {
            GuardarInteraccionesActivas();
            CambiarInteraccion(false);
            StartCoroutine(ActivarAgarreTrasRetraso());
        }

        if (cuerpoRigidbody == null) return;
        constraintsOriginales = cuerpoRigidbody.constraints;
        if (congelarHastaTenerEsquis)
            cuerpoRigidbody.constraints = RigidbodyConstraints.FreezeAll;
    }

    private IEnumerator ActivarAgarreTrasRetraso()
    {
        yield return new WaitForSeconds(retrasoActivarAgarre);
        if (equipado) yield break;
        CambiarInteraccion(true);
        Debug.Log("[Esquis] Agarre activado", this);
    }

    // Solo recordamos los ISDK_ que estaban activos al empezar: los que tú dejes desactivados se quedan así
    private void GuardarInteraccionesActivas()
    {
        interaccionesActivas.Clear();
        foreach (Transform hijo in esquisAgarrables.transform)
            if (hijo.name.StartsWith("ISDK_") && hijo.gameObject.activeSelf)
                interaccionesActivas.Add(hijo.gameObject);
    }

    private void CambiarInteraccion(bool activa)
    {
        foreach (GameObject go in interaccionesActivas)
            if (go != null) go.SetActive(activa);
    }

    // ---------- Eventos del Pointable Unity Event Wrapper ----------

    // El "When Select" conectado a este método arma la comprobación de distancia
    public void PonerEsquisDesdeAgarre(PointerEvent evento) { EmpezarAcercamiento(evento); }

    public void EmpezarAcercamiento(PointerEvent evento)
    {
        Debug.Log($"[Esquis] Evento recibido: {evento.Type} | origen: {evento.Data} | t = {Time.timeSinceLevelLoad:F1} s", this);
        if (equipado || esquisAgarrables == null)
        {
            Debug.Log($"[Esquis] Ignorado: equipado = {equipado}, Esquis Agarrables asignado = {esquisAgarrables != null}", this);
            return;
        }

        if (!armado)
        {
            armado = true;
            CambiarEstado("ACERCANDO");
        }
    }

    // "When Unselect": ya no hace falta, la comprobación sigue aunque se suelten.
    // Se deja el método para no romper la conexión del Inspector.
    public void SoltadoEsquis(PointerEvent evento) { }

    // ---------- Lógica ----------

    private void Update()
    {
        if (equipado || !armado || esquisAgarrables == null) return;

        Vector3 esquis = esquisAgarrables.transform.position;
        Vector3 cuerpo = cuerpoCollider.bounds.center;
        distanciaEsquis = new Vector2(esquis.x - cuerpo.x, esquis.z - cuerpo.z).magnitude;

        if (Time.time - ultimoLog > 0.25f)
        {
            Debug.Log($"[Esquis] distancia horizontal al cuerpo: {distanciaEsquis:F2} m (se ponen a < {distanciaHorizontalParaPonerse:F2})", this);
            ultimoLog = Time.time;
        }

        if (distanciaEsquis < distanciaHorizontalParaPonerse)
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
        CambiarEstado("EQUIPADO");
        Debug.Log("Esquís puestos");
    }

    private IEnumerator OcultarEsquisTrasUnFrame()
    {
        yield return null; // un frame: no tocamos el objeto mientras el SDK procesa el agarre
        if (esquisAgarrables != null) esquisAgarrables.SetActive(false);
    }

    private void CambiarEstado(string nuevo)
    {
        estado = nuevo;
        Debug.Log($"[Esquis] Estado: {nuevo}", this);
    }
}