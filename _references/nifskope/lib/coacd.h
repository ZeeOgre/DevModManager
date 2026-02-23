#ifndef COACD_H_INCLUDED
#define COACD_H_INCLUDED

#include <cstdint>
#include <vector>
#include <array>

class QSettings;

class CoACD
{
public:
	float	threshold = 0.05f;
	int		maxConvexHull = -1;
	int		preprocessMode = 0;		// 0 = auto, 1 = on, 2 = off
	int		prepResolution = 50;
	int		sampleResolution = 2000;
	int		mctsNodes = 20;
	int		mctsIteration = 150;
	int		mctsMaxDepth = 3;
	bool	pca = false;
	bool	merge = false;
	bool	decimate = true;
	int		maxCHVertex = 256;
	bool	extrude = false;
	float	extrudeMargin = 0.0f;
	int		apxMode = 0;			// 0 = ch, 1 = box
	int		seed = 0;

	struct Mesh {
		std::vector< std::array< double, 3 > >	vertices;
		std::vector< std::array< int, 3 > >		indices;
	};

	std::vector< Mesh > processMesh( const Mesh & m );
	void loadSettings( QSettings & settings );
	void saveSettings( QSettings & settings );

	// CoACD C library interface

	struct MeshDataC {
		double *	vertices;
		std::uint64_t	numVerts;
		int *		indices;
		std::uint64_t	numTriangles;
	};

	struct MeshArrayC {
		MeshDataC *	meshes;
		std::uint64_t	numMeshes;
	};

	typedef void ( *fnSetLogLevel )( const char * );
	typedef MeshArrayC ( *fnRun )( const MeshDataC *, double, int, int, int, int, int, int, int, bool, bool,
									bool, int, bool, double, int, unsigned int );
	typedef void ( *fnFreeMeshArray )( MeshArrayC m );
};

#endif
