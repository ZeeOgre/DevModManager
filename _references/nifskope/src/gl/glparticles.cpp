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

#include "glparticles.h"

#include "gl/controllers.h"
#include "gl/glscene.h"
#include "gl/renderer.h"
#include "glview.h"
#include "model/nifmodel.h"


/*
 *  Particle
 */

void Particles::clear()
{
	Node::clear();

	verts.clear();
	colors.clear();
	sizes.clear();
}

void Particles::updateImpl( const NifModel * nif, const QModelIndex & index )
{
	Node::updateImpl( nif, index );

	if ( index == iBlock ) {
		for (const auto link : nif->getChildLinks(id())) {
			QModelIndex iChild = nif->getBlockIndex(link);

			if (!iChild.isValid())
				continue;

			if (nif->blockInherits(iChild, "NiParticlesData")) {
				iData = iChild;
				updateData = true;
			}
		}
	}

	if ( index == iData )
		updateData = true;
}

void Particles::setController( const NifModel * nif, const QModelIndex & index )
{
	auto contrName = nif->itemName(index);
	if ( contrName == "NiParticleSystemController" || contrName == "NiBSPArrayController" ) {
		Controller * ctrl = new ParticleController( this, index );
		registerController(nif, ctrl);
	} else {
		Node::setController( nif, index );
	}
}

void Particles::transform()
{
	auto nif = NifModel::fromValidIndex(iBlock);
	if ( !nif ) {
		clear();
		return;
	}

	if ( updateData ) {
		updateData = false;

		verts  = nif->getArray<Vector3>( nif->getIndex( iData, "Vertices" ) );
		colors = nif->getArray<Color4>( nif->getIndex( iData, "Vertex Colors" ) );
		sizes  = nif->getArray<float>( nif->getIndex( iData, "Sizes" ) );

		active = nif->get<int>( iData, "Num Valid" );
		size = nif->get<float>( iData, "Active Radius" );
	}

	Node::transform();
}

void Particles::transformShapes()
{
	Node::transformShapes();
}

BoundSphere Particles::bounds() const
{
	BoundSphere sphere( verts );
	sphere.radius += size;
	return worldTrans() * sphere | Node::bounds();
}

void Particles::drawShapes( NodeList * secondPass )
{
	if ( isHidden() || scene->selecting > (unsigned char) Scene::SelObject || !scene->renderer || !scene->nifModel
		|| verts.isEmpty() || active < 1 ) {
		return;
	}

	AlphaProperty * aprop = findProperty<AlphaProperty>();

	if ( aprop && aprop->hasAlphaBlend() && secondPass ) {
		secondPass->add( this );
		return;
	}

	auto	prog = scene->renderer->useProgram( !scene->selecting ? "particles.prog" : "selection.prog" );
	if ( !prog )
		return;

	prog->uni4m( "modelViewMatrix", viewTrans().toMatrix4() );

	if ( scene->selecting ) {
		prog->uni1i( "selectionFlags", 0x0001 );
		prog->uni1i( "selectionParam", scene->nifModel->getBlockNumber( iBlock ) );
		glPointSize( GLView::Settings::vertexSelectPointSize );
		glDisable( GL_BLEND );

	} else {
		float	s2 = size * worldTrans().scale;
		prog->uni2f( "particleScale", s2, s2 );

		// setup blending

		AlphaProperty::glProperty( aprop, prog );

		// setup vertex colors

		VertexColorProperty::glProperty( nullptr, FloatVector4( colors.size() < verts.size() ? 1.0f : 0.0f ), prog );

		// setup material

		MaterialProperty::glProperty( findProperty<MaterialProperty>(), findProperty<SpecularProperty>(), prog );
		prog->uni4f( "frontMaterialEmission", FloatVector4( 0.0f ) );

		// setup texturing

		for ( int i = 0; i < TexturingProperty::numTextures; i++ ) {
			prog->uni1i_l( prog->uniLocation( "textureUnits[%d]", i ), 0 );
			prog->uni1i_l( prog->uniLocation( "textures[%d].textureUnit", i ), 0 );
		}

		int	stage = 0;

		if ( auto p = findProperty<TexturingProperty>(); p )
			stage += int( p->bind( 0, 0, prog ) );

		if ( auto p = findProperty<BSShaderLightingProperty>(); p ) {
			if ( p->bind( 0 ) ) {
				prog->uni1i_l( prog->uniLocation( "textureUnits[%d]", stage ), stage );
				stage++;
			}
			prog->uni2f_l( prog->uniLocation( "textures[%d].uvCenter", 0 ), 0.5f, 0.5f );
			prog->uni2f_l( prog->uniLocation( "textures[%d].uvScale", 0 ), 1.0f, 1.0f );
			prog->uni2f_l( prog->uniLocation( "textures[%d].uvOffset", 0 ), 0.0f, 0.0f );
			prog->uni1f_l( prog->uniLocation( "textures[%d].uvRotation", 0 ), 0.0f );
			prog->uni1i_l( prog->uniLocation( "textures[%d].coordSet", 0 ), 0 );
			prog->uni1i_l( prog->uniLocation( "textures[%d].textureUnit", 0 ), stage );
		}

		if ( !stage ) {
			static const QString	defaultTexture = "#FFFFFFFF";
			scene->textures->activateTextureUnit( 0 );
			scene->textures->bind( defaultTexture, scene->nifModel );
		}
	}

	// setup z buffer

	ZBufferProperty::glProperty( findProperty<ZBufferProperty>() );

	// setup stencil

	StencilProperty::glProperty( findProperty<StencilProperty>() );

	// wireframe ?

#if 0
	WireframeProperty::glProperty( findProperty<WireframeProperty>() );
#endif

	// render the particles

	qsizetype	numVerts = verts.size();
	const float *	attrData[5] = { &( verts.constFirst()[0] ), nullptr, nullptr, nullptr, nullptr };
	unsigned int	attrMask = 0x03;
	if ( colors.size() >= numVerts ) {
		attrData[1] = &( colors.constFirst()[0] );
		attrMask = 0x43;
	}
	if ( sizes.size() >= numVerts ) {
		attrData[4] = sizes.constData();
		attrMask = attrMask | 0x00010000;
	}
	scene->renderer->bindShape( (unsigned int) numVerts, attrMask, 0, attrData, nullptr );

	if ( active < numVerts )
		numVerts = active;
	scene->renderer->fn->glDrawArrays( GL_POINTS, 0, GLsizei( numVerts ) );
}
