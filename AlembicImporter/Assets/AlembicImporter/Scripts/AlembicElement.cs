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
public abstract class AlembicElement : MonoBehaviour
{
    public AlembicStream m_abcStream;
    public AbcAPI.aiObject m_abcObj;
    public AbcAPI.aiSchema m_abcSchema;
    public GCHandle m_thisHandle;
    
    protected Transform m_trans;
    protected bool m_instance;
    protected AbcAPI.aiObject m_abcSource;

    bool m_verbose;
    bool m_pendingUpdate;


    static void ConfigCallback(IntPtr __this, ref AbcAPI.aiConfig config)
    {
        GCHandle gch = GCHandle.FromIntPtr(__this);
        if (gch.IsAllocated)
        {
            var _this = gch.Target as AlembicElement;
            _this.AbcGetConfig(ref config);
        }
    }

    static void SampleCallback(IntPtr __this, AbcAPI.aiSample sample, bool topologyChanged)
    {
        GCHandle gch = GCHandle.FromIntPtr(__this);
        if (gch.IsAllocated)
        {
            var _this = gch.Target as AlembicElement;
            _this.AbcSampleUpdated(sample, topologyChanged);
        }
    }
    
    static void DestroyCallback(IntPtr __this)
    {
        GCHandle gch = GCHandle.FromIntPtr(__this);
        if (gch.IsAllocated)
        {
            var _this = gch.Target as AlembicElement;
            _this.AbcInvalidate(true);
        }
    }


    public T GetOrAddComponent<T>() where T : Component
    {
        var c = gameObject.GetComponent<T>();
        if (c == null)
        {
            c = gameObject.AddComponent<T>();
        }
        return c;
    }

    public virtual void OnDestroy()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (m_abcStream != null)
                {
                    m_abcStream.AbcDestroyElement(this);
                    return;
                }
            }
#else
            if (m_abcStream != null)
            {
                m_abcStream.AbcDestroyElement(this);
                return;
            }
#endif
        }
        
        AbcInvalidate(false);
    }
    
    void Update()
    {
        AbcUpdate();
    }

    protected void ResetInstance()
    {
        m_instance = false;
        m_abcSource.ptr = (System.IntPtr)0;
    }

    protected void AbcBaseSetup(AlembicStream abcStream,
                                AbcAPI.aiObject abcObj,
                                AbcAPI.aiSchema abcSchema)
    {
        m_abcStream = abcStream;
        m_abcObj = abcObj;
        m_abcSchema = abcSchema;
        m_trans = GetComponent<Transform>();
        m_instance = AbcAPI.aiIsInstance(abcObj);

        if (m_instance)
        {
            m_abcSource = AbcAPI.aiGetInstanceSource(abcObj);
            
            if (m_abcSource.ptr == (System.IntPtr)0)
            {
                Debug.LogWarning("Cannot find abc instance source for \"" + AbcAPI.aiGetFullName(m_abcObj) + "\"");
                m_instance = false;
            }
        }
        else
        {
            m_abcSource.ptr = (System.IntPtr)0;
        }
    }
    
    protected void AbcCallbackSetup(AbcAPI.aiObject abcObj,
                                    AbcAPI.aiSchema abcSchema)
    {
        if (!m_thisHandle.IsAllocated)
        {
            m_thisHandle = GCHandle.Alloc(this);
        }

        IntPtr ptr = GCHandle.ToIntPtr(m_thisHandle);

        AbcAPI.aiSetDestroyCallback(abcObj, DestroyCallback, ptr);
        AbcAPI.aiSchemaSetConfigCallback(abcSchema, ConfigCallback, ptr);
        AbcAPI.aiSchemaSetSampleCallback(abcSchema, SampleCallback, ptr);
    }

    public virtual void AbcSetup(AlembicStream abcStream,
                                 AbcAPI.aiObject abcObj,
                                 AbcAPI.aiSchema abcSchema)
    {
        AbcBaseSetup(abcStream, abcObj, abcSchema);
        AbcCallbackSetup(abcObj, abcSchema);
    }

    public virtual void AbcInvalidate(bool abcObjDeleted)
    {
        if (m_abcObj.ptr != (System.IntPtr)0)
        {
            if (!abcObjDeleted)
            {
                AbcAPI.aiSetDestroyCallback(m_abcObj, null, (System.IntPtr)0);
            }
            m_abcObj.ptr = (System.IntPtr)0;
        }

        if (m_abcSchema.ptr != (System.IntPtr)0)
        {
            if (!abcObjDeleted)
            {
                AbcAPI.aiSchemaSetConfigCallback(m_abcSchema, null, (System.IntPtr)0);
                AbcAPI.aiSchemaSetSampleCallback(m_abcSchema, null, (System.IntPtr)0);
            }
            m_abcSchema.ptr = (System.IntPtr)0;
        }
        
        if (m_abcSource.ptr != (System.IntPtr)0)
        {
            m_abcSource.ptr = (System.IntPtr)0;
        }

        if (m_thisHandle.IsAllocated)
        {
            m_thisHandle.Free();
        }
        
        m_instance = false;
        m_abcStream = null;
    }

    public bool AbcIsValid()
    {
        // Is only checking m_abcSchema sufficient? (should)
        return (m_abcSchema.ptr != (System.IntPtr)0);
    }

    public AbcAPI.aiSample AbcGetSample()
    {
        return AbcGetSample((m_abcStream != null ? m_abcStream.m_time : 0.0f));
    }

    public AbcAPI.aiSample AbcGetSample(float time)
    {
        return AbcAPI.aiSchemaGetSample(m_abcSchema, time);
    }

    // Called by loading thread (not necessarily the main thread)
    public virtual void AbcGetConfig(ref AbcAPI.aiConfig config)
    {
        // Overrides aiConfig options here if needed
    }

    // Called by loading thread (not necessarily the main thread)
    public abstract void AbcSampleUpdated(AbcAPI.aiSample sample, bool topologyChanged);

    // Called in main thread
    public abstract void AbcUpdate();

    protected void AbcVerboseLog(string msg)
    {
        if (m_abcStream != null && m_abcStream.m_verbose)
        {
            Debug.Log(msg);
        }
    }

    protected void AbcDirty()
    {
        m_pendingUpdate = true;
    }

    protected void AbcClean()
    {
        m_pendingUpdate = false;
    }

    protected bool AbcIsDirty()
    {
        return m_pendingUpdate;
    }
}
