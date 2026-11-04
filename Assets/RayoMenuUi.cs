using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Rayo propio para manejar los menús de la escena (menú de colores, panel de muerte...) con los mandos.
//  - Sale del mando (derecho o izquierdo, el que esté apuntando al menú), calcula dónde corta el panel
//    y mira qué botón hay justo ahí. No usa el EventSystem del Interaction SDK.
//  - El punto donde apuntas se SUAVIZA: el temblor natural de la mano no hace vibrar el rayo.
//  - Al empezar a apretar el gatillo se BLOQUEA el botón al que apuntabas, así el movimiento del dedo
//    al pulsar no te cambia a otro botón.
//  - El botón sigue funcionando con su On Click de siempre.
//
// Uso: añade este script a cualquier objeto y DESACTIVA los ISDK_RayCanvasInteraction de los menús
// (si no, cada clic se procesaría dos veces y verías dos rayos).
public class RayoMenuUI : MonoBehaviour
{
    private struct Impacto
    {
        public OVRInput.Controller mano;
        public Ray rayo;
        public Canvas canvas;
        public Vector3 punto;
        public int puntuacion; // 2 = apunta a un botón, 1 = apunta al panel, 0 = a nada
    }

    [Header("Menús que maneja")]
    [SerializeField] private Canvas[] canvases;               // vacío = todos los Canvas World Space con botones de la escena

    [Header("Mandos")]
    [SerializeField] private OVRCameraRig rig;                // vacío = lo busca solo
    [SerializeField] private float inclinacionRayo = 0f;      // grados: + baja el rayo, - lo sube (si no coincide con tu mando)
    [SerializeField] private float distanciaMaxima = 5f;
    [SerializeField] private bool vibrarAlPasar = true;

    [Header("Estabilidad (si el rayo vibra)")]
    [SerializeField] private float frecuenciaMinima = 1f;     // más bajo = más estable pero con más retardo (prueba 0.6 - 2)
    [SerializeField] private float reaccion = 10f;            // más alto = sigue mejor los movimientos rápidos
    [SerializeField] private float margenBoton = 0.12f;       // zona de acierto extra alrededor de cada botón (fracción de su tamaño)
    [SerializeField] private float histeresis = 0.08f;        // margen extra para no soltar el botón en el que ya estás

    [Header("Rayo visible")]
    [SerializeField] private bool dibujarRayo = true;
    [SerializeField] private Color colorRayo = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color colorSobreBoton = new Color(0.3f, 1f, 0.4f, 1f);
    [SerializeField] private float grosorRayo = 0.004f;
    [SerializeField] private float tamanoPunto = 0.02f;       // el circulito donde apuntas

    [Header("Otros")]
    [SerializeField] private bool mostrarDebug = true;

    [Header("Solo lectura (míralo en Play)")]
    [SerializeField] private string botonBajoRayo = "-";
    [SerializeField] private string manoApuntando = "-";
    [SerializeField] private bool gatilloBloqueado;

    private static readonly OVRInput.Controller[] manos = { OVRInput.Controller.RTouch, OVRInput.Controller.LTouch };

    private readonly List<Canvas> menus = new List<Canvas>();
    private readonly Dictionary<Canvas, Button[]> botonesDe = new Dictionary<Canvas, Button[]>();
    private Button hover, pulsado, bloqueado;
    private bool armado;
    private OVRInput.Controller manoPulsada;
    private OVRInput.Controller ultimaMano = OVRInput.Controller.RTouch;

    // Filtro del punto donde apuntas
    private bool filtrando;
    private Vector3 puntoSuave, puntoCrudoAnterior;
    private float velocidadSuave;
    private OVRInput.Controller manoFiltro;
    private Canvas canvasFiltro;

    // Dibujo (se actualiza justo antes de renderizar, con la posición más reciente del mando)
    private LineRenderer linea, punto;
    private bool lineaVisible;
    private OVRInput.Controller manoLinea;
    private Vector3 puntoLinea;

    private void OnEnable()
    {
        Application.onBeforeRender += AntesDeRenderizar;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= AntesDeRenderizar;
        if (linea != null) linea.enabled = false;
        if (punto != null) punto.enabled = false;
    }

    private void Start()
    {
        if (rig == null) rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogError("[RayoMenu] No encuentro el OVRCameraRig.", this);
            enabled = false;
            return;
        }

        if (canvases != null && canvases.Length > 0)
        {
            foreach (Canvas c in canvases) if (c != null) menus.Add(c);
        }
        else
        {
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.WorldSpace && c.GetComponentInChildren<Button>(true) != null)
                    menus.Add(c);
        }

        foreach (Canvas c in menus)
            botonesDe[c] = c.GetComponentsInChildren<Button>(true);

        if (EventSystem.current == null)
            Debug.LogWarning("[RayoMenu] No hay EventSystem en la escena: los botones pueden no reaccionar.", this);

        linea = CrearLinea("RayoMenuUI_Rayo", grosorRayo, 0);
        punto = CrearLinea("RayoMenuUI_Punto", tamanoPunto, 8);

        if (mostrarDebug)
            Debug.Log($"[RayoMenu] Listo. Menús controlados: {menus.Count}", this);
    }

    // LateUpdate: el rig ya ha actualizado la posición de los mandos en este frame
    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        // 1) Con cada mano miramos a qué apunta y nos quedamos con la mejor
        Impacto mejor = new Impacto { mano = ultimaMano, puntuacion = -1 };
        foreach (OVRInput.Controller mano in manos)
        {
            if (!OVRInput.IsControllerConnected(mano)) continue;
            Impacto i = Apuntar(mano);
            if (i.puntuacion > mejor.puntuacion || (i.puntuacion == mejor.puntuacion && mano == ultimaMano))
                mejor = i;
        }

        if (mejor.puntuacion < 0)
        {
            lineaVisible = false;
            armado = false;
            bloqueado = null;
            CambiarHover(null);
            return;
        }

        // 2) Suavizamos el punto y miramos qué botón hay debajo
        Button boton = null;
        if (mejor.puntuacion > 0)
        {
            if (mejor.mano != manoFiltro || mejor.canvas != canvasFiltro) filtrando = false;
            manoFiltro = mejor.mano;
            canvasFiltro = mejor.canvas;
            ultimaMano = mejor.mano;
            puntoSuave = Suavizar(mejor.punto, dt);
            boton = BotonBajo(mejor.canvas, puntoSuave, hover);
        }
        else
        {
            filtrando = false;
        }
        manoApuntando = mejor.mano.ToString();

        // 3) Gatillo: al empezar a apretar (antes del clic) fijamos el botón; así el temblor del dedo no lo cambia
        float gatillo = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, mejor.mano);
        if (gatillo < 0.1f)
        {
            armado = false;
            bloqueado = null;
        }
        else if (!armado)
        {
            armado = true;
            bloqueado = boton;
        }
        gatilloBloqueado = armado;
        if (armado) boton = bloqueado;

        CambiarHover(boton);
        botonBajoRayo = hover != null ? hover.name : "-";

        // 4) Pulsar y soltar (el clic se da al soltar, sobre el botón fijado)
        if (hover != null && pulsado == null && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, mejor.mano))
        {
            pulsado = hover;
            manoPulsada = mejor.mano;
            ExecuteEvents.Execute(pulsado.gameObject, NuevoEvento(), ExecuteEvents.pointerDownHandler);
        }
        if (pulsado != null && OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, manoPulsada))
        {
            PointerEventData e = NuevoEvento();
            ExecuteEvents.Execute(pulsado.gameObject, e, ExecuteEvents.pointerUpHandler);
            if (pulsado == hover)
            {
                if (mostrarDebug) Debug.Log($"[RayoMenu] CLIC en '{pulsado.name}' con {manoPulsada}", this);
                ExecuteEvents.Execute(pulsado.gameObject, e, ExecuteEvents.pointerClickHandler);
            }
            pulsado = null;
        }

        // 5) Lo que se dibujará
        lineaVisible = dibujarRayo && mejor.puntuacion > 0;
        manoLinea = mejor.mano;
        puntoLinea = puntoSuave;
        Color color = hover != null ? colorSobreBoton : colorRayo;
        if (linea != null) { linea.startColor = color; linea.endColor = color; }
        if (punto != null) { punto.startColor = color; punto.endColor = color; }
    }

    // ---------- Apuntar ----------

    private Ray RayoDe(OVRInput.Controller mano)
    {
        Transform ancla = mano == OVRInput.Controller.RTouch ? rig.rightControllerAnchor : rig.leftControllerAnchor;
        Quaternion giro = ancla.rotation * Quaternion.Euler(inclinacionRayo, 0f, 0f);
        return new Ray(ancla.position, giro * Vector3.forward);
    }

    private Impacto Apuntar(OVRInput.Controller mano)
    {
        Impacto res = new Impacto { mano = mano, rayo = RayoDe(mano), puntuacion = 0 };
        float mejorDist = distanciaMaxima;

        foreach (Canvas c in menus)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;

            // Dónde corta el rayo al plano del panel
            Plane plano = new Plane(c.transform.forward, c.transform.position);
            float d;
            if (!plano.Raycast(res.rayo, out d) || d > mejorDist) continue;
            Vector3 puntoMundo = res.rayo.GetPoint(d);

            // ¿Hay un botón ahí? Si no, ¿al menos cae dentro del panel? (con un margen del 10 %)
            bool hayBoton = BotonBajo(c, puntoMundo, null) != null;
            if (!hayBoton)
            {
                RectTransform rc = (RectTransform)c.transform;
                Vector3 lp = rc.InverseTransformPoint(puntoMundo);
                Rect r = rc.rect;
                float mx = r.width * 0.1f, my = r.height * 0.1f;
                if (lp.x < r.xMin - mx || lp.x > r.xMax + mx || lp.y < r.yMin - my || lp.y > r.yMax + my) continue;
            }

            mejorDist = d;
            res.canvas = c;
            res.punto = puntoMundo;
            res.puntuacion = hayBoton ? 2 : 1;
        }
        return res;
    }

    private bool ContieneBoton(Button b, Vector3 puntoMundo, float margen)
    {
        RectTransform rt = (RectTransform)b.transform;
        Vector3 lp = rt.InverseTransformPoint(puntoMundo);
        Rect r = rt.rect;
        float mx = r.width * margen, my = r.height * margen;
        return lp.x >= r.xMin - mx && lp.x <= r.xMax + mx && lp.y >= r.yMin - my && lp.y <= r.yMax + my;
    }

    // El botón bajo ese punto del mundo. Si ya estás sobre uno (preferido), lo mantiene aunque te salgas un poco.
    private Button BotonBajo(Canvas c, Vector3 puntoMundo, Button preferido)
    {
        if (preferido != null && preferido.isActiveAndEnabled && preferido.IsInteractable()
            && ContieneBoton(preferido, puntoMundo, margenBoton + histeresis))
            return preferido;

        Button encontrado = null;
        foreach (Button b in botonesDe[c])
        {
            if (b == null || !b.isActiveAndEnabled || !b.IsInteractable()) continue;
            if (ContieneBoton(b, puntoMundo, margenBoton)) encontrado = b; // los últimos de la jerarquía se dibujan encima
        }
        return encontrado;
    }

    // ---------- Estabilidad ----------

    // Filtro que suaviza mucho cuando la mano está casi quieta y deja de suavizar cuando te mueves rápido
    private Vector3 Suavizar(Vector3 crudo, float dt)
    {
        if (!filtrando)
        {
            filtrando = true;
            puntoSuave = crudo;
            puntoCrudoAnterior = crudo;
            velocidadSuave = 0f;
            return crudo;
        }

        // La velocidad también se suaviza (a 1 Hz): si no, el propio temblor parecería "movimiento rápido"
        float velocidad = (crudo - puntoCrudoAnterior).magnitude / dt;
        puntoCrudoAnterior = crudo;
        velocidadSuave = Mathf.Lerp(velocidadSuave, velocidad, AlfaFiltro(1f, dt));

        float corte = frecuenciaMinima + reaccion * velocidadSuave;
        return Vector3.Lerp(puntoSuave, crudo, AlfaFiltro(corte, dt));
    }

    private static float AlfaFiltro(float frecuenciaCorte, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * frecuenciaCorte);
        return 1f / (1f + tau / dt);
    }

    // ---------- Resaltado ----------

    private PointerEventData NuevoEvento()
    {
        return new PointerEventData(EventSystem.current);
    }

    private void CambiarHover(Button nuevo)
    {
        if (nuevo == hover) return;

        if (hover != null)
        {
            if (pulsado == hover) ExecuteEvents.Execute(hover.gameObject, NuevoEvento(), ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(hover.gameObject, NuevoEvento(), ExecuteEvents.pointerExitHandler);
        }

        hover = nuevo;

        if (hover != null)
        {
            ExecuteEvents.Execute(hover.gameObject, NuevoEvento(), ExecuteEvents.pointerEnterHandler);
            if (mostrarDebug) Debug.Log($"[RayoMenu] Apuntas a '{hover.name}' con {ultimaMano}", this);
            if (vibrarAlPasar) StartCoroutine(Pulso(ultimaMano));
        }
        else if (pulsado != null)
        {
            pulsado = null; // te has salido del botón con el gatillo pulsado: se cancela
        }
    }

    private IEnumerator Pulso(OVRInput.Controller mano)
    {
        OVRInput.SetControllerVibration(0.5f, 0.25f, mano);
        yield return new WaitForSeconds(0.04f);
        OVRInput.SetControllerVibration(0f, 0f, mano);
    }

    // ---------- Dibujo ----------

    private LineRenderer CrearLinea(string nombre, float grosor, int vertices)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = grosor;
        lr.endWidth = vertices > 0 ? grosor : grosor * 0.6f;
        lr.numCapVertices = vertices;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Shaders que siempre van en el build (un material sin shader saldría rosa)
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("UI/Default");
        if (sh != null) lr.material = new Material(sh);
        lr.enabled = false;
        return lr;
    }

    // Justo antes de renderizar usamos la posición MÁS reciente del mando, así el rayo no se retrasa ni tiembla respecto a él
    private void AntesDeRenderizar()
    {
        if (linea == null || punto == null) return;
        linea.enabled = lineaVisible;
        punto.enabled = lineaVisible;
        if (!lineaVisible) return;

        Ray r = RayoDe(manoLinea);
        linea.SetPosition(0, r.origin + r.direction * 0.03f);
        linea.SetPosition(1, puntoLinea);

        // El circulito, un poco por delante del panel para que no se meta dentro
        Vector3 p = puntoLinea - r.direction * 0.004f;
        punto.SetPosition(0, p);
        punto.SetPosition(1, p + Vector3.up * 0.0005f);
    }
}