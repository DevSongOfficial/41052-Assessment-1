using UnityEngine;

public class RayTracer : MonoBehaviour
{
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
        Color baseColor = tex.GetPixelBilinear(uv.x, uv.y); // Return the interpolated color at UV coordinate.
    }
}
