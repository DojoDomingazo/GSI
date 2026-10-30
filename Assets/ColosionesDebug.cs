using UnityEngine;

public class DebugColisiones : MonoBehaviour
{
    [SerializeField] private float caidaDeVelocidadParaAvisar = 2f;

    [Header("Solo lectura")]
    [SerializeField] private float velocidad;
    [SerializeField] private string ultimoContacto;

    private Rigidbody rb;
    private float velocidadAnterior;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        velocidad = rb.linearVelocity.magnitude;
        if (velocidadAnterior - velocidad > caidaDeVelocidadParaAvisar)
            Debug.LogWarning($"[Choque] FRENAZO {velocidadAnterior:F1} -> {velocidad:F1} m/s | último contacto: {ultimoContacto}", this);
        velocidadAnterior = velocidad;
    }

    private void OnCollisionEnter(Collision c)
    {
        if (EsSuelo(c.collider)) return;
        ultimoContacto = Ruta(c.collider);
        Debug.Log($"[Choque] ENTRA {ultimoContacto} | velocidad {velocidad:F1} m/s", c.collider);
    }

    private void OnCollisionStay(Collision c)
    {
        if (!EsSuelo(c.collider)) ultimoContacto = Ruta(c.collider);
    }

    private bool EsSuelo(Collider col) { return col.name.StartsWith("Tramo_"); }

    private string Ruta(Collider col)
    {
        string ruta = col.name;
        Transform t = col.transform.parent;
        while (t != null) { ruta = t.name + "/" + ruta; t = t.parent; }
        return ruta;
    }
}