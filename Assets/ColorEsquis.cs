/*using UnityEngine;

public class ColorEsquis : MonoBehaviour
{
    [Header("Qué pintar")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private int indiceMaterial = -1;            // -1 = todos los materiales
    [SerializeField] private string propiedadColor = "_BaseColor";

    [Header("Paleta (mismo orden que los botones)")]
    [SerializeField]
    private Color[] colores =
    {
        new Color(0.85f, 0.10f, 0.10f),
        new Color(0.10f, 0.35f, 0.90f),
        new Color(0.10f, 0.70f, 0.25f),
        new Color(1.00f, 0.80f, 0.10f),
        new Color(0.10f, 0.10f, 0.10f)
    };

    [Header("Solo lectura")]
    [SerializeField] private int colorActual = -1;

    private int siguientePrueba;

    private void Reset()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    // Los botones llaman a este método con un número (0 = Rojo, 1 = Azul...)
    public void PonerColor(int indice)
    {
        if (indice < 0 || indice >= colores.Length)
        {
            Debug.LogWarning($"[Color] Índice {indice} fuera de la paleta ({colores.Length} colores).", this);
            return;
        }

        int tocados = Aplicar(colores[indice]);
        colorActual = indice;
        Debug.Log($"[Color] Color {indice} aplicado a {tocados} material(es)", this);

        if (tocados == 0)
            Debug.LogWarning($"[Color] No encuentro la propiedad '{propiedadColor}'. Prueba con '_Color'.", this);
    }

    private int Aplicar(Color color)
    {
        int tocados = 0;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.materials; // copias propias de este objeto, no tocan los assets
            for (int i = 0; i < mats.Length; i++)
            {
                if (indiceMaterial >= 0 && i != indiceMaterial) continue;
                if (!mats[i].HasProperty(propiedadColor)) continue;
                mats[i].SetColor(propiedadColor, color);
                tocados++;
            }
        }
        return tocados;
    }

    [ContextMenu("Listar materiales")]
    private void ListarMateriales()
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                Debug.Log($"[Color] {r.name} | material {i}: {mats[i].name} ({mats[i].shader.name})", r);
        }
    }

    [ContextMenu("Probar siguiente color")]
    private void ProbarSiguienteColor()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Color] Hazlo con Play activo."); return; }
        PonerColor(siguientePrueba);
        siguientePrueba = (siguientePrueba + 1) % colores.Length;
    }
}*/

using UnityEngine;

public class ColorEsquis : MonoBehaviour
{
    [Header("Qué pintar")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private int indiceMaterial = -1;            // -1 = todos los materiales
    [SerializeField] private string propiedadColor = "_BaseColor";

    [Header("Paleta (mismo orden que los botones)")]
    [SerializeField]
    private Color[] colores =
    {
        new Color(0.85f, 0.10f, 0.10f),
        new Color(0.10f, 0.35f, 0.90f),
        new Color(0.10f, 0.70f, 0.25f),
        new Color(1.00f, 0.80f, 0.10f),
        new Color(0.10f, 0.10f, 0.10f)
    };

    [Header("Solo lectura")]
    [SerializeField] private int colorActual = -1;
    [SerializeField] private int llamadas;

    private MaterialPropertyBlock bloque;
    private int siguientePrueba;

    private void Reset()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    // Los botones llaman a este método con un número (0 = Rojo, 1 = Azul...)
    public void PonerColor(int indice)
    {
        llamadas++;
        Debug.Log($"[Color] PonerColor({indice}) | llamada nº {llamadas} | objeto activo: {gameObject.activeInHierarchy}", this);

        if (indice < 0 || indice >= colores.Length)
        {
            Debug.LogWarning($"[Color] Índice {indice} fuera de la paleta ({colores.Length} colores).", this);
            return;
        }

        int tocados = Aplicar(colores[indice]);
        colorActual = indice;
        Debug.Log($"[Color] Aplicado a {tocados} material(es)", this);

        if (tocados == 0)
            Debug.LogWarning($"[Color] No encuentro la propiedad '{propiedadColor}'. Prueba con '_Color'.", this);
    }

    private int Aplicar(Color color)
    {
        if (bloque == null) bloque = new MaterialPropertyBlock();

        int tocados = 0;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (indiceMaterial >= 0 && i != indiceMaterial) continue;
                if (mats[i] == null || !mats[i].HasProperty(propiedadColor)) continue;

                r.GetPropertyBlock(bloque, i);
                bloque.SetColor(propiedadColor, color);
                r.SetPropertyBlock(bloque, i);
                tocados++;

                Debug.Log($"[Color]   {r.name} | visible en escena: {r.gameObject.activeInHierarchy} | material {i} '{mats[i].name}' -> {color}", r);
            }
        }
        return tocados;
    }

    [ContextMenu("Listar materiales")]
    private void ListarMateriales()
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                Debug.Log($"[Color] {r.name} (activo: {r.gameObject.activeInHierarchy}) | material {i}: {mats[i].name} ({mats[i].shader.name})", r);
        }
    }

    [ContextMenu("Probar siguiente color")]
    private void ProbarSiguienteColor()
    {
        PonerColor(siguientePrueba);
        siguientePrueba = (siguientePrueba + 1) % colores.Length;
    }
}