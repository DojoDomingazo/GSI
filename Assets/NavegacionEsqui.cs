using UnityEngine;

public class NavegacionEsqui : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Rigidbody cuerpo;
    [SerializeField] private Transform manoIzquierda;
    [SerializeField] private Transform manoDerecha;

    [Header("Ajustes de giro")]
    [SerializeField] private float velocidadDeGiro = 90f;     // grados por segundo con el giro máximo
    [SerializeField] private float anguloMaximo = 15f;        // inclinación de manos que da el giro máximo
    [SerializeField] private float zonaMuerta = 3f;           // grados que se ignoran
    [SerializeField] private float separacionMinima = 0.15f;  // metros mínimos entre manos
    [SerializeField] private float suavizado = 15f;           // más alto = respuesta más rápida
    [SerializeField] private float curvaRespuesta = 0.7f;     // menor que 1 = el giro se marca antes

    [Header("Pruebas")]
    [SerializeField] private bool usarStickParaPruebas;
    [SerializeField] private bool mostrarDebug = true;

    [Header("Solo lectura (míralo en Play)")]
    [SerializeField] private string estado;
    [SerializeField] private float inclinacionManosGrados;
    [SerializeField] private float separacionHorizontalManos;
    [SerializeField] private float direccionObjetivo;
    [SerializeField] private float direccionActual;
    [SerializeField] private float velocidadMs;
    [SerializeField] private float rumboGrados;
    [SerializeField] private string objetoSuelo;

    private string estadoAnterior;
    private float tiempoUltimoLog;

    private void Reset()
    {
        cuerpo = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (cuerpo == null || manoIzquierda == null || manoDerecha == null)
        {
            Debug.LogError("[Nav] Falta una referencia (Cuerpo, Mano Izquierda o Mano Derecha).", this);
            enabled = false;
            return;
        }
        direccionActual = 0f;
        estadoAnterior = null;
        if (mostrarDebug) Debug.Log("[Nav] ACTIVADA. Modo: " + (usarStickParaPruebas ? "STICK" : "MANOS"), this);
    }

    private void OnDisable()
    {
        if (mostrarDebug) Debug.Log("[Nav] DESACTIVADA", this);
    }

    private void FixedUpdate()
    {
        Vector3 d = manoDerecha.position - manoIzquierda.position;
        separacionHorizontalManos = new Vector2(d.x, d.z).magnitude;

        direccionObjetivo = usarStickParaPruebas ? LeerStick() : LeerInclinacionManos(d);
        direccionActual = Mathf.Lerp(direccionActual, direccionObjetivo, suavizado * Time.fixedDeltaTime);

        Vector3 velocidad = cuerpo.linearVelocity;
        velocidadMs = velocidad.magnitude;
        rumboGrados = Mathf.Atan2(velocidad.x, velocidad.z) * Mathf.Rad2Deg;

        if (mostrarDebug)
        {
            Debug.DrawLine(manoIzquierda.position, manoDerecha.position, Color.yellow, Time.fixedDeltaTime);
            Debug.DrawRay(cuerpo.position, velocidad, Color.cyan, Time.fixedDeltaTime);
        }

        ActualizarEstado();

        if (velocidadMs < 0.1f) return;

        // Giramos la velocidad alrededor de la normal del suelo, para seguir pegados a la pendiente
        Vector3 normal = Vector3.up;
        if (Physics.Raycast(cuerpo.position, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal;
            objetoSuelo = hit.collider.name;
        }
        else
        {
            objetoSuelo = "(nada)";
        }

        float angulo = direccionActual * velocidadDeGiro * Time.fixedDeltaTime;
        cuerpo.linearVelocity = Quaternion.AngleAxis(angulo, normal) * velocidad;
    }

    private float LeerStick()
    {
        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).x;
    }

    private float LeerInclinacionManos(Vector3 d)
    {
        if (separacionHorizontalManos < separacionMinima)
        {
            inclinacionManosGrados = 0f;
            return 0f;
        }

        // Positivo si la mano derecha está más baja que la izquierda
        inclinacionManosGrados = Mathf.Atan2(-d.y, separacionHorizontalManos) * Mathf.Rad2Deg;

        float magnitud = Mathf.Abs(inclinacionManosGrados);
        if (magnitud < zonaMuerta) return 0f;

        float valor = Mathf.InverseLerp(zonaMuerta, anguloMaximo, magnitud);
        valor = Mathf.Pow(valor, curvaRespuesta);
        return Mathf.Sign(inclinacionManosGrados) * valor;
    }

    private void ActualizarEstado()
    {
        if (velocidadMs < 0.1f) estado = "SIN VELOCIDAD (no se puede girar)";
        else if (!usarStickParaPruebas && separacionHorizontalManos < separacionMinima) estado = "MANOS MUY JUNTAS (no mide)";
        else if (direccionObjetivo > 0.05f) estado = "DERECHA";
        else if (direccionObjetivo < -0.05f) estado = "IZQUIERDA";
        else estado = "RECTO";

        if (!mostrarDebug) return;

        if (estado != estadoAnterior)
        {
            Debug.Log($"[Nav] {estado} | inclinación manos: {inclinacionManosGrados:F1}° | dirección: {direccionObjetivo:F2} | velocidad: {velocidadMs:F1} m/s | rumbo: {rumboGrados:F0}°", this);
            estadoAnterior = estado;
            tiempoUltimoLog = Time.time;
        }
        else if ((estado == "DERECHA" || estado == "IZQUIERDA") && Time.time - tiempoUltimoLog > 0.5f)
        {
            Debug.Log($"[Nav] girando {estado} | dirección: {direccionActual:F2} | rumbo: {rumboGrados:F0}° | velocidad: {velocidadMs:F1} m/s | suelo: {objetoSuelo}", this);
            tiempoUltimoLog = Time.time;
        }
    }
}