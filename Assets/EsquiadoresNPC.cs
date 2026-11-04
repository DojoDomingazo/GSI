using System.Collections.Generic;
using UnityEngine;

// Esquiadores que bajan por la pista y te adelantan por el lado.
//  - No tienen collider ni Rigidbody: físicamente es imposible chocar con ellos (y no quitan vidas).
//  - Además se apartan solos para dejarte espacio cuando se acercan a ti, y esquivan árboles y vallas.
//  - Siguen la línea central de la pista que genera GeneradorPista (no hace falta tocar nada más).
//
// Uso: añade este script a cualquier objeto de la escena (por ejemplo el mismo que GeneradorPista).
// Si no arrastras un prefab de esquiador, se crea uno sencillo con cubos y cápsulas.
public class EsquiadoresNPC : MonoBehaviour
{
    private class Esquiador
    {
        public Transform raiz;
        public float s;             // metros recorridos desde la salida
        public float lateral;       // posición lateral actual (m, + = derecha)
        public float carril;        // lateral por el que quiere ir
        public float velocidad;     // m/s
        public float velLateral;    // m/s, solo para inclinar el cuerpo en las curvas
        public float fase;          // para que cada uno oscile distinto
        public float ladoSeguro;    // +1 / -1 mientras se aparta de ti, 0 si no
        public float proximaEsquiva;
    }

    [Header("Referencias")]
    [SerializeField] private GeneradorPista pista;          // vacío = lo busca solo
    [SerializeField] private Rigidbody cuerpoJugador;       // vacío = el SkierBody del generador
    [SerializeField] private GameObject esquiadorPrefab;    // vacío = esquiador sencillo hecho por código

    [Header("Aparición")]
    [SerializeField] private int maxEsquiadores = 3;
    [SerializeField] private float primeraEspera = 8f;               // segundos hasta el primero
    [SerializeField] private float intervaloMin = 8f;
    [SerializeField] private float intervaloMax = 16f;
    [SerializeField] private float distanciaAparicionAtras = 70f;    // aparecen tantos metros detrás de ti
    [SerializeField] private float velocidadMinJugador = 3f;         // no aparecen si estás parado
    [SerializeField] private float distanciaDesaparicion = 110f;     // se borran a esta distancia de ti (delante o detrás)

    [Header("Movimiento")]
    [SerializeField] private float excesoVelocidadMin = 6f;          // m/s más rápidos que tú al aparecer
    [SerializeField] private float excesoVelocidadMax = 12f;
    [SerializeField] private float velocidadMinima = 14f;
    [SerializeField] private float velocidadMaxima = 45f;
    [SerializeField] private float margenBorde = 3f;                 // metros que dejan hasta la valla
    [SerializeField] private float oscilacion = 1f;                  // meneo lateral natural (m)
    [SerializeField] private float velocidadLateral = 6f;            // m/s al cambiar de carril

    [Header("Seguridad con el jugador")]
    [SerializeField] private float separacionLateral = 7f;           // metros de lado a lado que respetan al cruzarse contigo
    [SerializeField] private float zonaAviso = 25f;                  // desde cuántos metros (a lo largo de la pista) se apartan

    [Header("Esquivar obstáculos")]
    [SerializeField] private bool esquivarObstaculos = true;
    [SerializeField] private float distanciaSonda = 14f;             // hasta dónde miran hacia delante
    [SerializeField] private float radioSonda = 2.2f;
    [SerializeField] private float desvio = 4.5f;                    // cuánto se apartan de un obstáculo (m)

    [Header("Aspecto del esquiador sencillo")]
    [SerializeField]
    private Color[] coloresChaqueta =
    {
        new Color(0.85f, 0.10f, 0.12f), new Color(0.10f, 0.35f, 0.85f), new Color(0.95f, 0.65f, 0.05f),
        new Color(0.10f, 0.65f, 0.30f), new Color(0.60f, 0.15f, 0.70f)
    };

    [Header("Otros")]
    [SerializeField] private bool mostrarDebug = true;

    [Header("Solo lectura (míralo en Play)")]
    [SerializeField] private int esquiadoresActivos;
    [SerializeField] private float sJugador;
    [SerializeField] private float lateralJugador;

    private readonly List<Esquiador> esquiadores = new List<Esquiador>();
    private readonly Collider[] buffer = new Collider[16];
    private Transform contenedor;
    private float proximaAparicion;
    private int creados;

    private void Start()
    {
        if (pista == null) pista = FindFirstObjectByType<GeneradorPista>();
        if (pista == null)
        {
            Debug.LogError("[Esquiadores] No encuentro el GeneradorPista en la escena.", this);
            enabled = false;
            return;
        }

        if (cuerpoJugador == null) cuerpoJugador = pista.CuerpoJugador;
        if (cuerpoJugador == null)
        {
            Debug.LogError("[Esquiadores] No tengo el cuerpo del jugador (SkierBody). Arrástralo a 'Cuerpo Jugador'.", this);
            enabled = false;
            return;
        }

        contenedor = new GameObject("EsquiadoresNPC").transform;
        proximaAparicion = Time.time + primeraEspera;
    }

    private void Update()
    {
        float sJug, lJug;
        if (!pista.ProyectarEnPista(cuerpoJugador.position, out sJug, out lJug)) return;
        sJugador = sJug;
        lateralJugador = lJug;

        float vJug = cuerpoJugador.linearVelocity.magnitude;
        float lim = pista.AnchoPista * 0.5f - margenBorde;

        if (Time.time >= proximaAparicion && esquiadores.Count < maxEsquiadores && vJug >= velocidadMinJugador)
        {
            IntentarAparecer(sJug, lJug, vJug, lim);
            proximaAparicion = Time.time + Random.Range(intervaloMin, intervaloMax);
        }

        for (int i = esquiadores.Count - 1; i >= 0; i--)
        {
            Esquiador e = esquiadores[i];
            bool vivo = Mover(e, sJug, lJug, lim, Time.deltaTime);
            if (vivo && Mathf.Abs(e.s - sJug) > distanciaDesaparicion) vivo = false;

            if (!vivo)
            {
                if (mostrarDebug) Debug.Log($"[Esquiadores] {e.raiz.name} fuera de rango: se borra (s = {e.s:F0} m, tú s = {sJug:F0} m)", this);
                Destroy(e.raiz.gameObject);
                esquiadores.RemoveAt(i);
            }
        }

        esquiadoresActivos = esquiadores.Count;
    }

    // ---------- Aparición ----------

    private void IntentarAparecer(float sJug, float lJug, float vJug, float lim)
    {
        float s = sJug - distanciaAparicionAtras;
        if (s < 3f) return; // detrás de la salida no hay pista

        // Carril en el lado de la pista que tiene más espacio, lejos de ti
        float lado = lJug >= 0f ? -1f : 1f;
        float carril = lado * Random.Range(Mathf.Min(separacionLateral * 0.8f, lim), lim);

        Vector3 punto, adelante, derecha;
        if (!pista.PuntoEnPista(s, carril, out punto, out adelante, out derecha)) return;

        GameObject go = esquiadorPrefab != null ? Instantiate(esquiadorPrefab) : CrearEsquiadorSencillo();
        creados++;
        go.name = "Esquiador_NPC_" + creados;
        QuitarFisica(go);
        go.transform.SetParent(contenedor, false);
        go.transform.position = punto;

        Esquiador e = new Esquiador
        {
            raiz = go.transform,
            s = s,
            lateral = carril,
            carril = carril,
            velocidad = Mathf.Clamp(vJug + Random.Range(excesoVelocidadMin, excesoVelocidadMax), velocidadMinima, velocidadMaxima),
            fase = Random.Range(0f, 6.28f)
        };
        esquiadores.Add(e);

        if (mostrarDebug)
            Debug.Log($"[Esquiadores] Aparece {go.name} | {distanciaAparicionAtras:F0} m detrás de ti | carril {carril:F1} m | velocidad {e.velocidad:F0} m/s (tú vas a {vJug:F0}) | activos {esquiadores.Count}", this);
    }

    // ---------- Movimiento ----------

    // Devuelve false si el esquiador se ha salido de la pista generada
    private bool Mover(Esquiador e, float sJug, float lJug, float lim, float dt)
    {
        e.s += e.velocidad * dt;

        // Lateral al que quiere ir: su carril con un pequeño meneo
        float objetivo = Mathf.Clamp(e.carril + Mathf.Sin(Time.time * 0.8f + e.fase) * oscilacion, -lim, lim);

        // Si estamos cerca de ti a lo largo de la pista, nos apartamos para dejarte espacio
        if (Mathf.Abs(e.s - sJug) < zonaAviso)
        {
            if (e.ladoSeguro == 0f)
            {
                // Nos quedamos del lado en el que ya estamos (así nunca te cruzamos); si ahí no cabe, del otro
                e.ladoSeguro = e.lateral >= lJug ? 1f : -1f;
                float candidato = lJug + e.ladoSeguro * separacionLateral;
                if (candidato > lim || candidato < -lim) e.ladoSeguro = -e.ladoSeguro;
            }

            float seguro = Mathf.Clamp(lJug + e.ladoSeguro * separacionLateral, -lim, lim);
            bool muyCerca = Mathf.Abs(objetivo - lJug) < separacionLateral;
            bool delOtroLado = Mathf.Sign(objetivo - lJug) != e.ladoSeguro;
            if (muyCerca || delOtroLado) objetivo = seguro;
        }
        else
        {
            e.ladoSeguro = 0f;
        }

        float anterior = e.lateral;
        e.lateral = Mathf.MoveTowards(e.lateral, objetivo, velocidadLateral * dt);
        e.velLateral = Mathf.Lerp(e.velLateral, (e.lateral - anterior) / Mathf.Max(dt, 0.0001f), 5f * dt);

        Vector3 punto, adelante, derecha;
        if (!pista.PuntoEnPista(e.s, e.lateral, out punto, out adelante, out derecha)) return false;

        // Sigue la inclinación de la pista y se inclina un poco al cambiar de carril
        Vector3 arriba = Vector3.Cross(adelante, derecha).normalized;
        float inclinacion = -Mathf.Clamp(e.velLateral * 2f, -20f, 20f);
        e.raiz.SetPositionAndRotation(punto, Quaternion.LookRotation(adelante, arriba) * Quaternion.Euler(0f, 0f, inclinacion));

        if (esquivarObstaculos && Time.time >= e.proximaEsquiva) Esquivar(e, punto, adelante, derecha, lim);
        return true;
    }

    // Mira hacia delante: si hay un árbol, una valla o un trampolín en el camino, cambia de carril
    private void Esquivar(Esquiador e, Vector3 punto, Vector3 adelante, Vector3 derecha, float lim)
    {
        Vector3 sonda = punto + adelante * distanciaSonda + Vector3.up * 1.2f;
        int n = Physics.OverlapSphereNonAlloc(sonda, radioSonda, buffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            Collider c = buffer[i];
            if (c == null) continue;
            if (c.name.StartsWith("Tramo_")) continue;                 // suelo y terreno
            if (c.attachedRigidbody == cuerpoJugador) continue;        // el jugador ya lo gestionamos aparte

            float ladoObstaculo = Vector3.Dot(c.bounds.center - punto, derecha);
            float nuevo = e.lateral + (ladoObstaculo >= 0f ? -desvio : desvio);
            if (nuevo > lim || nuevo < -lim) nuevo = e.lateral + (ladoObstaculo >= 0f ? desvio : -desvio);

            e.carril = Mathf.Clamp(nuevo, -lim, lim);
            e.proximaEsquiva = Time.time + 0.6f;
            if (mostrarDebug) Debug.Log($"[Esquiadores] {e.raiz.name} esquiva '{c.name}' -> carril {e.carril:F1} m", this);
            return;
        }
    }

    // ---------- Esquiador ----------

    // Sin colliders ni física: no puede chocar contigo ni empujarte
    private void QuitarFisica(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    private GameObject CrearEsquiadorSencillo()
    {
        Color chaqueta = coloresChaqueta[Random.Range(0, coloresChaqueta.Length)];
        Color pantalon = new Color(0.12f, 0.13f, 0.18f);
        Color piel = new Color(0.90f, 0.72f, 0.58f);
        Color casco = new Color(0.95f, 0.95f, 0.95f);
        Color oscuro = new Color(0.06f, 0.06f, 0.07f);

        GameObject raiz = new GameObject("Esquiador_NPC");

        // Postura de descenso: cuerpo inclinado hacia delante desde la cadera
        Transform cuerpo = new GameObject("Cuerpo").transform;
        cuerpo.SetParent(raiz.transform, false);
        cuerpo.localPosition = new Vector3(0f, 0.8f, 0f);
        cuerpo.localRotation = Quaternion.Euler(25f, 0f, 0f);

        Pieza(raiz.transform, PrimitiveType.Capsule, "Piernas", new Vector3(0f, 0.45f, 0f), new Vector3(0.28f, 0.45f, 0.28f), Vector3.zero, pantalon);
        Pieza(cuerpo, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.35f, 0f), new Vector3(0.38f, 0.35f, 0.28f), Vector3.zero, chaqueta);
        Pieza(cuerpo, PrimitiveType.Sphere, "Cabeza", new Vector3(0f, 0.85f, 0f), new Vector3(0.24f, 0.24f, 0.24f), Vector3.zero, piel);
        Pieza(cuerpo, PrimitiveType.Sphere, "Casco", new Vector3(0f, 0.9f, 0f), new Vector3(0.27f, 0.25f, 0.27f), Vector3.zero, casco);

        for (int lado = -1; lado <= 1; lado += 2)
        {
            Pieza(raiz.transform, PrimitiveType.Cube, "Esqui", new Vector3(lado * 0.14f, 0.02f, 0.15f), new Vector3(0.09f, 0.03f, 1.5f), Vector3.zero, oscuro);
            Pieza(raiz.transform, PrimitiveType.Cylinder, "Baston", new Vector3(lado * 0.4f, 0.55f, 0.15f), new Vector3(0.025f, 0.55f, 0.025f), new Vector3(20f, 0f, 0f), oscuro);
        }
        return raiz;
    }

    private void Pieza(Transform padre, PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala, Vector3 giro, Color color)
    {
        GameObject p = GameObject.CreatePrimitive(tipo);
        p.name = nombre;
        DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(padre, false);
        p.transform.localPosition = pos;
        p.transform.localRotation = Quaternion.Euler(giro);
        p.transform.localScale = escala;

        // PropertyBlock: cambia el color sin crear un material nuevo por cada pieza
        MaterialPropertyBlock bloque = new MaterialPropertyBlock();
        bloque.SetColor("_BaseColor", color);
        bloque.SetColor("_Color", color);
        p.GetComponent<Renderer>().SetPropertyBlock(bloque);
    }
}