using System;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class AlembicMaterial : Shada
{
    void Start()
    {
    }
    
    string GetFullpath(Transform t)
    {
        if (t == null)
        {
            return "";
        }
        else
        {
            return (GetFullpath(t.parent) + "/" + t.name);
        }
    }
    
    bool SameFacesets(Facesets fs0, Facesets fs1)
    {
        if ((fs0.faceCounts.Length != fs1.faceCounts.Length) ||
            (fs0.faceIndices.Length != fs1.faceIndices.Length))
        {
            return false;
        }
        for (int i=0; i<fs0.faceCounts.Length; ++i)
        {
            if (fs0.faceCounts[i] != fs1.faceCounts[i])
            {
                return false;
            }
        }
        for (int i=0; i<fs0.faceIndices.Length; ++i)
        {
            if (fs0.faceIndices[i] != fs1.faceIndices[i])
            {
                return false;
            }
        }
        return true;
    }
    
    // Override Shada Component Update
    void Update()
    {
        if (m_materials.Count > 0)
        {
            AlembicMesh abcmesh = gameObject.GetComponent<AlembicMesh>();

            if (abcmesh != null)
            {
                int splitIndex = 0;
                int submeshIndex = 0;
                int materialIndex = 0;

                if (abcmesh.GetSubMeshCount() < m_materials.Count)
                {
                    // should have at least m_materials.Count submeshes
                    Debug.LogWarning("\"" + GetFullpath(transform) + "\": Not enough submeshes for all assigned materials. (" + m_materials.Count + " material(s) for " + abcmesh.GetSubMeshCount() + " submesh(es))");
                    return;
                }

                MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

                if (abcmesh.IsInstance())
                {
                    // If facesets are different, submeshing must differ and, as such,
                    // mesh must be be uninstanced
                    AlembicMaterial mmat = abcmesh.GetInstanceSourceMaterial();
                    if (mmat != null)
                    {
                        if (!SameFacesets(m_facesetsCache, mmat.m_facesetsCache))
                        {
                            Debug.LogWarning("\"" + GetFullpath(transform) + "\": Instance facesets differ from source object. Material assignments may lead to unexpected results.");
                        }
                    }
                }

                foreach (AlembicMesh.Submesh submesh in abcmesh.GetSubMeshes())
                {
                    if (submesh.splitIndex != splitIndex)
                    {
                        Debug.Log("\"" + GetFullpath(transform) + "\": Reset submesh index to 0.");
                        submeshIndex = 0;
                    }

                    MeshRenderer splitRenderer = null;

                    #if UNITY_2017_1_OR_NEWER
                        Transform split = gameObject.transform.Find(gameObject.name + "_split_" + submesh.splitIndex);
                    #else
                        Transform split = gameObject.transform.FindChild(gameObject.name + "_split_" + submesh.splitIndex);
                    #endif

                    if (split == null)
                    {
                        if (submesh.splitIndex > 0)
                        {
                            Debug.LogWarning("\"" + GetFullpath(transform) + "\": Invalid split index");
                            return;
                        }

                        splitRenderer = renderer;
                    }
                    else
                    {
                        if (submesh.splitIndex == 0 && !split.gameObject.activeSelf)
                        {
                            // First split sub object not active means the mesh is hold be the current object
                            splitRenderer = renderer;
                        }
                        else
                        {
                            splitRenderer = split.gameObject.GetComponent<MeshRenderer>();
                        }
                    }

                    if (splitRenderer == null)
                    {
                        Debug.LogWarning("\"" + GetFullpath(transform) + "\": No renderer on \"" + gameObject.name + "\" to assign materials to");
                        return;
                    }

                    Material[] assignedMaterials = splitRenderer.sharedMaterials;

                    if (submesh.facesetIndex != -1)
                    {
                        materialIndex = submesh.facesetIndex;

                        // Try to accomodate for invalid values
                        if (materialIndex < 0)
                        {
                            materialIndex = 0;
                        }
                        else if (materialIndex >= m_materials.Count)
                        {
                            materialIndex = m_materials.Count - 1;
                        }

                        if (materialIndex != submesh.facesetIndex)
                        {
                            Debug.LogWarning("\"" + GetFullpath(transform) + "\": Invalid faceset index " + submesh.facesetIndex + ". Use material " + materialIndex + " instead (" + m_materials.Count + " material(s))");
                        }

                        if (submeshIndex >= assignedMaterials.Length)
                        {
                            int oldLen = assignedMaterials.Length;
                            Array.Resize(ref assignedMaterials, submeshIndex + 1);
                            for (int newIdx=oldLen; newIdx<=submeshIndex; ++newIdx)
                            {
                                assignedMaterials[newIdx] = null;
                            }
                        }

                        Material material = (materialIndex < 0 ? null : m_materials[materialIndex]);

                        if (assignedMaterials[submeshIndex] != material)
                        {
                            assignedMaterials[submeshIndex] = material;
                            splitRenderer.sharedMaterials = assignedMaterials;

                            // propagate first split single material assignment to parent renderer if it exists
                            if (submesh.splitIndex == 0 && splitRenderer != renderer && renderer != null && assignedMaterials.Length == 1)
                            {
                                renderer.sharedMaterials = assignedMaterials;
                            }
                        }
                    }
                    else
                    {
                        // should I reset to default material or leave it as it is
                    }

                    splitIndex = submesh.splitIndex;
                    ++submeshIndex;
                }
            }

            m_materials.Clear();
        }
    }

    public void GetFacesets(ref AbcAPI.aiFacesets facesets)
    {
        facesets.count = m_facesetsCache.faceCounts.Length;
        facesets.faceCounts = Marshal.UnsafeAddrOfPinnedArrayElement(m_facesetsCache.faceCounts, 0);
        facesets.faceIndices = Marshal.UnsafeAddrOfPinnedArrayElement(m_facesetsCache.faceIndices, 0);
    }

#if UNITY_EDITOR
    
    public static void Import(string xmlPath, GameObject root, string materialFolder)
    {
        ShadaAPI.Import<AlembicMaterial>(xmlPath, root, materialFolder);

        // Force refresh
        AlembicStream abcstream = root.GetComponent<AlembicStream>();
            
        if (abcstream != null)
        {
            abcstream.m_forceRefresh = true;
            abcstream.SendMessage("AbcUpdate", abcstream.m_time, SendMessageOptions.DontRequireReceiver);
            EditorUtility.SetDirty(root);
        }
    }

    public static void Export(string xmlPath, GameObject root)
    {
        // Not Yet Implemented
        Debug.Log("Material assignment export not yet implemented");
    }
    
#endif
}
