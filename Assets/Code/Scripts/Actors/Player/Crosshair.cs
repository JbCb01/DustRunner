using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class Crosshair : MaskableGraphic
{
    [Header("Ring Settings")] 
    public float Radius = 20f;
    public float Thickness = 2f;
    [Range(3, 64)] public int Segments = 32;
    public bool Filled = false;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float outerRadius = Radius;
        float innerRadius = Filled ? 0 : Radius - Thickness;

        // Prevent negative inner radius
        if (innerRadius < 0) innerRadius = 0;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        float deltaAngle = (2 * Mathf.PI) / Segments;

        for (int i = 0; i < Segments + 1; i++)
        {
            float angle = deltaAngle * i;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Outer Vertex
            vertex.position = new Vector3(cos * outerRadius, sin * outerRadius);
            vh.AddVert(vertex);

            // Inner Vertex
            vertex.position = new Vector3(cos * innerRadius, sin * innerRadius);
            vh.AddVert(vertex);
        }

        // Create Triangles
        for (int i = 0; i < Segments; i++)
        {
            int index = i * 2;
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index + 3, index + 2, index);
        }
    }

    // Force update when values change in Inspector or Code
    public void SetRadius(float r, float t)
    {
        if (Mathf.Abs(Radius - r) > 0.01f || Mathf.Abs(Thickness - t) > 0.01f)
        {
            Radius = r;
            Thickness = t;
            SetVerticesDirty(); // Triggers OnPopulateMesh
        }
    }

    public void SetFilled(bool filled)
    {
        if (Filled != filled)
        {
            Filled = filled;
            SetVerticesDirty();
        }
    }
}