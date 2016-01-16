#include "AlembicImporter.h"
#include "aiLogger.h"
#include "aiContext.h"
#include "aiObject.h"
#include "aiSchema.h"
#include "aiCamera.h"
#include "aiPoints.h"
#include "aiXForm.h"

// ---

aiPointsSample::aiPointsSample(aiPoints *schema)
    : super(schema)
{
}

aiPointsSample::~aiPointsSample()
{
}

void aiPointsSample::updateConfig(const aiConfig &config, bool &topoChanged, bool &dataChanged)
{
    DebugLog("aiPointsSample::updateConfig()");

    topoChanged = false;
    dataChanged = (config.swapHandedness != m_config.swapHandedness);

    m_config = config;
}

int aiPointsSample::getPointsCount() const
{
    return (m_positions->valid() ? (int) m_positions->size() : 0);
}

void aiPointsSample::getSummary(aiPointsSampleSummary &summary)
{
    summary.hasVelocities = m_velocities->valid();
    summary.hasIDs = m_ids->valid();
}

void aiPointsSample::getData(aiPointsSampleData &data)
{
    if (!m_positions->valid() || !data.positions)
    {
        return;
    }
    
    float scl = (m_config.swapHandedness ? -1.0f : 1.0f);
    
    int count = (int) m_positions->size();
    
    data.count = count;
    data.boundsCenter = m_bounds.center();
    data.boundsCenter.x *= scl;
    data.boundsExtents = m_bounds.size();
    
    bool copyVelocities = (data.velocities && m_velocities->valid());
    bool copyIDs = (data.ids && m_ids->valid());
    
    if (data.velocities && !copyVelocities)
    {
        aiLogger::Info("%s: Reset velocities", getSchema()->getObject()->getFullName());
        memset(data.velocities, 0, count * sizeof(Abc::V3f));
    }
    
    if (data.ids && !copyIDs)
    {
        aiLogger::Info("%s: Reset IDs", getSchema()->getObject()->getFullName());
        memset(data.ids, 0, count * sizeof(uint64_t));
    }
    
    if (copyVelocities)
    {
        if (copyIDs)
        {
            for (int i = 0; i < count; ++i)
            {
                data.positions[i] = (*m_positions)[i];
                data.positions[i].x *= scl;
                data.velocities[i] = (*m_velocities)[i];
                data.velocities[i].x *= scl;
                data.ids[i] = (*m_ids)[i];
            }
        }
        else
        {
            for (int i = 0; i < count; ++i)
            {
                data.positions[i] = scl * (*m_positions)[i];
                data.positions[i].x *= scl;
                data.velocities[i] = scl * (*m_velocities)[i];
                data.velocities[i].x *= scl;
            }
        }
    }
    else
    {
        if (copyIDs)
        {
            for (int i = 0; i < count; ++i)
            {
                data.positions[i] = scl * (*m_positions)[i];
                data.positions[i].x *= scl;
                data.ids[i] = (*m_ids)[i];
            }
        }
        else
        {
            for (int i = 0; i < count; ++i)
            {
                data.positions[i] = scl * (*m_positions)[i];
                data.positions[i].x *= scl;
            }
        }
    }
}

void aiPointsSample::getRawData(aiPointsSampleData &data)
{
    if (!m_positions->valid())
    {
        return;
    }
    
    data.count = (int) m_positions->size();
    data.positions = (abcV3*) m_positions->get();
    data.boundsCenter = m_bounds.center();
    data.boundsExtents = m_bounds.size();
    
    if (m_velocities && m_velocities->size() == m_positions->size())
    {
        data.velocities = (abcV3*) m_velocities->get();
    }

    if (m_ids && m_ids->size() == m_positions->size())
    {
        data.ids = (uint64_t*) m_ids->get();
    }
}

// ---

aiPoints::aiPoints(aiObject *obj)
    : super(obj)
    , m_peakVertexCount(0)
{

}

aiPoints::Sample* aiPoints::newSample()
{
    Sample *sample = getSample();

    if (!sample)
    {
        sample = new Sample(this);
    }

    return sample;
}

aiPoints::Sample* aiPoints::readSample(float time, bool &topologyChanged)
{
    DebugLog("aiPoints::readSample(t=%f)", time);

    Sample *ret = newSample();
    auto ss = MakeSampleSelector(time);

    // read positions
    m_schema.getPositionsProperty().get(ret->m_positions, ss);

    // read velocities
    ret->m_velocities.reset();
    auto velocitiesProp = m_schema.getVelocitiesProperty();
    if (velocitiesProp.valid())
    {
        DebugLog("  Read velocities");
        velocitiesProp.get(ret->m_velocities, ss);
    }

    // read IDs
    ret->m_ids.reset();
    auto idProp = m_schema.getIdsProperty();
    if (idProp.valid())
    {
        DebugLog("  Read IDs");
        idProp.get(ret->m_ids, ss);
    }
    
    // read bounding box
    auto boundsProp = m_schema.getSelfBoundsProperty();
    if (boundsProp.valid())
    {
        DebugLog("  Read bounds");
        boundsProp.get(ret->m_bounds, ss);
    }

    return ret;
}

int aiPoints::getPeakVertexCount() const
{
    if (m_peakVertexCount == 0)
    {
        DebugLog("aiPoints::getPeakVertexCount()");

        Util::Dimensions dim;

        auto positionsProp = m_schema.getPositionsProperty();

        int numSamples = (int) positionsProp.getNumSamples();

        if (numSamples == 0)
        {
            return 0;
        }
        else if (positionsProp.isConstant())
        {
            positionsProp.getDimensions(dim, Abc::ISampleSelector(int64_t(0)));

            m_peakVertexCount = (int) dim.numPoints();
        }
        else
        {
            m_peakVertexCount = 0;

            for (int i = 0; i < numSamples; ++i)
            {
                positionsProp.getDimensions(dim, Abc::ISampleSelector(int64_t(i)));

                size_t numVertices = dim.numPoints();

                if (numVertices > size_t(m_peakVertexCount))
                {
                    m_peakVertexCount = int(numVertices);
                }
            }
        }
    }

    return m_peakVertexCount;
}
