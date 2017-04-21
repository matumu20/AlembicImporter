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
public class AlembicStream : MonoBehaviour
{
    public enum CycleType { Hold, Loop, Reverse, Bounce, Clip };

    [Header("Abc")]
    public string m_pathToAbc = "";
    public float m_time = 0.0f;

    [Header("Playback")]
    public float m_startTime = 0.0f;
    public float m_endTime = 0.0f;
    public float m_timeOffset = 0.0f;
    public float m_timeScale = 1.0f;
    public bool m_preserveStartTime = true;
    public CycleType m_cycle = CycleType.Hold;

    [Header("Data")]
    public bool m_swapHandedness = false;
    public bool m_swapFaceWinding = false;
    public bool m_submeshPerUVTile = true;
    public AbcAPI.aiNormalsMode m_normalsMode = AbcAPI.aiNormalsMode.ComputeIfMissing;
    public AbcAPI.aiTangentsMode m_tangentsMode = AbcAPI.aiTangentsMode.None;
    public AbcAPI.aiAspectRatioMode m_aspectRatioMode = AbcAPI.aiAspectRatioMode.CurrentResolution;

    [Header("Diagnostic")]
    public bool m_verbose = false;
    public bool m_logToFile = false;
    public string m_logPath = "";
    
    [Header("Advanced")]
    public bool m_useThreads = false;
    public int m_sampleCacheSize = 0;
    public bool m_forceRefresh = false;

    [HideInInspector] public AbcAPI.aiConfig m_config;

    bool m_loaded;
    float m_lastAbcTime;
    bool m_lastSwapHandedness;
    bool m_lastSwapFaceWinding;
    bool m_lastSubmeshPerUVTile; 
    AbcAPI.aiNormalsMode m_lastNormalsMode;
    AbcAPI.aiTangentsMode m_lastTangentsMode;
    bool m_lastIgnoreMissingNodes;
    float m_lastAspectRatio = -1.0f;
    bool m_lastLogToFile = false;
    string m_lastLogPath = "";
    float m_timeEps = 0.001f;
    AbcAPI.aiContext m_abc;
    Transform m_trans;
    string m_lastPathToAbc;

    [HideInInspector] public bool m_clip = false;

    // --- For internal use ---

    bool AbcIsValid()
    {
        return (m_abc.ptr != (IntPtr)0);
    }

    void AbcSyncConfig()
    {
        m_config.swapHandedness = m_swapHandedness;
        m_config.swapFaceWinding = m_swapFaceWinding;
        m_config.normalsMode = m_normalsMode;
        m_config.tangentsMode = m_tangentsMode;
        m_config.cacheTangentsSplits = true;
        m_config.aspectRatio = AbcAPI.GetAspectRatio(m_aspectRatioMode);
        m_config.forceUpdate = false; // m_forceRefresh; ?
        m_config.useThreads = m_useThreads;
        m_config.cacheSamples = m_sampleCacheSize;
        m_config.submeshPerUVTile = m_submeshPerUVTile;

        if (AbcIsValid())
        {
            AbcAPI.aiSetConfig(m_abc, ref m_config);
        }
    }

    bool AbcTimeIsClipped(float inTime)
    {
        if (m_cycle == CycleType.Clip)
        {
            float outTime = m_timeScale * (inTime - m_timeOffset) - (m_preserveStartTime ? (m_startTime * (m_timeScale - 1.0f)) : 0.0f);
            return (outTime < (m_startTime - m_timeEps) || (m_endTime + m_timeEps) < outTime);
        }
        else
        {
            return false;
        }
    }

    float AbcTime(float inTime)
    {
        float extraOffset = 0.0f;

        // compute extra time offset to counter-balance effect of m_timeScale on m_startTime
        if (m_preserveStartTime)
        {
            extraOffset = m_startTime * (m_timeScale - 1.0f);
        }

        float playTime = m_endTime - m_startTime;

        // apply speed and offset
        float outTime = m_timeScale * (inTime - m_timeOffset) - extraOffset;

        if (m_cycle == CycleType.Hold || m_cycle == CycleType.Clip)
        {
            if (outTime < (m_startTime - m_timeEps))
            {
                outTime = m_startTime;
            }
            else if (outTime > (m_endTime + m_timeEps))
            {
                outTime = m_endTime;
            }
        }
        else
        {
            float normalizedTime = (outTime - m_startTime) / playTime;
            float playRepeat = (float)Math.Floor(normalizedTime);
            float fraction = Math.Abs(normalizedTime - playRepeat);
            
            if (m_cycle == CycleType.Reverse)
            {
                if (outTime > (m_startTime + m_timeEps) && outTime < (m_endTime - m_timeEps))
                {
                    // inside alembic sample range
                    outTime = m_endTime - fraction * playTime;
                }
                else if (outTime < (m_startTime + m_timeEps))
                {
                    outTime = m_endTime;
                }
                else
                {
                    outTime = m_startTime;
                }
            }
            else
            {
                if (outTime < (m_startTime - m_timeEps) || outTime > (m_endTime + m_timeEps))
                {
                    // outside alembic sample range
                    if (m_cycle == CycleType.Loop || ((int)playRepeat % 2) == 0)
                    {
                        outTime = m_startTime + fraction * playTime;
                    }
                    else
                    {
                        outTime = m_endTime - fraction * playTime;
                    }
                }
            }
        }

        return outTime;
    }

    bool AbcUpdateRequired(float abcTime, float aspectRatio)
    {
        if (m_forceRefresh || 
            m_swapHandedness != m_lastSwapHandedness ||
            m_swapFaceWinding != m_lastSwapFaceWinding ||
            m_submeshPerUVTile != m_lastSubmeshPerUVTile ||
            m_normalsMode != m_lastNormalsMode ||
            m_tangentsMode != m_lastTangentsMode ||
            Math.Abs(abcTime - m_lastAbcTime) > m_timeEps ||
            aspectRatio != m_lastAspectRatio ||
            m_pathToAbc != m_lastPathToAbc)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    void AbcSetLastUpdateState(float abcTime, float aspectRatio)
    {
        m_lastAbcTime = abcTime;
        m_lastSwapHandedness = m_swapHandedness;
        m_lastSwapFaceWinding = m_swapFaceWinding;
        m_lastSubmeshPerUVTile = m_submeshPerUVTile;
        m_lastNormalsMode = m_normalsMode;
        m_lastTangentsMode = m_tangentsMode;
        m_lastAspectRatio = aspectRatio;
        m_forceRefresh = false;
        m_lastPathToAbc = m_pathToAbc;
    }

    int AbcGetElements(ref HashSet<AlembicElement> elements)
    {
        elements.Clear();
        
        // would a single gameObject.GetComponentsInChildren<AlembicElement>(true) work?
        
        foreach (AlembicMesh mesh in gameObject.GetComponentsInChildren<AlembicMesh>(true))
        {
            elements.Add(mesh);
        }
        foreach (AlembicXForm xform in gameObject.GetComponentsInChildren<AlembicXForm>(true))
        {
            elements.Add(xform);
        }
        foreach (AlembicCamera camera in gameObject.GetComponentsInChildren<AlembicCamera>(true))
        {
            elements.Add(camera);
        }
        foreach (AlembicLight light in gameObject.GetComponentsInChildren<AlembicLight>(true))
        {
            elements.Add(light);
        }
        
        return elements.Count;
    }

    void AbcCleanupSubTree(Transform tr, ref HashSet<AlembicElement> elements, ref List<GameObject> objsToDelete)
    {
        // Would a single GetComponent<AlembicElement>() work?
        
        AlembicElement elem = tr.gameObject.GetComponent<AlembicMesh>() as AlembicElement;
        if (elem == null)
        {
            elem = tr.gameObject.GetComponent<AlembicXForm>() as AlembicElement;
            if (elem == null)
            {
                elem = tr.gameObject.GetComponent<AlembicCamera>() as AlembicElement;
                if (elem == null)
                {
                    elem = tr.gameObject.GetComponent<AlembicLight>() as AlembicElement;
                }
            }
        }

        if (elem != null && !elements.Contains(elem))
        {
            if (m_verbose)
            {
                Debug.Log("Alembic.AbcCleanupSubTree: Node \"" + tr.gameObject.name + "\" no longer in alembic tree");
            }
            objsToDelete.Add(tr.gameObject);
        }
        else
        {
            foreach (Transform childTr in tr)
            {
                AbcCleanupSubTree(childTr, ref elements, ref objsToDelete);
            }
        }
    }

    void AbcCleanupTree(ref HashSet<AlembicElement> elements)
    {
        List<GameObject> objsToDelete = new List<GameObject>();
        
        foreach (Transform tr in gameObject.transform)
        {
            AbcCleanupSubTree(tr, ref elements, ref objsToDelete);
        }

        foreach (GameObject obj in objsToDelete)
        {
            GameObject.DestroyImmediate(obj);
        }
    }

    bool AbcRecoverContext()
    {
        if (!AbcIsValid())
        {
            if (m_verbose)
            {
                Debug.Log("AlembicStream.AbcRecoverContext: Try to recover alembic context");
            }

            m_abc = AbcAPI.aiCreateContext(gameObject.GetInstanceID());

            if (AbcIsValid())
            {
                float abcStart = AbcAPI.aiGetStartTime(m_abc);
                float abcEnd = AbcAPI.aiGetEndTime(m_abc);

                if (m_startTime >= m_endTime)
                {
                    m_startTime = abcStart;
                    m_endTime = abcEnd;
                }
                else
                {
                    if (m_startTime < abcStart)
                    {
                        m_startTime = abcStart;
                    }
                    if (m_endTime > abcEnd)
                    {
                        m_endTime = abcEnd;
                    }
                }

                m_timeOffset = -abcStart;
                m_timeScale = 1.0f;
                m_preserveStartTime = true;
                m_forceRefresh = true;
                m_trans = GetComponent<Transform>();
                m_clip = AbcTimeIsClipped(m_time);

                AbcSyncConfig();

                AbcAPI.UpdateAbcTree(m_abc, m_trans, AbcTime(m_time), false);

                if (m_verbose)
                {
                    Debug.Log("AlembicStream.AbcRecoverContext: Succeeded");
                }

                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return true;
        }
    }

    void AbcUpdate(float time)
    {
        if (m_lastLogToFile != m_logToFile ||
            m_lastLogPath != m_logPath)
        {
            AbcAPI.aiEnableFileLog(m_logToFile, m_logPath);

            m_lastLogToFile = m_logToFile;
            m_lastLogPath = m_logPath;
        }
        
        if (!m_loaded && m_pathToAbc != null)
        {
            // We have lost the alembic context, try to recover it
            m_loaded = AbcRecoverContext();
        }

        if (m_loaded)
        {
            if (!AbcIsValid())
            {
                // We have lost the alembic context, try to recover
                m_loaded = AbcRecoverContext();
                if (!m_loaded)
                {
                    Debug.LogWarning("AlembicStream.AbcUpdate: Lost alembic context");
                    
                    return;
                }
            }

            m_time = time;
            m_clip = AbcTimeIsClipped(m_time);

            float abcTime = AbcTime(m_time);
            float aspectRatio = AbcAPI.GetAspectRatio(m_aspectRatioMode);

            if (AbcUpdateRequired(abcTime, aspectRatio))
            {
                if (m_verbose)
                {
                    Debug.Log("AlembicStream.AbcUpdate: t=" + m_time + " (t'=" + abcTime + ")");
                }
                
                if (m_pathToAbc != m_lastPathToAbc)
                {
                    if (m_verbose)
                    {
                        Debug.Log("AlembicStream.AbcUpdate: Path to alembic file changed");
                    }

                    // List all existing AlembicElement components in current tree
                    HashSet<AlembicElement> elements = new HashSet<AlembicElement>();
                    AbcGetElements(ref elements);

                    AbcAPI.aiDestroyContext(m_abc);

                    AbcLoad(true);

                    AbcCleanupTree(ref elements);
                }
                else
                {
                    AbcSyncConfig();

                    AbcAPI.aiUpdateSamples(m_abc, abcTime);
                }   
                
                AbcSetLastUpdateState(abcTime, aspectRatio);
            }
        }
    }

    // --- public api ---
    
    public void AbcDestroyElement(AlembicElement e)
    {
        if (e != null)
        {
            if (m_verbose)
            {
                Debug.Log("AlembicStream.AbcDestroyElement: \"" + e.gameObject.name + "\"");
            }
            AbcAPI.aiDestroyObject(m_abc, e.m_abcObj);
        }
    }
    
    public void AbcLoad(bool createMissingNodes=false)
    {
        if (m_pathToAbc == null)
        {
            return;
        }

        m_trans = GetComponent<Transform>();
        m_abc = AbcAPI.aiCreateContext(gameObject.GetInstanceID());
        m_loaded = AbcAPI.aiLoad(m_abc, Application.streamingAssetsPath + "/" + m_pathToAbc);

        if (m_loaded)
        {
            float abcStart = AbcAPI.aiGetStartTime(m_abc);
            float abcEnd = AbcAPI.aiGetEndTime(m_abc);

            if (m_startTime >= m_endTime)
            {
                m_startTime = abcStart;
                m_endTime = abcEnd;
            }
            else
            {
                if (m_startTime < abcStart)
                {
                    m_startTime = abcStart;
                }
                if (m_endTime > abcEnd)
                {
                    m_endTime = abcEnd;
                }
            }

            if (createMissingNodes)
            {
                m_timeOffset = -abcStart;
                m_timeScale = 1.0f;
                m_preserveStartTime = true;
            }
            m_forceRefresh = true;
            m_clip = AbcTimeIsClipped(m_time);

            AbcSyncConfig();

            AbcAPI.UpdateAbcTree(m_abc, m_trans, AbcTime(m_time), createMissingNodes);
        }
    }

    public bool AbcIsClipped(AbcAPI.aiObject obj)
    {
        if (m_cycle == CycleType.Clip)
        {
            if (!m_clip)
            {
                float s = AbcAPI.aiGetObjectStartTime(obj);
                float e = AbcAPI.aiGetObjectEndTime(obj);
                float t = AbcTime(m_time);
                return (t < (s - m_timeEps) || (e + m_timeEps) < t);
            }
            else
            {
                return true;
            }
        }
        else
        {
            return false;
        }
    }

    // --- method overrides ---

    void OnApplicationQuit()
    {
        AbcAPI.aiCleanup();
    }

    public void Awake()
    {
        AbcLoad();
        AbcAPI.aiEnableFileLog(m_logToFile, m_logPath);
    }

    void OnDestroy()
    {
        if (!AbcIsValid())
        {
            return;
        }

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                AbcAPI.aiDestroyContext(m_abc);
                m_abc = default(AbcAPI.aiContext);
            }
#else
            AbcAPI.aiDestroyContext(m_abc);
            m_abc = default(AbcAPI.aiContext);
#endif
        }
    }

    void Start()
    {
        m_time = 0.0f;
        m_forceRefresh = true;

        AbcSetLastUpdateState(AbcTime(0.0f), AbcAPI.GetAspectRatio(m_aspectRatioMode));
    }

    // In play mode, do the update in 'FixedUpdate' rather than 'Update' method to ensure
    //   precedence of alembic sampling on the actual unity mesh update that happens
    //   in AlembicElement components 'Update' method

    void Update()
    {
        if (!Application.isPlaying)
        {
            AbcUpdate(m_time);
        }
    }

    void FixedUpdate()
    {
        if (Application.isPlaying)
        {
            AbcUpdate(Time.time);
        }
    }
}
