using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[AddComponentMenu("Alembic/PointsRenderer")]
[RequireComponent(typeof(AlembicPoints))]
public class AlembicPointsRenderer : MonoBehaviour
{
    const int TextureWidth = 2048;

    public Mesh m_mesh;
    public Material[] m_materials;
    public bool m_castShadow = false;
    public bool m_receiveShadows = false;
    public float m_countRate = 1.0f;
    public Vector3 m_modelScale = Vector3.one;
    public Vector3 m_transScale = Vector3.one;
#if UNITY_EDITOR
    public bool m_showBounds = true;
#endif

    int m_instancesPerBatch;
    int m_layer;
    Mesh m_expandedMesh;
    Bounds m_bounds;
    List<List<Material> > m_actualMaterials;

    RenderTexture m_texPositions;
    RenderTexture m_texIDs;

#region static

    public const int MaxVertices = 65000; // Mesh's limitation

    public static int ceildiv(int v, int d)
    {
        return v / d + (v % d == 0 ? 0 : 1);
    }

    public static Vector3 mul(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    static RenderTexture CreateDataTexture(int w, int h, RenderTextureFormat f)
    {
        RenderTexture r = new RenderTexture(w, h, 0, f);
        r.filterMode = FilterMode.Point;
        r.useMipMap = false;
        r.generateMips = false;
        r.Create();
        return r;
    }

    public static Mesh CreateExpandedMesh(Mesh mesh, int required_instances, out int instancesPerBatch)
    {
        Vector3[] verticesBase = mesh.vertices;
        Vector3[] normalsBase = (mesh.normals == null || mesh.normals.Length == 0) ? null : mesh.normals;
        Vector4[] tangentsBase = (mesh.tangents == null || mesh.tangents.Length == 0) ? null : mesh.tangents;
        Vector2[] uvBase = (mesh.uv == null || mesh.uv.Length == 0) ? null : mesh.uv;
        Color[] colorsBase = (mesh.colors == null || mesh.colors.Length == 0) ? null : mesh.colors;
        int[] indicesBase = (mesh.triangles == null || mesh.triangles.Length == 0) ? null : mesh.triangles;
        instancesPerBatch = Mathf.Min(MaxVertices / mesh.vertexCount, required_instances);

        Vector3[] vertices = new Vector3[verticesBase.Length * instancesPerBatch];
        Vector2[] idata = new Vector2[verticesBase.Length * instancesPerBatch];
        Vector3[] normals = normalsBase == null ? null : new Vector3[normalsBase.Length * instancesPerBatch];
        Vector4[] tangents = tangentsBase == null ? null : new Vector4[tangentsBase.Length * instancesPerBatch];
        Vector2[] uv = uvBase == null ? null : new Vector2[uvBase.Length * instancesPerBatch];
        Color[] colors = colorsBase == null ? null : new Color[colorsBase.Length * instancesPerBatch];
        int[] indices = indicesBase == null ? null : new int[indicesBase.Length * instancesPerBatch];

        for (int ii = 0; ii < instancesPerBatch; ++ii)
        {
            for (int vi = 0; vi < verticesBase.Length; ++vi)
            {
                int i = ii * verticesBase.Length + vi;
                vertices[i] = verticesBase[vi];
                idata[i] = new Vector2((float)ii, (float)vi);
            }
            
            if (normals != null)
            {
                for (int vi = 0; vi < normalsBase.Length; ++vi)
                {
                    int i = ii * normalsBase.Length + vi;
                    normals[i] = normalsBase[vi];
                }
            }
            
            if (tangents != null)
            {
                for (int vi = 0; vi < tangentsBase.Length; ++vi)
                {
                    int i = ii * tangentsBase.Length + vi;
                    tangents[i] = tangentsBase[vi];
                }
            }
            
            if (uv != null)
            {
                for (int vi = 0; vi < uvBase.Length; ++vi)
                {
                    int i = ii * uvBase.Length + vi;
                    uv[i] = uvBase[vi];
                }
            }
            
            if (colors != null)
            {
                for (int vi = 0; vi < colorsBase.Length; ++vi)
                {
                    int i = ii * colorsBase.Length + vi;
                    colors[i] = colorsBase[vi];
                }
            }
            
            if (indices != null)
            {
                for (int vi = 0; vi < indicesBase.Length; ++vi)
                {
                    int i = ii * indicesBase.Length + vi;
                    indices[i] = ii * verticesBase.Length + indicesBase[vi];
                }
            }
        }
        
        Mesh ret = new Mesh();
        ret.vertices = vertices;
        ret.normals = normals;
        ret.tangents = tangents;
        ret.uv = uv;
        ret.colors = colors;
        ret.uv2 = idata;
        ret.triangles = indices;
        
        return ret;
    }

#endregion

    void ForEachMaterials(System.Action<Material> a)
    {
        m_actualMaterials.ForEach((ma) => { ma.ForEach(v => { a(v); }); });
    }

    Material CloneMaterial(Material src, int nth)
    {
        Material m = new Material(src);
        m.SetInt("_BatchBegin", nth * m_instancesPerBatch);
        m.SetTexture("_PositionBuffer", m_texPositions);
        m.SetTexture("_IDBuffer", m_texIDs);

        // fix rendering order for transparent objects
        if (m.renderQueue >= 3000)
        {
            m.renderQueue = m.renderQueue + (nth + 1);
        }
        return m;
    }

    public void RefreshMaterials()
    {
        m_actualMaterials = null;
        Flush();
    }

    public void Flush()
    {
        if (m_mesh == null)
        {
            Debug.LogWarning("AlembicPointsRenderer: mesh is not assigned");
            return;
        }

        if (m_materials == null || m_materials.Length==0 || (m_materials.Length==1 && m_materials[0]==null))
        {
            Debug.LogWarning("AlembicPointsRenderer: material is not assigned");
            return;
        }

        var points = GetComponent<AlembicPoints>();
        var abcData = points.abcData;
        int maxInstances = points.abcPeakVertexCount;
        int instanceCount = abcData.count;
        m_bounds.center = mul(abcData.boundsCenter, m_transScale);
        m_bounds.extents = mul(abcData.boundsExtents, m_transScale);

        if (instanceCount == 0)
        {
            // nothing to draw
            return;
        }

        // update data texture
        if (m_texPositions == null)
        {
            int height = ceildiv(maxInstances, TextureWidth);
            m_texPositions = CreateDataTexture(TextureWidth, height, RenderTextureFormat.ARGBFloat);
            m_texIDs = CreateDataTexture(TextureWidth, height, RenderTextureFormat.RFloat);
        }

        AbcAPI.aiPointsCopyPositionsToTexture(ref abcData, m_texPositions.GetNativeTexturePtr(), m_texPositions.width, m_texPositions.height, AbcAPI.GetTextureFormat(m_texPositions));
        AbcAPI.aiPointsCopyIDsToTexture(ref abcData, m_texIDs.GetNativeTexturePtr(), m_texIDs.width, m_texIDs.height, AbcAPI.GetTextureFormat(m_texIDs));

        if (m_expandedMesh == null)
        {
            m_expandedMesh = CreateExpandedMesh(m_mesh, maxInstances, out m_instancesPerBatch);
            m_expandedMesh.UploadMeshData(true);
            return;
        }

        if (m_actualMaterials == null)
        {
            m_actualMaterials = new List<List<Material>>();
            while (m_actualMaterials.Count < m_materials.Length)
            {
                m_actualMaterials.Add(new List<Material>());
            }
        }

        var trans = GetComponent<Transform>();
        m_expandedMesh.bounds = m_bounds;
        m_countRate = Mathf.Max(m_countRate, 0.0f);
        instanceCount = Mathf.Min((int)(instanceCount * m_countRate), (int)(maxInstances * m_countRate));
        int batch_count = ceildiv(instanceCount, m_instancesPerBatch);

        // clone materials if needed
        for (int i = 0; i < m_actualMaterials.Count; ++i)
        {
            var a = m_actualMaterials[i];
            while (a.Count < batch_count)
            {
                Material m = CloneMaterial(m_materials[i], a.Count);
                a.Add(m);
            }
        }

        // update materials
        var worldToLocalMatrix = trans.localToWorldMatrix;
        ForEachMaterials((m) =>
        {
            m.SetInt("_NumInstances", instanceCount);
            m.SetVector("_CountRate", new Vector4(m_countRate, 1.0f / m_countRate, 0.0f, 0.0f));
            m.SetVector("_ModelScale", m_modelScale);
            m.SetVector("_TransScale", m_transScale);
            m.SetMatrix("_Transform", worldToLocalMatrix);
        });

        // issue draw calls
        int layer = gameObject.layer;
        Matrix4x4 matrix = Matrix4x4.identity;
        m_actualMaterials.ForEach(a =>
        {
            for (int i = 0; i < batch_count; ++i)
            {
                Graphics.DrawMesh(m_expandedMesh, matrix, a[i], layer, null, 0, null, m_castShadow, m_receiveShadows);
            }
        });
    }

    void ReleaseGPUResoureces()
    {
        if (m_actualMaterials != null)
        {
            m_actualMaterials.ForEach(a => { a.Clear(); });
        }

        if (m_texPositions != null)
        {
            m_texPositions.Release();
            m_texPositions = null;
        }

        if (m_texIDs != null)
        {
            m_texIDs.Release();
            m_texIDs = null;
        }

        m_bounds = new Bounds();
    }

    void OnDisable()
    {
        ReleaseGPUResoureces();
    }

    void LateUpdate()
    {
        Flush();
    }

#if UNITY_EDITOR
    void Reset()
    {
        m_mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/AlembicImporter/Meshes/IcoSphere.asset");
        m_materials = new Material[1] { AssetDatabase.LoadAssetAtPath<Material>("Assets/AlembicImporter/Materials/AlembicPointsDefault.mat") };
        ReleaseGPUResoureces();
    }

    void OnValidate()
    {
        ReleaseGPUResoureces();
    }
    
    void OnDrawGizmos()
    {
        if (m_showBounds)
        {
            Gizmos.matrix = GetComponent<Transform>().localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(m_bounds.center, m_bounds.extents);
        }
    }
#endif
}
