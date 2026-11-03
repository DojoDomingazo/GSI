using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SistemaVidas : MonoBehaviour
{
    [Header("Vidas")]
    [SerializeField] private int vidasMax = 3;
    [SerializeField] private float umbralImpacto = 8f;       // m/s contra la superficie para que cuente como choque fuerte
    [SerializeField] private float invulnerabilidad = 2f;    // segundos sin poder perder otra vida tras un golpe
    [SerializeField] private bool vibrarAlChocar = true;

    [Header("Referencias")]
    [SerializeField] private Rigidbody cuerpo;               // SkierBody (se rellena solo)
    [SerializeField] private NavegacionEsqui navegacion;     // el script NavegacionEsqui de SkierBody
    [SerializeField] private Transform cabeza;               // CenterEyeAnchor (si lo dejas vacío, lo busca solo)

    [Header("Vidas en pantalla (arriba a la derecha)")]
    [SerializeField] private Vector2 posicionHud = new Vector2(0.35f, 0.22f);   // metros a la derecha y arriba desde el centro de tu vista
    [SerializeField] private float distanciaHud = 1f;                           // metros delante de ti
    [SerializeField] private Color colorVida = new Color(0.9f, 0.1f, 0.15f, 1f);
    [SerializeField] private Color colorVidaPerdida = new Color(1f, 1f, 1f, 0.15f);

    [Header("Menú de muerte")]
    [SerializeField] private GameObject panelMuerte;         // el panel con el botón de reiniciar
    [SerializeField] private float distanciaPanelMuerte = 1.5f;
    [SerializeField] private float alturaPanelMuerte = -0.1f; // respecto a tu cabeza
    [SerializeField] private bool reiniciarConBotonA = true;  // plan B: A o X del mando reinician

    [Header("Solo lectura (míralo en Play)")]
    [SerializeField] private int vidas;
    [SerializeField] private bool muerto;
    [SerializeField] private float ultimoImpacto;
    [SerializeField] private string ultimoObjetoGolpeado;

    private Image[] corazones;
    private float finInvulnerabilidad;

    private void Reset()
    {
        cuerpo = GetComponent<Rigidbody>();
        navegacion = GetComponent<NavegacionEsqui>();
    }

    private void Awake()
    {
        if (cuerpo == null) cuerpo = GetComponent<Rigidbody>();
        vidas = vidasMax;
        muerto = false;
        if (panelMuerte != null) panelMuerte.SetActive(false);
    }

    private void Start()
    {
        if (cabeza == null)
        {
            OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null) cabeza = rig.centerEyeAnchor;
        }
        if (cabeza == null && Camera.main != null) cabeza = Camera.main.transform;

        if (cabeza == null)
            Debug.LogError("[Vidas] No encuentro la cabeza (CenterEyeAnchor): no puedo poner las vidas en pantalla.", this);
        else
            CrearHud();

        Debug.Log($"[Vidas] Listo. Vidas: {vidas} | umbral de choque fuerte: {umbralImpacto:F1} m/s", this);
    }

    private void Update()
    {
        if (muerto && reiniciarConBotonA && OVRInput.GetDown(OVRInput.Button.One | OVRInput.Button.Three))
            Reiniciar();
    }

    // ---------- Choques ----------

    private void OnCollisionEnter(Collision c)
    {
        if (muerto) return;
        if (c.collider.name.StartsWith("Tramo_")) return;   // el suelo de la pista no cuenta
        if (Time.time < finInvulnerabilidad) return;

        // Velocidad con la que has ido contra la superficie (la parte que roza de lado no cuenta)
        float impacto = c.contactCount > 0
            ? Mathf.Abs(Vector3.Dot(c.relativeVelocity, c.GetContact(0).normal))
            : c.relativeVelocity.magnitude;

        ultimoImpacto = impacto;
        ultimoObjetoGolpeado = c.collider.name;
        Debug.Log($"[Vidas] Choque con '{c.collider.name}' | velocidad de impacto: {impacto:F1} m/s (umbral {umbralImpacto:F1})", this);

        if (impacto < umbralImpacto) return;
        PerderVida();
    }

    [ContextMenu("Perder una vida (prueba)")]
    public void PerderVida()
    {
        if (muerto) return;

        vidas = Mathf.Max(0, vidas - 1);
        finInvulnerabilidad = Time.time + invulnerabilidad;
        ActualizarHud();
        Debug.Log($"[Vidas] CHOQUE FUERTE contra '{ultimoObjetoGolpeado}' | vidas: {vidas}/{vidasMax}", this);

        if (vibrarAlChocar) StartCoroutine(Vibrar());
        if (vidas <= 0) Morir();
    }

    private void Morir()
    {
        muerto = true;
        Debug.Log("[Vidas] SIN VIDAS: has muerto.", this);

        if (navegacion != null) navegacion.enabled = false;
        if (cuerpo != null)
        {
            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
            cuerpo.constraints = RigidbodyConstraints.FreezeAll;
        }

        ColocarPanelMuerte();
        if (panelMuerte != null)
            panelMuerte.SetActive(true);
        else
            Debug.LogWarning("[Vidas] No hay Panel Muerte asignado. Pulsa A o X del mando para reiniciar.", this);
    }

    // ---------- Reinicio ----------

    // Conéctalo al On Click del botón de reiniciar
    [ContextMenu("Reiniciar (prueba)")]
    public void Reiniciar()
    {
        string nombre = SceneManager.GetActiveScene().name;
        if (!Application.CanStreamedLevelBeLoaded(nombre))
        {
            Debug.LogError($"[Vidas] La escena '{nombre}' no está en File > Build Profiles > Scene List. Añádela para poder reiniciar.", this);
            return;
        }

        Debug.Log("[Vidas] Reiniciando la partida...", this);
        SceneManager.LoadScene(nombre);
    }

    private void ColocarPanelMuerte()
    {
        if (panelMuerte == null) return;

        Vector3 origen = cabeza != null ? cabeza.position : transform.position;
        Vector3 frente = cabeza != null ? Vector3.ProjectOnPlane(cabeza.forward, Vector3.up) : transform.forward;
        if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
        frente.Normalize();

        Transform t = panelMuerte.transform;
        t.position = origen + frente * distanciaPanelMuerte + Vector3.up * alturaPanelMuerte;
        t.rotation = Quaternion.LookRotation(frente, Vector3.up);
    }

    // ---------- Vidas en pantalla ----------

    private void CrearHud()
    {
        GameObject go = new GameObject("HUD_Vidas");
        go.transform.SetParent(cabeza, false);
        go.transform.localPosition = new Vector3(posicionHud.x, posicionHud.y, distanciaHud);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.001f;   // 1 unidad del canvas = 1 mm

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        ((RectTransform)go.transform).sizeDelta = new Vector2(300f, 100f);

        Sprite sprite = CrearSpriteCorazon(64);
        corazones = new Image[vidasMax];
        for (int i = 0; i < vidasMax; i++)
        {
            GameObject c = new GameObject("Corazon_" + (i + 1));
            c.transform.SetParent(go.transform, false);

            Image img = c.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;

            RectTransform r = img.rectTransform;
            r.sizeDelta = new Vector2(80f, 80f);
            r.anchoredPosition = new Vector2((i - (vidasMax - 1) * 0.5f) * 90f, 0f);
            corazones[i] = img;
        }

        ActualizarHud();
    }

    private void ActualizarHud()
    {
        if (corazones == null) return;
        for (int i = 0; i < corazones.Length; i++)
            corazones[i].color = i < vidas ? colorVida : colorVidaPerdida;
    }

    // Dibuja un corazón con una fórmula, así no hace falta ninguna imagen
    private static Sprite CrearSpriteCorazon(int tam)
    {
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixeles = new Color32[tam * tam];
        for (int py = 0; py < tam; py++)
        {
            for (int px = 0; px < tam; px++)
            {
                float x = ((px + 0.5f) / tam * 2f - 1f) * 1.3f;
                float y = ((py + 0.5f) / tam * 2f - 1f) * 1.3f + 0.12f;
                float a = x * x + y * y - 1f;
                float f = a * a * a - x * x * y * y * y;
                pixeles[py * tam + px] = f <= 0f ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        tex.SetPixels32(pixeles);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f);
    }

    private IEnumerator Vibrar()
    {
        OVRInput.SetControllerVibration(1f, 1f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(1f, 1f, OVRInput.Controller.RTouch);
        yield return new WaitForSeconds(0.3f);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }
}