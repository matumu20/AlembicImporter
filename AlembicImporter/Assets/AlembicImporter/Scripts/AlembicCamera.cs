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
public class AlembicCamera : AlembicElement
{
    public AbcAPI.aiAspectRatioModeOverride m_aspectRatioMode = AbcAPI.aiAspectRatioModeOverride.InheritStreamSetting;
    public bool m_ignoreClippingPlanes = false;

    Camera m_camera;
    AbcAPI.aiCameraData m_abcData;
    bool m_lastIgnoreClippingPlanes = false;
#if UNITY_EDITOR
    AbcAPI.aiAspectRatioModeOverride m_lastAspectRatioMode = AbcAPI.aiAspectRatioModeOverride.InheritStreamSetting;
#endif
    
    static Vector3 RotY180 = new Vector3(0, 180, 0);

    public override void AbcSetup(AlembicStream abcStream,
                                  AbcAPI.aiObject abcObj,
                                  AbcAPI.aiSchema abcSchema)
    {
        base.AbcSetup(abcStream, abcObj, abcSchema);

        m_camera = GetOrAddComponent<Camera>();
    }

    public override void AbcGetConfig(ref AbcAPI.aiConfig config)
    {
        if (!AbcIsValid())
        {
            return;
        }
        
        if (m_aspectRatioMode != AbcAPI.aiAspectRatioModeOverride.InheritStreamSetting)
        {
            config.aspectRatio = AbcAPI.GetAspectRatio((AbcAPI.aiAspectRatioMode) m_aspectRatioMode);
        }
    }

    public override void AbcSampleUpdated(AbcAPI.aiSample sample, bool topologyChanged)
    {
        if (!AbcIsValid())
        {
            return;
        }
        
        AbcAPI.aiCameraGetData(sample, ref m_abcData);

        AbcDirty();
    }

    public override void AbcUpdate()
    {
        if (AbcIsValid())
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && m_aspectRatioMode != m_lastAspectRatioMode)
            {
                m_abcStream.m_forceRefresh = true;
                
                EditorUtility.SetDirty(m_abcStream.gameObject);
                
                m_lastAspectRatioMode = m_aspectRatioMode;
            }
#endif

            if (AbcIsDirty() || m_lastIgnoreClippingPlanes != m_ignoreClippingPlanes)
        {
                // m_trans.forward = -m_trans.parent.forward;
                // => This seems to be doing some weirdness
                // 
                // => rather assume identity on transform of nodes with AlembicCamera component
                m_trans.localPosition = Vector3.zero;
                m_trans.localEulerAngles = RotY180;
                m_trans.localScale = Vector3.one;
                
            m_camera.fieldOfView = m_abcData.fieldOfView;

            if (!m_ignoreClippingPlanes)
            {
                m_camera.nearClipPlane = m_abcData.nearClippingPlane;
                m_camera.farClipPlane = m_abcData.farClippingPlane;
            }
            
            // no use for focusDistance and focalLength yet (could be usefull for DoF component)
            
            AbcClean();

            m_lastIgnoreClippingPlanes = m_ignoreClippingPlanes;
        }
    }
}
}
