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
public class AlembicXForm : AlembicElement
{
    AbcAPI.aiXFormData m_abcData;
    bool m_freshSetup = false;

    public override void AbcSetup(AlembicStream abcStream,
                                  AbcAPI.aiObject abcObj,
                                  AbcAPI.aiSchema abcSchema)
    {
        base.AbcSetup(abcStream, abcObj, abcSchema);
        
        m_freshSetup = true;
    }

    public override void AbcGetConfig(ref AbcAPI.aiConfig config)
    {
        if (!AbcIsValid())
        {
            return;
        }
        
        config.forceUpdate = m_freshSetup;
    }
    
    public override void AbcSampleUpdated(AbcAPI.aiSample sample, bool topologyChanged)
    {
        if (!AbcIsValid())
        {
            return;
        }
        
        AbcAPI.aiXFormGetData(sample, ref m_abcData);
        
        m_freshSetup = false;
        
        AbcDirty();
    }

    public override void AbcUpdate()
    {
        if (AbcIsValid() && AbcIsDirty())
        {
            if (m_abcData.inherits)
            {
                m_trans.localPosition = m_abcData.translation;
                m_trans.localRotation = m_abcData.rotation;
                m_trans.localScale = m_abcData.scale;
            }
            else
            {
                m_trans.position = m_abcData.translation;
                m_trans.rotation = m_abcData.rotation;
                m_trans.localScale = m_abcData.scale;
            }

            AbcClean();
        }
    }
}
