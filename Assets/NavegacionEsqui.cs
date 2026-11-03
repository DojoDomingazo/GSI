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

    [Header("Freno al girar")]
    [SerializeField] private float frenoGiro = 0.2f;               // fracción de la velocidad que se pierde por segundo con el giro máximo
    [SerializeField] private float velocidadMinimaFrenoGiro = 4f;  // por debajo de esta velocidad (m/s) el giro ya no frena
    [SerializeField] private float distanciaMaximaSuelo = 1.6f;    // metros del centro del cuerpo al suelo para contar como "en el suelo"

    [Header("Remar con los brazos")]
    [SerializeField] private bool permitirRemar = true;
    [SerializeField] private float umbralRemo = 1f;                // m/s hacia atrás (respecto a tu cuerpo) a partir de los que cuenta como remada
    [SerializeField] private float aceleracionRemo = 1.5f;         // m/s² extra por cada m/s por encima del umbral, por mano
    [SerializeField] private float aceleracionRemoMax = 6f;        // tope de la aceleración total del remo
    [SerializeField] private float suavizadoMano = 20f;            // más alto = el movimiento de las manos se mide con menos retardo
    [SerializeField] private float velocidadTope = 40f;          // m/s, tope de seguridad

    [Header("Posición de huevo (tuck): agachas la cabeza y bajas las manos")]
    [SerializeField] private bool permitirTuck = true;
    [SerializeField] private float descensoManosTuck = 1.1f;     // metros que las manos tienen que estar por debajo de tu altura de pie
    [SerializeField] private bool exigirAgacharse = true;        // además hay que agachar la cabeza
    [SerializeField] private float descensoCabezaTuck = 0.3f;    // metros que hay que bajar la cabeza respecto a estar de pie
    [SerializeField] private float confirmacionTuck = 0.2f;      // segundos en postura antes de empezar a acelerar
    [SerializeField] private float aceleracionTuck = 2f;         // m/s² al empezar
    [SerializeField] private float crecimientoTuck = 1f;         // m/s² extra por cada segundo seguido en tuck
    [SerializeField] private float aceleracionTuckMax = 10f;     // tope de la aceleración
    [SerializeField] private float giroQueCortaTuck = 0.2f;      // si giras más que esto (0 a 1) se corta el tuck

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
    [SerializeField] private float distanciaSuelo;
    [SerializeField] private float frenoActualPorSegundo;
    [SerializeField] private float atrasManoIzquierda;
    [SerializeField] private float atrasManoDerecha;
    [SerializeField] private float aceleracionRemoActual;
    [SerializeField] private float alturaDePie;
    [SerializeField] private float bajadaCabeza;
    [SerializeField] private float bajadaManos;
    [SerializeField] private bool posturaTuck;
    [SerializeField] private float tiempoEnTuck;
    [SerializeField] private float aceleracionTuckActual;

    private string estadoAnterior;
    private float tiempoUltimoLog;

    // Seguimiento de las manos (en el espacio de tracking, que no se mueve con el esquiador)
    private Transform espacio;
    private Vector3 manoIzqPrevia, manoDerPrevia;
    private Vector3 velManoIzq, velManoDer;
    private bool tieneManoPrevia;

    // Seguimiento de remadas y giros (solo para los logs)
    private int contadorRemadas, contadorGiros;
    private bool remandoLog, girandoLog;
    private float tiempoSinEmpuje;
    private float inicioRemoTiempo, velocidadInicioRemo, picoEmpujeRemo, picoAtrasRemo;
    private float inicioGiroTiempo, velocidadInicioGiro, rumboInicioGiro, giroMaximo, frenoMaxGiro, ladoGiro;

    // Postura (tuck)
    private Transform cabeza;
    private bool alturaCalibrada;
    private bool enSueloActual;
    private int contadorTucks;
    private bool tuckLog;
    private float sinTuckTiempo, inicioTuckTiempo, velocidadInicioTuck, picoAceleracionTuck;

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

        OVRCameraRig rig = manoIzquierda.GetComponentInParent<OVRCameraRig>();
        espacio = (rig != null && rig.trackingSpace != null) ? rig.trackingSpace : manoIzquierda.parent;
        tieneManoPrevia = false;
        velManoIzq = Vector3.zero;
        velManoDer = Vector3.zero;
        remandoLog = false;
        girandoLog = false;
        tiempoSinEmpuje = 0f;

        // Cabeza: sirve para saber cuánto te agachas
        cabeza = (rig != null) ? rig.centerEyeAnchor : null;
        if (cabeza == null && Camera.main != null) cabeza = Camera.main.transform;
        if (cabeza == null && permitirTuck)
            Debug.LogWarning("[Nav] No encuentro la cabeza (CenterEyeAnchor): el tuck queda desactivado.", this);
        alturaCalibrada = false;
        tiempoEnTuck = 0f;
        tuckLog = false;
        if (espacio == null)
            Debug.LogWarning("[Nav] No encuentro el espacio de tracking de las manos: el remo queda desactivado.", this);

        if (mostrarDebug) Debug.Log("[Nav] ACTIVADA. Modo: " + (usarStickParaPruebas ? "STICK" : "MANOS"), this);
    }

    private void OnDisable()
    {
        if (mostrarDebug) Debug.Log("[Nav] DESACTIVADA", this);
    }

    // Medimos la velocidad de las manos a cada frame, respecto al espacio de tracking:
    // así el movimiento del esquiador por la pista no cuenta como "mover las manos".
    private void Update()
    {
        if (espacio == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 pi = espacio.InverseTransformPoint(manoIzquierda.position);
        Vector3 pd = espacio.InverseTransformPoint(manoDerecha.position);

        if (tieneManoPrevia)
        {
            Vector3 vi = espacio.TransformDirection((pi - manoIzqPrevia) / dt);
            Vector3 vd = espacio.TransformDirection((pd - manoDerPrevia) / dt);
            float k = Mathf.Clamp01(suavizadoMano * dt);
            velManoIzq = Vector3.Lerp(velManoIzq, vi, k);
            velManoDer = Vector3.Lerp(velManoDer, vd, k);
        }

        manoIzqPrevia = pi;
        manoDerPrevia = pd;
        tieneManoPrevia = true;

        ActualizarPostura(pi.y, pd.y);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        Vector3 d = manoDerecha.position - manoIzquierda.position;
        separacionHorizontalManos = new Vector2(d.x, d.z).magnitude;

        direccionObjetivo = usarStickParaPruebas ? LeerStick() : LeerInclinacionManos(d);
        direccionActual = Mathf.Lerp(direccionActual, direccionObjetivo, suavizado * dt);

        Vector3 velocidad = cuerpo.linearVelocity;
        velocidadMs = velocidad.magnitude;
        rumboGrados = Mathf.Atan2(velocidad.x, velocidad.z) * Mathf.Rad2Deg;

        if (mostrarDebug)
        {
            Debug.DrawLine(manoIzquierda.position, manoDerecha.position, Color.yellow, dt);
            Debug.DrawRay(cuerpo.position, velocidad, Color.cyan, dt);
        }

        ActualizarEstado();

        if (velocidadMs < 0.1f)
        {
            frenoActualPorSegundo = 0f;
            aceleracionRemoActual = 0f;
            aceleracionTuckActual = 0f;
            tiempoEnTuck = 0f;
            RegistrarRemo(dt);
            RegistrarGiro();
            RegistrarTuck(dt);
            return;
        }

        // Suelo: su normal sirve para girar pegados a la pendiente
        Vector3 normal = Vector3.up;
        bool enSuelo = false;
        if (Physics.Raycast(cuerpo.position, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal;
            objetoSuelo = hit.collider.name;
            distanciaSuelo = hit.distance;
            enSuelo = hit.distance <= distanciaMaximaSuelo;
        }
        else
        {
            objetoSuelo = "(nada)";
            distanciaSuelo = -1f;
        }

        enSueloActual = enSuelo;

        // 1) Giro: giramos la velocidad alrededor de la normal del suelo
        float angulo = direccionActual * velocidadDeGiro * dt;
        Vector3 nuevaVelocidad = Quaternion.AngleAxis(angulo, normal) * velocidad;

        // 2) Freno al girar: cuanto más cierras el giro, más velocidad pierdes (como el canto del esquí rozando la nieve)
        frenoActualPorSegundo = 0f;
        if (enSuelo && velocidadMs > velocidadMinimaFrenoGiro)
        {
            frenoActualPorSegundo = frenoGiro * Mathf.Abs(direccionActual);
            float objetivo = Mathf.Max(velocidadMinimaFrenoGiro, nuevaVelocidad.magnitude * (1f - frenoActualPorSegundo * dt));
            nuevaVelocidad = nuevaVelocidad.normalized * objetivo;
        }

        // 3) Remo: llevar las manos hacia atrás rápido empuja hacia delante, como los bastones
        aceleracionRemoActual = CalcularRemo(nuevaVelocidad, enSuelo);
        if (aceleracionRemoActual > 0f)
        {
            Vector3 avance = Vector3.ProjectOnPlane(nuevaVelocidad, normal).normalized;
            nuevaVelocidad += avance * aceleracionRemoActual * dt;
        }

        // 4) Posición de huevo (tuck): acelera cada vez más hasta que giras o te levantas
        aceleracionTuckActual = CalcularTuck(dt);
        if (aceleracionTuckActual > 0f)
        {
            Vector3 avanceTuck = Vector3.ProjectOnPlane(nuevaVelocidad, normal).normalized;
            nuevaVelocidad += avanceTuck * aceleracionTuckActual * dt;
        }

        // 5) Tope de seguridad
        if (nuevaVelocidad.magnitude > velocidadTope)
            nuevaVelocidad = nuevaVelocidad.normalized * velocidadTope;

        cuerpo.linearVelocity = nuevaVelocidad;

        RegistrarRemo(dt);
        RegistrarGiro();
        RegistrarTuck(dt);
    }

    private float CalcularRemo(Vector3 velocidad, bool enSuelo)
    {
        atrasManoIzquierda = 0f;
        atrasManoDerecha = 0f;
        if (!permitirRemar || espacio == null || !enSuelo) return 0f;

        // Dirección de bajada (en horizontal)
        Vector3 adelante = Vector3.ProjectOnPlane(velocidad, Vector3.up);
        if (adelante.sqrMagnitude < 0.25f) return 0f;
        adelante.Normalize();

        // Positivo = la mano va hacia atrás respecto a la dirección de bajada
        atrasManoIzquierda = -Vector3.Dot(velManoIzq, adelante);
        atrasManoDerecha = -Vector3.Dot(velManoDer, adelante);

        float total = Empuje(atrasManoIzquierda) + Empuje(atrasManoDerecha);
        return Mathf.Min(total, aceleracionRemoMax);
    }

    private float Empuje(float atras)
    {
        return atras > umbralRemo ? (atras - umbralRemo) * aceleracionRemo : 0f;
    }

    // Una remada empieza cuando hay empuje y termina cuando lleva 0,15 s sin haberlo
    private void RegistrarRemo(float dt)
    {
        if (!mostrarDebug) return;

        if (aceleracionRemoActual > 0f)
        {
            tiempoSinEmpuje = 0f;
            if (!remandoLog)
            {
                remandoLog = true;
                contadorRemadas++;
                inicioRemoTiempo = Time.time;
                velocidadInicioRemo = velocidadMs;
                picoEmpujeRemo = 0f;
                picoAtrasRemo = 0f;
                Debug.Log($"[Nav] REMADA #{contadorRemadas} INICIO | velocidad: {velocidadMs:F1} m/s | mano izq. atrás: {atrasManoIzquierda:F1} m/s | mano der. atrás: {atrasManoDerecha:F1} m/s", this);
            }
            picoEmpujeRemo = Mathf.Max(picoEmpujeRemo, aceleracionRemoActual);
            picoAtrasRemo = Mathf.Max(picoAtrasRemo, atrasManoIzquierda, atrasManoDerecha);
        }
        else if (remandoLog)
        {
            tiempoSinEmpuje += dt;
            if (tiempoSinEmpuje > 0.15f)
            {
                remandoLog = false;
                float velocidadFinal = cuerpo.linearVelocity.magnitude;
                Debug.Log($"[Nav] REMADA #{contadorRemadas} FIN | duración: {Time.time - inicioRemoTiempo - tiempoSinEmpuje:F2} s | mano más rápida hacia atrás: {picoAtrasRemo:F1} m/s | empuje máx: {picoEmpujeRemo:F1} m/s² | velocidad {velocidadInicioRemo:F1} → {velocidadFinal:F1} m/s ({velocidadFinal - velocidadInicioRemo:+0.0;-0.0} m/s)", this);
            }
        }
    }

    // Un giro empieza cuando la dirección suavizada pasa de 0,15 y termina cuando baja de 0,05
    private void RegistrarGiro()
    {
        if (!mostrarDebug) return;

        float giro = Mathf.Abs(direccionActual);

        if (!girandoLog)
        {
            if (giro > 0.15f)
            {
                girandoLog = true;
                contadorGiros++;
                inicioGiroTiempo = Time.time;
                velocidadInicioGiro = velocidadMs;
                rumboInicioGiro = rumboGrados;
                giroMaximo = giro;
                frenoMaxGiro = frenoActualPorSegundo;
                ladoGiro = Mathf.Sign(direccionActual);
                Debug.Log($"[Nav] GIRO #{contadorGiros} INICIO a la {(ladoGiro > 0f ? "DERECHA" : "IZQUIERDA")} | velocidad: {velocidadMs:F1} m/s | rumbo: {rumboGrados:F0}° | inclinación manos: {inclinacionManosGrados:F1}°", this);
            }
            return;
        }

        giroMaximo = Mathf.Max(giroMaximo, giro);
        frenoMaxGiro = Mathf.Max(frenoMaxGiro, frenoActualPorSegundo);

        if (giro < 0.05f)
        {
            girandoLog = false;
            Debug.Log($"[Nav] GIRO #{contadorGiros} FIN | duración: {Time.time - inicioGiroTiempo:F1} s | giro máx: {giroMaximo:F2} | freno máx: {frenoMaxGiro * 100f:F0} %/s | rumbo {rumboInicioGiro:F0}° → {rumboGrados:F0}° | velocidad {velocidadInicioGiro:F1} → {velocidadMs:F1} m/s ({velocidadMs - velocidadInicioGiro:+0.0;-0.0} m/s)", this);
        }
    }

    // Mide cuánto has bajado la cabeza y las manos respecto a estar de pie (en el espacio de tracking)
    private void ActualizarPostura(float manoIzqY, float manoDerY)
    {
        if (cabeza == null)
        {
            posturaTuck = false;
            return;
        }

        float cabezaY = espacio.InverseTransformPoint(cabeza.position).y;
        if (!alturaCalibrada)
        {
            alturaDePie = cabezaY;
            alturaCalibrada = true;
        }
        alturaDePie = Mathf.Max(alturaDePie, cabezaY); // la altura de pie es lo más alto que ha estado tu cabeza

        bajadaCabeza = alturaDePie - cabezaY;
        bajadaManos = alturaDePie - (manoIzqY + manoDerY) * 0.5f;

        bool manosBajas = bajadaManos > descensoManosTuck;
        bool agachado = !exigirAgacharse || bajadaCabeza > descensoCabezaTuck;
        posturaTuck = manosBajas && agachado;
    }

    [ContextMenu("Recalibrar altura de pie")]
    private void RecalibrarAltura()
    {
        alturaCalibrada = false;
    }

    // Mientras estás en postura, la aceleración crece con el tiempo. Se corta si giras, te levantas o saltas.
    private float CalcularTuck(float dt)
    {
        bool giraMucho = Mathf.Abs(direccionActual) > giroQueCortaTuck;
        bool puede = permitirTuck && posturaTuck && enSueloActual && !giraMucho;

        if (!puede)
        {
            tiempoEnTuck = 0f;
            return 0f;
        }

        tiempoEnTuck += dt;
        if (tiempoEnTuck < confirmacionTuck) return 0f;

        float segundosAcelerando = tiempoEnTuck - confirmacionTuck;
        return Mathf.Min(aceleracionTuck + crecimientoTuck * segundosAcelerando, aceleracionTuckMax);
    }

    private void RegistrarTuck(float dt)
    {
        if (!mostrarDebug) return;

        if (aceleracionTuckActual > 0f)
        {
            sinTuckTiempo = 0f;
            if (!tuckLog)
            {
                tuckLog = true;
                contadorTucks++;
                inicioTuckTiempo = Time.time;
                velocidadInicioTuck = velocidadMs;
                picoAceleracionTuck = 0f;
                Debug.Log($"[Nav] TUCK #{contadorTucks} INICIO | cabeza baja: {bajadaCabeza:F2} m | manos bajan: {bajadaManos:F2} m | velocidad: {velocidadMs:F1} m/s", this);
            }
            picoAceleracionTuck = Mathf.Max(picoAceleracionTuck, aceleracionTuckActual);
        }
        else if (tuckLog)
        {
            sinTuckTiempo += dt;
            if (sinTuckTiempo > 0.1f)
            {
                tuckLog = false;
                string motivo = Mathf.Abs(direccionActual) > giroQueCortaTuck ? "GIRO"
                              : !posturaTuck ? "TE LEVANTAS"
                              : !enSueloActual ? "EN EL AIRE"
                              : "OTRO";
                float velocidadFinal = cuerpo.linearVelocity.magnitude;
                Debug.Log($"[Nav] TUCK #{contadorTucks} FIN ({motivo}) | duración: {Time.time - inicioTuckTiempo - sinTuckTiempo:F1} s | aceleración máx: {picoAceleracionTuck:F1} m/s² | velocidad {velocidadInicioTuck:F1} → {velocidadFinal:F1} m/s ({velocidadFinal - velocidadInicioTuck:+0.0;-0.0} m/s)", this);
            }
        }
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
    }
}