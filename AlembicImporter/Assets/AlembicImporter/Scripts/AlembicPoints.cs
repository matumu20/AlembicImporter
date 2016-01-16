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
public class AlembicPoints : AlembicElement
{
    AbcAPI.aiMeshSampleSummary m_summary;
    AbcAPI.aiPointsSampleData m_abcData;
    Vector3[] m_abcPositions;
    Int64[] m_abcIDs;
    Vector3[] m_abcVelocities;
    int m_abcPeakVertexCount;

    // properties
    public AbcAPI.aiPointsSampleData abcData { get { return m_abcData; } }
    public Vector3[] abcPositions { get { return m_abcPositions; } }
    public Vector3[] abcVelocities { get { return m_abcVelocities; } }
    public Int64[] abcIDs { get { return m_abcIDs; } }
    public int abcPeakVertexCount
    {
       get {
           if (m_abcPeakVertexCount == 0)
           {
               m_abcPeakVertexCount = AbcAPI.aiPointsGetPeakVertexCount(m_abcSchema);
           }
           return m_abcPeakVertexCount;
       }
    }

    public override void AbcSampleUpdated(AbcAPI.aiSample sample, bool topologyChanged)
    {
        int pointCount = AbcAPI.aiPointsGetCount(sample);
        
        if (pointCount > 0)
        {
            if (m_abcPositions == null)
            {
                m_abcPositions = new Vector3[pointCount];
            }
            else
            {
                Array.Resize(ref m_abcPositions, pointCount);
            }
            
            AbcAPI.aiPointsGetSampleSummary(sample, ref m_summary);
            
            if (m_summary.hasVelocities)
            {
                if (m_abcVelocities == null)
                {
                    m_abcVelocities = new Vector3[pointCount];
                }
                else
                {
                    Array.Resize(ref m_abcVelocities, pointCount);
                }
                m_abcData.velocities = Marshal.UnsafeAddrOfPinnedArrayElement(m_abcVelocities, 0);
            }
            else
            {
                if (m_abcVelocities)
                {
                    Array.Resize(ref m_abcVelocities, 0);
                }
                m_abcData.velocities = (IntPtr)0;
            }
            
            if (m_summary.hasIDs)
            {
                if (m_abcIDs == null)
                {
                    m_abcIDs = new Int64[pointCount];
                }
                else
                {
                    Array.Resize(ref m_abcIDs, pointCount);
                }
                m_abcData.ids = Marshal.UnsafeAddrOfPinnedArrayElement(m_abcIDs, 0);
            }
            else
            {
                if (m_abcIDs)
                {
                    Array.Resize(ref m_abcIDs, 0);
                }
                m_abcData.ids = (IntPtr)0;
            }
        }
        else
        {
            if (m_abcPositions != null)
            {
                Array.Resize(ref m_abcPositions, 0);
            }
            
            if (m_abcVelocities != null)
            {
                Array.Resize(ref m_abcVelocities, 0);
            }
            
            if (m_abcIDs != null)
            {
                Array.Resize(ref m_abcIDs, 0);
            }
            
            m_abcData.positions = (IntPtr)0;
            m_abcData.velocities = (IntPtr)0;
            m_abcData.ids = (IntPtr)0;
        }
        
        AbcAPI.aiPointsGetData(sample, ref m_abcData);
        
        AbcDirty();
    }

    public override void AbcUpdate()
    {
        if (AbcIsDirty())
        {
            // nothing to do in this component.
            AbcClean();
        }
    }

    void Reset()
    {
        // Add renderer
        var c = gameObject.GetComponent<AlembicPointsRenderer>();
        
        if (c == null)
        {
            c = gameObject.AddComponent<AlembicPointsRenderer>();
        }
    }
}
