using System.Collections.Generic;
using UnityEngine;

public class GeneradorPista : MonoBehaviour
{
    private class Tramo
    {
        public GameObject objeto;
        public Vector3 fin;
    }

    [Header("Referencias")]
    [SerializeField] private Transform jugador;
    [SerializeField] private Transform puntoInicio;
    [SerializeField] private Rigidbody cuerpoJugador;
    [SerializeField] private Transform esquis;
    [SerializeField] private bool colocarJugadorAlInicio = true;
    [SerializeField] private float distanciaInicio = 3f;
    [SerializeField] private float alturaInicio = 1.2f;
    [SerializeField] private Vector3 offsetEsquis = new Vector3(1.5f, 0.9f, 0.3f);
    [SerializeField] private float alturaEsquisSobreSuelo = 0.05f;
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

    [Header("Forma de la pista")]
    [SerializeField] private float ancho = 16f;
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
    [Range(0f, 1f)][SerializeField] private float probValla = 0.9f;
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

    [Header("Solo lectura (míralo en Play)")]
    [SerializeField] private int tramosActivos;
    [SerializeField] private float distanciaAlFinal;
    [SerializeField] private float largoVallaMedido;

    private System.Random rng;
    private readonly Queue<Tramo> tramos = new Queue<Tramo>();
    private Transform contenedor;
    private Vector3 centroActual;
    private float rumboBase, rumboActual, curvaturaActual, pendienteActual, pendienteObjetivo;
    private int filasGeneradas, tramosGenerados;
    private float largoValla, giroValla;

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
        GenerarHastaDistancia();
        if (colocarJugadorAlInicio)
        {
            ColocarJugador();
        }
    }

    private void Update()
    {
        GenerarHastaDistancia();
        BorrarTramosViejos();
        tramosActivos = tramos.Count;
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
            Vector3 derecha = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 posEsquis = destino + derecha * offsetEsquis.x + dir * offsetEsquis.z;

            // Buscamos el suelo justo debajo de esa posición, para que no floten en la pendiente
            float mejorEsquis = float.MaxValue;
            Vector3 sueloEsquis = Vector3.zero;
            bool hayEsquis = false;

            foreach (RaycastHit h in Physics.RaycastAll(posEsquis + Vector3.up * 5f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!h.collider.name.StartsWith("Tramo_") || h.distance >= mejorEsquis) continue;
                mejorEsquis = h.distance;
                sueloEsquis = h.point;
                hayEsquis = true;
            }

            esquis.position = hayEsquis
                ? sueloEsquis + Vector3.up * alturaEsquisSobreSuelo
                : posEsquis + Vector3.up * offsetEsquis.y;
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
            MeshFilter mf = viejo.objeto.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
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

        GameObject tramo = CrearMalla(centros, rumbos);
        int objetos = PoblarTramo(tramo.transform, centros);
        tramos.Enqueue(new Tramo { objeto = tramo, fin = centroActual });

        if (mostrarDebug)
            Debug.Log($"[Pista] Tramo {tramosGenerados} | rumbo {rumboActual:F0}° | pendiente {pendienteActual:F0}° | objetos {objetos} | tramos activos {tramos.Count}");

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

    private int PoblarTramo(Transform padre, List<Vector3> centros)
    {
        int n = centros.Count;
        float[] acum = new float[n];
        for (int i = 1; i < n; i++) acum[i] = acum[i - 1] + Vector3.Distance(centros[i], centros[i - 1]);
        float total = acum[n - 1];
        int contador = 0;

        // Vallas a los dos lados, con huecos al azar
        if (vallas != null && vallas.Length > 0 && largoValla > 0.1f)
        {
            for (float s = largoValla * 0.5f; s <= total - largoValla * 0.5f; s += largoValla)
            {
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    if (Azar() > probValla) continue;
                    Muestrear(centros, acum, s, out Vector3 posV, out Vector3 adelV, out Vector3 derV);
                    Vector3 posicion = posV + derV * (lado * ancho * 0.5f) + Vector3.up * alturaExtraValla;
                    Quaternion rotacion = Quaternion.LookRotation(adelV, Vector3.up) * Quaternion.Euler(0f, giroValla + giroExtraValla, 0f);
                    if (Colocar(Elegir(vallas), posicion, rotacion, padre) != null) contador++;
                }
            }
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
        }
        DestroyImmediate(temp);
        largoVallaMedido = largoValla;
    }

    private GameObject Elegir(GameObject[] lista) { return lista[rng.Next(lista.Length)]; }
    private float Rango(float min, float max) { return min + (float)rng.NextDouble() * (max - min); }
    private float Azar() { return (float)rng.NextDouble(); }

    private void OnDrawGizmosSelected()
    {
        if (puntoInicio == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(puntoInicio.position, puntoInicio.position + puntoInicio.forward * 10f);
        Gizmos.matrix = Matrix4x4.TRS(puntoInicio.position, Quaternion.Euler(0f, puntoInicio.eulerAngles.y, 0f), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(ancho, 0.2f, 2f));
    }


}