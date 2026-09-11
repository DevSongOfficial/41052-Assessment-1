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

    private readonly Color DefaultColor = Color.black;
    private readonly Vector2Int FOV = new Vector2Int(50, 50);

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Start()
    {
        InitializePixels();
        Render();
    }

    private void InitializePixels()
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

    private void Render()
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (float)x  / width;
                float v = (float)y  / height;

                float horizontalAngle = Mathf.Lerp(-FOV.x * 0.5f, FOV.x * 0.5f, u);
                float verticalAngle = Mathf.Lerp(FOV.y * 0.5f, -FOV.y * 0.5f, v);

                Quaternion rotation = Quaternion.Euler(verticalAngle, horizontalAngle, 0);
                Vector3 direction = rotation * transform.forward;

                Ray ray = new Ray(transform.position, direction);
                Color color = TraceRay(ray);

                SetPixel(x, y, color);
            }
        }
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

        // (2) Make diffuse color
        Vector3 lightDirection = -lightSource.transform.forward;
        float diffuse = Mathf.Max(0, Vector3.Dot(hit.normal, lightDirection));

        // (3) Combine ambient and diffuse to prevent shaded area being black
        float ambientStrength = 0.4f;
        float lighting = Mathf.Clamp01(ambientStrength + diffuse);

        Color renderColor = baseColor * lighting;
        
        return renderColor;
    }
}
