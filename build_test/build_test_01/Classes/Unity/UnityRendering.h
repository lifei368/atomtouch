#pragma once


// trick to make structure inheritance work transparently between c/cpp
// for c we use "anonymous struct"
#ifdef __cplusplus
	#define START_STRUCT(T, Base)	struct T : Base {
	#define END_STRUCT(T)			};
#else
	#define START_STRUCT(T, Base)	typedef struct T { struct Base;
	#define END_STRUCT(T)			} T;
#endif

// we will keep objc objects in struct, so we need to explicitely mark references as strong to not confuse ARC
// please note that actual object lifetime is managed in objc++ code, so __unsafe_unretained is good enough for objc code
// DO NOT assign objects to UnityDisplaySurface* members in objc code.
// DO NOT store objects from UnityDisplaySurface* members in objc code, as this wont be caught by ARC
#ifdef __OBJC__
	#ifdef __cplusplus
		#define OBJC_OBJECT_PTR	__strong
	#else
		#define OBJC_OBJECT_PTR	__unsafe_unretained
	#endif
#else
	#define OBJC_OBJECT_PTR
#endif

// unity common rendering (display) surface
typedef struct
UnityDisplaySurfaceBase
{
	UnityRenderBuffer	unityColorBuffer;
	UnityRenderBuffer	unityDepthBuffer;

	UnityRenderBuffer	systemColorBuffer;
	UnityRenderBuffer	systemDepthBuffer;

	void*				cvTextureCache;			// CVOpenGLESTextureCacheRef
	void*				cvTextureCacheTexture;	// CVOpenGLESTextureRef
	void*				cvPixelBuffer;			// CVPixelBufferRef

	unsigned			targetW, targetH;
	unsigned			systemW, systemH;

	int					msaaSamples;
	int					useCVTextureCache;		// [bool]
	int					srgb;					// [bool]
	int					disableDepthAndStencil;	// [bool]
	int					allowScreenshot;		// [bool] currently we allow screenshots (from script) only on main display

	int					api;					// [UnityRenderingAPI]
}
UnityDisplaySurfaceBase;

// GLES display surface
START_STRUCT(UnityDisplaySurfaceGLES, UnityDisplaySurfaceBase)
	OBJC_OBJECT_PTR	CAEAGLLayer*	layer;
	OBJC_OBJECT_PTR	EAGLContext*	context;

	// system FB
	unsigned	systemFB;
	unsigned	systemColorRB;

	// target resolution FB/target RT to blit from
	unsigned	targetFB;
	unsigned	targetColorRT;

	// MSAA FB
	unsigned	msaaFB;
	unsigned	msaaColorRB;

	// will be "shared", only one depth buffer is needed
	unsigned	depthRB;

	// render surface gl setup: formats and AA
	unsigned	colorFormat;
	unsigned	depthFormat;
END_STRUCT(UnityDisplaySurfaceGLES)

// Metal display surface
START_STRUCT(UnityDisplaySurfaceMTL, UnityDisplaySurfaceBase)
	OBJC_OBJECT_PTR	CAMetalLayer*		layer;
	OBJC_OBJECT_PTR	MTLDeviceRef		device;

	OBJC_OBJECT_PTR	MTLCommandQueueRef	commandQueue;
	OBJC_OBJECT_PTR	CAMetalDrawableRef	drawable;

	OBJC_OBJECT_PTR	MTLTextureRef		systemColorRB;
	OBJC_OBJECT_PTR	MTLTextureRef		targetColorRT;
	OBJC_OBJECT_PTR	MTLTextureRef		targetAAColorRT;

	OBJC_OBJECT_PTR	MTLTextureRef		depthRB;
	OBJC_OBJECT_PTR	MTLTextureRef		stencilRB;

	unsigned							colorFormat;	// [MTLPixelFormat]
	unsigned							depthFormat;	// [MTLPixelFormat]
END_STRUCT(UnityDisplaySurfaceMTL)

// unity common base for UIView ready to be rendered into
#ifdef __OBJC__
@interface UnityRenderingView : UIView {}
+ (void)InitializeForAPI:(UnityRenderingAPI)api;
@end
#endif

// gles display surface manipulation
#ifdef __cplusplus
extern "C" {
#endif

void CreateSystemRenderingSurfaceGLES(UnityDisplaySurfaceGLES* surface);
void DestroySystemRenderingSurfaceGLES(UnityDisplaySurfaceGLES* surface);
void CreateRenderingSurfaceGLES(UnityDisplaySurfaceGLES* surface);
void DestroyRenderingSurfaceGLES(UnityDisplaySurfaceGLES* surface);
void CreateSharedDepthbufferGLES(UnityDisplaySurfaceGLES* surface);
void DestroySharedDepthbufferGLES(UnityDisplaySurfaceGLES* surface);
void CreateUnityRenderBuffersGLES(UnityDisplaySurfaceGLES* surface);
void DestroyUnityRenderBuffersGLES(UnityDisplaySurfaceGLES* surface);
void PreparePresentGLES(UnityDisplaySurfaceGLES* surface);
void PrepareRenderingGLES(UnityDisplaySurfaceGLES* surface);
void TeardownRenderingGLES(UnityDisplaySurfaceGLES* surface);

#ifdef __cplusplus
} // extern "C"
#endif

// metal display surface manipulation
#ifdef __cplusplus
extern "C" {
#endif

void CreateSystemRenderingSurfaceMTL(UnityDisplaySurfaceMTL* surface);
void DestroySystemRenderingSurfaceMTL(UnityDisplaySurfaceMTL* surface);
void CreateRenderingSurfaceMTL(UnityDisplaySurfaceMTL* surface);
void DestroyRenderingSurfaceMTL(UnityDisplaySurfaceMTL* surface);
void CreateSharedDepthbufferMTL(UnityDisplaySurfaceMTL* surface);
void DestroySharedDepthbufferMTL(UnityDisplaySurfaceMTL* surface);
void CreateUnityRenderBuffersMTL(UnityDisplaySurfaceMTL* surface);
void DestroyUnityRenderBuffersMTL(UnityDisplaySurfaceMTL* surface);
void PreparePresentMTL(UnityDisplaySurfaceMTL* surface);
void PrepareRenderingMTL(UnityDisplaySurfaceMTL* surface);
void TeardownRenderingMTL(UnityDisplaySurfaceMTL* surface);

#ifdef __cplusplus
} // extern "C"
#endif

// metal/gles unification

#define GLES_METAL_COMMON_IMPL(f)													\
inline void f(UnityDisplaySurfaceBase* surface)										\
{																					\
	if(surface->api == apiMetal)	f ## MTL((UnityDisplaySurfaceMTL*)surface);		\
	else							f ## GLES((UnityDisplaySurfaceGLES*)surface);	\
}																					\

GLES_METAL_COMMON_IMPL(CreateSystemRenderingSurface);
GLES_METAL_COMMON_IMPL(DestroySystemRenderingSurface);
GLES_METAL_COMMON_IMPL(CreateRenderingSurface);
GLES_METAL_COMMON_IMPL(DestroyRenderingSurface);
GLES_METAL_COMMON_IMPL(CreateSharedDepthbuffer);
GLES_METAL_COMMON_IMPL(DestroySharedDepthbuffer);
GLES_METAL_COMMON_IMPL(CreateUnityRenderBuffers);
GLES_METAL_COMMON_IMPL(DestroyUnityRenderBuffers);
GLES_METAL_COMMON_IMPL(PreparePresent);
GLES_METAL_COMMON_IMPL(PrepareRendering);
GLES_METAL_COMMON_IMPL(TeardownRendering);

#undef GLES_METAL_COMMON_IMPL


#ifdef __cplusplus
extern "C" {
#endif

// helper to run unity loop along with proper handling of the rendering
void UnityRepaint();

#ifdef __cplusplus
} // extern "C"
#endif