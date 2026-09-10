using UnityEngine;

public class RayTracer : MonoBehaviour
{
    [SerializeField] private RenderTexture renderTexture;


    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        if (!hit.collider.TryGetComponent(out Renderer renderer))
            return;

        Texture2D tex = renderer.sharedMaterial.mainTexture as Texture2D;
        if (tex == null)
            return;

        Vector2 uv = hit.textureCoord;
        Color texColor = tex.GetPixelBilinear(uv.x, uv.y); // Return the interpolated color at UV coordinate.
        Color tint = renderer.sharedMaterial.GetColor("_BaseColor");

        Color baseColor = texColor * tint; 

        RenderTexture.active = renderTexture;  // Set current render target
        GL.Clear(false, true, baseColor);      // Initialize current render target with the base color
    }
}
