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

#include "glshape.h"

#include "gl/controllers.h"
#include "gl/glscene.h"
#include "model/nifmodel.h"
#include "io/material.h"
#include "gl/renderer.h"
#include "gl/glmesh.h"
#include "glview.h"

#include <QDebug>
#include <QElapsedTimer>

Shape::Shape( Scene * s, const QModelIndex & b ) : Node( s, b )
{
	shapeNumber = s->shapes.count();
}

void Shape::clear()
{
	Node::clear();

	clearHash();

	resetSkinning();
	resetVertexData();
	resetSkeletonData();

	bssp = nullptr;
	bslsp = nullptr;
	bsesp = nullptr;
	alphaProperty = nullptr;

	isLOD = false;
	isDoubleSided = false;
}

void Shape::transform()
{
	if ( needUpdateData ) {
		needUpdateData = false;

		auto nif = NifModel::fromValidIndex( iBlock );
		if ( nif ) {
			clearHash();
			needUpdateBounds = true; // Force update bounds
			updateData(nif);
		} else {
			clear();
			return;
		}
	}

	Node::transform();
}

void Shape::updateBoneTransforms()
{
	qsizetype	numBones = boneData.size();
	if ( numBones < 1 ) {
		transformRigid = true;
		return;
	}
	boneTransforms.assign( size_t( numBones ) * 3, FloatVector4( 0.0f ) );
	transformRigid = false;

	Node * root = findParent( skeletonRoot );

	boundSphere = BoundSphere();

	Transform	wtInv = ( !( scene->nifModel && scene->nifModel->getBSVersion() < 100 && partitions.isEmpty() ) ?
							worldTrans().inverted() : Transform( scene->nifModel, iSkinData ) );

	for ( qsizetype i = 0; i < numBones; i++ ) {
		const BoneData &	bw = boneData.at( i );
		Transform	t = wtInv;
		Node * bone = root ? root->findChild( bw.bone ) : nullptr;
		if ( !bone )
			t = t * bw.trans.inverted();
		else
			t = t * bone->localTrans( skeletonRoot );
		boundSphere |= BoundSphere( t * bw.center, t.scale * bw.radius );
		t = t * bw.trans;

		FloatVector4 *	bt = boneTransforms.data() + ( i * 3 );
		bt[0] = FloatVector4::convertVector3( t.rotation.data() ) * t.scale;
		bt[0][3] = t.translation[0];
		bt[1] = FloatVector4::convertVector3( t.rotation.data() + 3 ) * t.scale;
		bt[1][3] = t.translation[1];
		bt[2] = FloatVector4::convertVector3( t.rotation.data() + 6 ) * t.scale;
		bt[2][3] = t.translation[2];
	}

#if 0
	// precise bounding sphere calculation using transformed vertex positions
	QVector< Vector3 >	transVerts = verts;
	Vector3 *	p = transVerts.data();
	qsizetype	numVerts = transVerts.size();
	int	numWeights = ( qsizetype( boneWeights1.size() ) < numVerts ? 4 : 8 );
	for ( qsizetype i = 0; i < numVerts; i++ ) {
		FloatVector4	v = FloatVector4::convertVector3( &( p[i][0] ) );
		v[3] = 1.0f;
		const float *	wp = &( boneWeights0[i][0] );
		FloatVector4	xTmp( 0.0f );
		FloatVector4	yTmp( 0.0f );
		FloatVector4	zTmp( 0.0f );
		float	wSum = 0.0f;
		for ( int j = 0; j < numWeights; j++, wp++ ) {
			if ( j == 4 )
				wp = &( boneWeights1[i][0] );
			float	w = *wp;
			if ( !( w > 0.0f ) )
				break;
			int	b = int( w );
			if ( b < 0 || b >= numBones ) [[unlikely]]
				continue;
			w -= float( b );
			const FloatVector4 *	bt = boneTransforms.data() + ( b * 3 );
			FloatVector4	vTmp = v * w;
			xTmp += vTmp * bt[0];
			yTmp += vTmp * bt[1];
			zTmp += vTmp * bt[2];
			wSum += w;
		}
		if ( wSum > 0.0f ) {
			FloatVector4	wSumInv( 1.0f / wSum );
			p[i][0] = xTmp.dotProduct( wSumInv );
			p[i][1] = yTmp.dotProduct( wSumInv );
			p[i][2] = zTmp.dotProduct( wSumInv );
		}
	}

	boundSphere = BoundSphere( transVerts );
#endif

	needUpdateBounds = false;
}

void Shape::convertTriangleStrip( const void * indicesData, size_t numIndices )
{
	qsizetype	numTriangles = 0;
	const quint16 *	indices = nullptr;
	if ( numIndices >= 3 && indicesData ) {
		indices = reinterpret_cast< const quint16 * >( indicesData );
		for ( size_t i = 2; i < numIndices; i++ ) {
			if ( indices[i - 2] != indices[i - 1] && indices[i - 2] != indices[i] && indices[i - 1] != indices[i] )
				numTriangles++;
		}
	}
	qsizetype	j = triangles.size();
	triangles.resize( j + numTriangles );
	tristripOffsets.append( std::pair< qsizetype, qsizetype >( j, numTriangles ) );
	Triangle *	triangleData = triangles.data();
	for ( size_t i = 2; i < numIndices && j < triangles.size(); i++ ) {
		if ( indices[i - 2] != indices[i - 1] && indices[i - 2] != indices[i] && indices[i - 1] != indices[i] ) {
			if ( !( i & 1 ) )
				triangleData[j] = Triangle( indices[i - 2], indices[i - 1], indices[i] );
			else
				triangleData[j] = Triangle( indices[i - 2], indices[i], indices[i - 1] );
			j++;
		}
	}
}

void Shape::removeInvalidIndices()
{
	qsizetype	numVerts = verts.size();
	qsizetype	numTriangles = triangles.size();
	// validate triangles' vertex indices, throw out triangles with the wrong ones
	for ( qsizetype i = 0; i < numTriangles; i++ ) {
		const Triangle &	t = triangles.at( i );
		qsizetype	maxVertex = std::max( t[0], std::max( t[1], t[2] ) );
		if ( maxVertex < numVerts ) [[likely]]
			continue;
		auto	minVertex = std::min( t[0], std::min( t[1], t[2] ) );
		if ( qsizetype( minVertex ) >= numVerts )
			minVertex = 0;
		Triangle &	tmp = triangles[i];
		if ( qsizetype( tmp[0] ) >= numVerts )
			tmp[0] = minVertex;
		if ( qsizetype( tmp[1] ) >= numVerts )
			tmp[1] = minVertex;
		if ( qsizetype( tmp[2] ) >= numVerts )
			tmp[2] = minVertex;
	}
}

void Shape::drawVerts( float pointSize, int vertexSelected ) const
{
	auto	prog = scene->useProgram( "selection.prog" );
	if ( !prog )
		return;
	auto	context = scene->renderer;
	setUniforms( prog );

	int	selectionFlags = 0x0002;
	int	selectionParam = vertexSelected;

	glEnable( GL_POLYGON_OFFSET_POINT );
	glPolygonOffset( 0.0f, -100.0f );

	if ( scene->selecting ) {
		selectionFlags = selectionFlags | 0x0001;
		selectionParam = shapeNumber << 16;
		glDisable( GL_BLEND );
	} else {
		pointSize += 0.5f;
		setGLColor( scene->wireframeColor );
		selectionFlags = selectionFlags | ( roundFloat( std::min( std::max( pointSize * 8.0f, 0.0f ), 255.0f ) ) << 8 );
		if ( vertexSelected >= 0 )
			prog->uni4f( "highlightColor", scene->highlightColor );
		glEnable( GL_BLEND );
		context->fn->glBlendFuncSeparate( GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA );
	}
	glPointSize( pointSize );
	prog->uni1i( "selectionFlags", selectionFlags );
	prog->uni1i( "selectionParam", selectionParam );

	qsizetype	numVerts = verts.size();
	context->fn->glDrawArrays( GL_POINTS, 0, GLsizei( numVerts ) );
	if ( !scene->selecting && vertexSelected >= 0 && vertexSelected < numVerts ) {
		pointSize = GLView::Settings::vertexPointSizeSelected + 0.5f;
		glPointSize( pointSize );
		selectionFlags = ( roundFloat( std::min( std::max( pointSize * 8.0f, 0.0f ), 255.0f ) ) << 8 ) | 0x0002;
		prog->uni1i( "selectionFlags", selectionFlags );
		context->fn->glDrawArrays( GL_POINTS, GLint( vertexSelected ), 1 );
	}

	glDisable( GL_POLYGON_OFFSET_POINT );
	prog->uni1i( "selectionFlags", 0 );
}

void Shape::drawNormals( int btnMask, int vertexSelected, float lineLength ) const
{
	if ( scene->selecting || !( btnMask & 7 ) )
		return;
	auto	prog = scene->useProgram( "normals.prog" );
	if ( !prog )
		return;
	auto	context = scene->renderer;
	setUniforms( prog );

	prog->uni1i( "btnSelection", ( !( btnMask & 4 ) ? ( !( btnMask & 1 ) ? 1 : 0 ) : 2 ) );
	prog->uni1f( "normalLineLength", lineLength );
	prog->uni1f( "lineWidth", GLView::Settings::lineWidthWireframe * 0.78125f );
	setGLColor( scene->wireframeColor );
	if ( vertexSelected >= 0 )
		prog->uni4f( "highlightColor", scene->highlightColor );
	prog->uni1i( "selectionParam", vertexSelected );

	glEnable( GL_BLEND );
	context->fn->glBlendFuncSeparate( GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA );

	qsizetype	numVerts = verts.size();
	context->fn->glDrawArrays( GL_POINTS, 0, GLsizei( numVerts ) );
	if ( vertexSelected >= 0 && vertexSelected < numVerts ) {
		prog->uni1f( "lineWidth", GLView::Settings::lineWidthHighlight * 1.2f );
		glDepthFunc( GL_ALWAYS );
		context->fn->glDrawArrays( GL_POINTS, GLint( vertexSelected ), 1 );
		if ( ( btnMask & 7 ) == 3 ) {
			prog->uni1i( "btnSelection", 1 );
			// yellow -> cyan (2, 1, 0, 3)
			prog->uni4f( "highlightColor", FloatVector4( scene->highlightColor ).shuffleValues( 0xC6 ) );
			context->fn->glDrawArrays( GL_POINTS, GLint( vertexSelected ), 1 );
		}
		glDepthFunc( GL_LEQUAL );
	}
}

void Shape::drawWireframe( FloatVector4 color ) const
{
	auto	context = scene->renderer;
	qsizetype	n = std::min< qsizetype >( lodTriangleCount, triangles.size() );
	if ( !context || n < 1 )
		return;
	auto	prog = context->useProgram( "wireframe.prog" );
	if ( !prog )
		return;

	setUniforms( prog );
	prog->uni4f( "vertexColorOverride", FloatVector4( 1.0e-15f ).maxValues( color ) );
	prog->uni1i( "selectionParam", -1 );
	prog->uni1f( "lineWidth", GLView::Settings::lineWidthWireframe );

	context->fn->glDrawElements( GL_TRIANGLES, GLsizei( n * 3 ), GL_UNSIGNED_SHORT, (void *) 0 );
}

void Shape::drawTriangles( qsizetype i, qsizetype n, FloatVector4 color ) const
{
	if ( i < 0 ) {
		n += i;
		i = 0;
	}
	auto	context = scene->renderer;
	if ( !context || n < 1 || i >= triangles.size() )
		return;
	n = std::min< qsizetype >( n, triangles.size() - i );
	auto	prog = context->useProgram( "selection.prog" );
	if ( !prog )
		return;

	setUniforms( prog );
	prog->uni4f( "vertexColorOverride", FloatVector4( 1.0e-15f ).maxValues( color ) );
	prog->uni1i( "selectionFlags", 0 );
	prog->uni1i( "selectionParam", -1 );

	context->fn->glDrawElements( GL_TRIANGLES, GLsizei( n * 3 ), GL_UNSIGNED_SHORT, (void *) ( i * 6 ) );
}

void Shape::drawWeights( int vertexSelected ) const
{
	auto	context = scene->renderer;
	if ( !context || scene->selecting )
		return;
	auto	prog = context->useProgram( "selection.prog" );
	if ( !prog )
		return;

	setUniforms( prog );
	prog->uni4f( "vertexColorOverride", FloatVector4( 1.0e-15f ) );
	prog->uni1i( "selectionFlags", 0x08 );
	prog->uni1i( "selectionParam", -1 );

	context->fn->glDrawElements( GL_TRIANGLES, GLsizei( triangles.size() * 3 ), GL_UNSIGNED_SHORT, (void *) 0 );

	if ( vertexSelected >= 0 && vertexSelected < verts.size() ) {
		glEnable( GL_POLYGON_OFFSET_POINT );
		glPolygonOffset( 0.0f, -100.0f );

		float	pointSize = GLView::Settings::vertexPointSizeSelected + 0.5f;
		glPointSize( pointSize );
		int	selectionFlags = ( roundFloat( std::min( std::max( pointSize * 8.0f, 0.0f ), 255.0f ) ) << 8 ) | 0x0002;
		prog->uni4f( "highlightColor", scene->highlightColor );
		prog->uni1i( "selectionFlags", selectionFlags );
		prog->uni1i( "selectionParam", vertexSelected );
		context->fn->glDrawArrays( GL_POINTS, GLint( vertexSelected ), 1 );

		glDisable( GL_POLYGON_OFFSET_POINT );
	}

	prog->uni1i( "selectionFlags", 0 );
}

void Shape::drawBoundingSphere( const BoundSphere & sph, FloatVector4 color ) const
{
	scene->setGLColor( color );
	scene->setGLLineWidth( GLView::Settings::lineWidthWireframe );
	scene->loadModelViewMatrix( viewTrans() );
	scene->drawSphereSimple( sph.center, sph.radius, 72, 4 );
}

void Shape::drawBoundingBox( const Vector3 & boundsCenter, const Vector3 & boundsDims, FloatVector4 color ) const
{
	scene->setGLColor( color );
	scene->setGLLineWidth( GLView::Settings::lineWidthWireframe );
	scene->loadModelViewMatrix( viewTrans() );
	scene->drawBox( boundsCenter - boundsDims, boundsCenter + boundsDims );
}

void Shape::setController( const NifModel * nif, const QModelIndex & iController )
{
	QString contrName = nif->itemName(iController);
	if ( contrName == "NiGeomMorpherController" ) {
		Controller * ctrl = new MorphController( this, iController );
		registerController(nif, ctrl);
	} else if ( contrName == "NiUVController" ) {
		Controller * ctrl = new UVController( this, iController );
		registerController(nif, ctrl);
	} else {
		Node::setController( nif, iController );
	}
}

void Shape::updateImpl( const NifModel * nif, const QModelIndex & index )
{
	Node::updateImpl( nif, index );

	if ( index == iBlock ) {
		shader = nullptr;	// Reset stored shader so it can reassess conditions

		bslsp = nullptr;
		bsesp = nullptr;
		bssp = properties.get<BSShaderLightingProperty>();
		if ( bssp ) {
			auto shaderType = bssp->typeId();
			if ( shaderType == "BSLightingShaderProperty" )
				bslsp = bssp->cast<BSLightingShaderProperty>();
			else if ( shaderType == "BSEffectShaderProperty" )
				bsesp = bssp->cast<BSEffectShaderProperty>();
		}

		alphaProperty = properties.get<AlphaProperty>();

		needUpdateData = true;
		updateShader();

	} else if ( isSkinned && (index == iSkin || index == iSkinData || index == iSkinPart) ) {
		needUpdateData = true;

	} else if ( (bssp && bssp->isParamBlock(index)) || (alphaProperty && index == alphaProperty->index()) ) {
		updateShader();

	}
}

void Shape::boneSphere( const NifModel * nif, const QModelIndex & index ) const
{
	Node * root = findParent( skeletonRoot );
	Node * bone = root ? root->findChild( bones.value( index.row() ) ) : nullptr;

	auto bSphere = BoundSphere( nif, index );
	if ( bSphere.radius > 0.0 ) {
		scene->setGLColor( 1.0f, 1.0f, 1.0f, 0.33f );
		scene->setGLLineWidth( GLView::Settings::lineWidthWireframe );
		if ( !scene->hasOption( Scene::DoSkinning ) ) {
			scene->loadModelViewMatrix( viewTrans() * Transform( nif, index ).inverted() );
		} else {
			bool	useSkeletonTrans = ( nif->getBSVersion() < 100 && partitions.isEmpty() );
			Matrix4	m = ( !useSkeletonTrans ?
							scene->view.toMatrix4() : viewTrans().toMatrix4() * Transform( nif, iSkinData ) );
			if ( !bone ) {
				m = m * Transform( nif, index ).inverted();
			} else {
				if ( !useSkeletonTrans )
					m = m * localTrans( skeletonRoot );
				m = m * bone->localTrans( skeletonRoot );
			}
			scene->loadModelViewMatrix( m );
		}
		scene->drawSphereSimple( bSphere.center, bSphere.radius, 36 );
	}
}

void Shape::resetSkinning()
{
	isSkinned = false;
	iSkin = iSkinData = iSkinPart = QModelIndex();
}

void Shape::resetVertexData()
{
	iData = iTangentData = QModelIndex();

	verts.clear();
	norms.clear();
	colors.clear();
	tangents.clear();
	bitangents.clear();
	coords.clear();
	triangles.clear();
	lodTriangleCount = 0;
	tristripOffsets.clear();
}

void Shape::resetSkeletonData()
{
	skeletonRoot = scene->defaultSkeletonRoot;

	boneTransforms.clear();
	boneWeights0.clear();
	boneWeights1.clear();

	bones.clear();
	boneData.clear();
	partitions.clear();
}

void Shape::updateShader()
{
	if ( bslsp )
		translucent = (bslsp->alpha < 1.0) || bslsp->hasRefraction;
	else if ( bsesp )
		translucent = (bsesp->getAlpha() < 1.0) && !alphaProperty;
	else
		translucent = false;

	drawInSecondPass = false;
	if ( translucent )
		drawInSecondPass = true;
	else if ( alphaProperty && alphaProperty->hasAlphaBlend() )
		drawInSecondPass = true;
	else if ( bssp ) {
		if ( bssp->bsVersion >= 170 ) {
			const CE2Material *	sfMat = nullptr;
			bssp->getSFMaterial( sfMat, scene->nifModel );
			if ( sfMat && ( sfMat->shaderRoute != 0 || (sfMat->flags & CE2Material::Flag_IsDecal) ) )
				drawInSecondPass = true;
		} else {
			Material * mat = bssp->getMaterial();
			if ( !mat ) {
				drawInSecondPass = bssp->hasSF1( ShaderFlags::SF1( ShaderFlags::SLSF1_Decal
																	| ShaderFlags::SLSF1_Dynamic_Decal ) );
			} else {
				drawInSecondPass = ( mat->hasAlphaBlend() || mat->hasDecal() );
			}
		}
	}

	if ( bssp ) {
		depthTest = bssp->depthTest;
		depthWrite = bssp->depthWrite;
		isDoubleSided = bssp->isDoubleSided;
		isVertexAlphaAnimation = bssp->isVertexAlphaAnimation;
	} else {
		depthTest = true;
		depthWrite = true;
		isDoubleSided = false;
		isVertexAlphaAnimation = false;
	}
}

void Shape::setUniforms( NifSkopeOpenGLContext::Program * prog ) const
{
	if ( !prog ) [[unlikely]]
		return;

	if ( !transformRigid && !boneTransforms.empty() )
		scene->renderer->updateBoneTransforms( boneTransforms.data(), boneTransforms.size() / 3 );

	const Transform &	v = viewTrans();
	prog->uni3m( "normalMatrix", v.rotation );
	prog->uni4m( "modelViewMatrix", v.toMatrix4() );
}

bool Shape::bindShape() const
{
	NifSkopeOpenGLContext *	context = scene->renderer;
	if ( !context ) [[unlikely]]
		return false;

	qsizetype	numVerts = verts.size();
	qsizetype	numTriangles = triangles.size();
	if ( !( numVerts > 0 && numTriangles > 0 ) ) [[unlikely]]
		return false;

	const float *	vertexAttrs[16];
	vertexAttrs[0] = &( verts.constFirst()[0] );
	std::uint64_t	attrModeMask = 3U;

	if ( colors.size() >= numVerts ) {
		vertexAttrs[1] = &( colors.constFirst()[0] );
		attrModeMask |= 0x00000040ULL;
	}

	if ( norms.size() >= numVerts ) [[likely]] {
		vertexAttrs[2] = &( norms.constFirst()[0] );
		attrModeMask |= 0x00000300ULL;
	}
	if ( tangents.size() >= numVerts ) [[likely]] {
		vertexAttrs[3] = &( tangents.constFirst()[0] );
		attrModeMask |= 0x00003000ULL;
	}
	if ( bitangents.size() >= numVerts ) [[likely]] {
		vertexAttrs[4] = &( bitangents.constFirst()[0] );
		attrModeMask |= 0x00030000ULL;
	}

	if ( boneWeights0.size() >= size_t( numVerts ) ) [[unlikely]] {
		vertexAttrs[5] = &( boneWeights0.front()[0] );
		attrModeMask |= 0x00400000ULL;
	}
	if ( boneWeights1.size() >= size_t( numVerts ) ) [[unlikely]] {
		vertexAttrs[6] = &( boneWeights1.front()[0] );
		attrModeMask |= 0x04000000ULL;
	}

	unsigned char	numUVs = (unsigned char) std::min< qsizetype >( std::max< qsizetype >( coords.size(), 0 ), 9 );
	std::uint64_t	tmp = 0;
	for ( unsigned char i = numUVs; i-- > 0; ) {
		tmp = tmp << 4;
		const auto &	c = coords.at( i );
		if ( c.size() >= numVerts ) [[likely]] {
			vertexAttrs[i + 7] = &( c.constFirst()[0] );
			tmp |= 0x20000000ULL;
		}
	}
	attrModeMask |= tmp;

	size_t	elementDataSize = size_t( numTriangles ) * sizeof( Triangle );
	if ( !( dataHash.attrMask && numVerts == dataHash.numVerts && elementDataSize == dataHash.elementBytes ) ) {
		dataHash = NifSkopeOpenGLContext::ShapeDataHash( std::uint32_t( numVerts ), attrModeMask, elementDataSize,
															vertexAttrs, triangles.constData() );
	}

	context->bindShape( dataHash, vertexAttrs, triangles.constData() );

	return true;
}
