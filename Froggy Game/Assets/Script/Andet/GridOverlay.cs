using UnityEngine;
using UnityEngine.Rendering; // SRP hooks

/// Attach to your Main Camera (Orthographic).
/// Works in Built-in + URP/HDRP. Press G to toggle.
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class GridOverlay : MonoBehaviour
{
    [Header("Sizing")]
    public FrogMovement frog;             // optional: reads frog.gridSize
    public float cellSize = 2f;           // fallback if no frog

    [Header("Anchor / Offset")]
    public Transform anchor;              // e.g., your Frog transform
    public Vector2 anchorOffset = Vector2.zero; // world-space nudge (set Y negative to push grid down)
    public bool snapAnchorToCell = true;  // if true, grid lines lock to anchor on exact cell steps

    [Header("Appearance")]
    public Color lineColor = new Color(1f, 1f, 1f, 0.35f);
    public Color axisColor = new Color(1f, 1f, 0f, 0.8f);
    public int boldEvery = 5;
    public float alphaBoldBoost = 0.25f;

    [Header("Controls")]
    public bool show = true;              // starts visible
    public KeyCode toggleKey = KeyCode.G;

    Camera _cam;
    static Material _mat;
    bool _usingSRP;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        EnsureMat();
        _usingSRP = GraphicsSettings.currentRenderPipeline != null;
        if (_usingSRP)
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        if (_usingSRP)
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    void Update()
    {
        if (frog) cellSize = Mathf.Max(0.0001f, frog.gridSize);
        if (Application.isPlaying && Input.GetKeyDown(toggleKey)) show = !show;

        // default anchor to frog if not set
        if (!anchor && frog) anchor = frog.transform;
    }

    // Built-in path
    void OnRenderObject()
    {
        if (_usingSRP) return;
        if (!Validate()) return;
        if (Camera.current != _cam) return;
        DrawGridForCamera(_cam);
    }

    // SRP path (URP/HDRP)
    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _cam) return;
        if (!Validate()) return;
        DrawGridForCamera(cam);
    }

    bool Validate()
    {
        if (!show) return false;
        if (!_cam) _cam = GetComponent<Camera>();
        if (!_cam) return false;
        if (!_cam.orthographic) return false;
        EnsureMat();
        if (_mat == null) return false;
        if (cellSize <= 0f) return false;
        return true;
    }

    void DrawGridForCamera(Camera cam)
    {
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;

        float minX = c.x - w * 0.5f;
        float maxX = c.x + w * 0.5f;
        float minY = c.y - h * 0.5f;
        float maxY = c.y + h * 0.5f;

        // === Anchor-aligned origin (this is the magic) ===
        Vector2 origin = Vector2.zero;
        if (anchor)
            origin = (Vector2)anchor.position + anchorOffset;

        if (snapAnchorToCell && cellSize > 0f)
        {
            origin.x = Mathf.Round(origin.x / cellSize) * cellSize;
            origin.y = Mathf.Round(origin.y / cellSize) * cellSize;
        }

        // Phase-align the first visible line so the grid stays locked to origin
        float startX = minX - Mathf.Repeat(minX - origin.x, cellSize);
        float startY = minY - Mathf.Repeat(minY - origin.y, cellSize);
        float endX = maxX;
        float endY = maxY;

        // Set proper matrices so GL draws in world space for this camera
        GL.PushMatrix();
        GL.LoadProjectionMatrix(cam.projectionMatrix);
        GL.modelview = cam.worldToCameraMatrix;

        _mat.SetPass(0);
        GL.Begin(GL.LINES);

        // Vertical lines
        for (float x = startX; x <= endX + 0.0001f; x += cellSize)
        {
            bool isAxis = Mathf.Abs(x) < 0.0001f;
            bool isBold = boldEvery > 0 && Mathf.Abs(Mathf.RoundToInt((x - origin.x) / cellSize)) % boldEvery == 0;

            Color cLine = isAxis ? axisColor : lineColor;
            if (isBold && !isAxis) cLine.a = Mathf.Clamp01(cLine.a + alphaBoldBoost);

            GL.Color(cLine);
            GL.Vertex(new Vector3(x, minY, 0f));
            GL.Vertex(new Vector3(x, maxY, 0f));
        }

        // Horizontal lines
        for (float y = startY; y <= endY + 0.0001f; y += cellSize)
        {
            bool isAxis = Mathf.Abs(y) < 0.0001f;
            bool isBold = boldEvery > 0 && Mathf.Abs(Mathf.RoundToInt((y - origin.y) / cellSize)) % boldEvery == 0;

            Color cLine = isAxis ? axisColor : lineColor;
            if (isBold && !isAxis) cLine.a = Mathf.Clamp01(cLine.a + alphaBoldBoost);

            GL.Color(cLine);
            GL.Vertex(new Vector3(minX, y, 0f));
            GL.Vertex(new Vector3(maxX, y, 0f));
        }

        GL.End();
        GL.PopMatrix();
    }

    static void EnsureMat()
    {
        if (_mat != null) return;

        var shader = Shader.Find("Hidden/Internal-Colored");
        if (!shader) shader = Shader.Find("Sprites/Default");
        if (!shader) return;

        _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _mat.SetInt("_ZWrite", 0);
        if (_mat.HasProperty("_ZTest"))
            _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }
}
