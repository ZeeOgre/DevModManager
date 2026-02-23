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

#ifndef GLTOOLS_H
#define GLTOOLS_H

#include "data/niftypes.h"
#include "filebuf.hpp"

#include <QPair>


//! @file gltools.h BoundSphere, VertexWeight, BoneData, SkinPartition

using TriStrip = QVector<quint16>;
Q_DECLARE_TYPEINFO(TriStrip, Q_MOVABLE_TYPE);
using TexCoords = QVector<Vector2>;
Q_DECLARE_TYPEINFO(TexCoords, Q_MOVABLE_TYPE);


//! A bounding sphere for an object, typically a Mesh
class BoundSphere final
{
public:
	BoundSphere();
	BoundSphere( const BoundSphere & );
	BoundSphere( const NifModel * nif, const QModelIndex & );
	BoundSphere( const Vector3 & center, float radius );
	BoundSphere( const Vector3 * vertexData, qsizetype vertexCnt, bool useMiniball = false );
	inline BoundSphere( const QVector<Vector3> & vertices, bool useMiniball = false )
	{
		(void) new( this ) BoundSphere( vertices.data(), vertices.size(), useMiniball );
	}

	Vector3 center;
	float radius;

	void update( NifModel * nif, const QModelIndex & );

	static void setBounds( NifModel * nif, const QModelIndex &, const Vector3 & center, float radius );

	BoundSphere & operator=( const BoundSphere & );
	BoundSphere & operator|=( const BoundSphere & );

	BoundSphere operator|( const BoundSphere & o );

	BoundSphere & apply( const Transform & t );
	BoundSphere & applyInv( const Transform & t );

	inline bool contains( const Vector3 & v ) const
	{
		// assumes non-empty bounds (radius >= 0)
		FloatVector4	d = FloatVector4( v ) - FloatVector4( center );
		return ( d.dotProduct3( d ) <= ( radius * radius ) );
	}

	friend BoundSphere operator*( const Transform & t, const BoundSphere & s );
};

//! A vertex, weight pair
class VertexWeight final
{
public:
	VertexWeight()
	{ vertex = 0; weight = 0.0; }
	VertexWeight( int v, float w )
	{ vertex = v; weight = w; }

	int vertex;
	float weight;
};

//! A bone, weight pair
class BoneWeightUNORM16 final
{
public:
	BoneWeightUNORM16()
	{
		bone = 0; weight = 0.0;
	}
	BoneWeightUNORM16(quint16 b, float w)
	{
		bone = b; weight = w;
	}

	quint16 bone;
	float weight;
};

//! A bone transform and bounds
class BoneData
{
public:
	BoneData() {}
	BoneData( const NifModel * nif, const QModelIndex & index, int b );

	void setTransform( const NifModel * nif, const QModelIndex & index );

	Transform trans;
	Vector3 center;
	float radius = 0;
	Vector3 tcenter;
	int bone = 0;
};

//! A skin partition
class SkinPartition final
{
public:
	SkinPartition() { numWeightsPerVertex = 0; }
	SkinPartition( const NifModel * nif, const QModelIndex & index );

	QVector<Triangle> getRemappedTriangles() const;
	QVector<QVector<quint16>> getRemappedTristrips() const;

	QVector<int> boneMap;
	QVector<int> vertexMap;

	int numWeightsPerVertex;
	QVector<QPair<int, float> > weights;

	QVector<Triangle> triangles;
	QVector<QVector<quint16> > tristrips;
};

float bhkScale( const NifModel * nif );
float bhkInvScale( const NifModel * nif );
float bhkScaleMult( const NifModel * nif );

Transform bhkBodyTrans( const NifModel * nif, const QModelIndex & index );

QModelIndex bhkGetEntity( const NifModel * nif, const QModelIndex & index, const QString & name );
QModelIndex bhkGetRBInfo( const NifModel * nif, const QModelIndex & index, const QString & name );

#if 0	// unused function
inline GLuint glClosestMatch( GLuint * buffer, GLint hits )
{
	// a little helper function, returns the closest matching hit from the name buffer
	GLuint choose = buffer[ 3 ];
	GLuint depth  = buffer[ 1 ];

	for ( int loop = 1; loop < hits; loop++ ) {
		if ( buffer[ loop * 4 + 1 ] < depth ) {
			choose = buffer[ loop * 4 + 3 ];
			depth  = buffer[ loop * 4 + 1 ];
		}
	}

	return choose;
}
#endif

static inline FloatVector4 getColorKeyFromID( int id )
{
	return FloatVector4( std::uint32_t( id + 1 ) ) * ( 1.0f / 255.0f ) + ( 1.0f / 1024.0f );
}

static inline int getIDFromColorKey( std::uint32_t rgba )
{
	return int( rgba ) - 1;
}

#endif
