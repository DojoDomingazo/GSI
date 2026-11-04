using System.Collections;
using UnityEngine;

// Agarrar los esquís con los mandos, sin el Interaction SDK y sin tener que caminar hasta ellos.
//  - Apunta con un mando a los esquís, o simplemente MÍRALOS, y aprieta el agarre (grip) o el gatillo de cualquier mando.
//  - Si están lejos, vuelan hasta tu mano. Mientras aprietas, los llevas contigo (mueves y giras).
//  - En cuanto están cerca de tu cuerpo, EquiparEsquis los pone y empieza el descenso.
//  - Si los sueltas antes, se quedan donde los dejes y puedes volver a cogerlos.
//
// Uso: añade este script al mismo objeto que EquiparEsquis (SkierBody).
public class AgarreEsquisFacil : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private EquiparEsquis equipar;      // vacío = el del mismo objeto
    [SerializeField] private OVRCameraRig rig;           // vacío = lo busca solo

    [Header("Agarre")]
    [SerializeField] private float alcance = 4f;                  // hasta dónde puedes apuntar a los esquís (m)
    [SerializeField] private float holgura = 0.35f;               // zona de acierto extra alrededor de los esquís (m)
    [SerializeField] private float tiempoAtraccion = 0.35f;       // segundos que tardan en llegar a la mano desde lejos
    [SerializeField] private Vector3 posicionEnMano = new Vector3(0f, 0f, 0.15f); // dónde quedan respecto al mando
    [SerializeField] private float inclinacionRayo = 0f;          // grados: + baja el rayo, - lo sube (si no coincide con tu mando)
    [SerializeField] private bool permitirMirada = true;          // también valen si los estás mirando (no depende de la posición del mando)

    [Header("Aviso visual")]
    [SerializeField] private bool dibujarRayo = true;
    [SerializeField] private Color colorRayo = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private float grosorRayo = 0.004f;
    [SerializeField] private float resaltado = 1.08f;             // los esquís crecen un poco al apuntarlos
    [SerializeField] private bool mostrarDebug = true;

    [Header("Solo lectura (míralo en Play)")]
    [SerializeField] private string estado = "ESPERANDO";

    private static readonly OVRInput.Controller[] manos = { OVRInput.Controller.RTouch, OVRInput.Controller.LTouch };
    private const OVRInput.Button botones = OVRInput.Button.PrimaryHandTrigger | OVRInput.Button.PrimaryIndexTrigger;

    private Transform esquis;
    private Renderer[] renderers;
    private Rigidbody[] cuerpos;
    private Vector3 escalaBase;
    private bool agarrado, apuntando;
    private OVRInput.Controller manoAgarre, manoApuntando;
    private Vector3 relPos0, puntoApuntado;
    private Quaternion relRot;
    private float tAtraccion;

    private LineRenderer linea;
    private float ultimoDiagnostico;

    private void OnEnable() { Application.onBeforeRender += AntesDeRenderizar; }
    private void OnDisable()
    {
        Application.onBeforeRender -= AntesDeRenderizar;
        if (linea != null) linea.enabled = false;
    }

    private void Start()
    {
        if (equipar == null) equipar = GetComponent<EquiparEsquis>();
        if (rig == null) rig = FindFirstObjectByType<OVRCameraRig>();
        if (equipar == null || equipar.Esquis == null || rig == null)
        {
            Debug.LogError("[AgarreFacil] Falta EquiparEsquis (con 'Esquis Agarrables' asignado) o el OVRCameraRig.", this);
            enabled = false;
            return;
        }

        esquis = equipar.Esquis.transform;
        escalaBase = esquis.localScale;
        renderers = esquis.GetComponentsInChildren<Renderer>(true);
        cuerpos = esquis.GetComponentsInChildren<Rigidbody>(true);
        linea = CrearLinea();
    }

    private void LateUpdate()
    {
        if (esquis == null) return;

        // Ya puestos (o aún no se pueden agarrar): nada que hacer
        if (equipar.Equipado || !esquis.gameObject.activeInHierarchy)
        {
            agarrado = false;
            Resaltar(false);
            apuntando = false;
            return;
        }

        if (agarrado)
        {
            Sujetar();
            return;
        }

        if (!equipar.AgarrePermitido)
        {
            Resaltar(false);
            apuntando = false;
            if (mostrarDebug && Time.time - ultimoDiagnostico >= 3f)
            {
                ultimoDiagnostico = Time.time;
                Debug.Log("[AgarreFacil] diag | agarre permitido: False (EquiparEsquis lo activa a los 4 s; si pasa de ahí, mira que 'Esquis Agarrables' esté asignado)", this);
            }
            return;
        }

        // ¿Algún mando apunta a los esquís (o los tiene al lado)?
        Bounds zona = ZonaEsquis();
        OVRInput.Controller mejorMano = OVRInput.Controller.None;
        float mejorDist = alcance + 1f;
        Vector3 mejorPunto = zona.center;

        foreach (OVRInput.Controller mano in manos)
        {
            if (!OVRInput.IsControllerConnected(mano)) continue;
            Transform ancla = AnclaDe(mano);
            float d;
            Vector3 punto;
            if (zona.Contains(ancla.position)) { d = 0f; punto = zona.center; }
            else
            {
                Ray rayo = RayoDe(mano);
                if (!zona.IntersectRay(rayo, out d) || d > alcance) continue;
                punto = rayo.GetPoint(d);
            }
            if (d < mejorDist) { mejorDist = d; mejorMano = mano; mejorPunto = punto; }
        }

        // Si ningún mando apunta, miramos si los estás mirando a ellos con la cabeza
        bool porMirada = false;
        if (mejorMano == OVRInput.Controller.None && permitirMirada && rig.centerEyeAnchor != null)
        {
            Ray mirada = new Ray(rig.centerEyeAnchor.position, rig.centerEyeAnchor.forward);
            float dm;
            if (zona.IntersectRay(mirada, out dm) && dm <= alcance)
            {
                porMirada = true;
                mejorDist = dm;
                mejorPunto = mirada.GetPoint(dm);
            }
        }

        bool ahora = mejorMano != OVRInput.Controller.None || porMirada;
        if (ahora && !apuntando)
        {
            if (mejorMano != OVRInput.Controller.None) StartCoroutine(Pulso(mejorMano));
            estado = "APUNTANDO";
            if (mostrarDebug)
                Debug.Log($"[AgarreFacil] Apuntas a los esquís ({(porMirada ? "con la mirada" : "con " + mejorMano)}, a {mejorDist:F1} m). Aprieta el agarre o el gatillo.", this);
        }
        else if (!ahora && apuntando)
        {
            estado = "ESPERANDO";
        }
        apuntando = ahora;
        manoApuntando = mejorMano;
        puntoApuntado = mejorPunto;
        Resaltar(ahora);

        // Pulsar agarre o gatillo en cualquier mando (primero el que apunta)
        if (ahora)
        {
            OVRInput.Controller pulsa = OVRInput.Controller.None;
            if (mejorMano != OVRInput.Controller.None && OVRInput.GetDown(botones, mejorMano)) pulsa = mejorMano;
            else
            {
                foreach (OVRInput.Controller mano in manos)
                    if (OVRInput.IsControllerConnected(mano) && OVRInput.GetDown(botones, mano)) { pulsa = mano; break; }
            }
            if (pulsa != OVRInput.Controller.None) Agarrar(pulsa);
        }

        Diagnostico(zona, ahora);
    }

    // Cada 3 segundos cuenta en la Console por qué no se agarran (así se ve qué falla)
    private void Diagnostico(Bounds zona, bool apuntandoAhora)
    {
        if (!mostrarDebug || Time.time - ultimoDiagnostico < 3f) return;
        ultimoDiagnostico = Time.time;

        string manosTxt = "";
        foreach (OVRInput.Controller mano in manos)
        {
            bool con = OVRInput.IsControllerConnected(mano);
            manosTxt += $"{mano}: " + (con ? $"conectado, a {Vector3.Distance(AnclaDe(mano).position, zona.ClosestPoint(AnclaDe(mano).position)):F1} m de los esquís" : "NO conectado") + " | ";
        }
        Debug.Log($"[AgarreFacil] diag | agarre permitido: {equipar.AgarrePermitido} | apuntando: {apuntandoAhora} | {manosTxt}", this);
    }

    // ---------- Agarrar y llevar ----------

    private void Agarrar(OVRInput.Controller mano)
    {
        manoAgarre = mano;
        agarrado = true;
        tAtraccion = 0f;
        estado = "AGARRADOS";

        Transform ancla = AnclaDe(mano);
        relPos0 = ancla.InverseTransformPoint(esquis.position);
        relRot = Quaternion.Inverse(ancla.rotation) * esquis.rotation;

        // Los movemos nosotros: que ninguna física los empuje
        foreach (Rigidbody rb in cuerpos)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Resaltar(false);
        StartCoroutine(Pulso(mano));
        if (mostrarDebug) Debug.Log($"[AgarreFacil] Esquís agarrados con {mano}", this);

        equipar.ArmarAcercamiento();
    }

    private void Sujetar()
    {
        Transform ancla = AnclaDe(manoAgarre);
        tAtraccion += Time.deltaTime;
        float k = Mathf.SmoothStep(0f, 1f, tAtraccion / Mathf.Max(0.01f, tiempoAtraccion));

        // Desde donde estaban hasta la mano, y a partir de ahí pegados al mando
        Vector3 rel = Vector3.Lerp(relPos0, posicionEnMano, k);
        esquis.SetPositionAndRotation(ancla.TransformPoint(rel), ancla.rotation * relRot);

        if (!OVRInput.Get(botones, manoAgarre))
        {
            agarrado = false;
            estado = "SUELTOS";
            if (mostrarDebug) Debug.Log("[AgarreFacil] Esquís soltados (aprieta otra vez para cogerlos)", this);
        }
    }

    // ---------- Utilidades ----------

    private Transform AnclaDe(OVRInput.Controller mano)
    {
        return mano == OVRInput.Controller.RTouch ? rig.rightControllerAnchor : rig.leftControllerAnchor;
    }

    private Ray RayoDe(OVRInput.Controller mano)
    {
        Transform ancla = AnclaDe(mano);
        return new Ray(ancla.position, ancla.rotation * Quaternion.Euler(inclinacionRayo, 0f, 0f) * Vector3.forward);
    }

    private Bounds ZonaEsquis()
    {
        Bounds b = new Bounds(esquis.position, Vector3.zero);
        bool primero = true;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (primero) { b = r.bounds; primero = false; }
            else b.Encapsulate(r.bounds);
        }
        b.Expand(holgura * 2f);
        return b;
    }

    private void Resaltar(bool si)
    {
        if (esquis == null) return;
        Vector3 objetivo = escalaBase * (si ? resaltado : 1f);
        esquis.localScale = Vector3.Lerp(esquis.localScale, objetivo, 12f * Time.deltaTime);
    }

    private IEnumerator Pulso(OVRInput.Controller mano)
    {
        OVRInput.SetControllerVibration(0.5f, 0.3f, mano);
        yield return new WaitForSeconds(0.05f);
        OVRInput.SetControllerVibration(0f, 0f, mano);
    }

    // ---------- Rayo visible ----------

    private LineRenderer CrearLinea()
    {
        GameObject go = new GameObject("AgarreEsquisFacil_Rayo");
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = grosorRayo;
        lr.endWidth = grosorRayo * 0.6f;
        lr.startColor = colorRayo;
        lr.endColor = colorRayo;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Shaders que siempre van en el build (un material sin shader saldría rosa)
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("UI/Default");
        if (sh != null) lr.material = new Material(sh);
        lr.enabled = false;
        return lr;
    }

    // Justo antes de renderizar usamos la posición más reciente del mando
    private void AntesDeRenderizar()
    {
        if (linea == null || rig == null) return;
        bool ver = dibujarRayo && apuntando && !agarrado;
        linea.enabled = ver;
        if (!ver) return;

        Ray r = RayoDe(manoApuntando);
        linea.SetPosition(0, r.origin + r.direction * 0.03f);
        linea.SetPosition(1, puntoApuntado);
    }
}