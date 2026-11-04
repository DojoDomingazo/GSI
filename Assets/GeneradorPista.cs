using System.Collections.Generic;
using UnityEngine;

public class GeneradorPista : MonoBehaviour
{
    private class Tramo
    {
        public GameObject objeto;
        public Vector3 fin;
        public List<Mesh> mallas; // todas las mallas creadas por código para este tramo (pista + terreno)
    }

    // Un árbol del terreno que todavía no se ha creado (se crean pocos por frame para que VR no dé tirones)
    private struct ArbolPendiente
    {
        public GameObject prefab;
        public Vector3 posicion;
        public Quaternion rotacion;
        public float escala;
        public Transform padre;
    }

    // Posición fija de los esquís en el mundo. No es un campo del Inspector,
    // así que lo que pongas aquí es lo que manda siempre.
    private static readonly Vector3 posicionEsquis = new Vector3(-434.03f, -746.08f, -125.72f);

    [Header("Referencias")]
    [SerializeField] private Transform jugador;
    [SerializeField] private Transform puntoInicio;
    [SerializeField] private Rigidbody cuerpoJugador;
    [SerializeField] private Transform esquis;
    [SerializeField] private bool colocarJugadorAlInicio = true;
    [SerializeField] private float distanciaInicio = 3f;
    [SerializeField] private float alturaInicio = 1.2f;
    [SerializeField] private Transform panelMenu;
    [SerializeField] private Vector3 offsetPanel = new Vector3(0f, 0.5f, 1.2f); // lateral, altura, distancia desde el centro del cuerpo
    [SerializeField] private bool usarPosicionDelEditor = true;



    [Header("Materiales")]
    [SerializeField] private Material materialNieve;
    [SerializeField] private PhysicsMaterial materialFisico;

    [Header("Prefabs (arrástralos desde Project)")]
    [SerializeField] private GameObject[] vallas;
    [SerializeField] private GameObject[] arboles;
    [SerializeField] private GameObject trampolin;

    // Ancho de la pista en metros. Es una constante: para cambiarlo, cambia este número.
    private const float ancho = 30f;

    [Header("Forma de la pista")]
    [SerializeField] private float largoPaso = 6f;
    [SerializeField] private int filasPorTramo = 8;
    [SerializeField] private int filasPlanasIniciales = 4;
    [SerializeField] private float pendienteMin = 14f;
    [SerializeField] private float pendienteMax = 24f;
    [SerializeField] private float curvaMaxima = 2.5f;
    [SerializeField] private float cambioCurva = 0.6f;
    [SerializeField] private float retornoRumbo = 0.03f;
    [SerializeField] private float metrosPorTextura = 10f;

    [Header("Generación")]
    [SerializeField] private float distanciaGeneracion = 200f;
    [SerializeField] private float distanciaBorrado = 150f;
    [SerializeField] private int semilla = 0;
    [SerializeField] private bool mostrarDebug = true;

    [Header("Vallas")]
    [SerializeField] private float alturaExtraValla = 0f;
    [SerializeField] private float giroExtraValla = 0f;

    [Header("Árboles")]
    [SerializeField] private int arbolesMin = 3;
    [SerializeField] private int arbolesMax = 8;
    [SerializeField] private float margenArbol = 1.5f;
    [SerializeField] private float escalaArbolMin = 0.8f;
    [SerializeField] private float escalaArbolMax = 1.3f;
    [SerializeField] private float alturaExtraArbol = 0f;

    [Header("Trampolín (opcional)")]
    [Range(0f, 1f)][SerializeField] private float probTrampolin = 0.25f;
    [SerializeField] private float giroExtraTrampolin = 0f;

    [Header("Otros")]
    [SerializeField] private int tramosSinObstaculos = 1;
    [SerializeField] private bool ponerColliderSiFalta = true;

    [Header("Entorno: terreno alrededor de la pista")]
    [SerializeField] private bool generarEntorno = true;
    [SerializeField] private Material materialTerreno;          // vacío = usa el material de la nieve
    [SerializeField] private Material materialRoca;             // opcional: se pone en las laderas muy empinadas
    [SerializeField] private float anchoTerreno = 100f;         // metros a cada lado, desde la valla (se limita solo si la pista gira muy cerrado)
    [SerializeField] private int columnasTerreno = 14;          // más columnas = relieve más fino pero más triángulos
    [SerializeField] private float escalaRelieve = 130f;        // tamaño aproximado (m) de una montaña o un valle
    [SerializeField] private float alturaMontana = 24f;         // lo que sube una montaña por encima de la pista (m)
    [SerializeField] private float profundidadValle = 14f;      // lo que baja un valle por debajo de la pista (m)
    [Range(0f, 0.9f)][SerializeField] private float zonasLlanas = 0.3f; // 0 = casi todo montañas y valles; más alto = más llanura
    [SerializeField] private float alturaMuralla = 32f;         // cordillera del borde exterior (tapa el vacío de detrás)
    [SerializeField] private float anchoOrilla = 45f;           // metros desde la valla hasta que el relieve llega a su altura completa
    [SerializeField] private float pendienteRoca = 50f;         // grados a partir de los cuales una ladera sale como roca
    [SerializeField] private bool ponerColliderTerreno = true;
    [SerializeField] private bool cerrarInicio = true;          // pone una montaña detrás de la salida
    [SerializeField] private float alturaCierreInicio = 28f;
    [SerializeField] private int filasCierreInicio = 8;

    [Header("Entorno: bosques")]
    [SerializeField] private int intentosArbolesTerreno = 90;   // intentos por lado y tramo (no todos prosperan)
    [SerializeField] private float anchoBosque = 80f;           // hasta qué distancia de la valla puede haber árboles
    [Range(0f, 1f)][SerializeField] private float densidadBosque = 0.8f;
    [SerializeField] private float pendienteMaxArbol = 40f;     // en laderas más empinadas no crecen árboles
    [SerializeField] private float escalaBosqueMin = 1f;
    [SerializeField] private float escalaBosqueMax = 1.8f;
    [SerializeField] private int arbolesPorFrame = 6;           // cuántos árboles se crean por frame

    [Header("Entorno: niebla")]
    [SerializeField] private bool activarNiebla = true;
    [SerializeField] private Color colorNiebla = new Color(0.80f, 0.87f, 0.95f, 1f); // ponlo parecido al horizonte del cielo
    [SerializeField] private float nieblaInicio = 60f;
    [SerializeField] private float nieblaFin = 180f;

    [Header("Solo lectura (míralo en Play)")]
    [SerializeField] private int tramosActivos;
    [SerializeField] private float distanciaAlFinal;
    [SerializeField] private float largoVallaMedido;
    [SerializeField] private float anchoTerrenoUsado;
    [SerializeField] private int arbolesEnCola;

    private System.Random rng;
    private readonly Queue<Tramo> tramos = new Queue<Tramo>();
    private Transform contenedor;
    private Vector3 centroActual;
    private float rumboBase, rumboActual, curvaturaActual, pendienteActual, pendienteObjetivo;
    private int filasGeneradas, tramosGenerados;
    private float largoValla, giroValla;
    private bool largoVallaEnX;
    private float desfaseLargoValla; // cuánto se aparta el centro de la valla de su pivote, a lo largo de la valla

    // Entorno. Usa su propio generador de números al azar para no cambiar la forma de la pista con una misma semilla.
    private System.Random rngTerreno;
    private float[] distanciasTerreno;   // distancia de cada columna del terreno al borde de la pista
    private Vector2 offsetRelieve, offsetCresta, offsetDetalle, offsetBosque;
    private readonly Queue<ArbolPendiente> arbolesPendientes = new Queue<ArbolPendiente>();

    // Línea central de toda la pista generada (una fila cada largoPaso metros). La usan los esquiadores NPC.
    private readonly List<Vector3> linea = new List<Vector3>();
    private readonly List<float> lineaRumbo = new List<float>();
    private readonly List<float> lineaAcum = new List<float>();   // metros recorridos desde la salida
    private int ultimoSegmentoProyectado;

    private void Start()
    {
        if (jugador == null || puntoInicio == null || materialNieve == null)
        {
            Debug.LogError("[Pista] Faltan referencias: Jugador, Punto Inicio o Material Nieve.", this);
            enabled = false;
            return;
        }

        rng = semilla == 0 ? new System.Random() : new System.Random(semilla);
        contenedor = new GameObject("PistaGenerada").transform;

        Vector3 f = puntoInicio.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
        rumboBase = rumboActual = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        centroActual = puntoInicio.position;
        pendienteActual = pendienteMin;

        MedirValla();
        if (generarEntorno) PrepararEntorno();
        GenerarHastaDistancia();
        if (generarEntorno && cerrarInicio) CrearCierreInicio();
        ColocarArbolesPendientes(int.MaxValue); // al empezar creamos todos los árboles de golpe
        if (colocarJugadorAlInicio)
        {
            ColocarJugador();
        }
    }

    private void Update()
    {
        GenerarHastaDistancia();
        BorrarTramosViejos();
        ColocarArbolesPendientes(arbolesPorFrame);
        tramosActivos = tramos.Count;
        arbolesEnCola = arbolesPendientes.Count;
        distanciaAlFinal = Vector3.Distance(jugador.position, centroActual);
    }

    private void ColocarJugador()
    {
        Vector3 dir = Quaternion.Euler(0f, rumboBase, 0f) * Vector3.forward;
        Vector3 offsetEditor = Vector3.zero;
        if (panelMenu != null && usarPosicionDelEditor)
        {
            Vector3 d0 = panelMenu.position - cuerpoJugador.position;
            Vector3 der0 = Vector3.Cross(Vector3.up, dir).normalized;
            offsetEditor = new Vector3(Vector3.Dot(d0, der0), d0.y, Vector3.Dot(d0, dir));
        }



        Vector3 origen = puntoInicio.position + dir * distanciaInicio + Vector3.up * 30f;

        Physics.SyncTransforms();

        bool encontrado = false;
        float mejor = float.MaxValue;
        Vector3 puntoSuelo = Vector3.zero;

        foreach (RaycastHit h in Physics.RaycastAll(origen, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!h.collider.name.StartsWith("Tramo_") || h.distance >= mejor) continue;
            mejor = h.distance;
            puntoSuelo = h.point;
            encontrado = true;
        }

        if (!encontrado)
        {
            Debug.LogWarning("[Pista] No he encontrado suelo para colocar al jugador.");
            return;
        }

        Vector3 destino = puntoSuelo + Vector3.up * alturaInicio;
        cuerpoJugador.position = destino;
        cuerpoJugador.linearVelocity = Vector3.zero;
        cuerpoJugador.angularVelocity = Vector3.zero;

        if (panelMenu != null)
        {
            Vector3 off = usarPosicionDelEditor ? offsetEditor : offsetPanel;
            Vector3 derechaPanel = Vector3.Cross(Vector3.up, dir).normalized;
            panelMenu.position = destino + derechaPanel * off.x + Vector3.up * off.y + dir * off.z;
            panelMenu.rotation = Quaternion.LookRotation(dir, Vector3.up);
            Debug.Log($"[Pista] Panel colocado. Offset usado (lateral, altura, distancia): {off}");
        }


        if (esquis != null)
        {
            esquis.position = posicionEsquis;
            if (mostrarDebug) Debug.Log($"[Pista] Esquís colocados en {posicionEsquis}");
        }

        Physics.SyncTransforms();
        if (mostrarDebug) Debug.Log($"[Pista] Jugador colocado en {destino}");
    }
    private void GenerarHastaDistancia()
    {
        int seguridad = 0;
        while ((tramos.Count < 3 || Vector3.Distance(jugador.position, centroActual) < distanciaGeneracion) && seguridad < 10)
        {
            GenerarTramo();
            seguridad++;
        }
    }

    private void BorrarTramosViejos()
    {
        while (tramos.Count > 3)
        {
            Tramo viejo = tramos.Peek();
            if (Vector3.Distance(jugador.position, viejo.fin) < distanciaBorrado) break;

            tramos.Dequeue();
            // Solo las mallas que hemos creado nosotros (nunca las de los prefabs de árboles o vallas)
            foreach (Mesh malla in viejo.mallas)
                if (malla != null) Destroy(malla);
            Destroy(viejo.objeto);
        }
    }

    [ContextMenu("Guardar posición del panel respecto al cuerpo")]
    private void GuardarOffsetPanel()
    {
        if (panelMenu == null || cuerpoJugador == null || puntoInicio == null)
        {
            Debug.LogWarning("[Pista] Faltan Panel Menu, Cuerpo Jugador o Punto Inicio.");
            return;
        }

        Vector3 f = puntoInicio.forward;
        f.y = 0f;
        f.Normalize();
        Vector3 derecha = Vector3.Cross(Vector3.up, f).normalized;
        Vector3 d = panelMenu.position - cuerpoJugador.transform.position;

        offsetPanel = new Vector3(Vector3.Dot(d, derecha), d.y, Vector3.Dot(d, f));
        Debug.Log($"[Pista] Offset del panel guardado: {offsetPanel}");
    }

    private void GenerarTramo()
    {
        List<Vector3> centros = new List<Vector3>();
        List<float> rumbos = new List<float>();
        centros.Add(centroActual);
        rumbos.Add(rumboActual);

        pendienteObjetivo = Rango(pendienteMin, pendienteMax);

        for (int i = 0; i < filasPorTramo; i++)
        {
            bool plano = filasGeneradas < filasPlanasIniciales;

            if (plano)
            {
                curvaturaActual = 0f;
            }
            else
            {
                curvaturaActual += Rango(-cambioCurva, cambioCurva);
                curvaturaActual += Mathf.DeltaAngle(rumboActual, rumboBase) * retornoRumbo;
                curvaturaActual = Mathf.Clamp(curvaturaActual, -curvaMaxima, curvaMaxima);
            }

            rumboActual += curvaturaActual;
            pendienteActual = Mathf.MoveTowards(pendienteActual, plano ? 0f : pendienteObjetivo, 1.5f);

            Vector3 dir = Quaternion.Euler(0f, rumboActual, 0f) * Vector3.forward;
            centroActual += dir * largoPaso + Vector3.down * (largoPaso * Mathf.Tan(pendienteActual * Mathf.Deg2Rad));

            centros.Add(centroActual);
            rumbos.Add(rumboActual);
            filasGeneradas++;
        }

        // Guardamos la línea central (la primera fila de un tramo es la última del anterior: no se repite)
        for (int i = linea.Count == 0 ? 0 : 1; i < centros.Count; i++)
        {
            float acumulado = linea.Count == 0 ? 0f : lineaAcum[lineaAcum.Count - 1] + Vector3.Distance(centros[i], linea[linea.Count - 1]);
            linea.Add(centros[i]);
            lineaRumbo.Add(rumbos[i]);
            lineaAcum.Add(acumulado);
        }

        GameObject tramo = CrearMalla(centros, rumbos);
        List<Mesh> mallas = new List<Mesh>();
        mallas.Add(tramo.GetComponent<MeshFilter>().sharedMesh);

        int objetos = PoblarTramo(tramo.transform, centros, rumbos);
        if (generarEntorno) CrearEntorno(tramo.transform, centros, rumbos, mallas);
        tramos.Enqueue(new Tramo { objeto = tramo, fin = centroActual, mallas = mallas });

        if (mostrarDebug)
            Debug.Log($"[Pista] Tramo {tramosGenerados} | rumbo {rumboActual:F0}° | pendiente {pendienteActual:F0}° | objetos {objetos} | árboles en cola {arbolesPendientes.Count} | tramos activos {tramos.Count}");

        tramosGenerados++;
    }

    private GameObject CrearMalla(List<Vector3> centros, List<float> rumbos)
    {
        int n = centros.Count;
        Vector3[] vertices = new Vector3[n * 2];
        Vector2[] uvs = new Vector2[n * 2];
        int[] triangulos = new int[(n - 1) * 6];

        float recorrido = 0f;
        for (int r = 0; r < n; r++)
        {
            Vector3 adelante = Quaternion.Euler(0f, rumbos[r], 0f) * Vector3.forward;
            Vector3 derecha = Vector3.Cross(Vector3.up, adelante).normalized;

            vertices[r * 2] = centros[r] - derecha * (ancho * 0.5f);
            vertices[r * 2 + 1] = centros[r] + derecha * (ancho * 0.5f);

            if (r > 0) recorrido += Vector3.Distance(centros[r], centros[r - 1]);
            uvs[r * 2] = new Vector2(0f, recorrido / metrosPorTextura);
            uvs[r * 2 + 1] = new Vector2(ancho / metrosPorTextura, recorrido / metrosPorTextura);
        }

        for (int r = 0; r < n - 1; r++)
        {
            int a = r * 2, b = a + 1, c = a + 2, d = a + 3;
            int t = r * 6;
            triangulos[t] = a; triangulos[t + 1] = c; triangulos[t + 2] = b;
            triangulos[t + 3] = c; triangulos[t + 4] = d; triangulos[t + 5] = b;
        }

        Mesh mesh = new Mesh();
        mesh.name = "TramoPista";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangulos;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Tramo_" + tramosGenerados);
        go.transform.SetParent(contenedor, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = materialNieve;
        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        if (materialFisico != null) mc.sharedMaterial = materialFisico;
        return go;
    }

    private int PoblarTramo(Transform padre, List<Vector3> centros, List<float> rumbos)
    {
        int n = centros.Count;
        float[] acum = new float[n];
        for (int i = 1; i < n; i++) acum[i] = acum[i - 1] + Vector3.Distance(centros[i], centros[i - 1]);
        float total = acum[n - 1];
        int contador = 0;

        // Vallas continuas a los dos lados, siguiendo el borde real de la pista (sin huecos)
        if (vallas != null && vallas.Length > 0 && largoValla > 0.1f)
        {
            contador += PonerVallasBorde(padre, centros, rumbos, -1);
            contador += PonerVallasBorde(padre, centros, rumbos, 1);
        }

        // Árboles al azar dentro de la pista
        if (arboles != null && arboles.Length > 0 && tramosGenerados >= tramosSinObstaculos)
        {
            int cuantos = rng.Next(arbolesMin, Mathf.Max(arbolesMin, arbolesMax) + 1);
            float limite = Mathf.Max(0f, ancho * 0.5f - margenArbol);
            for (int i = 0; i < cuantos; i++)
            {
                float s = Rango(0f, total);
                float lateral = Rango(-limite, limite);
                Muestrear(centros, acum, s, out Vector3 posA, out Vector3 adelA, out Vector3 derA);
                Vector3 posicion = posA + derA * lateral + Vector3.up * alturaExtraArbol;
                GameObject arbol = Colocar(Elegir(arboles), posicion, Quaternion.Euler(0f, Rango(0f, 360f), 0f), padre);
                if (arbol == null) continue;
                arbol.transform.localScale *= Rango(escalaArbolMin, escalaArbolMax);
                contador++;
            }
        }

        // Trampolín ocasional
        if (trampolin != null && tramosGenerados >= tramosSinObstaculos && total > 30f && Azar() < probTrampolin)
        {
            float s = Rango(15f, total - 15f);
            float lateral = Rango(-ancho * 0.25f, ancho * 0.25f);
            Muestrear(centros, acum, s, out Vector3 posT, out Vector3 adelT, out Vector3 derT);
            Quaternion rotacion = Quaternion.LookRotation(adelT, Vector3.up) * Quaternion.Euler(0f, giroExtraTrampolin, 0f);
            if (Colocar(trampolin, posT + derT * lateral, rotacion, padre) != null) contador++;
        }

        return contador;
    }

    // Pone vallas pegadas al borde de la malla, sin huecos. Cada tramo reparte sus vallas entre sus dos
    // extremos (estirándolas o encogiéndolas un poco), así que empalman con las del tramo siguiente.
    private int PonerVallasBorde(Transform padre, List<Vector3> centros, List<float> rumbos, int lado)
    {
        int n = centros.Count;
        Vector3[] borde = new Vector3[n];
        float[] acumBorde = new float[n];

        // El borde son exactamente los vértices de la malla de ese lado
        for (int i = 0; i < n; i++)
        {
            Vector3 adelante = Quaternion.Euler(0f, rumbos[i], 0f) * Vector3.forward;
            Vector3 derecha = Vector3.Cross(Vector3.up, adelante).normalized;
            borde[i] = centros[i] + derecha * (lado * ancho * 0.5f);
            if (i > 0) acumBorde[i] = acumBorde[i - 1] + Vector3.Distance(borde[i], borde[i - 1]);
        }

        float totalBorde = acumBorde[n - 1];
        int cuantas = Mathf.Max(1, Mathf.CeilToInt(totalBorde / largoValla));
        float largoReal = totalBorde / cuantas;
        float estiramiento = largoReal / largoValla;
        Vector3 escala = largoVallaEnX ? new Vector3(estiramiento, 1f, 1f) : new Vector3(1f, 1f, estiramiento);
        Vector3 desfaseLocal = largoVallaEnX
            ? new Vector3(desfaseLargoValla * estiramiento, 0f, 0f)
            : new Vector3(0f, 0f, desfaseLargoValla * estiramiento);

        int puestas = 0;
        for (int j = 0; j < cuantas; j++)
        {
            float s = (j + 0.5f) * largoReal;

            int k = 0;
            while (k < n - 2 && acumBorde[k + 1] < s) k++;
            float largoSegmento = acumBorde[k + 1] - acumBorde[k];
            float t = largoSegmento > 0.0001f ? Mathf.Clamp01((s - acumBorde[k]) / largoSegmento) : 0f;

            Vector3 centro = Vector3.Lerp(borde[k], borde[k + 1], t) + Vector3.up * alturaExtraValla;
            Vector3 direccion = (borde[k + 1] - borde[k]).normalized;
            Quaternion rotacion = Quaternion.LookRotation(direccion, Vector3.up) * Quaternion.Euler(0f, giroValla + giroExtraValla, 0f);

            // Si el pivote de la valla no está en su centro, lo compensamos a lo largo de la valla
            Vector3 posicion = centro - rotacion * desfaseLocal;

            GameObject valla = Colocar(Elegir(vallas), posicion, rotacion, padre);
            if (valla == null) continue;
            valla.transform.localScale = Vector3.Scale(valla.transform.localScale, escala);
            puestas++;
        }
        return puestas;
    }

    // ======================= ENTORNO: montañas, valles, llanuras y bosques =======================
    //
    // Cada tramo de pista lleva a cada lado una malla de terreno hecha con las mismas filas que la pista.
    // Las columnas se alejan de la valla (muy juntas cerca, más separadas lejos). La altura de cada punto es
    // la altura de la pista en esa fila + un relieve que sale de un ruido calculado con la posición en el mundo,
    // así los tramos vecinos encajan sin costuras. Cerca de la valla el relieve vale 0 (el terreno empalma
    // exactamente con el borde de la pista) y va creciendo con la distancia.

    private void PrepararEntorno()
    {
        rngTerreno = new System.Random(semilla == 0 ? rng.Next() : semilla * 7919 + 13);
        offsetRelieve = new Vector2(RangoT(1000f, 9000f), RangoT(1000f, 9000f));
        offsetCresta = new Vector2(RangoT(1000f, 9000f), RangoT(1000f, 9000f));
        offsetDetalle = new Vector2(RangoT(1000f, 9000f), RangoT(1000f, 9000f));
        offsetBosque = new Vector2(RangoT(1000f, 9000f), RangoT(1000f, 9000f));

        // Si la pista gira muy cerrado, el terreno del lado interior se arrugaría: limitamos el ancho
        float radioMinimo = curvaMaxima > 0.01f ? largoPaso / (curvaMaxima * Mathf.Deg2Rad) : 100000f;
        float anchoMaximo = Mathf.Max(20f, radioMinimo * 0.8f - ancho * 0.5f);
        anchoTerrenoUsado = Mathf.Clamp(anchoTerreno, 20f, anchoMaximo);

        int columnas = Mathf.Max(4, columnasTerreno);
        distanciasTerreno = new float[columnas + 1];
        for (int c = 0; c <= columnas; c++)
            distanciasTerreno[c] = anchoTerrenoUsado * Mathf.Pow((float)c / columnas, 1.5f);

        if (activarNiebla)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = colorNiebla;
            RenderSettings.fogStartDistance = nieblaInicio;
            RenderSettings.fogEndDistance = nieblaFin;
        }

        if (mostrarDebug)
            Debug.Log($"[Pista] Entorno listo | ancho de terreno por lado: {anchoTerrenoUsado:F0} m | columnas: {columnas}");
    }

    // Ruido suave entre 0 y 1 (varias capas de Perlin sumadas). Usa coordenadas del mundo.
    private float Ruido(float x, float z, float escala, int octavas, Vector2 offset)
    {
        float suma = 0f, amplitud = 1f, total = 0f;
        float frecuencia = 1f / Mathf.Max(1f, escala);
        for (int i = 0; i < octavas; i++)
        {
            suma += Mathf.PerlinNoise(x * frecuencia + offset.x + i * 31.7f, z * frecuencia + offset.y - i * 17.3f) * amplitud;
            total += amplitud;
            amplitud *= 0.5f;
            frecuencia *= 2f;
        }
        return suma / total;
    }

    // Altura del terreno respecto a la pista (positivo = montaña, negativo = valle).
    // x, z: punto del mundo. d: distancia a la valla. fila: nº de fila de la pista (para dejar la salida despejada).
    private float Relieve(float x, float z, float d, int fila)
    {
        // Pegado a la valla el terreno está a la altura de la pista; luego el relieve va subiendo poco a poco
        float orilla = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 2f) / Mathf.Max(1f, anchoOrilla)));

        // En los primeros metros de pista el relieve crece despacio, para que la salida quede abierta
        float inicio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fila * largoPaso - 20f) / 80f));

        // Mapa grande: valores altos = montaña, bajos = valle, medios = llanura
        float g = Ruido(x, z, escalaRelieve, 3, offsetRelieve);
        float g1 = Mathf.Clamp((g - 0.5f) / 0.15f, -1f, 1f);
        float monte = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(zonasLlanas, 1f, g1));
        float valle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(zonasLlanas, 1f, -g1));

        // Crestas (picos más afilados) y pequeñas ondulaciones (las montañas más rugosas que la llanura)
        float cresta = 1f - Mathf.Abs(2f * Ruido(x, z, escalaRelieve * 0.3f, 2, offsetCresta) - 1f);
        float detalle = (Ruido(x, z, 30f, 2, offsetDetalle) - 0.5f) * 2f;

        float altura = monte * alturaMontana * (0.8f + 0.4f * cresta) - valle * profundidadValle + detalle * (1f + 3f * monte);
        altura *= orilla * inicio;

        // Cordillera del borde exterior: sube siempre, así nunca se ve el vacío detrás
        float anchoTotal = Mathf.Max(1f, distanciasTerreno[distanciasTerreno.Length - 1]);
        float lejos = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, d / anchoTotal));
        altura += lejos * alturaMuralla * (0.7f + 0.6f * g);

        return altura;
    }

    // Puntos del terreno de un lado del tramo: p[fila, columna]. lado = -1 (izquierda) o 1 (derecha).
    private Vector3[,] CalcularRejilla(List<Vector3> centros, List<float> rumbos, int lado, int filaInicial)
    {
        int n = centros.Count;
        int cols = distanciasTerreno.Length;
        Vector3[,] p = new Vector3[n, cols];

        for (int r = 0; r < n; r++)
        {
            Vector3 adelante = Quaternion.Euler(0f, rumbos[r], 0f) * Vector3.forward;
            Vector3 derecha = Vector3.Cross(Vector3.up, adelante).normalized;

            for (int c = 0; c < cols; c++)
            {
                float d = distanciasTerreno[c];
                Vector3 pos = centros[r] + derecha * (lado * (ancho * 0.5f + d));
                pos.y = centros[r].y + Relieve(pos.x, pos.z, d, filaInicial + r);
                p[r, c] = pos;
            }
        }
        return p;
    }

    // Convierte la rejilla en una malla "low poly" (cada triángulo con su propia normal, sin suavizar)
    private Mesh ConstruirMallaTerreno(Vector3[,] p, string nombre)
    {
        int filas = p.GetLength(0);
        int cols = p.GetLength(1);

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normales = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> nieve = new List<int>();
        List<int> roca = new List<int>();

        for (int r = 0; r < filas - 1; r++)
        {
            for (int c = 0; c < cols - 1; c++)
            {
                Vector3 p00 = p[r, c], p10 = p[r, c + 1], p01 = p[r + 1, c], p11 = p[r + 1, c + 1];
                // Misma división en triángulos que usa PonerArbolesTerreno para posar los árboles
                AnadirTriangulo(p00, p10, p01, verts, normales, uvs, nieve, roca);
                AnadirTriangulo(p11, p01, p10, verts, normales, uvs, nieve, roca);
            }
        }

        Mesh m = new Mesh();
        m.name = nombre;
        if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts);
        m.SetNormals(normales);
        m.SetUVs(0, uvs);
        if (materialRoca != null)
        {
            m.subMeshCount = 2;
            m.SetTriangles(nieve, 0);
            m.SetTriangles(roca, 1);
        }
        else
        {
            m.SetTriangles(nieve, 0);
        }
        m.RecalculateBounds();
        return m;
    }

    private void AnadirTriangulo(Vector3 a, Vector3 b, Vector3 c, List<Vector3> verts, List<Vector3> normales,
                                 List<Vector2> uvs, List<int> nieve, List<int> roca)
    {
        // Unity dibuja la cara de delante cuando los vértices van en sentido horario visto desde arriba:
        // si el triángulo mira hacia abajo, intercambiamos dos vértices para darle la vuelta
        Vector3 normal = Vector3.Cross(b - a, c - a);
        if (normal.y < 0f)
        {
            Vector3 t = b; b = c; c = t;
            normal = -normal;
        }
        if (normal.sqrMagnitude < 0.00000001f) return; // triángulo degenerado
        normal.Normalize();

        bool esRoca = materialRoca != null && normal.y < Mathf.Cos(pendienteRoca * Mathf.Deg2Rad);
        List<int> destino = esRoca ? roca : nieve;

        int i0 = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        normales.Add(normal); normales.Add(normal); normales.Add(normal);
        uvs.Add(new Vector2(a.x / metrosPorTextura, a.z / metrosPorTextura));
        uvs.Add(new Vector2(b.x / metrosPorTextura, b.z / metrosPorTextura));
        uvs.Add(new Vector2(c.x / metrosPorTextura, c.z / metrosPorTextura));
        destino.Add(i0); destino.Add(i0 + 1); destino.Add(i0 + 2);
    }

    private GameObject CrearObjetoTerreno(string nombre, Mesh malla, Transform padre)
    {
        // El nombre empieza por "Tramo_": el sistema de vidas lo trata como suelo (no quita vidas)
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshFilter>().sharedMesh = malla;

        Material baseMat = materialTerreno != null ? materialTerreno : materialNieve;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = materialRoca != null ? new Material[] { baseMat, materialRoca } : new Material[] { baseMat };

        if (ponerColliderTerreno)
        {
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = malla;
            if (materialFisico != null) mc.sharedMaterial = materialFisico;
        }
        return go;
    }

    private void CrearEntorno(Transform padre, List<Vector3> centros, List<float> rumbos, List<Mesh> mallas)
    {
        // Número de fila (desde la salida) del primer punto de este tramo
        int filaInicial = filasGeneradas - (centros.Count - 1);

        for (int lado = -1; lado <= 1; lado += 2)
        {
            string sufijo = lado < 0 ? "_Izq" : "_Der";
            Vector3[,] p = CalcularRejilla(centros, rumbos, lado, filaInicial);

            Mesh malla = ConstruirMallaTerreno(p, "Terreno" + sufijo);
            mallas.Add(malla);
            CrearObjetoTerreno("Tramo_Terreno_" + tramosGenerados + sufijo, malla, padre);

            if (arboles != null && arboles.Length > 0)
                PonerArbolesTerreno(padre, p, centros);
        }
    }

    // Busca puntos al azar sobre la superficie del terreno y deja en cola un árbol en los que valen:
    // poca pendiente, zona de bosque (otro ruido, así hay claros y manchas de árboles) y no demasiado alto
    private void PonerArbolesTerreno(Transform padre, Vector3[,] p, List<Vector3> centros)
    {
        int n = p.GetLength(0);
        int cols = p.GetLength(1);
        float dMin = 3f;
        float dMax = Mathf.Min(anchoBosque, distanciasTerreno[cols - 1]);
        if (dMax <= dMin) return;

        float cosMax = Mathf.Cos(pendienteMaxArbol * Mathf.Deg2Rad);
        float alturaLimite = Mathf.Max(1f, alturaMontana * 0.8f); // por encima de esto casi no hay árboles

        for (int i = 0; i < intentosArbolesTerreno; i++)
        {
            // Punto al azar: s recorre las filas, d la distancia a la valla (reparto uniforme en superficie)
            float s = RangoT(0f, n - 1f);
            int r = Mathf.Min((int)s, n - 2);
            float fv = s - r;

            float d = RangoT(dMin, dMax);
            int c = 0;
            while (c < cols - 2 && distanciasTerreno[c + 1] <= d) c++;
            float fu = Mathf.InverseLerp(distanciasTerreno[c], distanciasTerreno[c + 1], d);

            // Punto sobre el triángulo que toca (mismos triángulos que la malla)
            Vector3 p00 = p[r, c], p10 = p[r, c + 1], p01 = p[r + 1, c], p11 = p[r + 1, c + 1];
            Vector3 punto, normal;
            if (fu + fv <= 1f)
            {
                punto = p00 + (p10 - p00) * fu + (p01 - p00) * fv;
                normal = Vector3.Cross(p10 - p00, p01 - p00);
            }
            else
            {
                punto = p11 + (p01 - p11) * (1f - fu) + (p10 - p11) * (1f - fv);
                normal = Vector3.Cross(p01 - p11, p10 - p11);
            }
            if (normal.y < 0f) normal = -normal;
            normal.Normalize();
            if (normal.y < cosMax) continue; // demasiado empinado

            // Zona de bosque o claro
            float bosque = Ruido(punto.x, punto.z, 90f, 2, offsetBosque);
            float densidad = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f, bosque)) * densidadBosque;

            // Cuanto más alto respecto a la pista, menos árboles
            float alturaPista = Mathf.Lerp(centros[r].y, centros[r + 1].y, fv);
            float altura = punto.y - alturaPista;
            densidad *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(alturaLimite * 0.7f, alturaLimite * 1.3f, altura));

            if (AzarT() > densidad) continue;

            arbolesPendientes.Enqueue(new ArbolPendiente
            {
                prefab = arboles[rngTerreno.Next(arboles.Length)],
                posicion = punto + Vector3.up * alturaExtraArbol,
                rotacion = Quaternion.Euler(0f, RangoT(0f, 360f), 0f),
                escala = RangoT(escalaBosqueMin, escalaBosqueMax),
                padre = padre
            });
        }
    }

    private void ColocarArbolesPendientes(int maximo)
    {
        int hechos = 0;
        while (arbolesPendientes.Count > 0 && hechos < maximo)
        {
            ArbolPendiente a = arbolesPendientes.Dequeue();
            if (a.padre == null) continue; // ese tramo ya se ha borrado

            GameObject arbol = Colocar(a.prefab, a.posicion, a.rotacion, a.padre);
            if (arbol != null) arbol.transform.localScale *= a.escala;
            hechos++;
        }
    }

    // Una ladera detrás de la salida para que no se vea el vacío al mirar hacia atrás.
    // Empalma con el borde de la primera fila de la pista y sube hacia atrás.
    private void CrearCierreInicio()
    {
        int filas = Mathf.Max(2, filasCierreInicio);
        int pasosCentro = Mathf.Max(2, Mathf.RoundToInt(ancho / 6f));
        int cols = distanciasTerreno.Length;

        // Posición lateral de cada columna, de izquierda a derecha (las de los lados coinciden con las del terreno)
        List<float> lateral = new List<float>();
        for (int c = cols - 1; c >= 1; c--) lateral.Add(-(ancho * 0.5f + distanciasTerreno[c]));
        for (int i = 0; i <= pasosCentro; i++) lateral.Add(-ancho * 0.5f + ancho * i / pasosCentro);
        for (int c = 1; c < cols; c++) lateral.Add(ancho * 0.5f + distanciasTerreno[c]);

        Vector3 adelante = Quaternion.Euler(0f, rumboBase, 0f) * Vector3.forward;
        Vector3 derecha = Vector3.Cross(Vector3.up, adelante).normalized;
        Vector3 origen = puntoInicio.position;

        int nc = lateral.Count;
        Vector3[,] p = new Vector3[filas + 1, nc];
        for (int k = 0; k <= filas; k++)
        {
            Vector3 centro = origen - adelante * (k * largoPaso);
            float sube = alturaCierreInicio * Mathf.SmoothStep(0f, 1f, (float)k / filas);
            for (int j = 0; j < nc; j++)
            {
                float l = lateral[j];
                float d = Mathf.Max(0f, Mathf.Abs(l) - ancho * 0.5f);
                Vector3 pos = centro + derecha * l;
                pos.y = centro.y + Relieve(pos.x, pos.z, d, 0) + sube;
                p[k, j] = pos;
            }
        }

        Mesh malla = ConstruirMallaTerreno(p, "TerrenoCierreInicio");
        CrearObjetoTerreno("Tramo_Cierre", malla, contenedor);
    }

    // ======================= API para otros scripts (esquiadores NPC) =======================
    // Una posición en la pista se describe con dos números:
    //   s       = metros recorridos a lo largo de la pista desde la salida
    //   lateral = metros a la derecha (+) o a la izquierda (-) del centro de la pista

    public Rigidbody CuerpoJugador { get { return cuerpoJugador; } }
    public float AnchoPista { get { return ancho; } }
    public float LargoGenerado { get { return lineaAcum.Count > 0 ? lineaAcum[lineaAcum.Count - 1] : 0f; } }

    private static Vector3 DerechaDeRumbo(float rumbo)
    {
        Vector3 adelante = Quaternion.Euler(0f, rumbo, 0f) * Vector3.forward;
        return Vector3.Cross(Vector3.up, adelante).normalized;
    }

    // Punto de la superficie de la pista en (s, lateral). Devuelve false si s cae fuera de lo generado.
    public bool PuntoEnPista(float s, float lateral, out Vector3 punto, out Vector3 adelante, out Vector3 derecha)
    {
        punto = Vector3.zero; adelante = Vector3.forward; derecha = Vector3.right;
        int n = linea.Count;
        if (n < 2 || s < 0f || s > lineaAcum[n - 1]) return false;

        // Búsqueda del segmento que contiene s
        int lo = 0, hi = n - 1;
        while (hi - lo > 1)
        {
            int medio = (lo + hi) / 2;
            if (lineaAcum[medio] <= s) lo = medio; else hi = medio;
        }

        float largo = lineaAcum[hi] - lineaAcum[lo];
        float t = largo > 0.0001f ? (s - lineaAcum[lo]) / largo : 0f;

        Vector3 d0 = DerechaDeRumbo(lineaRumbo[lo]);
        Vector3 d1 = DerechaDeRumbo(lineaRumbo[hi]);
        Vector3 a = linea[lo] + d0 * lateral;
        Vector3 b = linea[hi] + d1 * lateral;

        punto = Vector3.Lerp(a, b, t);
        adelante = (b - a).normalized;
        derecha = Vector3.Lerp(d0, d1, t).normalized;
        return true;
    }

    // Al revés: dónde está un punto del mundo (por ejemplo el jugador) respecto a la pista
    public bool ProyectarEnPista(Vector3 mundo, out float s, out float lateral)
    {
        s = 0f; lateral = 0f;
        int n = linea.Count;
        if (n < 2) return false;

        // Buscamos cerca del último resultado; si no hay nada cerca, miramos toda la pista
        int idx; float t; float dist2;
        MejorSegmento(mundo, Mathf.Max(0, ultimoSegmentoProyectado - 20), Mathf.Min(n - 2, ultimoSegmentoProyectado + 60), out idx, out t, out dist2);
        if (dist2 > 3600f) MejorSegmento(mundo, 0, n - 2, out idx, out t, out dist2);
        ultimoSegmentoProyectado = idx;

        Vector3 a = linea[idx], b = linea[idx + 1];
        Vector3 cercano = Vector3.Lerp(a, b, t);
        Vector3 derecha = Vector3.Lerp(DerechaDeRumbo(lineaRumbo[idx]), DerechaDeRumbo(lineaRumbo[idx + 1]), t).normalized;

        s = Mathf.Lerp(lineaAcum[idx], lineaAcum[idx + 1], t);
        lateral = Vector3.Dot(mundo - cercano, derecha);
        return true;
    }

    private void MejorSegmento(Vector3 p, int desde, int hasta, out int mejorIdx, out float mejorT, out float mejorDist2)
    {
        mejorIdx = desde; mejorT = 0f; mejorDist2 = float.MaxValue;
        for (int i = desde; i <= hasta; i++)
        {
            float abx = linea[i + 1].x - linea[i].x, abz = linea[i + 1].z - linea[i].z;
            float apx = p.x - linea[i].x, apz = p.z - linea[i].z;
            float len2 = abx * abx + abz * abz;
            float t = len2 > 0.0001f ? Mathf.Clamp01((apx * abx + apz * abz) / len2) : 0f;
            float dx = apx - abx * t, dz = apz - abz * t;
            float d2 = dx * dx + dz * dz;
            if (d2 < mejorDist2) { mejorDist2 = d2; mejorIdx = i; mejorT = t; }
        }
    }

    private void Muestrear(List<Vector3> centros, float[] acum, float s, out Vector3 pos, out Vector3 adelante3D, out Vector3 derecha)
    {
        int k = 0;
        while (k < acum.Length - 2 && acum[k + 1] < s) k++;
        float largo = acum[k + 1] - acum[k];
        float t = largo > 0.0001f ? Mathf.Clamp01((s - acum[k]) / largo) : 0f;
        pos = Vector3.Lerp(centros[k], centros[k + 1], t);
        adelante3D = (centros[k + 1] - centros[k]).normalized;
        Vector3 plano = new Vector3(adelante3D.x, 0f, adelante3D.z).normalized;
        derecha = Vector3.Cross(Vector3.up, plano).normalized;
    }

    private GameObject Colocar(GameObject prefab, Vector3 posicion, Quaternion rotacion, Transform padre)
    {
        if (prefab == null) return null;
        GameObject go = Instantiate(prefab, posicion, rotacion, padre);
        if (ponerColliderSiFalta) AsegurarColliders(go);
        return go;
    }

    // Si el prefab no tiene ningún collider, le pone una caja que lo envuelva
    private void AsegurarColliders(GameObject raiz)
    {
        if (raiz.GetComponentInChildren<Collider>() != null) return;

        Matrix4x4 aLocal = raiz.transform.worldToLocalMatrix;
        bool inicializado = false;
        Bounds caja = new Bounds();

        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            Matrix4x4 m = aLocal * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 signo = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                Vector3 enLocal = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, signo));
                if (!inicializado) { caja = new Bounds(enLocal, Vector3.zero); inicializado = true; }
                else caja.Encapsulate(enLocal);
            }
        }

        if (!inicializado) return;
        BoxCollider bc = raiz.AddComponent<BoxCollider>();
        bc.center = caja.center;
        bc.size = caja.size;
    }

    // Mide la valla para saber su largo y hacia qué eje está orientada
    private void MedirValla()
    {
        largoValla = 0f;
        giroValla = 0f;
        if (vallas == null || vallas.Length == 0 || vallas[0] == null) return;

        GameObject temp = Instantiate(vallas[0], Vector3.zero, Quaternion.identity);
        Renderer[] rs = temp.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            bool largoEnX = b.size.x > b.size.z;
            largoValla = largoEnX ? b.size.x : b.size.z;
            giroValla = largoEnX ? -90f : 0f;
            largoVallaEnX = largoEnX;
            desfaseLargoValla = largoEnX ? b.center.x : b.center.z;
        }
        DestroyImmediate(temp);
        largoVallaMedido = largoValla;
    }

    private GameObject Elegir(GameObject[] lista) { return lista[rng.Next(lista.Length)]; }
    private float Rango(float min, float max) { return min + (float)rng.NextDouble() * (max - min); }
    private float Azar() { return (float)rng.NextDouble(); }
    private float RangoT(float min, float max) { return min + (float)rngTerreno.NextDouble() * (max - min); }
    private float AzarT() { return (float)rngTerreno.NextDouble(); }

    private void OnDrawGizmosSelected()
    {
        if (puntoInicio == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(puntoInicio.position, puntoInicio.position + puntoInicio.forward * 10f);
        Gizmos.matrix = Matrix4x4.TRS(puntoInicio.position, Quaternion.Euler(0f, puntoInicio.eulerAngles.y, 0f), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(ancho, 0.2f, 2f));
    }


}