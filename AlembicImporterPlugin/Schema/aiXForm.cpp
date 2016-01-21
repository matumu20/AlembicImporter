#include "pch.h"
#include "AlembicImporter.h"
#include "aiLogger.h"
#include "aiContext.h"
#include "aiObject.h"
#include "aiSchema.h"
#include "aiCamera.h"
#include "aiPolyMesh.h"
#include "aiXForm.h"


aiXFormSample::aiXFormSample(aiXForm *schema)
    : super(schema)
{
}

void aiXFormSample::updateConfig(const aiConfig &config, bool &topoChanged, bool &dataChanged)
{
    DebugLog("aiXFormSample::updateConfig()");
    
    topoChanged = false;
    dataChanged = (config.swapHandedness != m_config.swapHandedness);
    m_config = config;
}

void aiXFormSample::getData(aiXFormData &outData) const
{
    DebugLog("aiXFormSample::getData()");
    
    Abc::M44f M(m_sample.getMatrix());
    Imath::V3f S(1.0f, 1.0f, 1.0f);
    Imath::V3f Sh(0.0f, 0.0f, 0.0f); // Shear (ignored)
    
    Imath::extractAndRemoveScalingAndShear(M, S, Sh, false);
    
    Imath::V3f T = M.translation();
    Imath::Quatf Q = Imath::extractQuat(M);
    Imath::V3f Raxis = Q.axis();
    float Rangle = Q.angle();
    
    if (m_config.swapHandedness)
    {
        T.x *= -1.0f;
        Rangle *= -1.0f;
        Raxis.x *= -1.0f;
        Q.setAxisAngle(Raxis, Rangle);
    }
    
    outData.inherits = m_sample.getInheritsXforms();
    outData.translation = T;
    outData.rotation = abcV4(Q.v.x, Q.v.y, Q.v.z, Q.r);
    outData.scale = S;
}



aiXForm::aiXForm(aiObject *obj)
    : super(obj)
{
}

aiXForm::Sample* aiXForm::newSample()
{
    Sample *sample = getSample();
    
    if (!sample)
    {
        sample = new Sample(this);
    }
    
    return sample;
}

aiXForm::Sample* aiXForm::readSample(float time, bool &topologyChanged)
{
    DebugLog("aiXForm::readSample(t=%f)", time);
    
    Sample *ret = newSample();

    m_schema.get(ret->m_sample, MakeSampleSelector(time));

    topologyChanged = false;

    return ret;
}

