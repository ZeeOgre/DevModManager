#include "coacd.h"

#include <QCoreApplication>
#include <QDir>
#include <QLibrary>
#include <QMessageBox>
#include <QSettings>
#include <QString>
#include <cstdlib>
#include <cstring>

std::vector< CoACD::Mesh > CoACD::processMesh( const Mesh & m )
{
	std::vector< Mesh >	coacdOutput;

#ifdef Q_OS_WIN32
	QLibrary	coacdLib( QLatin1StringView("lib_coacd") );
	coacdLib.load();
#else
	QLibrary	coacdLib( QDir( QCoreApplication::applicationDirPath() ).filePath( QLatin1StringView("coacd/lib_coacd") ) );
	if ( !coacdLib.load() ) {
		coacdLib.setFileName( QLatin1StringView("_coacd") );
		coacdLib.load();
	}
#endif
	if ( !coacdLib.isLoaded() ) {
		QMessageBox::critical( nullptr, "NifSkope error", QLatin1StringView( "Failed to load CoACD library" ) );
	} else if ( !( m.vertices.empty() || m.indices.empty() ) ) {
		fnSetLogLevel	setLogLevel = fnSetLogLevel( coacdLib.resolve( "CoACD_setLogLevel" ) );
		fnRun	coacdRun = fnRun( coacdLib.resolve( "CoACD_run" ) );
		fnFreeMeshArray	freeMeshArray = fnFreeMeshArray( coacdLib.resolve( "CoACD_freeMeshArray" ) );
		if ( coacdRun && freeMeshArray ) {
			if ( setLogLevel )
				setLogLevel( "error" );
			MeshDataC	tmp;
			tmp.vertices = const_cast< double * >( &( m.vertices.front()[0] ) );
			tmp.numVerts = std::uint64_t( m.vertices.size() );
			tmp.indices = const_cast< int * >( &( m.indices.front()[0] ) );
			tmp.numTriangles = std::uint64_t( m.indices.size() );
			MeshArrayC	tmp2 = coacdRun( &tmp, threshold, maxConvexHull, preprocessMode, prepResolution,
											sampleResolution, mctsNodes, mctsIteration, mctsMaxDepth, pca,
											merge, decimate, maxCHVertex, extrude, extrudeMargin, apxMode,
											(unsigned int) seed );
			if ( tmp2.meshes && tmp2.numMeshes ) {
				coacdOutput.resize( size_t( tmp2.numMeshes ) );
				for ( size_t i = 0; i < coacdOutput.size(); i++ ) {
					size_t	n = size_t( tmp2.meshes[i].numVerts );
					coacdOutput[i].vertices.resize( n );
					std::memcpy( coacdOutput[i].vertices.data(), tmp2.meshes[i].vertices, n * sizeof( double ) * 3 );
					n = size_t( tmp2.meshes[i].numTriangles );
					coacdOutput[i].indices.resize( n );
					std::memcpy( coacdOutput[i].indices.data(), tmp2.meshes[i].indices, n * sizeof( int ) * 3 );
				}
			}
			freeMeshArray( tmp2 );
		}
	}

	return coacdOutput;
}

void CoACD::loadSettings( QSettings & settings )
{
	threshold = settings.value( "Threshold", 0.05f ).toFloat();
	maxConvexHull = settings.value( "Max Convex Hull", -1 ).toInt();
	preprocessMode = settings.value( "Preprocess Mode", 0 ).toInt();
	prepResolution = settings.value( "Preprocess Resolution", 50 ).toInt();
	sampleResolution = settings.value( "Sample Resolution", 2000 ).toInt();
	mctsNodes = settings.value( "MCTS Nodes", 20 ).toInt();
	mctsIteration = settings.value( "MCTS Iteration", 150 ).toInt();
	mctsMaxDepth = settings.value( "MCTS Max Depth", 3 ).toInt();
	pca = settings.value( "PCA", false ).toBool();
	merge = ( maxConvexHull > 0 );
	maxCHVertex = settings.value( "Max Convex Hull Vertex", 256 ).toInt();
	decimate = ( maxCHVertex > 0 );
	extrudeMargin = settings.value( "Extrude Margin", 0.0f ).toFloat();
	extrude = ( extrudeMargin >= 0.00005f );
	apxMode = settings.value( "Approximation Mode", 0 ).toInt();
	seed = settings.value( "Seed", 0 ).toInt();
}

void CoACD::saveSettings( QSettings & settings )
{
	settings.setValue( "Threshold", QVariant( threshold ) );
	settings.setValue( "Max Convex Hull", QVariant( maxConvexHull ) );
	settings.setValue( "Preprocess Mode", QVariant( preprocessMode ) );
	settings.setValue( "Preprocess Resolution", QVariant( prepResolution ) );
	settings.setValue( "Sample Resolution", QVariant( sampleResolution ) );
	settings.setValue( "MCTS Nodes", QVariant( mctsNodes ) );
	settings.setValue( "MCTS Iteration", QVariant( mctsIteration ) );
	settings.setValue( "MCTS Max Depth", QVariant( mctsMaxDepth ) );
	settings.setValue( "PCA", QVariant( pca ) );
	settings.setValue( "Max Convex Hull Vertex", QVariant( maxCHVertex ) );
	settings.setValue( "Extrude Margin", QVariant( extrudeMargin ) );
	settings.setValue( "Approximation Mode", QVariant( apxMode ) );
	settings.setValue( "Seed", QVariant( seed ) );
}
