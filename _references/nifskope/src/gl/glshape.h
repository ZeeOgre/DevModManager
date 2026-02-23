/***** BEGIN LICENSE BLOCK *****

BSD License

Copyright (c) 2005-2015, NIF File Format Library and Tools
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.
3. The name of the NIF File Format Library and Tools project may not be
   used to endorse or promote products derived from this software
   without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

***** END LICENCE BLOCK *****/

#ifndef GLSHAPE_H
#define GLSHAPE_H

#include "gl/glnode.h" // Inherited
#include "gl/gltools.h"
#include "gl/glcontext.hpp"

#include <QPersistentModelIndex>
#include <QVector>
#include <QString>

//! @file glshape.h Shape

class NifModel;

class Shape : public Node
{
	friend class MorphController;
	friend class UVController;
	friend class Renderer;
	friend class GltfStore;

public:
	Shape( Scene * s, const QModelIndex & b );

	// IControllable

	void clear() override;
	void transform() override;

	// end IControllable

	void updateBoneTransforms();
	void convertTriangleStrip( const void * indicesData, size_t numIndices );
	void removeInvalidIndices();
	void drawVerts( float pointSize, int vertexSelected ) const;
	// btnMask & 1 = draw bitangents, btnMask & 2 = draw tangents, btnMask & 4 = draw normals
	void drawNormals( int btnMask = 4, int vertexSelected = -1, float lineLength = 0.25f ) const;
	void drawWireframe( FloatVector4 color ) const;
	// i = first triangle, n = number of triangles to draw
	void drawTriangles( qsizetype i, qsizetype n, FloatVector4 color ) const;
	void drawWeights( int vertexSelected ) const;
	void drawBoundingSphere( const BoundSphere & sph, FloatVector4 color ) const;
	void drawBoundingBox( const Vector3 & boundsCenter, const Vector3 & boundsDims, FloatVector4 color ) const;
	void setUniforms( NifSkopeOpenGLContext::Program * prog ) const;
	bool bindShape() const;

	virtual QModelIndex vertexAt( int ) const { return QModelIndex(); }
	virtual QModelIndex triangleAt( int ) const { return QModelIndex(); }
	virtual void updateLodLevel() { lodTriangleCount = triangles.size(); }

protected:
	int shapeNumber;

	void setController( const NifModel * nif, const QModelIndex & controller ) override;
	void updateImpl( const NifModel * nif, const QModelIndex & index ) override;
	virtual void updateData( const NifModel* nif ) = 0;

	void boneSphere( const NifModel * nif, const QModelIndex & index ) const;

	//! Shape data
	QPersistentModelIndex iData;
	//! Tangent data
	QPersistentModelIndex iTangentData;
	//! Does the data need updating?
	bool needUpdateData = false;

	void resetSkinning();

public:
	//! Skin instance
	QPersistentModelIndex iSkin;
	//! Skin data
	QPersistentModelIndex iSkinData;
	//! Skin partition
	QPersistentModelIndex iSkinPart;

	//! Vertices
	QVector<Vector3> verts;
	//! Normals
	QVector<Vector3> norms;
	//! Vertex colors
	QVector<Color4> colors;
	//! Tangents
	QVector<Vector3> tangents;
	//! Bitangents
	QVector<Vector3> bitangents;
	//! UV coordinate sets
	QVector<TexCoords> coords;
	//! Triangles
	QVector<Triangle> triangles;
protected:
	//! Number of triangles to render at the current level of detail
	qsizetype lodTriangleCount = 0;
	//! Offsets and lengths of converted triangle strips in triangles
	QVector< std::pair<qsizetype, qsizetype> > tristripOffsets;

	bool isLOD = false;

	void resetVertexData();

	//! Toggle for skinning
	bool isSkinned = false;
	//! Is the transform rigid or weighted?
	bool transformRigid = true;

	//! Bone transforms as 4x3 matrices in row-major order
	std::vector<FloatVector4> boneTransforms;
	//! Bone weights 0 to 3 (integer part = bone index, fractional part = weight * 65535.0 / 65536.0), terminated by 0.0
	std::vector<FloatVector4> boneWeights0;
	//! Bone weights 4 to 7 (may be empty if the maximum number of weights per vertex is 4 or less)
	std::vector<FloatVector4> boneWeights1;

	int skeletonRoot = 0;
	QVector<int> bones;
	QVector<BoneData> boneData;
	QVector<SkinPartition> partitions;

	void resetSkeletonData();

	//! Holds the shader program used by this shape
	NifSkopeOpenGLContext::Program * shader = nullptr;

	//! Shader property
	BSShaderLightingProperty * bssp = nullptr;
	//! Skyrim shader property
	BSLightingShaderProperty * bslsp = nullptr;
	//! Skyrim effect shader property
	BSEffectShaderProperty * bsesp = nullptr;

	AlphaProperty * alphaProperty = nullptr;

	//! Is shader set to double sided?
	bool isDoubleSided = false;
	//! Is shader set to animate using vertex alphas?
	bool isVertexAlphaAnimation = false;
	//! Is "Has Vertex Colors" set to Yes
	bool hasVertexColors = false;

	bool depthTest = true;
	bool depthWrite = true;
	bool drawInSecondPass = false;
	bool translucent = false;

	void updateShader();

	mutable bool needUpdateBounds = false;
	mutable BoundSphere boundSphere;

	mutable NifSkopeOpenGLContext::ShapeDataHash	dataHash;

public:
	inline void clearHash()
	{
		dataHash.attrMask = 0;
	}
};

#endif
