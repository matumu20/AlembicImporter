#ifndef aiCamera_h
#define aiCamera_h

class aiCameraSample : public aiSampleBase
{
typedef aiSampleBase super;
public:
    aiCameraSample(aiCamera *schema, float time);
    
    void updateConfig(const aiConfig &config, bool &topoChanged, bool &dataChanged) override;

    void getData(aiCameraData &outData);
    
public:
    AbcGeom::CameraSample m_sample;
};


struct aiCameraTraits
{
    typedef aiCameraSample SampleT;
    typedef AbcGeom::ICameraSchema AbcSchemaT;
};


class aiCamera : public aiTSchema<aiCameraTraits>
{
typedef aiTSchema<aiCameraTraits> super;
public:
    aiCamera(aiObject *obj);

    Sample* readSample(float time) override;
};

#endif
