#include "pch.h"
#include "AlembicImporter.h"
#include "aiLogger.h"
#include "aiContext.h"
#include "aiObject.h"
#include "Schema/aiSchema.h"
#include "Schema/aiXForm.h"
#include "Schema/aiPolyMesh.h"
#include "Schema/aiCamera.h"

aiObject::aiObject()
    : m_ctx(0)
    , m_parent(0)
    , m_destroyCb(nullptr)
    , m_destroyCbArg(nullptr)
{
}

aiObject::aiObject(aiContext *ctx, abcObject &abc)
    : m_ctx(ctx)
    , m_abc(abc)
    , m_parent(0)
    , m_destroyCb(nullptr)
    , m_destroyCbArg(nullptr)
{
    if (m_abc.valid())
    {
        const auto& metadata = m_abc.getMetaData();
        
        if (AbcGeom::IXformSchema::matches(metadata))
        {
            m_xform.reset(new aiXForm(this));
            m_schemas.push_back(m_xform.get());
        }
        
        if (AbcGeom::IPolyMeshSchema::matches(metadata))
        {
            m_polymesh.reset(new aiPolyMesh(this));
            m_schemas.push_back(m_polymesh.get());
        }
        
        if (AbcGeom::ICameraSchema::matches(metadata))
        {
            m_camera.reset(new aiCamera(this));
            m_schemas.push_back(m_camera.get());
        }
    }
}

aiObject::~aiObject()
{
    invokeDestroyCallback();
}

void aiObject::setDestroyCallback(aiDestroyCallback cb, void *arg)
{
    m_destroyCb = cb;
    m_destroyCbArg = arg;
}

void aiObject::invokeDestroyCallback()
{
    if (m_destroyCb)
    {
        m_destroyCb(m_destroyCbArg);
    }
}

void aiObject::addChild(aiObject *c)
{
    if (!c)
    {
        return;
    }

    m_children.push_back(c);
    c->m_parent = this;
}

void aiObject::removeChild(aiObject *c)
{
    if (!c)
    {
        return;
    }

    std::vector<aiObject*>::iterator it = std::find(m_children.begin(), m_children.end(), c);
    if (it != m_children.end())
    {
        c->m_parent = 0;
        m_children.erase(it);
    }
}

void aiObject::readConfig()
{
    for (auto s : m_schemas)
    {
        s->readConfig();
    }
}

void aiObject::updateSample(float time)
{
    DebugLog("aiObject::updateSample(obj='%s', t=%f)", getFullName(), time);
    
    for (auto s : m_schemas)
    {
        s->updateSample(time);
    }
}

void aiObject::notifyUpdate()
{
    for (auto s : m_schemas)
    {
        s->notifyUpdate();
    }
}

aiObject* aiObject::getChild(const char *name, size_t nameLen)
{
    if (name)
    {
        if (nameLen == std::string::npos)
        {
            for (std::vector<aiObject*>::iterator it=m_children.begin(); it!=m_children.end(); ++it)
            {
                if (!strcmp(name, (*it)->getName()))
                {
                    return *it;
                }
            }
        }
        else
        {
            for (std::vector<aiObject*>::iterator it=m_children.begin(); it!=m_children.end(); ++it)
            {
                size_t childNameLen = (*it)->getNameLength();
                if (childNameLen == nameLen && !strncmp(name, (*it)->getName(), childNameLen))
                {
                    return *it;
                }
            }
        }
    }
    
    return 0;
}

aiObject* aiObject::find(const char *name)
{
    if (name)
    {
        const char *next = strchr(name, '/');
        
        if (next == name)
        {
            return (m_parent == 0 ? find(name + 1) : 0);
        }
        else if (!next)
        {
            return getChild(name);
        }
        else
        {
            aiObject *child = getChild(name, next - name);
            return (child ? child->find(next + 1) : 0);
        }
    }
    else
    {
        return 0;
    }
}

bool aiObject::isInstance() const
{
    return m_abc.isInstanceDescendant();
}

aiObject* aiObject::getInstanceSource()
{
    if (!isInstance())
    {
        return 0;
    }
    else
    {
        if (m_abc.isInstanceRoot())
        {
            std::string path = m_abc.instanceSourcePath();
            return m_ctx->findObject(path.c_str());
        }
        else
        {
            aiObject *pinst = m_parent->getInstanceSource();
            return (pinst ? pinst->getChild(getName()) : 0);
        }
    }
}

aiContext*  aiObject::getContext()           { return m_ctx; }
abcObject&  aiObject::getAbcObject()         { return m_abc; }
const char* aiObject::getName() const        { return m_abc.getName().c_str(); }
size_t      aiObject::getNameLength() const  { return m_abc.getName().length(); }
const char* aiObject::getFullName() const    { return m_abc.getFullName().c_str(); }
uint32_t    aiObject::getNumChildren() const { return m_children.size(); }
aiObject*   aiObject::getChild(int i)        { return m_children[i]; }
aiObject*   aiObject::getParent()            { return m_parent; }

bool aiObject::hasXForm() const    { return m_xform != nullptr; }
bool aiObject::hasPolyMesh() const { return m_polymesh != nullptr; }
bool aiObject::hasCamera() const   { return m_camera != nullptr; }

aiXForm&    aiObject::getXForm()      { return *m_xform; }
aiPolyMesh& aiObject::getPolyMesh()   { return *m_polymesh; }
aiCamera&   aiObject::getCamera()     { return *m_camera; }

float aiObject::getStartTime() const
{
    return (m_schemas.size() == 0 ? 0.0f : m_schemas[0]->getStartTime());
}

float aiObject::getEndTime() const
{
    return (m_schemas.size() == 0 ? 0.0f : m_schemas[0]->getEndTime());
}
