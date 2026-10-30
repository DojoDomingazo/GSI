using System.Collections;
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
    [SerializeField] private GameObject esquisAgarrables;          // skier_1
    [SerializeField] private Transform direccionReferencia;        // PuntoInicio (hacia donde baja la pista)
    [SerializeField] private float distanciaParaPonerse = 0.8f;    // metros al centro del cuerpo
    [SerializeField] private float distanciaParaVolver = 2f;       // si los sueltas más lejos, vuelven a su sitio
    [SerializeField] private Vector3 offsetPies = new Vector3(0f, -1f, 0f);  // lateral, altura, adelante desde el centro del cuerpo
    [SerializeField] private Vector3 giroModelo = Vector3.zero;    // si el modelo mira de lado: prueba Y = 90, -90 o 180

    [Header("Solo lectura")]
    [SerializeField] private string estado = "ESPERANDO";
    [SerializeField] private float distanciaEsquis;

    private bool equipado, siguiendoPies, encajado, poseGuardada;
    private RigidbodyConstraints constraintsOriginales;
    private Vector3 posicionInicial;
    private Quaternion rotacionInicial;
    private Vector3 rumbo = Vector3.forward;
    private Coroutine rutinaVigilar, rutinaVolver;
    private readonly RaycastHit[] golpes = new RaycastHit[8];

    private void Reset()
    {
        cuerpoCollider = GetComponent<Collider>();
        cuerpoRigidbody = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (cuerpoRigidbody == null) return;
        constraintsOriginales = cuerpoRigidbody.constraints;
        if (congelarHastaTenerEsquis)
            cuerpoRigidbody.constraints = RigidbodyConstraints.FreezeAll;
    }

    // ---------- Eventos del Pointable Unity Event Wrapper ----------

    // El "When Select" que ya tenías conectado a este método ahora inicia el acercamiento
    public void PonerEsquisDesdeAgarre(PointerEvent evento) { EmpezarAcercamiento(evento); }

    public void EmpezarAcercamiento(PointerEvent evento)
    {
        if (equipado || esquisAgarrables == null) return;

        if (!poseGuardada)
        {
            posicionInicial = esquisAgarrables.transform.position;
            rotacionInicial = esquisAgarrables.transform.rotation;
            poseGuardada = true;
        }

        if (rutinaVolver != null) { StopCoroutine(rutinaVolver); rutinaVolver = null; }
        if (rutinaVigilar != null) StopCoroutine(rutinaVigilar);
        rutinaVigilar = StartCoroutine(VigilarDistancia());
        CambiarEstado("ACERCANDO");
    }

    // Conéctalo a "When Unselect"
    public void SoltadoEsquis(PointerEvent evento)
    {
        if (equipado) return;
        if (rutinaVigilar != null) { StopCoroutine(rutinaVigilar); rutinaVigilar = null; }
        rutinaVolver = StartCoroutine(VolverSiEstanLejos());
    }

    // ---------- Lógica ----------

    private IEnumerator VigilarDistancia()
    {
        float ultimoLog = 0f;
        while (!equipado)
        {
            distanciaEsquis = Vector3.Distance(esquisAgarrables.transform.position, cuerpoCollider.bounds.center);

            if (Time.time - ultimoLog > 0.25f)
            {
                Debug.Log($"[Esquis] distancia al cuerpo: {distanciaEsquis:F2} m (se ponen a < {distanciaParaPonerse:F2})", this);
                ultimoLog = Time.time;
            }

            if (distanciaEsquis < distanciaParaPonerse)
            {
                PonerEsquis();
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator VolverSiEstanLejos()
    {
        yield return new WaitForSeconds(1.5f);
        if (equipado) yield break;

        Transform t = esquisAgarrables.transform;
        if (Vector3.Distance(t.position, cuerpoCollider.bounds.center) < distanciaParaVolver) yield break;

        Vector3 desde = t.position;
        Quaternion giroDesde = t.rotation;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.6f)
        {
            if (equipado) yield break;
            t.SetPositionAndRotation(Vector3.Lerp(desde, posicionInicial, k), Quaternion.Slerp(giroDesde, rotacionInicial, k));
            yield return null;
        }
        t.SetPositionAndRotation(posicionInicial, rotacionInicial);
        CambiarEstado("ESPERANDO");
        Debug.Log("[Esquis] Soltados lejos: vuelven a su sitio", this);
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

        StartCoroutine(AcoplarAlosPies());
        CambiarEstado("EQUIPADO");
        Debug.Log("Esquís puestos");
    }

    private IEnumerator AcoplarAlosPies()
    {
        yield return null; // un frame: no tocamos el objeto mientras el SDK procesa el agarre

        // Ya no se pueden coger
        foreach (Transform hijo in esquisAgarrables.transform)
            if (hijo.name.StartsWith("ISDK_")) hijo.gameObject.SetActive(false);

        Grabbable grab = esquisAgarrables.GetComponent<Grabbable>();
        if (grab != null) grab.enabled = false;

        Collider col = esquisAgarrables.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Rumbo inicial: hacia donde baja la pista
        Vector3 f = direccionReferencia != null ? direccionReferencia.forward : cuerpoRigidbody.transform.forward;
        f.y = 0f;
        rumbo = f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;

        encajado = false;
        siguiendoPies = true;
        Debug.Log("[Esquis] Acoplados a los pies", this);
    }

    private void LateUpdate()
    {
        if (!siguiendoPies || esquisAgarrables == null) return;

        Transform cuerpo = cuerpoRigidbody.transform;
        Transform t = esquisAgarrables.transform;

        // Los esquís apuntan hacia donde va el cuerpo
        Vector3 v = cuerpoRigidbody.linearVelocity;
        v.y = 0f;
        if (v.sqrMagnitude > 1f)
            rumbo = Vector3.Slerp(rumbo, v.normalized, 8f * Time.deltaTime).normalized;

        Vector3 normal = BuscarNormalSuelo(cuerpo.position);
        Vector3 adelante = Vector3.ProjectOnPlane(rumbo, normal);
        if (adelante.sqrMagnitude < 0.0001f) adelante = rumbo;
        adelante.Normalize();
        Vector3 derecha = Vector3.Cross(normal, adelante);

        Vector3 posicion = cuerpo.position + derecha * offsetPies.x + Vector3.up * offsetPies.y + adelante * offsetPies.z;
        Quaternion rotacion = Quaternion.LookRotation(adelante, normal) * Quaternion.Euler(giroModelo);

        if (!encajado)
        {
            t.position = Vector3.MoveTowards(t.position, posicion, 8f * Time.deltaTime);
            if ((t.position - posicion).sqrMagnitude < 0.0004f) encajado = true;
        }
        else
        {
            t.position = posicion;
        }
        t.rotation = Quaternion.Slerp(t.rotation, rotacion, 12f * Time.deltaTime);
    }

    private Vector3 BuscarNormalSuelo(Vector3 desde)
    {
        int n = Physics.RaycastNonAlloc(desde, Vector3.down, golpes, 4f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        Vector3 normal = Vector3.up;
        for (int i = 0; i < n; i++)
        {
            if (!golpes[i].collider.name.StartsWith("Tramo_") || golpes[i].distance >= mejor) continue;
            mejor = golpes[i].distance;
            normal = golpes[i].normal;
        }
        return normal;
    }

    private void CambiarEstado(string nuevo)
    {
        estado = nuevo;
        Debug.Log($"[Esquis] Estado: {nuevo}", this);
    }
}