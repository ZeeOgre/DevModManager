#include "nvtristripwrapper.h"
#include "data/niftypes.h"

#include "meshoptimizer/src/meshoptimizer.h"


QVector<QVector<quint16> > stripify( const QVector<Triangle> & triangles, bool stitch )
{
	QVector<QVector<quint16>>	strips;

	if ( triangles.isEmpty() )
		return strips;

	size_t	numTriangles = size_t( triangles.size() );
	std::vector< unsigned int >	triangleBuf( numTriangles * 3 );
	std::vector< unsigned int >	tristripBuf( meshopt_stripifyBound( triangleBuf.size() ) );

	quint16	maxVertex = 0;
	for ( size_t i = 0; i < numTriangles; i++ ) {
		const Triangle &	t = triangles.at( qsizetype(i) );
		quint16	v0 = t[0];
		quint16	v1 = t[1];
		quint16	v2 = t[2];
		maxVertex = std::max( maxVertex, std::max( v0, std::max( v1, v2 ) ) );
		tristripBuf[i * 3] = v0;
		tristripBuf[i * 3 + 1] = v1;
		tristripBuf[i * 3 + 2] = v2;
	}
	meshopt_optimizeVertexCacheStrip( triangleBuf.data(), tristripBuf.data(), triangleBuf.size(),
										size_t( maxVertex ) + 1 );

	size_t	indicesCnt = meshopt_stripify( tristripBuf.data(), triangleBuf.data(), triangleBuf.size(),
											size_t( maxVertex ) + 1, (unsigned int) stitch - 1U );

	for ( size_t i = 0; i < indicesCnt; i++ ) {
		size_t	j = 0;
		for ( ; ( i + j ) < indicesCnt; j++ ) {
			if ( tristripBuf[i + j] == (unsigned int) -1 )
				break;
		}
		if ( j > 0 ) {
			strips.append( QVector<quint16>() );
			strips.last().resize( qsizetype( j ) );
			quint16 *	stripData = strips.last().data();
			do {
				*stripData = quint16( tristripBuf[i] );
				i++;
				stripData++;
			} while ( --j );
		}
	}

	return strips;
}

QVector<Triangle> triangulate( const QVector<quint16> & strip )
{
	QVector<Triangle> tris;
	quint16 a, b = strip.value( 0 ), c = strip.value( 1 );
	bool flip = false;

	for ( int s = 2; s < strip.count(); s++ ) {
		a = b;
		b = c;
		c = strip.value( s );

		if ( a != b && b != c && c != a ) {
			if ( !flip )
				tris.append( Triangle( a, b, c ) );
			else
				tris.append( Triangle( a, c, b ) );
		}

		flip = !flip;
	}

	return tris;
}

QVector<Triangle> triangulate( const QVector< QVector<quint16> > & strips )
{
	QVector<Triangle> tris;
	for ( const QVector<quint16>& strip : strips ) {
		tris += triangulate( strip );
	}
	return tris;
}

