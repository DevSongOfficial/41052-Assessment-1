using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Diagnostics;

public class RayTracer : MonoBehaviour
{
    [SerializeField] private Light lightSource;

    [SerializeField] private Renderer pixelPrefab;
    private MaterialPropertyBlock propertyBlock;

    [Header("UI")]
    [SerializeField] private TMP_InputField widthInputField;
    [SerializeField] private TMP_InputField heightInputField;
    [Space]
    [SerializeField] private Toggle shadowToggle;
    [SerializeField] private Toggle diffuseToggle;
    [SerializeField] private Toggle ambientToggle;
    [SerializeField] private Toggle specularToggle;

    [Space]
    [SerializeField] private int width = 16;
    [SerializeField] private int height = 16;
    [SerializeField] private float screenWidth = 10f;
    [SerializeField] private float screenHeight = 10f;

    private List<Renderer> pixels = new List<Renderer>();

    private readonly Color DefaultColor = Color.black;
    private readonly Vector2Int FOV = new Vector2Int(50, 50);
    private float renderDuration = 5f;

    private Coroutine renderCoroutine;
    
    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        widthInputField.SetTextWithoutNotify(width.ToString());
        heightInputField.SetTextWithoutNotify(height.ToString());

        //InitializePixels();
        //Render();
    }

    [ContextMenu("TEST")]
    public void MeasureTimeWithoutRendering()
    {
        for(int resolution = 8; resolution <= 2048; resolution = resolution * 2)
        {
            width = resolution;
            height = resolution;

            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;

                    float planeX = (2 * u - 1) * Mathf.Tan(FOV.x * 0.5f * Mathf.Deg2Rad);
                    float planeY = (1 - 2 * v) * Mathf.Tan(FOV.y * 0.5f * Mathf.Deg2Rad);

                    Vector3 localDirection = new Vector3(planeX, planeY, 1f).normalized;
                    Vector3 direction = transform.TransformDirection(localDirection);

                    Ray ray = new Ray(transform.position, direction);

                    Color color = TraceRay(ray);

                    // ...
                }
            }

            stopwatch.Stop();

            UnityEngine.Debug.Log($"{width}, {height}: ");
            UnityEngine.Debug.Log($"Time: {stopwatch.Elapsed.TotalMilliseconds}");
        }

    }

    public void InitializePixels()
    {
        UpdateResolution();

        foreach (var pixel in pixels.ToArray())
            GameObject.Destroy(pixel.gameObject);

        pixels.Clear();

        float pixelWidth = screenWidth / width;
        float pixelHeight = screenHeight / height;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Renderer pixel = Instantiate(pixelPrefab);
                
                SetRendererColor(pixel, DefaultColor);

                pixel.transform.localScale = new Vector3(pixelWidth, pixelHeight, 0.5f);
                pixel.transform.localPosition = new Vector3(-screenWidth  * 0.5f + pixelWidth  * x, 
                                                             screenHeight * 0.5f - pixelHeight * y, 0);

                pixels.Add(pixel);
            }
    }

    public void Render()
    {
        if (renderCoroutine != null)
            StopCoroutine(renderCoroutine);

        renderCoroutine = StartCoroutine(RenderCoroutine());
    }

    private IEnumerator RenderCoroutine()
    {
        UpdateResolution();
        if (pixels.Count != width * height) InitializePixels();

        foreach (Renderer pixel in pixels)
            SetRendererColor(pixel, DefaultColor);

        int totalPixels = width * height;
        int renderedPixels = 0;
        float startTime = Time.time;

        while (renderedPixels < totalPixels)
        {
            float progress = Mathf.Clamp01((Time.time - startTime) / renderDuration);
            int targetPixelCount = Mathf.CeilToInt(totalPixels * progress);

            while (renderedPixels < targetPixelCount)
            {
                int x = renderedPixels % width;
                int y = renderedPixels / width;

                float u = (float)x / width;
                float v = (float)y / height;

                float planeX = (2 * u - 1) * Mathf.Tan(FOV.x * 0.5f * Mathf.Deg2Rad);
                float planeY = (1 - 2 * v) * Mathf.Tan(FOV.y * 0.5f * Mathf.Deg2Rad);

                Vector3 localDirection = new Vector3(planeX, planeY, 1f).normalized;
                Vector3 direction = transform.TransformDirection(localDirection);

                Ray ray = new Ray(transform.position, direction);
                Color color = TraceRay(ray);

                SetPixel(x, y, color);

                renderedPixels++;
            }

            yield return null;
        }

        renderCoroutine = null;
    }

    private void SetPixel(int x, int y, Color color)
    {
        int index = width * y + x;
        SetRendererColor(pixels[index], color);
    }

    private void SetRendererColor(Renderer renderer, Color color)
    {
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateResolution()
    {
        if (int.TryParse(widthInputField.text, out int width))
            this.width = Mathf.Clamp(width, 1, 512);

        if (int.TryParse(heightInputField.text, out int height))
            this.height = Mathf.Clamp(height, 1, 512);
    }

    private Color TraceRay(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return DefaultColor;

        if (!hit.collider.TryGetComponent(out Renderer renderer))
            return DefaultColor;

        Texture2D tex = renderer.sharedMaterial.mainTexture as Texture2D;
        if (tex == null)
            return DefaultColor;

        // (1) Make base color
        Vector2 uv = hit.textureCoord;
        Color texColor = tex.GetPixelBilinear(uv.x, uv.y); // Return the interpolated color at UV coordinate.
        Color tint = renderer.sharedMaterial.GetColor("_BaseColor");
        Color baseColor = texColor * tint;

        Vector3 lightDirection = -lightSource.transform.forward;

        // (2) Check if the target is in shadow
        bool inShadow = false;

        if (shadowToggle.isOn)
        {
            float originOffset = 0.01f; // Used to avoid self-collision
            Vector3 origin = hit.point + hit.normal * originOffset;
            inShadow = Physics.Raycast(origin, lightDirection);
        }

        // (3) Make ambient color
        Color ambientColor = DefaultColor;

        if (ambientToggle.isOn)
        {
            float ambientStrength = 0.3f;
            ambientColor = baseColor * ambientStrength;
        }

        // (4) Make diffuse color
        Color diffuseColor = DefaultColor;

        if (diffuseToggle.isOn)
        {
            float diffuseFactor = Mathf.Max(0, Vector3.Dot(hit.normal, lightDirection));

            if (inShadow)
                diffuseFactor = 0;

            Color lightColor = lightSource.color * lightSource.intensity;
            diffuseColor = baseColor * lightColor * diffuseFactor;
        }

        // (5) Make specular color
        Color specularColor = DefaultColor;

        if (specularToggle.isOn)
        {
            Vector3 viewDirection = (transform.position - hit.point).normalized;
            Vector3 reflectionDirection = Vector3.Reflect(-lightDirection, hit.normal);

            float specularFactor = Mathf.Pow(Mathf.Max(0, Vector3.Dot(viewDirection, reflectionDirection)), 16);

            if (inShadow)
                specularFactor = 0;

            Color lightColor = lightSource.color * lightSource.intensity;
            specularColor = lightColor * specularFactor;
        }

        Color renderColor = ambientColor + diffuseColor + specularColor;

        return renderColor;
    }
}
