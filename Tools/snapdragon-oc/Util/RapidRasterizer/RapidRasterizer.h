//============================================================================================================
//
//
//                  Copyright (c) 2023, Qualcomm Innovation Center, Inc. All rights reserved.
//                              SPDX-License-Identifier: BSD-3-Clause
//
//============================================================================================================


#pragma once

#include "../../Common/SOCUtil.h"


namespace SDOCUtil
{

#ifdef HIDE_RASTERIZER
	class SDOCCore;
#else
	class Rasterizer;
#endif


class OccluderInput
{
public:
	bool potentialVisible = true;
	bool backfaceCull = true;
	bool IsRawMesh = false;
	bool IsValidRawMesh = false;
	bool IsOccludee = false;
	uint16_t priority = 0;
	unsigned int nVert = 0;
	unsigned int nIdx = 0;
	const float *modelWorld = nullptr;
	const float * inVtx = nullptr;
	const unsigned short * inIdx = nullptr;
};

class OccluderManager 
{
public:
	std::vector< OccluderInput*> mPool;
	OccluderManager() 
	{
		mPool.reserve(256);
		for (int idx = 0; idx < 2; idx++) {
			OccluderInput* occs = new OccluderInput[128];
			for (int i = 0; i < 128; i++)
			{
				mPool.push_back(&occs[i]);
			}
		}


		mLastFinalRendered = 0;
		mCurrentOccNum = 0;
	}
	OccluderInput * RequestOccluder()
	{
		mLastFinalRendered = 0;
		OccluderInput * occ = nullptr;
		if (mCurrentOccNum < mPool.size()) 
		{
			occ = mPool[mCurrentOccNum++];
		}
		else 
		{
			OccluderInput* occs = new OccluderInput[128];
			for (int i = 0; i < 128; i++) 
			{
				mPool.push_back(&occs[i]);
			}

			occ = mPool[mCurrentOccNum++];
		}
		occ->IsOccludee = false;
		return occ;
	}

	int GetMemory() {
		int occ = sizeof(OccluderInput);
		return (int)( mPool.capacity() * occ);
	}
	

	~OccluderManager() 
	{
		for (int i = 0; i < mPool.size(); i+=128)
		{
			delete[] mPool[i];
		}
		mPool.clear();
	}

	bool LatestFrameInterleaveDraw = false;
	int mCurrentOccNum = 0;
	int mCurrentRasterizedNum = 0;
	int mLastFinalRendered = 0;
	void OnRenderEnd();

};
class RapidRasterizer
{
public:
    RapidRasterizer();
    ~RapidRasterizer();

    // set resolution
    void setResolution(unsigned int width, unsigned int height);

	void OnRenderFinish();
	// dump depth map
    bool dumpDepthMap(unsigned char *depthMap, SDOCCommon::DumpImageMode mode) ;

	void configCoherentMode(int interleaveRatio);



	Rasterizer* m_instance = nullptr;
	bool batchQuery(const float * bbox, unsigned int nMesh, bool * results, bool obbMode);

	bool mShowCulled = false; //default to be false
	bool mInRenderingState = false;
	void setNearPlane(float nearPlane);
	int mLastBakedOccluderNum = 0;
	int mLastFullOccluderNum = 0;
	bool PrintNumberOfOccluderOnce = false;
	void BeforeQueryTreatTrueAsCulled();
public:

	uint64_t mFrameNum = 0;

	size_t getMemoryByteUsed();

	void SetCCW(bool IsModelCCW);
	int GetCW();
	void ShowOccludeeInDepthmap(int value);
	void onNewFrame(uint64_t frame, bool criticalFrame, bool isRotating, const float* CameraPos);


	void setRenderType(int renderType);
	bool SubmitRawOccluder(const float * vertices, const unsigned short * indices, unsigned int nVert, unsigned int nIdx,  const float * localToWorld, bool backfaceCull);
	bool QueryRawOccludee(const float* vertices, const unsigned short* indices, unsigned int nVert, unsigned int nIdx, const float* localToWorld, bool backfaceCull, const float* WorldAABB);
	bool RasterizeOccluder(OccluderInput * occ);
	void SubmitBakedOccluder(unsigned short * inVtx, const float * modelWorld);
	void UsePrevDepthData();

	int mCurrentOccluderIdx = 0;

	SDOCCommon::Matrix4x4 mViewProjT;
	
	bool mBackFaceCullOffFirst = true;

	OccluderManager* mOccluderCenter = nullptr;
	void UsePrevFrameOccluders(int sameOccluderNum);

private:
	void FlushCachedOccluder(int endOccluderNum);
	void CalculateMeshMinExtent(unsigned int nVert, const float * inVtx, float* minExtent);
public:
	void EnablePriorityQueue(bool value);
	bool mSortByPriorityQueue = false;



	void SyncOccluderPVS(bool* value);
	int mInvalidRawMeshNum = 0;
	int mInvalidRawMeshGroup = 0;
	void ConfigQueryChildData(uint16_t* value);
private:
	bool RasterizeOccludeeMesh(OccluderInput* occ, const float* worldAABB);
};

} // namespace util
