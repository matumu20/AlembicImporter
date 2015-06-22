#include <cstdlib>
#include <cstdio>
#include <cstdint>
#include <iostream>
#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#endif

struct aiContext;
struct aiObject;
struct abcV2;
struct abcV3;

struct aiSubmeshInfo
{
    int index;
    int face_count;
    int vertex_count;
    int triangle_count;
};

#ifdef _WIN32
typedef void (__stdcall *aiNodeEnumerator)(aiObject*, void*);
#else
typedef void (*aiNodeEnumerator)(aiObject*, void*);
#endif

typedef aiContext* (*aiCreateContextFunc)();
typedef void (*aiDestroyContextFunc)(aiContext*);

typedef bool (*aiLoadFunc)(aiContext*, const char*);
typedef float (*aiGetStartTimeFunc)(aiContext*);
typedef float (*aiGetEndTimeFunc)(aiContext*);
typedef aiObject* (*aiGetTopObjectFunc)(aiContext*);

typedef void (*aiEnumerateChildFunc)(aiObject*, aiNodeEnumerator, void*);
typedef const char* (*aiGetNameSFunc)(aiObject*);
typedef const char* (*aiGetFullNameSFunc)(aiObject*);
typedef uint32_t (*aiGetNumChildrenFunc)(aiObject*);
typedef void (*aiSetCurrentTimeFunc)(aiObject*, float);

typedef bool (*aiHasPolyMeshFunc)(aiObject*);
typedef bool (*aiPolyMeshHasNormalsFunc)(aiObject* obj);
typedef bool (*aiPolyMeshHasUVsFunc)(aiObject* obj);
typedef void (*aiPolyMeshPrepareSubmeshesFunc)(aiObject*, int);
typedef bool (*aiPolyMeshGetNextSubmeshFunc)(aiObject*, aiSubmeshInfo*);
typedef void (*aiPolyMeshCopySubmeshIndicesFunc)(aiObject*, int*, const aiSubmeshInfo*);
typedef void (*aiPolyMeshCopySubmeshVerticesFunc)(aiObject*, abcV3*, const aiSubmeshInfo*);
typedef void (*aiPolyMeshCopySubmeshNormalsFunc)(aiObject*, abcV3*, const aiSubmeshInfo*);
typedef void (*aiPolyMeshCopySubmeshUVsFunc)(aiObject*, abcV2*, const aiSubmeshInfo*);

#ifdef _WIN32

typedef HMODULE Dso;

Dso LoadDso(const char *path)
{
    return LoadLibrary(path);
}

template <typename FuncType>
FuncType GetDsoSymbol(Dso dso, const char *symbol)
{
    return (FuncType) (dso != NULL ? GetProcAddress(dso, symbol) : 0);
}

void UnloadDso(Dso dso)
{
    if (dso != NULL) FreeLibrary(dso);
}

#else

typedef void* Dso;

Dso LoadDso(const char *path)
{
    return dlopen(path, RTLD_LAZY|RTLD_LOCAL);
}

template <typename FuncType>
FuncType GetDsoSymbol(Dso dso, const char *symbol)
{
    return (FuncType) (dso ? dlsym(dso, symbol) : 0);
}

void UnloadDso(Dso dso)
{
    if (dso) dlclose(dso);
}

#endif

struct API
{
    Dso dso;
    
    aiCreateContextFunc aiCreateContext;
    aiDestroyContextFunc aiDestroyContext;
    
    aiLoadFunc aiLoad;
    aiGetStartTimeFunc aiGetStartTime;
    aiGetEndTimeFunc aiGetEndTime;
    aiGetTopObjectFunc aiGetTopObject;
    
    aiEnumerateChildFunc aiEnumerateChild;
    aiGetNameSFunc aiGetName;
    aiGetFullNameSFunc aiGetFullName;
    aiGetNumChildrenFunc aiGetNumChildren;
    aiSetCurrentTimeFunc aiSetCurrentTime;
    
    aiHasPolyMeshFunc aiHasPolyMesh;
    aiPolyMeshHasNormalsFunc aiPolyMeshHasNormals;
    aiPolyMeshHasUVsFunc aiPolyMeshHasUVs;
    aiPolyMeshPrepareSubmeshesFunc aiPolyMeshPrepareSubmeshes;
    aiPolyMeshGetNextSubmeshFunc aiPolyMeshGetNextSubmesh;
    aiPolyMeshCopySubmeshIndicesFunc aiPolyMeshCopySubmeshIndices;
    aiPolyMeshCopySubmeshVerticesFunc aiPolyMeshCopySubmeshVertices;
    aiPolyMeshCopySubmeshNormalsFunc aiPolyMeshCopySubmeshNormals;
    aiPolyMeshCopySubmeshUVsFunc aiPolyMeshCopySubmeshUVs;
    
    API()
        : dso(0)
        , aiCreateContext(0)
        , aiDestroyContext(0)
        , aiLoad(0)
        , aiGetStartTime(0)
        , aiGetEndTime(0)
        , aiGetTopObject(0)
        , aiEnumerateChild(0)
        , aiGetName(0)
        , aiGetFullName(0)
        , aiSetCurrentTime(0)
        , aiHasPolyMesh(0)
        , aiPolyMeshPrepareSubmeshes(0)
        , aiPolyMeshGetNextSubmesh(0)
        , aiPolyMeshCopySubmeshIndices(0)
        , aiPolyMeshCopySubmeshVertices(0)
        , aiPolyMeshCopySubmeshNormals(0)
        , aiPolyMeshCopySubmeshUVs(0)
    {
    }
    
    API(const char *dsoPath)
    {
        dso = LoadDso(dsoPath);
        
        aiCreateContext = GetDsoSymbol<aiCreateContextFunc>(dso, "aiCreateContext");
        aiDestroyContext = GetDsoSymbol<aiDestroyContextFunc>(dso, "aiDestroyContext");
        
        aiLoad = GetDsoSymbol<aiLoadFunc>(dso, "aiLoad");
        aiGetStartTime = GetDsoSymbol<aiGetStartTimeFunc>(dso, "aiGetStartTime");
        aiGetEndTime = GetDsoSymbol<aiGetEndTimeFunc>(dso, "aiGetEndTime");
        aiGetTopObject = GetDsoSymbol<aiGetTopObjectFunc>(dso, "aiGetTopObject");
        
        aiEnumerateChild = GetDsoSymbol<aiEnumerateChildFunc>(dso, "aiEnumerateChild");
        aiGetName = GetDsoSymbol<aiGetNameSFunc>(dso, "aiGetNameS");
        aiGetFullName = GetDsoSymbol<aiGetFullNameSFunc>(dso, "aiGetFullNameS");
        aiGetNumChildren = GetDsoSymbol<aiGetNumChildrenFunc>(dso, "aiGetNumChildren");
        aiSetCurrentTime = GetDsoSymbol<aiSetCurrentTimeFunc>(dso, "aiSetCurrentTime");
        
        aiHasPolyMesh = GetDsoSymbol<aiHasPolyMeshFunc>(dso, "aiHasPolyMesh");
        aiPolyMeshHasNormals = GetDsoSymbol<aiPolyMeshHasNormalsFunc>(dso, "aiPolyMeshHasNormals");
        aiPolyMeshHasUVs = GetDsoSymbol<aiPolyMeshHasUVsFunc>(dso, "aiPolyMeshHasUVs");
        aiPolyMeshPrepareSubmeshes = GetDsoSymbol<aiPolyMeshPrepareSubmeshesFunc>(dso, "aiPolyMeshPrepareSubmeshes");
        aiPolyMeshGetNextSubmesh = GetDsoSymbol<aiPolyMeshGetNextSubmeshFunc>(dso, "aiPolyMeshGetNextSubmesh");
        aiPolyMeshCopySubmeshIndices = GetDsoSymbol<aiPolyMeshCopySubmeshIndicesFunc>(dso, "aiPolyMeshCopySubmeshIndices");
        aiPolyMeshCopySubmeshVertices = GetDsoSymbol<aiPolyMeshCopySubmeshVerticesFunc>(dso, "aiPolyMeshCopySubmeshVertices");
        aiPolyMeshCopySubmeshNormals = GetDsoSymbol<aiPolyMeshCopySubmeshNormalsFunc>(dso, "aiPolyMeshCopySubmeshNormals");
        aiPolyMeshCopySubmeshUVs = GetDsoSymbol<aiPolyMeshCopySubmeshUVsFunc>(dso, "aiPolyMeshCopySubmeshUVs");
    }
    
    ~API()
    {
        UnloadDso(dso);
    }
};

struct EnumerateData
{
    API *api;
    aiContext *ctx;
};

void EnumerateMesh(aiObject *obj, void *userdata)
{
    EnumerateData *data = (EnumerateData*) userdata;
    
    data->api->aiSetCurrentTime(obj, data->api->aiGetStartTime(data->ctx));
    
    if (data->api->aiHasPolyMesh(obj))
    {
        std::cout << "Found mesh: " << data->api->aiGetFullName(obj) << std::endl;
        
        data->api->aiPolyMeshPrepareSubmeshes(obj, 65000);
        
        aiSubmeshInfo submesh;
        size_t i = 0;
        
        while (data->api->aiPolyMeshGetNextSubmesh(obj, &submesh))
        {
            std::cout << "  Submesh " << i << std::endl;
            
            /*
            struct aiSubmeshInfo
{
    int index;
    int face_count;
    int vertex_count;
    int triangle_count;
};
            */
            std::cout << "    index: " << submesh.index << std::endl;
            std::cout << "    faces: " << submesh.face_count << std::endl;
            std::cout << "    triangles: " << submesh.triangle_count << std::endl;
            std::cout << "    vertices: " << submesh.vertex_count << std::endl;

            int *indices = new int[submesh.triangle_count * 3];
            float *vertices = new float[submesh.vertex_count * 3];
            float *normals = 0;
            float *uvs = 0;

            std::cout << "    copy indices..." << std::endl;
            data->api->aiPolyMeshCopySubmeshIndices(obj, indices, &submesh);

            std::cout << "    copy vertices..." << std::endl;
            data->api->aiPolyMeshCopySubmeshVertices(obj, (abcV3*)vertices, &submesh);

            float *P = vertices;
            for (int j=0; j<submesh.vertex_count; ++j, P+=3)
            {
                std::cout << "      P[" << j << "] = (" << P[0] << ", " << P[1] << ", " << P[2] << ")" << std::endl;
            }

            if (data->api->aiPolyMeshHasNormals(obj))
            {
                normals = new float[submesh.vertex_count * 3];
                std::cout << "    copy normals..." << std::endl;
                data->api->aiPolyMeshCopySubmeshNormals(obj, (abcV3*)normals, &submesh);

                float *N = normals;
                for (int j=0; j<submesh.vertex_count; ++j, N+=3)
                {
                    std::cout << "      N[" << j << "] = (" << N[0] << ", " << N[1] << ", " << N[2] << ")" << std::endl;
                }
            }

            if (data->api->aiPolyMeshHasUVs(obj))
            {
                uvs = new float[submesh.vertex_count * 2];
                std::cout << "    copy uvs..." << std::endl;
                data->api->aiPolyMeshCopySubmeshUVs(obj, (abcV2*)uvs, &submesh);

                float *uv = uvs;
                for (int j=0; j<submesh.vertex_count; ++j, uv+=2)
                {
                    std::cout << "      uv[" << j << "] = (" << uv[0] << ", " << uv[1] << ")" << std::endl;
                }
            }

            int *index = indices;
            for (int j=0; j<submesh.triangle_count; ++j, index+=3)
            {
                std::cout << "      triangle[" << j << "] = " << index[0] << ", " << index[1] << ", " << index[2] << std::endl;
            }

            ++i;
        }
    }
    else if (data->api->aiGetNumChildren(obj) > 0)
    {
        data->api->aiEnumerateChild(obj, EnumerateMesh, userdata);
    }
}

int main(int argc, char **argv)
{
    if (argc != 3)
    {
        std::cerr << "tester [dsopath] [alembicpath]" << std::endl;
        return 1;
    }
    
    API api(argv[1]);
    
    if (!api.dso)
    {
        std::cerr << "invalid dso: " << argv[1] << std::endl;
        return 1;
    }
    
    aiContext *ctx = api.aiCreateContext();
    
    if (ctx)
    {
        if (api.aiLoad(ctx, argv[2]))
        {
            float start = api.aiGetStartTime(ctx);
            float end = api.aiGetEndTime(ctx);
            
            std::cout << "alembic time range: [" << start << ", " << end << "]" << std::endl;
            
            EnumerateData data = { &api, ctx };
            
            api.aiEnumerateChild(api.aiGetTopObject(ctx), EnumerateMesh, (void*) &data);
        }
        else
        {
            std::cout << "invalid alembic: " << argv[2] << std::endl;
        }
        
        api.aiDestroyContext(ctx);
    }
    else
    {
        std::cerr << "could not create context" << std::endl;
    }
    
    return 0;
}

