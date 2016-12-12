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
public class AlembicMaterial : MonoBehaviour
{
    public class Assignment
    {
        public Material material;
        public List<int> faces;
    }

    [Serializable]
    public class Facesets
    {
        public int[] faceCounts;
        public int[] faceIndices;
    }

    bool dirty = false;
    List<Material> materials = new List<Material>();
    // Need to keep those around when scene is serialized
    [HideInInspector] public Facesets facesetsCache = new Facesets { faceCounts = new int[0], faceIndices = new int[0] };
    
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
    
    void Update()
    {
        if (materials.Count > 0)
        {
            AlembicMesh abcmesh = gameObject.GetComponent<AlembicMesh>();

            if (abcmesh != null)
            {
                int splitIndex = 0;
                int submeshIndex = 0;
                int materialIndex = 0;

                if (abcmesh.GetSubMeshCount() < materials.Count)
                {
                    // should have at least materials.Count submeshes
                    Debug.LogWarning("\"" + GetFullpath(transform) + "\": Not enough submeshes for all assigned materials. (" + materials.Count + " material(s) for " + abcmesh.GetSubMeshCount() + " submesh(es))");
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
                        if (!SameFacesets(facesetsCache, mmat.facesetsCache))
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

                    Transform split = gameObject.transform.FindChild(gameObject.name + "_split_" + submesh.splitIndex);

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
                        else if (materialIndex >= materials.Count)
                        {
                            materialIndex = materials.Count - 1;
                        }

                        if (materialIndex != submesh.facesetIndex)
                        {
                            Debug.LogWarning("\"" + GetFullpath(transform) + "\": Invalid faceset index " + submesh.facesetIndex + ". Use material " + materialIndex + " instead (" + materials.Count + " material(s))");
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

                        Material material = (materialIndex < 0 ? null : materials[materialIndex]);

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

            materials.Clear();
        }
    }

    public int GetFacesetsCount()
    {
        return facesetsCache.faceCounts.Length;
    }

    public void GetFacesets(ref AbcAPI.aiFacesets facesets)
    {
        facesets.count = facesetsCache.faceCounts.Length;
        facesets.faceCounts = Marshal.UnsafeAddrOfPinnedArrayElement(facesetsCache.faceCounts, 0);
        facesets.faceIndices = Marshal.UnsafeAddrOfPinnedArrayElement(facesetsCache.faceIndices, 0);
    }

    public bool HasFacesetsChanged()
    {
        return dirty;
    }

    public void AknowledgeFacesetsChanges()
    {
        dirty = false;
    }

    public void UpdateAssignments(List<Assignment> assignments)
    {
        int count = 0;
        int indicesCount = 0;

        // keep list of materials for next update
        materials.Clear();

        if (facesetsCache.faceCounts.Length < assignments.Count)
        {
            Array.Resize(ref facesetsCache.faceCounts, assignments.Count);
            dirty = true;
        }

        for (int i=0; i<assignments.Count; ++i)
        {
            materials.Add(assignments[i].material);

            int face_count = assignments[i].faces.Count;

            dirty = dirty || (facesetsCache.faceCounts[count] != face_count);
            
            facesetsCache.faceCounts[count++] = face_count;

            if (facesetsCache.faceIndices.Length < (indicesCount + face_count))
            {
                Array.Resize(ref facesetsCache.faceIndices, indicesCount + face_count);
                dirty = true;
            }

            for (int j=0; j<face_count; ++j, ++indicesCount)
            {
                int face_index = assignments[i].faces[j];

                dirty = dirty || (facesetsCache.faceIndices[indicesCount] != face_index);
                
                facesetsCache.faceIndices[indicesCount] = face_index;
            }
        }
    }

#if UNITY_EDITOR

    static char[] FaceSep = new char[1] { ',' };
    static char[] RangeSep = new char[1] { '-' };
    
    static Material GetMaterial(string name, string matfolder)
    {
        // FindAssets will return all shaders that contains name in a case insensitive way
        string[] guids = AssetDatabase.FindAssets(name + " t:material", new string[1] { matfolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            Material mat = AssetDatabase.LoadAssetAtPath(path, typeof(Material)) as Material;
            
            if (mat.name == name)
            {
                return mat;
            }
        }
        
        return null;
    }

    public static void Import(string xmlPath, GameObject root, string materialFolder)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(xmlPath);
        
        XmlNode xmlRoot = doc.DocumentElement;
        
        XmlNodeList nodes = xmlRoot.SelectNodes("/assignments/node");

        Dictionary<GameObject, List<AlembicMaterial.Assignment> > allAssignments = new Dictionary<GameObject, List<AlembicMaterial.Assignment> >();

        foreach (XmlNode node in nodes)
        {
            string path = node.Attributes["path"].Value;

            XmlNodeList shaders = node.SelectNodes("shader");

            foreach (XmlNode shader in shaders)
            {
                XmlAttribute name = shader.Attributes["surface"];
                XmlAttribute inst = shader.Attributes["instance"];

                if (name == null)
                {
                    name = shader.Attributes["name"];
                    if (name == null)
                    {
                        continue;
                    }
                }

                int instNum = (inst == null ? 0 : Convert.ToInt32(inst.Value));

                GameObject target = AbcUtils.FindNode(root, path, instNum, typeof(AlembicMesh));
                
                if (target == null)
                {
                    // Debug.Log("Could not find node: " + path);
                    continue;
                }

                List<AlembicMaterial.Assignment> assignments;

                if (!allAssignments.ContainsKey(target))
                {
                    assignments = new List<AlembicMaterial.Assignment>();
                    allAssignments.Add(target, assignments);
                }
                else
                {
                    assignments = allAssignments[target];
                }

                Material material = GetMaterial(name.Value, materialFolder);

                if (material == null)
                {
                    material = new Material(Shader.Find("Standard"));

                    material.color = new Color(UnityEngine.Random.value,
                                               UnityEngine.Random.value,
                                               UnityEngine.Random.value);
                    
                    AssetDatabase.CreateAsset(material, materialFolder + "/" + name.Value + ".mat");
                }

                // Get or create material assignment
                bool newlyAssigned = false;
                AlembicMaterial.Assignment a = null;
                
                if (a == null)
                {
                    a = new AlembicMaterial.Assignment();
                    a.material = material;
                    a.faces = new List<int>();

                    assignments.Add(a);

                    newlyAssigned = true;
                }

                string faceset = shader.InnerText;
                faceset.Trim();

                if (faceset.Length > 0 && (newlyAssigned || a.faces.Count > 0))
                {
                    string[] items = faceset.Split(FaceSep, StringSplitOptions.RemoveEmptyEntries);

                    for (int i=0; i<items.Length; ++i)
                    {
                        string[] rng = items[i].Split(RangeSep, StringSplitOptions.RemoveEmptyEntries);

                        if (rng.Length == 1)
                        {
                            a.faces.Add(Convert.ToInt32(rng[0]));
                        }
                        else if (rng.Length == 2)
                        {
                            int j0 = Convert.ToInt32(rng[0]);
                            int j1 = Convert.ToInt32(rng[1]);

                            for (int j=j0; j<=j1; ++j)
                            {
                                a.faces.Add(j);
                            }
                        }
                    }

                    if (!newlyAssigned)
                    {
                        a.faces = new List<int>(new HashSet<int>(a.faces));
                    }
                }
                else if (faceset.Length == 0 && a.faces.Count > 0)
                {
                    // Shader assgined to whole object, remove any face level assignments
                    a.faces.Clear();
                }
            }
        }

        // Update AlembicMaterial components
        foreach (KeyValuePair<GameObject, List<AlembicMaterial.Assignment> > pair in allAssignments)
        {
            AlembicMaterial abcmaterial = pair.Key.GetComponent<AlembicMaterial>();

            if (abcmaterial == null)
            {
                abcmaterial = pair.Key.AddComponent<AlembicMaterial>();
            }

            abcmaterial.UpdateAssignments(pair.Value);
        }

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
