#include "mesh.h"
#include "gl/gltools.h"

#include <cfloat>
#include <unordered_set>

#include "fp32vec4.hpp"
#include "meshoptimizer/src/meshoptimizer.h"
#include "simplify.h"

// Brief description is deliberately not autolinked to class Spell
/*! \file simplify.cpp
 * \brief LOD generation spell
 *
 * All classes here inherit from the Spell class.
 */

//! Simplifies Starfield meshes
class spSimplifySFMesh final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Generate LODs" ); }
	QString page() const override final { return Spell::tr( "Mesh" ); }

	struct Meshes {
		size_t	totalIndices;
		size_t	totalVertices;
		std::vector< unsigned int >	indices;
		std::vector< unsigned int >	newIndices[3];
		std::vector< float >	positions;
		std::vector< std::uint32_t >	blockNumbers;
		std::vector< unsigned int >	blockVertexRanges;
		Meshes()
			: totalIndices( 0 ), totalVertices( 0 ), blockVertexRanges( 1, 0U )
		{
		}
		static void getTransform( Transform & t, const NifModel * nif, const QModelIndex & index );
		void loadGeometryData( const NifModel * nif, const QModelIndex & index );
		void simplifyMeshes( bool noMessages = false );
		int vertexBlockNum( unsigned int v ) const;
		void saveGeometryData( NifModel * nif ) const;
	};

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		if ( !( nif && nif->getBSVersion() >= 170 ) )
			return false;
		if ( !index.isValid() )
			return true;
		return nif->isNiBlock( index, "BSGeometry" );
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final;

	static QModelIndex cast_Static( NifModel * nif, const QModelIndex & index );
};

void spSimplifySFMesh::Meshes::getTransform( Transform & t, const NifModel * nif, const QModelIndex & index )
{
	if ( !index.isValid() )
		return;

	const NifItem *	i = nif->getItem( index );
	if ( i && nif->getIndex( i, "Scale" ).isValid() ) {
		if ( i->hasStrType( "BSMeshData" ) )
			t.scale *= nif->get<float>( i, "Scale" );
		else if ( nif->getIndex( i, "Translation" ).isValid() && nif->getIndex( i, "Rotation" ).isValid() )
			t = Transform( nif, index ) * t;
	}

	return getTransform( t, nif, index.parent() );
}

void spSimplifySFMesh::Meshes::loadGeometryData( const NifModel * nif, const QModelIndex & index )
{
	if ( !index.isValid() ) {
		for ( int b = 0; b < nif->getBlockCount(); b++ ) {
			auto	i = nif->getBlockIndex( qint32(b) );
			if ( i.isValid() )
				loadGeometryData( nif, i );
		}
		return;
	}
	if ( !( nif->blockInherits( index, "BSGeometry" ) && ( nif->get<quint32>(index, "Flags") & 0x0200 ) != 0 ) )
		return;
	int	blockNum = nif->getBlockNumber( index );
	if ( blockNum < 0 )
		return;

	QModelIndex	iMeshData = nif->getIndex( index, "Meshes" );
	if ( iMeshData.isValid() )
		iMeshData = nif->getIndex( iMeshData, 0 );
	if ( iMeshData.isValid() && nif->get<bool>( iMeshData, "Has Mesh" ) )
		iMeshData = nif->getIndex( iMeshData, "Mesh" );
	else
		return;
	if ( iMeshData.isValid() )
		iMeshData = nif->getIndex( iMeshData, "Mesh Data" );
	if ( !iMeshData.isValid() )
		return;

	std::uint32_t	numVerts = nif->get<quint32>( iMeshData, "Num Verts" );
	std::uint32_t	numTriangles = nif->get<quint32>( iMeshData, "Indices Size" ) / 3U;
	if ( numVerts < 3 || !numTriangles )
		return;
	std::uint32_t	weightsPerVertex = nif->get<quint32>( iMeshData, "Weights Per Vertex" );
	std::uint32_t	numUVs = nif->get<quint32>( iMeshData, "Num UVs" );
	std::uint32_t	numUVs2 = nif->get<quint32>( iMeshData, "Num UVs 2" );
	std::uint32_t	numColors = nif->get<quint32>( iMeshData, "Num Vertex Colors" );
	std::uint32_t	numNormals = nif->get<quint32>( iMeshData, "Num Normals" );
	std::uint32_t	numTangents = nif->get<quint32>( iMeshData, "Num Tangents" );
	std::uint32_t	numWeights = nif->get<quint32>( iMeshData, "Num Weights" );
	if ( ( numUVs && numUVs != numVerts ) || ( numUVs2 && numUVs2 != numVerts )
		|| ( numColors && numColors != numVerts ) || ( numNormals && numNormals != numVerts )
		|| ( numTangents && numTangents != numVerts ) || ( numWeights != ( size_t(numVerts) * weightsPerVertex ) ) ) {
		QMessageBox::critical( nullptr, "NifSkope error", QString("Mesh has inconsistent number of vertex attributes, cannot generate LODs") );
		return;
	}

	Transform	t;
	getTransform( t, nif, iMeshData );
	std::vector< float >	tmpPositions( size_t(numVerts) * 3 );
	if ( auto i = nif->getItem( iMeshData, "Vertices" ); i ) {
		for ( size_t j = 0; j < numVerts; j++ ) {
			Vector3	tmp = t * nif->get<Vector3>( i->child( int(j) ) );
			tmpPositions[j * 3] = tmp[0];
			tmpPositions[j * 3 + 1] = tmp[1];
			tmpPositions[j * 3 + 2] = tmp[2];
		}
	}

	size_t	indicesCnt = size_t(numTriangles) * 3;
	std::vector< unsigned int >	tmpIndices( indicesCnt );
	if ( auto i = nif->getItem( iMeshData, "Triangles" ); i ) {
		for ( size_t j = 0; j < numTriangles; j++ ) {
			Triangle	tmp = nif->get<Triangle>( i->child( int(j) ) );
			if ( tmp[0] >= numVerts || tmp[1] >= numVerts || tmp[2] >= numVerts ) {
				QMessageBox::critical( nullptr, "NifSkope error", QString("Mesh has invalid indices, cannot generate LODs") );
				return;
			}
			tmpIndices[j * 3] = (unsigned int) ( totalVertices + tmp[0] );
			tmpIndices[j * 3 + 1] = (unsigned int) ( totalVertices + tmp[1] );
			tmpIndices[j * 3 + 2] = (unsigned int) ( totalVertices + tmp[2] );
		}
	}

	totalIndices = totalIndices + indicesCnt;
	totalVertices = totalVertices + numVerts;
	indices.insert( indices.end(), tmpIndices.begin(), tmpIndices.end() );
	positions.insert( positions.end(), tmpPositions.begin(), tmpPositions.end() );
	blockNumbers.push_back( std::uint32_t(blockNum) );
	blockVertexRanges.push_back( (unsigned int) totalVertices );
}

void spSimplifySFMesh::Meshes::simplifyMeshes( bool noMessages )
{
	if ( blockNumbers.empty() || !( totalIndices >= 3 && totalVertices >= 1 ) )
		return;

	QSettings	settings;
	Vector3	err;
	int	numTriangles = int( totalIndices / 3 );
	for ( int l = 0; l < 3; l++ ) {
		float	x = 0.2f / float( 1 << l );
		x = settings.value( QString("Settings/Nif/Sf LOD Gen Target Cnt %1").arg(l + 1), x ).toFloat();
		float	targetCnt_f = std::min( std::max( x, 0.0f ), 1.0f );
		x = 0.005f * float( 1 << l );
		x = settings.value( QString("Settings/Nif/Sf LOD Gen Target Err %1").arg(l + 1), x ).toFloat();
		float	targetErr = std::min( std::max( x, 0.0f ), 1.0f );
		int	n = 200 >> l;
		n = settings.value( QString("Settings/Nif/Sf LOD Gen Min Tri Cnt %1").arg(l + 1), n ).toInt();
		size_t	minTriCnt = size_t( std::min< int >( std::max< int >( n, 0 ), 1000000 ) ) * blockNumbers.size();
		minTriCnt = std::min< size_t >( minTriCnt, 1000000000 );

		if ( !( targetCnt_f >= 0.0005f && targetErr < 0.99995f ) )
			break;

		newIndices[l].resize( totalIndices );
		size_t	newIndicesCnt = 0;
		int	targetCnt = std::max< int >( roundFloat( float( numTriangles ) * targetCnt_f ), int( minTriCnt ) );
		if ( targetCnt >= numTriangles ) {
			newIndicesCnt = totalIndices;
			std::memcpy( newIndices[l].data(), indices.data(), newIndicesCnt * sizeof(unsigned int) );
		} else {
			newIndicesCnt = meshopt_simplify(
								newIndices[l].data(), indices.data(), totalIndices,
								positions.data(), totalVertices, sizeof(float) * 3,
								size_t(targetCnt) * 3, targetErr, meshopt_SimplifyLockBorder, &(err[l]) );
		}
		newIndices[l].resize( newIndicesCnt );
	}

	if ( noMessages )
		return;
	QString	msg = QString( "LOD0: %1 triangles" ).arg( numTriangles );
	for ( int l = 0; l < 3; l++ )
		msg.append( QString("\nLOD%1: %2 triangles, error = %3").arg(l + 1).arg(newIndices[l].size() / 3).arg(err[l]) );
	QMessageBox::information( nullptr, "LOD generation results", msg );
}

int spSimplifySFMesh::Meshes::vertexBlockNum( unsigned int v ) const
{
	if ( blockVertexRanges.empty() || v >= blockVertexRanges.back() )
		return -1;
	size_t	n0 = 0;
	size_t	n2 = blockVertexRanges.size() - 1;
	while ( ( n0 + 1 ) < n2 ) {
		size_t	n1 = ( n0 + n2 ) >> 1;
		( v < blockVertexRanges[n1] ? n2 : n0 ) = n1;
	}
	return int( n0 );
}

void spSimplifySFMesh::Meshes::saveGeometryData( NifModel * nif ) const
{
	if ( blockNumbers.empty() || !( totalIndices >= 3 && totalVertices >= 1 ) )
		return;

	QVector< QVector< QVector< Triangle > > >	blockTriangles;
	blockTriangles.resize( qsizetype(blockNumbers.size()) );
	for ( auto & b : blockTriangles )
		b.resize( 3 );
	for ( int l = 0; l < 3; l++ ) {
		for ( size_t i = 0; ( i + 3 ) <= newIndices[l].size(); i = i + 3 ) {
			unsigned int	v0 = newIndices[l][i];
			unsigned int	v1 = newIndices[l][i + 1];
			unsigned int	v2 = newIndices[l][i + 2];
			int	b0 = vertexBlockNum( v0 );
			int	b1 = vertexBlockNum( v1 );
			int	b2 = vertexBlockNum( v2 );
			if ( ( b0 | b1 | b2 ) < 0 || b0 != b1 || b0 != b2 ) {
				QMessageBox::critical( nullptr, "NifSkope error", QString("spSimplifySFMesh: internal error: invalid index in simplified mesh data") );
				return;
			}
			v0 -= blockVertexRanges[b0];
			v1 -= blockVertexRanges[b0];
			v2 -= blockVertexRanges[b0];
			blockTriangles[b0][l].append( Triangle( quint16(v0), quint16(v1), quint16(v2) ) );
		}
	}

	for ( int b = 0; b < int( blockNumbers.size() ); b++ ) {
		QModelIndex	index = nif->getBlockIndex( qint32(blockNumbers[b]) );
		if ( !( index.isValid() && nif->blockInherits( index, "BSGeometry" ) ) ) {
			QMessageBox::critical( nullptr, "NifSkope error", QString("spSimplifySFMesh: internal error: block not found") );
			continue;
		}

		QModelIndex	iMeshData = nif->getIndex( index, "Meshes" );
		if ( iMeshData.isValid() )
			iMeshData = nif->getIndex( iMeshData, 0 );
		if ( iMeshData.isValid() && nif->get<bool>( iMeshData, "Has Mesh" ) )
			iMeshData = nif->getIndex( iMeshData, "Mesh" );
		else
			continue;
		if ( iMeshData.isValid() )
			iMeshData = nif->getIndex( iMeshData, "Mesh Data" );
		if ( !iMeshData.isValid() )
			continue;

		std::uint32_t	numVerts = nif->get<quint32>( iMeshData, "Num Verts" );
		std::uint32_t	weightsPerVertex = nif->get<quint32>( iMeshData, "Weights Per Vertex" );
		bool	isSkinned = ( weightsPerVertex != 0 || nif->getBlockIndex( nif->getLink( index, "Skin" ) ).isValid() );

		if ( auto i = nif->getItem( index ); i ) {
			i->invalidateVersionCondition();
			i->invalidateCondition();
		}
		nif->set<quint32>( iMeshData, "Version", std::max<quint32>( nif->get<quint32>( iMeshData, "Version" ), 1U ) );
		nif->set<quint32>( iMeshData, "Num LODs", 0 );
		if ( auto i = nif->getIndex( iMeshData, "LODs" ); i.isValid() )
			nif->updateArraySize( i );
		for ( int l = 0; l < 3; l++ ) {
			const QVector< Triangle > &	newTriangles = blockTriangles.at( b ).at( l );
			size_t	newIndicesCnt = size_t( newTriangles.size() ) * 3;

			QModelIndex	iMesh = nif->getIndex( index, "Meshes" );
			if ( iMesh.isValid() )
				iMesh = nif->getIndex( iMesh, l + 1 );
			if ( iMesh.isValid() )
				nif->set<bool>( iMesh, "Has Mesh", ( newIndicesCnt && !isSkinned ) );
			else if ( !isSkinned )
				continue;
			if ( !newIndicesCnt )
				continue;
			if ( !isSkinned ) {
				iMesh = nif->getIndex( iMesh, "Mesh" );
				if ( !iMesh.isValid() )
					continue;
				nif->set<quint32>( iMesh, "Indices Size", quint32(newIndicesCnt) );
				nif->set<quint32>( iMesh, "Num Verts", numVerts );
				nif->set<quint32>( iMesh, "Flags", 64 );
				iMesh = nif->getIndex( iMesh, "Mesh Data" );
				if ( !iMesh.isValid() )
					continue;
				nif->set<quint32>( iMesh, "Version", 2 );
				nif->set<quint32>( iMesh, "Indices Size", quint32(newIndicesCnt) );
				QModelIndex	i;
				if ( ( i = nif->getIndex( iMesh, "Triangles" ) ).isValid() ) {
					nif->updateArraySize( i );
					nif->setArray<Triangle>( i, newTriangles );
				}
				nif->set<float>( iMesh, "Scale", nif->get<float>( iMeshData, "Scale" ) );
				nif->set<quint32>( iMesh, "Weights Per Vertex", weightsPerVertex );
				nif->set<quint32>( iMesh, "Num Verts", numVerts );
				if ( ( i = nif->getIndex( iMesh, "Vertices" ) ).isValid() ) {
					nif->updateArraySize( i );
					nif->setArray<ShortVector3>( i, nif->getArray<ShortVector3>( iMeshData, "Vertices" ) );
				}
				nif->set<quint32>( iMesh, "Num UVs", nif->get<quint32>( iMeshData, "Num UVs" ) );
				if ( ( i = nif->getIndex( iMesh, "UVs" ) ).isValid() ) {
					nif->updateArraySize( i );
					nif->setArray<HalfVector2>( i, nif->getArray<HalfVector2>( iMeshData, "UVs" ) );
				}
				nif->set<quint32>( iMesh, "Num UVs 2", nif->get<quint32>( iMeshData, "Num UVs 2" ) );
				if ( ( i = nif->getIndex( iMesh, "UVs 2" ) ).isValid() ) {
					nif->updateArraySize( i );
					nif->setArray<HalfVector2>( i, nif->getArray<HalfVector2>( iMeshData, "UVs 2" ) );
				}
				nif->set<quint32>( iMesh, "Num Vertex Colors", nif->get<quint32>( iMeshData, "Num Vertex Colors" ) );
				if ( ( i = nif->getIndex( iMesh, "Vertex Colors" ) ).isValid() ) {
					nif->updateArraySize( i );
					nif->setArray<ByteColor4BGRA>( i, nif->getArray<ByteColor4BGRA>( iMeshData, "Vertex Colors" ) );
				}
				nif->set<quint32>( iMesh, "Num Normals", nif->get<quint32>( iMeshData, "Num Normals" ) );
				if ( ( i = nif->getIndex( iMesh, "Normals" ) ).isValid() ) {
					nif->updateArraySize( i );
					nif->setArray<UDecVector4>( i, nif->getArray<UDecVector4>( iMeshData, "Normals" ) );
				}
				nif->set<quint32>( iMesh, "Num Tangents", nif->get<quint32>( iMeshData, "Num Tangents" ) );
				if ( ( i = nif->getIndex( iMesh, "Tangents" ) ).isValid() ) {
					nif->updateArraySize( i );
					nif->setArray<UDecVector4>( i, nif->getArray<UDecVector4>( iMeshData, "Tangents" ) );
				}
				nif->set<quint32>( iMesh, "Num Weights", 0 );
				nif->set<quint32>( iMesh, "Num LODs", 0 );
				nif->set<quint32>( iMesh, "Num Meshlets", 0 );
				nif->set<quint32>( iMesh, "Num Cull Data", 0 );
			} else {
				NifItem *	i = nif->getItem( iMeshData );
				if ( i ) {
					i->invalidateVersionCondition();
					i->invalidateCondition();
				}
				nif->set<quint32>( iMeshData, "Num LODs", quint32(l + 1) );
				auto	lodsIndex = nif->getIndex( iMeshData, "LODs" );
				if ( lodsIndex.isValid() ) {
					nif->updateArraySize( lodsIndex );
					auto	lodIndex = nif->getIndex( lodsIndex, l );
					if ( lodIndex.isValid() ) {
						nif->set<quint32>( lodIndex, "Indices Size", quint32(newIndicesCnt) );
						QModelIndex	lodTrianglesIndex;
						if ( ( lodTrianglesIndex = nif->getIndex( lodIndex, "Triangles" ) ).isValid() ) {
							nif->updateArraySize( lodTrianglesIndex );
							nif->setArray<Triangle>( lodTrianglesIndex, newTriangles );
						}
					}
				}
			}
		}

		spRemoveWasteVertices::cast_Starfield( nif, index, true );
	}
}

QModelIndex spSimplifySFMesh::cast( NifModel * nif, const QModelIndex & index )
{
	if ( !( nif && nif->getBSVersion() >= 170 && nif->checkInternalGeometry( index ) ) )
		return index;

	Meshes	m;
	nif->setState( BaseModel::Processing );
	m.loadGeometryData( nif, index );
	m.simplifyMeshes( nif->getBatchProcessingMode() );
	m.saveGeometryData( nif );
	nif->restoreState();

	return index;
}

QModelIndex spSimplifySFMesh::cast_Static( NifModel * nif, const QModelIndex & index )
{
	spSimplifySFMesh	tmp;
	if ( tmp.isApplicable( nif, index ) )
		return tmp.cast( nif, index );
	return index;
}

REGISTER_SPELL( spSimplifySFMesh )


//! Simplifies a Skyrim SE, Fallout 4 or 76 mesh
class spSimplifyBSTriShape final : public spRemoveWasteVertices
{
public:
	QString name() const override final { return Spell::tr( "Simplify" ); }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		if ( !( nif && nif->getBSVersion() >= 100 && nif->getBSVersion() < 170 ) )
			return false;
		if ( !index.isValid() )
			return true;
		return ( nif->blockInherits( index, "BSTriShape" )
				&& nif->getIndex( nif->getBlockIndex( index ), "Vertex Data" ).isValid() );
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final;
	void cast_batchMode( NifModel * nif, const QModelIndex & index );
};

void SimplifyMeshDialog::findBlock( int blockNum )
{
	QModelIndex	iBlock = nif->getBlockIndex( blockNum );
	if ( !iBlock.isValid() && nif->blockInherits( iBlock, "BSTriShape" ) )
		return;
	if ( nif->getBSVersion() < 130 && nif->getBlockIndex( nif->getLink( iBlock, "Skin" ) ).isValid() ) {
		Message::append( nullptr, Spell::tr( "Simplify failed" ),
							Spell::tr( "Block %1: skinned meshes are not supported yet" ).arg( blockNum ),
							QMessageBox::Warning );
		return;
	}
	QModelIndex	iVertexData = nif->getIndex( iBlock, "Vertex Data" );
	QModelIndex	iTriangles = nif->getIndex( iBlock, "Triangles" );
	if ( !( iVertexData.isValid() && iTriangles.isValid() ) )
		return;
	quint32	vertCnt = nif->get<quint32>( iBlock, "Num Vertices" );
	quint32	triCnt = nif->get<quint32>( iBlock, "Num Triangles" );
	if ( !( vertCnt > 0 && triCnt > 0 ) )
		return;
	if ( nif->rowCount( iVertexData ) != int( vertCnt ) || nif->rowCount( iTriangles ) != int( triCnt ) ) {
		Message::append( nullptr, Spell::tr( "Simplify failed" ),
							Spell::tr( "Block %1: vertex or triangle count does not match array size" ).arg( blockNum ),
							QMessageBox::Warning );
		errorFlag = true;
		return;
	}

	blockNumbers.push_back( blockNum );
	if ( blockVertexRanges.empty() )
		blockVertexRanges.resize( 1, 0U );
	numVerts = numVerts + vertCnt;
	numTriangles = numTriangles + triCnt;
	blockVertexRanges.push_back( (unsigned int) numVerts );
	blockTriangleCounts.push_back( triCnt );
}

bool SimplifyMeshDialog::loadGeometryData()
{
	vertexAttrData.resize( numVerts * 12, 0.0f );
	indicesData.resize( numTriangles * 3, 0U );
	newIndicesData.resize( numTriangles * 3, 0U );

	size_t	numBlocks = blockNumbers.size();
	size_t	indexOffs = 0;
	for ( size_t n = 0; n < numBlocks; n++ ) {
		int	blockNum = blockNumbers[n];
		QModelIndex	iBlock = nif->getBlockIndex( blockNum );
		if ( !iBlock.isValid() && nif->blockInherits( iBlock, "BSTriShape" ) )
			continue;
		QModelIndex	iVertexData = nif->getIndex( iBlock, "Vertex Data" );
		QModelIndex	iTriangles = nif->getIndex( iBlock, "Triangles" );
		if ( !( iVertexData.isValid() && iTriangles.isValid() ) )
			continue;
		unsigned int	vertCnt = blockVertexRanges[n + 1] - blockVertexRanges[n];
		unsigned int	triCnt = blockTriangleCounts[n];
		if ( !( vertCnt && triCnt ) )
			continue;
		size_t	vertexOffs = blockVertexRanges[n];

		for ( size_t i = 0; i < triCnt; i++, indexOffs = indexOffs + 3 ) {
			Triangle	t;
			if ( QModelIndex iTriangle = nif->getIndex( iTriangles, int( i ) ); iTriangle.isValid() )
				t = nif->get<Triangle>( iTriangle );
			if ( t.v1() >= vertCnt || t.v2() >= vertCnt || t.v3() >= vertCnt ) {
				Message::append( nullptr, Spell::tr( "Simplify failed" ),
									Spell::tr( "Block %1: invalid index in triangle %2" ).arg( blockNum ).arg( i ),
									QMessageBox::Warning );
				errorFlag = true;
				return false;
			}
			indicesData[indexOffs] = vertexOffs + t.v1();
			indicesData[indexOffs + 1] = vertexOffs + t.v2();
			indicesData[indexOffs + 2] = vertexOffs + t.v3();
		}

		Transform	t;
		if ( numBlocks > 1 )
			spSimplifySFMesh::Meshes::getTransform( t, nif, iBlock );

		vertexOffs = vertexOffs * 12;
		for ( size_t i = 0; i < vertCnt; i++, vertexOffs = vertexOffs + 12 ) {
			if ( QModelIndex iVertex = nif->getIndex( iVertexData, int( i ) ); iVertex.isValid() ) {
				if ( QModelIndex v = nif->getIndex( iVertex, "Vertex" ); v.isValid() ) {
					Vector3	xyz = nif->get<Vector3>( v );
					if ( numBlocks > 1 )
						xyz = t * xyz;
					FloatVector4( xyz ).convertToVector3( vertexAttrData.data() + vertexOffs );
				}
				if ( QModelIndex v = nif->getIndex( iVertex, "Normal" ); v.isValid() ) {
					Vector3	n = nif->get<Vector3>( v );
					if ( numBlocks > 1 )
						n = t.rotation * n;
					FloatVector4( n ).convertToVector3( vertexAttrData.data() + ( vertexOffs + 3 ) );
				}
				if ( QModelIndex v = nif->getIndex( iVertex, "UV" ); v.isValid() ) {
					Vector2	coords = nif->get<Vector2>( v );
					vertexAttrData[vertexOffs + 6] = coords[0];
					vertexAttrData[vertexOffs + 7] = coords[1];
				}
				if ( QModelIndex v = nif->getIndex( iVertex, "Vertex Colors" ); v.isValid() ) {
					FloatVector4	c = FloatVector4( nif->get<Color4>( v ) );
					c.convertToFloats( vertexAttrData.data() + ( vertexOffs + 8 ) );
				}
			}
		}
	}

	return true;
}

int SimplifyMeshDialog::getVertexBlock( unsigned int v ) const
{
	size_t	i0 = 0;
	size_t	i2 = blockVertexRanges.size();
	while ( i2 > i0 ) {
		size_t	i1 = ( i0 + i2 ) >> 1;
		if ( v < blockVertexRanges[i1] )
			i2 = i1;
		else
			i0 = i1 + 1;
	}
	if ( i0 >= blockVertexRanges.size() ) [[unlikely]]
		return -1;
	return int( i0 - 1 );
}

void SimplifyMeshDialog::storeGeometryData( size_t newTriangleCnt )
{
	QVector< QVector< Triangle > >	newTriangles;
	qsizetype	numBlocks = qsizetype( blockNumbers.size() );
	newTriangles.resize( numBlocks );

	const unsigned int *	p = newIndicesData.data();
	if ( newTriangleCnt >= numTriangles ) {
		newTriangleCnt = numTriangles;
		p = indicesData.data();
	}

	std::vector< size_t >	triCnts( blockNumbers.size() );
	for ( size_t i = 0; i < newTriangleCnt; i++ ) {
		unsigned int	v0 = p[i * 3];
		unsigned int	v1 = p[i * 3 + 1];
		unsigned int	v2 = p[i * 3 + 2];
		int	b0 = getVertexBlock( v0 );
		int	b1 = getVertexBlock( v1 );
		int	b2 = getVertexBlock( v2 );
		if ( b0 < 0 || b0 != b1 || b0 != b2 ) {
			Message::append( nullptr, Spell::tr( "Simplify failed" ), QString( "Invalid indices in output data" ),
								QMessageBox::Critical );
			errorFlag = true;
			break;
		}
		triCnts[b0] = triCnts[b0] + 1;
	}
	if ( errorFlag )
		return;
	for ( qsizetype i = 0; i < numBlocks; i++ ) {
		newTriangles[i].resize( triCnts[i] );
		triCnts[i] = 0;
	}
	for ( size_t i = 0; i < newTriangleCnt; i++ ) {
		unsigned int	v0 = p[i * 3];
		unsigned int	v1 = p[i * 3 + 1];
		unsigned int	v2 = p[i * 3 + 2];
		int	n = getVertexBlock( v0 );
		v0 = v0 - blockVertexRanges[n];
		v1 = v1 - blockVertexRanges[n];
		v2 = v2 - blockVertexRanges[n];
		qsizetype	j = qsizetype( triCnts[n] );
		triCnts[n] = triCnts[n] + 1;
		newTriangles[n][j] = Triangle( quint16( v0 ), quint16( v1 ), quint16( v2 ) );
	}

	for ( qsizetype i = 0; i < numBlocks; i++ ) {
		int	blockNum = blockNumbers[i];
		QModelIndex	iBlock = nif->getBlockIndex( blockNum );
		if ( !iBlock.isValid() && nif->blockInherits( iBlock, "BSTriShape" ) )
			continue;
		if ( nif->getBSVersion() < 130 )
			nif->set<quint16>( iBlock, "Num Triangles", quint16(triCnts[i]) );
		else
			nif->set<quint32>( iBlock, "Num Triangles", quint32(triCnts[i]) );
		QModelIndex	iTriangles = nif->getIndex( iBlock, "Triangles" );
		if ( !iTriangles.isValid() )
			continue;
		nif->updateArraySize( iTriangles );
		nif->setArray<Triangle>( iTriangles, newTriangles[i] );
		spRemoveWasteVertices::updateBSTriShape( nif, iBlock );
	}
}

SimplifyMeshDialog::SimplifyMeshDialog( NifModel * nifModel, const QModelIndex & index )
	: nif( nifModel ), numVerts( 0 ), numTriangles( 0 ), batchMode( false ), errorFlag( false )
{
	if ( !index.isValid() ) {
		for ( int n = 0; n < nif->getBlockCount(); n++ ) {
			QModelIndex idx = nif->getBlockIndex( n );

			if ( idx.isValid() && nif->blockInherits( idx, "BSTriShape" )
				&& nif->getIndex( idx, "Vertex Data" ).isValid() ) {
				findBlock( n );
			}
		}
	} else {
		findBlock( nif->getBlockNumber( index ) );
	}

	if ( !errorFlag )
		loadGeometryData();
}

SimplifyMeshDialog::~SimplifyMeshDialog()
{
}

void SimplifyMeshDialog::updateIndices()
{
	if ( !batchMode ) {
		QSettings	settings;
		settings.beginGroup( "Spells/Mesh/Simplify" );
		settings.setValue( "Target Count", QVariant( float(targetCount->value()) ) );
		settings.setValue( "Max Error", QVariant( float(maxError->value()) ) );
		settings.setValue( "Min Triangles", QVariant( roundFloat( float(minTriangles->value()) ) ) );
		settings.setValue( "Lock Borders", QVariant( lockBorders->isChecked() ) );
		settings.setValue( "Prune", QVariant( enablePrune->isChecked() ) );
		settings.setValue( "Normal Weight", QVariant( float(normalWeight->value()) ) );
		settings.setValue( "UV Weight", QVariant( float(uvWeight->value()) ) );
		settings.setValue( "Color Weight", QVariant( float(colorWeight->value()) ) );
		settings.endGroup();
	}

	float	attrWeights[9] = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
	size_t	attrCnt = 0;
	if ( float w = float( normalWeight->value() ); w > 0.0005f ) {
		FloatVector4( w ).convertToVector3( attrWeights );
		attrCnt = 3;
	}
	if ( float w = float( uvWeight->value() ); w > 0.0005f ) {
		attrWeights[3] = w;
		attrWeights[4] = w;
		attrCnt = 5;
	}
	if ( float w = float( colorWeight->value() ); w > 0.0005f ) {
		FloatVector4( w ).convertToFloats( &(attrWeights[5]) );
		attrCnt = 9;
	}

	int	targetCnt =
		int( std::max< double >( targetCount->value() * double( int(numTriangles) ), minTriangles->value() ) + 0.5 );
	targetCnt = std::clamp< int >( targetCnt, 1, int( numTriangles ) );
	float	targetError = float( maxError->value() );
	unsigned int	simplifyOpts = ( lockBorders->isChecked() ? (unsigned int) meshopt_SimplifyLockBorder : 0U );
	if ( enablePrune->isChecked() )
		simplifyOpts = simplifyOpts | (unsigned int) meshopt_SimplifyPrune;
	float	err = 0.0f;
	size_t	newIndicesCnt;
	if ( attrCnt < 1 ) {
		newIndicesCnt =
			meshopt_simplify( newIndicesData.data(), indicesData.data(), indicesData.size(),
								vertexAttrData.data(), numVerts, sizeof( float ) * 12,
								size_t( targetCnt ) * 3, targetError, simplifyOpts, &err );
	} else {
		newIndicesCnt =
			meshopt_simplifyWithAttributes( newIndicesData.data(), indicesData.data(), indicesData.size(),
											vertexAttrData.data(), numVerts, sizeof( float ) * 12,
											vertexAttrData.data() + 3, sizeof( float ) * 12, attrWeights, attrCnt,
											nullptr, size_t( targetCnt ) * 3, targetError, simplifyOpts, &err );
	}

	size_t	newTriangleCnt = newIndicesCnt / 3;
	if ( !batchMode ) {
		resultTriangles->setText( QString( "Triangles: %1" ).arg( newTriangleCnt ) );
		resultError->setText( QString( "Error: %1" ).arg( QString::number( err, 'f', 6 ) ) );
	}

	storeGeometryData( newTriangleCnt );
}

int SimplifyMeshDialog::exec()
{
	setWindowTitle( Spell::tr( "Simplify Mesh" ) );

	QGridLayout * grid = new QGridLayout;
	setLayout( grid );

	targetCount = new QDoubleSpinBox;
	targetCount->setRange( 0.001, 1.0 );
	targetCount->setDecimals( 3 );
	targetCount->setSingleStep( 0.001 );

	grid->addWidget( new QLabel( Spell::tr( "Target Count" ) ), 0, 0, 1, 3 );
	grid->addWidget( targetCount, 0, 3 );

	maxError = new QDoubleSpinBox;
	maxError->setRange( 0.0001, 0.5 );
	maxError->setDecimals( 4 );
	maxError->setSingleStep( 0.0001 );

	grid->addWidget( new QLabel( Spell::tr( "Max. Error" ) ), 1, 0, 1, 3 );
	grid->addWidget( maxError, 1, 3 );

	minTriangles = new QDoubleSpinBox;
	minTriangles->setRange( 1.0, 100000.0 );
	minTriangles->setDecimals( 0 );
	minTriangles->setSingleStep( 1.0 );

	grid->addWidget( new QLabel( Spell::tr( "Min. Triangles" ) ), 2, 0, 1, 3 );
	grid->addWidget( minTriangles, 2, 3 );

	lockBorders = new QCheckBox;

	grid->addWidget( new QLabel( Spell::tr( "Lock Borders" ) ), 3, 0 );
	grid->addWidget( lockBorders, 3, 1 );

	enablePrune = new QCheckBox;

	{
		auto	tmpLabel = new QLabel( Spell::tr( "Prune" ) );
		tmpLabel->setAlignment( Qt::AlignRight );
		grid->addWidget( tmpLabel, 3, 2 );
	}
	grid->addWidget( enablePrune, 3, 3 );

	normalWeight = new QDoubleSpinBox;
	normalWeight->setRange( 0.0, 100.0 );
	normalWeight->setDecimals( 3 );
	normalWeight->setSingleStep( 0.001 );

	grid->addWidget( new QLabel( Spell::tr( "Normal Weight" ) ), 4, 0, 1, 3 );
	grid->addWidget( normalWeight, 4, 3 );

	uvWeight = new QDoubleSpinBox;
	uvWeight->setRange( 0.0, 100.0 );
	uvWeight->setDecimals( 3 );
	uvWeight->setSingleStep( 0.001 );

	grid->addWidget( new QLabel( Spell::tr( "UV Weight" ) ), 5, 0, 1, 3 );
	grid->addWidget( uvWeight, 5, 3 );

	colorWeight = new QDoubleSpinBox;
	colorWeight->setRange( 0.0, 100.0 );
	colorWeight->setDecimals( 3 );
	colorWeight->setSingleStep( 0.001 );

	grid->addWidget( new QLabel( Spell::tr( "Vertex Color Weight" ) ), 6, 0, 1, 3 );
	grid->addWidget( colorWeight, 6, 3 );

	{
		QSettings	settings;
		settings.beginGroup( "Spells/Mesh/Simplify" );
		targetCount->setValue( settings.value( "Target Count", 0.5f ).toFloat() );
		maxError->setValue( settings.value( "Max Error", 0.005f ).toFloat() );
		minTriangles->setValue( double( settings.value( "Min Triangles", 100 ).toInt() ) );
		lockBorders->setChecked( settings.value( "Lock Borders", true ).toBool() );
		enablePrune->setChecked( settings.value( "Prune", false ).toBool() );
		normalWeight->setValue( settings.value( "Normal Weight", 0.0f ).toFloat() );
		uvWeight->setValue( settings.value( "UV Weight", 0.0f ).toFloat() );
		colorWeight->setValue( settings.value( "Color Weight", 0.0f ).toFloat() );
		settings.endGroup();
	}

	resultTriangles = new QLabel( QString( "Triangles: %1" ).arg( numTriangles ) );
	grid->addWidget( resultTriangles, 7, 0, 1, 2 );

	resultError = new QLabel( QString( "Error: 0.000000" ) );
	grid->addWidget( resultError, 7, 2, 1, 2 );

	QPushButton * btUpdate = new QPushButton( Spell::tr( "Simplify" ) );
	QObject::connect( btUpdate, &QPushButton::clicked, this, &SimplifyMeshDialog::updateIndices );

	QPushButton * btOk = new QPushButton( Spell::tr( "Finish" ) );
	QObject::connect( btOk, &QPushButton::clicked, this, &QDialog::accept );

	QPushButton * btCancel = new QPushButton( Spell::tr( "Cancel" ) );
	QObject::connect( btCancel, &QPushButton::clicked, this, &QDialog::reject );

	grid->addWidget( btUpdate, 8, 0 );
	grid->addWidget( btOk, 8, 2 );
	grid->addWidget( btCancel, 8, 3 );

	if ( batchMode )
		return int( QDialog::Accepted );

	return QDialog::exec();
}

QModelIndex spSimplifyBSTriShape::cast( NifModel * nif, const QModelIndex & index )
{
	SimplifyMeshDialog	dlg( nif, index );
	if ( !dlg.isValid() )
		return index;

	if ( dlg.exec() == QDialog::Accepted ) {
		dlg.updateIndices();
		if ( !index.isValid() ) {
			for ( int blockNum : dlg.blockNumbers ) {
				QModelIndex	iBlock = nif->getBlockIndex( blockNum );
				if ( iBlock.isValid() )
					spRemoveWasteVertices::cast( nif, iBlock );
			}
		} else {
			spRemoveWasteVertices::cast( nif, index );
		}
	} else {
		// revert original geometry
		dlg.storeGeometryData( dlg.numTriangles );
	}

	return index;
}

void spSimplifyBSTriShape::cast_batchMode( NifModel * nif, const QModelIndex & index )
{
	SimplifyMeshDialog	dlg( nif, index );
	if ( !dlg.isValid() )
		return;
	dlg.batchMode = true;
	dlg.exec();
	dlg.updateIndices();
	spRemoveWasteVertices::cast( nif, index );
}

REGISTER_SPELL( spSimplifyBSTriShape )

class spSimplifyAllBSTriShapes final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Simplify All Shapes" ); }
	QString page() const override final { return Spell::tr( "Batch" ); }

	bool isApplicable( const NifModel * nif, const QModelIndex & idx ) override final
	{
		return ( nif && nif->getBSVersion() >= 100 && nif->getBSVersion() < 170 && !idx.isValid() );
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & ) override final
	{
		spSimplifyBSTriShape sp;

		for ( int n = 0; n < nif->getBlockCount(); n++ ) {
			QModelIndex idx = nif->getBlockIndex( n );

			if ( sp.isApplicable( nif, idx ) )
				sp.cast_batchMode( nif, idx );
		}

		return QModelIndex();
	}

	static QModelIndex cast_Static( NifModel * nif, const QModelIndex & index );
};

QModelIndex spSimplifyAllBSTriShapes::cast_Static( NifModel * nif, const QModelIndex & index )
{
	spSimplifyAllBSTriShapes	sp;
	return sp.cast( nif, index );
}

REGISTER_SPELL( spSimplifyAllBSTriShapes )

