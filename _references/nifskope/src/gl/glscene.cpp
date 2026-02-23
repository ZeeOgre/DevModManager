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

#include "glscene.h"

#include "gl/renderer.h"
#include "gl/gltex.h"
#include "gl/glcontroller.h"
#include "gl/glmesh.h"
#include "gl/bsshape.h"
#include "gl/BSMesh.h"
#include "gl/glparticles.h"
#include "gl/gltex.h"
#include "model/nifmodel.h"
#include "glview.h"
#include "nifskope.h"

#include <QAction>
#include <QOpenGLContext>
#include <QOpenGLFunctions>
#include <QSettings>


//! \file glscene.cpp %Scene management

Scene::Scene( TexCache * texcache, QObject * parent ) :
	QObject( parent )
{
	currentBlock = currentIndex = QModelIndex();
	selecting = 0;
	animate = true;
	defaultSkeletonRoot = -1;

	time = 0.0;
	sceneBoundsValid = timeBoundsValid = false;

	textures = texcache;

	options = ( DoLighting | DoTexturing | DoMultisampling | DoBlending | DoVertexColors | DoSpecular | DoGlow | DoCubeMapping );

	lodLevel = Level0;

	visMode = VisNone;

	selMode = SelObject;

	// Startup Defaults

	QSettings settings;
	settings.beginGroup( "Settings/Render/General/Startup Defaults" );

	if ( settings.value( "Show Axes", true ).toBool() )
		options |= ShowAxes;
	if ( settings.value( "Show Grid", true ).toBool() )
		options |= ShowGrid;
	if ( settings.value( "Show Collision" ).toBool() )
		options |= ShowCollision;
	if ( settings.value( "Show Constraints" ).toBool() )
		options |= ShowConstraints;
	if ( settings.value( "Show Markers" ).toBool() )
		options |= ShowMarkers;
	if ( settings.value( "Show Nodes" ).toBool() )
		options |= ShowNodes;
	if ( settings.value( "Show Hidden" ).toBool() )
		options |= ShowHidden;
	if ( settings.value( "Do Skinning", true ).toBool() )
		options |= DoSkinning;
	if ( settings.value( "Do Error Color", true ).toBool() )
		options |= DoErrorColor;

	settings.endGroup();

	currentGLColor = FloatVector4( 0.0f );
	currentGLLineWidth = 1.0f;
	currentGLPointSize = 1.0f;
	currentModelViewMatrix = modelViewMatrixStack;

	updateSettings( settings );
}

Scene::~Scene()
{
	if ( renderer )
		delete renderer;
}

void Scene::setOpenGLContext( QOpenGLContext * context )
{
	if ( renderer || !context )
		return;
	renderer = new Renderer( context );
}

void Scene::updateShaders()
{
	renderer->updateShaders();
}

void Scene::updateSettings( QSettings & settings )
{
	settings.beginGroup( "Settings/Render/Colors/" );

	gridColor = FloatVector4( Color4( settings.value( "Grid Color", QColor( 99, 99, 99, 204 ) ).value<QColor>() ) );
	highlightColor = FloatVector4( Color4( settings.value( "Highlight", QColor( 255, 255, 0 ) ).value<QColor>() ) );
	wireframeColor = FloatVector4( Color4( settings.value( "Wireframe", QColor( 0, 255, 0 ) ).value<QColor>() ) );

	settings.endGroup();

	int	tmp = settings.value( "Settings/Render/General/Default Skeleton Root", -1 ).toInt();
	defaultSkeletonRoot = std::int16_t( std::clamp< int >( tmp, -1, 32767 ) );
}

void Scene::clear( [[maybe_unused]] bool flushTextures )
{
	nodes.clear();
	properties.clear();
	roots.clear();
	shapes.clear();

	animGroups.clear();
	animTags.clear();

	//if ( flushTextures )
	textures->flush();
	if ( renderer )
		renderer->flushCache();

	sceneBoundsValid = timeBoundsValid = false;

	nifModel = nullptr;
}

void Scene::update( const NifModel * nif, const QModelIndex & index )
{
	if ( !nif )
		return;

	nifModel = nif;

	if ( index.isValid() ) {
		QModelIndex block = nif->getBlockIndex( index );
		if ( !block.isValid() )
			return;

		for ( Property * prop : properties )
			prop->update( nif, block );

		for ( Node * node : nodes.list() )
			node->update( nif, block );
	} else {
		properties.validate();
		nodes.validate();

		for ( Property * p : properties )
			p->update( nif, p->index() );

		for ( Node * n : nodes.list() )
			n->update( nif, n->index() );

		roots.clear();
		for ( const auto link : nif->getRootLinks() ) {
			QModelIndex iBlock = nif->getBlockIndex( link );
			if ( iBlock.isValid() ) {
				Node * node = getNode( nif, iBlock );
				if ( node ) {
					node->makeParent( 0 );
					roots.add( node );
				}
			}
		}
	}

	timeBoundsValid = false;
}

void Scene::updateSceneOptions( bool checked )
{
	Q_UNUSED( checked );

	QAction * action = qobject_cast<QAction *>(sender());
	if ( action ) {
		options ^= SceneOptions( action->data().toInt() );
		emit sceneUpdated();
	}
}

void Scene::updateSceneOptionsGroup( QAction * action )
{
	if ( !action )
		return;

	options ^= SceneOptions( action->data().toInt() );
	emit sceneUpdated();
}

void Scene::updateSelectMode( QAction * action )
{
	if ( !action )
		return;

	selMode = SelMode( action->data().toInt() );
	emit sceneUpdated();
}

void Scene::updateLodLevel( int level )
{
	if ( Game::GameManager::get_game( nifModel ) != Game::STARFIELD )
		level = std::min( level, 2 );
	lodLevel = LodLevel( level );

	for ( Shape * s : shapes )
		s->updateLodLevel();
}

void Scene::make( NifModel * nif, bool flushTextures )
{
	clear( flushTextures );

	if ( !nif )
		return;

	update( nif, QModelIndex() );

	if ( !animGroups.contains( animGroup ) ) {
		if ( animGroups.isEmpty() )
			animGroup = QString();
		else
			animGroup = animGroups.first();
	}

	setSequence( animGroup );
}

Node * Scene::getNode( const NifModel * nif, const QModelIndex & iNode )
{
	if ( !nif || !iNode.isValid() )
		return nullptr;

	Node * node = nodes.get( iNode );

	if ( node )
		return node;

	auto nodeName = nif->itemName(iNode);
	if ( nif->blockInherits( iNode, "NiNode" ) ) {
		if ( nodeName == "NiLODNode" )
			node = new LODNode( this, iNode );
		else if ( nodeName == "NiBillboardNode" )
			node = new BillboardNode( this, iNode );
		else
			node = new Node( this, iNode );
	} else if ( nodeName == "NiTriShape" || nodeName == "NiTriStrips" || nif->blockInherits( iNode, "NiTriBasedGeom" ) ) {
		node = new Mesh( this, iNode );
		shapes += static_cast<Shape *>(node);
	} else if ( nif->checkVersion( 0x14050000, 0 ) && nodeName == "NiMesh" ) {
		node = new Mesh( this, iNode );
	}
	//else if ( nif->blockInherits( iNode, "AParticleNode" ) || nif->blockInherits( iNode, "AParticleSystem" ) )
	else if ( nif->blockInherits( iNode, "NiParticles" ) ) {
		// ... where did AParticleSystem go?
		node = new Particles( this, iNode );
	} else if ( nif->blockInherits( iNode, "BSTriShape" ) ) {
		node = new BSShape( this, iNode );
		shapes += static_cast<Shape *>(node);
	} else if ( nif->blockInherits(iNode, "BSGeometry") ) {
		node = new BSMesh(this, iNode);
		shapes += static_cast<Shape*>(node);
	} else if ( nif->blockInherits( iNode, "NiAVObject" ) ) {
		if ( nodeName == "BSTreeNode" )
			node = new Node( this, iNode );
	}

	if ( node ) {
		nodes.add( node );
		node->update( nif, iNode );
	}

	return node;
}

Property * Scene::getProperty( const NifModel * nif, const QModelIndex & iProperty )
{
	Property * prop = properties.get( iProperty );
	if ( prop )
		return prop;

	prop = Property::create( this, nif, iProperty );
	if ( prop )
		properties.add( prop );
	return prop;
}

Property * Scene::getProperty( const NifModel * nif, const QModelIndex & iParentBlock, const QString & itemName, const QString & mustInherit )
{
	QModelIndex iPropertyBlock = nif->getBlockIndex( nif->getLink(iParentBlock, itemName) );
	if ( iPropertyBlock.isValid() && nif->blockInherits(iPropertyBlock, mustInherit) )
		return getProperty( nif, iPropertyBlock );
	return nullptr;
}

void Scene::setSequence( const QString & seqname )
{
	animGroup = seqname;

	for ( Node * node : nodes.list() ) {
		node->setSequence( seqname );
	}
	for ( Property * prop : properties ) {
		prop->setSequence( seqname );
	}

	timeBoundsValid = false;
}

void Scene::transform( const Transform & trans, float time )
{
	view = trans;
	this->time = time;

	transformCache.clear();

	for ( Property * prop : properties ) {
		prop->transform();
	}
	for ( Node * node : roots.list() ) {
		node->transform();
	}
	for ( Node * node : roots.list() ) {
		node->transformShapes();
	}

	sceneBoundsValid = false;

	// TODO: purge unused textures
}

void Scene::draw()
{
	drawShapes();

	glDisable( GL_CULL_FACE );
	glDisable( GL_STENCIL_TEST );
	glDisable( GL_FRAMEBUFFER_SRGB );

	if ( hasOption(ShowNodes) )
		drawNodes();
	if ( hasOption(ShowCollision) )
		drawHavok();
	if ( hasOption(ShowMarkers) )
		drawFurn();

	drawSelection();
}

void Scene::drawShapes()
{
	if ( hasOption(DoBlending) ) {
		NodeList secondPass;

		for ( Node * node : roots.list() ) {
			node->drawShapes( &secondPass );
		}

		drawGrid();

		if ( secondPass.list().count() > 0 )
			drawSelection(); // for transparency pass

		secondPass.alphaSort();

		for ( Node * node : secondPass.list() ) {
			node->drawShapes();
		}
	} else {
		for ( Node * node : roots.list() ) {
			node->drawShapes();
		}

		drawGrid();
	}
}

void Scene::drawGrid()
{
	// Draw the grid
	NifSkope *	w;
	if ( !hasOption(ShowGrid) || !nifModel || ( w = dynamic_cast< NifSkope * >( nifModel->getWindow() ) ) == nullptr )
		return;
	GLView *	v = w->getGLView();
	if ( !v )
		return;

	glDisable( GL_FRAMEBUFFER_SRGB );
	glEnable( GL_DEPTH_TEST );
	glDepthMask( GL_TRUE );
	glDepthFunc( GL_LESS );

	FloatVector4	c0 = gridColor;
	FloatVector4	c1( 1.0f, 0.0f, 0.0f, 1.0f );
	FloatVector4	c2( 0.0f, 1.0f, 0.0f, 1.0f );

	// Keep the grid "grounded" regardless of Up Axis
	Transform gridTrans = view;
	if ( v->cfg.upAxis != GLView::ZAxis ) {
		static const float	axisRotation[27] = {
			1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,  0.0f, 0.0f, 1.0f,		// Z
			0.0f, 1.0f, 0.0f,  0.0f, 0.0f, 1.0f,  1.0f, 0.0f, 0.0f,		// Y
			0.0f, 0.0f, 1.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f		// X
		};
		const float *	ap = axisRotation;
		if ( v->cfg.upAxis == GLView::YAxis ) {
			ap = ap + 9;
			c2 = c1;
			c1.shuffleValues( 0xC6 );	// 2, 1, 0, 3
		} else if ( v->cfg.upAxis == GLView::XAxis ) {
			ap = ap + 18;
			c1 = c2;
			c2.shuffleValues( 0xD8 );	// 0, 2, 1, 3
		}
		gridTrans.rotation = gridTrans.rotation * Matrix( ap );
	}
	c1 = ( ( c1 + c0 ) * 0.5f ).blendValues( c0, 0x08 );
	c2 = ( ( c2 + c0 ) * 0.5f ).blendValues( c0, 0x08 );

	loadModelViewMatrix( gridTrans );

	// TODO: Configurable grid in Settings
	// 1024 game units, major lines every 128, minor lines every 64
	drawGrid( ( nifModel->getBSVersion() >= 170 ? 16.0f : 1024.0f ), 16, 2, c0, c1, c2 );
}

void Scene::drawNodes()
{
	for ( Node * node : roots.list() ) {
		node->draw();
	}
}

void Scene::drawHavok()
{
	for ( Node * node : roots.list() ) {
		node->drawHavok();
	}
}

void Scene::drawFurn()
{
	for ( Node * node : roots.list() ) {
		node->drawFurn();
	}
}

void Scene::drawSelection() const
{
	if ( selecting )
		return; // do not render the selection when selecting

	for ( Node * node : nodes.list() ) {
		node->drawSelection();
	}
}

BoundSphere Scene::bounds() const
{
	if ( !sceneBoundsValid ) {
		bndSphere = BoundSphere();
		for ( Node * node : nodes.list() ) {
			if ( node->isVisible() )
				bndSphere |= node->bounds();
		}
		sceneBoundsValid = true;
	}

	return bndSphere;
}

void Scene::updateTimeBounds() const
{
	if ( !nodes.list().isEmpty() ) {
		tMin = +1000000000; tMax = -1000000000;
		for ( Node * node : nodes.list() ) {
			node->timeBounds( tMin, tMax );
		}
		for ( Property * prop : properties ) {
			prop->timeBounds( tMin, tMax );
		}
	} else {
		tMin = tMax = 0;
	}

	timeBoundsValid = true;
}

float Scene::timeMin() const
{
	if ( animTags.contains( animGroup ) ) {
		if ( animTags[ animGroup ].contains( "start" ) )
			return animTags[ animGroup ][ "start" ];
	}

	if ( !timeBoundsValid )
		updateTimeBounds();

	return ( tMin > tMax ? 0 : tMin );
}

float Scene::timeMax() const
{
	if ( animTags.contains( animGroup ) ) {
		if ( animTags[ animGroup ].contains( "end" ) )
			return animTags[ animGroup ][ "end" ];
	}

	if ( !timeBoundsValid )
		updateTimeBounds();

	return ( tMin > tMax ? 0 : tMax );
}

QString Scene::textStats()
{
	for ( Node * node : nodes.list() ) {
		if ( node->index() == currentBlock ) {
			return node->textStats();
		}
	}
	return QString();
}


Scene::TransformCache::TransformCache()
	: hashTable( nullptr ), hashMask( 0 ), numTransforms( 0 )
{
	rehashCache();
}

Scene::TransformCache::~TransformCache()
{
	delete[] hashTable;
}

const Transform * Scene::TransformCache::find( std::uint64_t key ) const
{
	std::uint64_t	h = 0xFFFFFFFFU;
	hashFunctionUInt64( h, key );
	const std::pair< Transform *, std::uint64_t > *	p = hashTable;
	std::uint32_t	m = hashMask;
	for ( size_t i = size_t( h & m ); p[i].first; i = ( i + 1 ) & m ) {
		if ( p[i].second == key ) [[likely]]
			return p[i].first;
	}
	return nullptr;
}

bool Scene::TransformCache::find( Transform *& valuePtr, std::uint64_t key )
{
	std::uint64_t	h = 0xFFFFFFFFU;
	hashFunctionUInt64( h, key );
	std::pair< Transform *, std::uint64_t > *	p = hashTable;
	std::uint32_t	m = hashMask;
	size_t	i = size_t( h & m );
	for ( ; p[i].first; i = ( i + 1 ) & m ) {
		if ( p[i].second == key ) [[likely]] {
			valuePtr = p[i].first;
			return true;
		}
	}

	Transform *	t = transformBuf.allocateObject< Transform >();
	p[i].first = t;
	p[i].second = key;
	valuePtr = t;
	numTransforms++;
	if ( ( size_t( numTransforms ) * 3 ) >= ( size_t( m ) * 2 ) ) [[unlikely]]
		rehashCache();

	return false;
}

void Scene::TransformCache::rehashCache()
{
	size_t	m = hashMask;
	while ( ( size_t( numTransforms ) * 3 ) >= ( m * 2 ) )
		m = ( m << 1 ) | 0x3F;
	size_t	n = m + 1;
	std::pair< Transform *, std::uint64_t > *	tmp = new std::pair< Transform *, std::uint64_t >[n]();
	if ( numTransforms > 0 ) {
		for ( size_t i = 0; i <= hashMask; i++ ) {
			if ( !hashTable[i].first )
				continue;
			std::uint64_t	h = 0xFFFFFFFFU;
			hashFunctionUInt64( h, hashTable[i].second );
			size_t	j = size_t( h & m );
			while ( tmp[j].first )
				j = ( j + 1 ) & m;
			tmp[j] = hashTable[i];
		}
	}
	delete[] hashTable;
	hashTable = tmp;
	hashMask = std::uint32_t( m );
}

void Scene::TransformCache::clear()
{
	delete[] hashTable;
	hashTable = nullptr;
	hashMask = 0;
	numTransforms = 0;
	transformBuf.clear();
	rehashCache();
}
