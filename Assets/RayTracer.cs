using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RayTracer : MonoBehaviour
{
    [SerializeField] private Light lightSource;

    [SerializeField] private Renderer pixelPrefab;
    private MaterialPropertyBlock propertyBlock;

    [Header("UI")]
    [SerializeField] private TMP_InputField widthInputField;
    [SerializeField] private TMP_InputField heightInputField;

    [Space]
    [SerializeField] private int width = 16;
    [SerializeField] private int height = 16;
    [SerializeField] private float screenWidth = 10f;
    [SerializeField] private float screenHeight = 10f;

    private List<Renderer> pixels = new List<Renderer>();

    private readonly Color DefaultColor = Color.clear;
    private readonly Vector2Int FOV = new Vector2Int(50, 50);

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        InitializePixels();
        Render();
    }

    public void InitializePixels()
    {
        foreach(var pixel in pixels.ToArray())
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
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (float)x  / width;
                float v = (float)y  / height;

                float planeX = (2 * u - 1) * Mathf.Tan(FOV.x * 0.5f * Mathf.Deg2Rad);
                float planeY = (1 - 2 * v) * Mathf.Tan(FOV.y * 0.5f * Mathf.Deg2Rad);

                Vector3 localDirection = new Vector3(planeX, planeY, 1f).normalized;
                Vector3 direction = transform.TransformDirection(localDirection);

                Ray ray = new Ray(transform.position, direction);
                Color color = TraceRay(ray);

                SetPixel(x, y, color);
            }
        }
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
        float originOffset = 0.01f; // Used to avoid self-collision
        Vector3 origin = hit.point + hit.normal * originOffset; 
        bool inShadow = Physics.Raycast(origin, lightDirection);

        // (3) Make diffuse color
        float diffuse = Mathf.Max(0, Vector3.Dot(hit.normal, lightDirection));
        diffuse = inShadow ? 0 : diffuse;

        // (4) Combine ambient and diffuse to prevent shaded area being black
        float ambientStrength = 0.4f;
        float lighting = Mathf.Clamp01(ambientStrength + diffuse);

        Color renderColor = baseColor * lighting;
        
        return renderColor;
    }
}
