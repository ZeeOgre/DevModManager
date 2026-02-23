#include "spellbook.h"

#include <QCoreApplication>
#include <QDataStream>
#include <QDir>
#include <QProcess>
#include <QTemporaryFile>

// Brief description is deliberately not autolinked to class Spell
/*! \file moppcode.cpp
 * \brief Havok MOPP spells
 *
 * Note that this code only works on the Windows platform due an external
 * dependency on the Havok SDK, with which NifMopp.dll is compiled.
 *
 * Most classes here inherit from the Spell class.
 */

struct HavokMoppCode
{
	Vector3 origin;
	float scale = 1.0f;
	QByteArray data;

	bool CalculateMoppCode( const QVector<int> & subshapeVerts, const QVector<Vector3> & verts,
							const QVector<Triangle> & triangles );
};

bool HavokMoppCode::CalculateMoppCode(
	const QVector<int> & subshapeVerts, const QVector<Vector3> & verts, const QVector<Triangle> & triangles )
{
	QTemporaryFile f;
	if ( !f.open() )
		return false;

	{
		QDataStream s( &f );
		s.setByteOrder( QDataStream::LittleEndian );
		s.setFloatingPointPrecision( QDataStream::SinglePrecision );

		s << quint32( 0x4853454D );		// "MESH"
		s << quint32( subshapeVerts.size() );
		for ( auto i : subshapeVerts )
			s << quint32( i );
		s << quint32( verts.size() );
		for ( const Vector3 & v : verts ) {
			s << float( v[0] );
			s << float( v[1] );
			s << float( v[2] );
		}
		s << quint32( triangles.size() );
		for ( const Triangle & t : triangles ) {
			s << quint16( t[0] );
			s << quint16( t[1] );
			s << quint16( t[2] );
		}
	}
	f.close();

	QString nifMoppPath = QDir( QCoreApplication::applicationDirPath() ).filePath( "NifMopp.exe" );
#ifdef Q_OS_WIN32
	int r = QProcess::execute( nifMoppPath, QStringList( f.fileName() ) );
#else
	int r = QProcess::execute( QString( "wine" ), QStringList( { nifMoppPath, f.fileName() } ) );
#endif
	if ( r != 0 || !f.open() )
		return false;

	if ( f.size() >= 25 ) {
		QDataStream s( &f );
		s.setByteOrder( QDataStream::LittleEndian );
		s.setFloatingPointPrecision( QDataStream::SinglePrecision );

		quint32 tmp;
		if ( s >> tmp; tmp == 0x50504F4D ) {	// "MOPP"
			s >> origin[0];
			s >> origin[1];
			s >> origin[2];
			s >> scale;
			s >> tmp;
			if ( f.size() == ( qint64( tmp ) + 24 ) ) {
				data.resize( qsizetype( tmp ) );
				s.readRawData( data.data(), tmp );
			}
		}
	}
	f.close();

	return !data.isEmpty();
}

//! Update Havok MOPP for a given shape
class spMoppCode final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Update MOPP Code" ); }
	QString page() const override final { return Spell::tr( "Havok" ); }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		if ( auto v = nif->getUserVersion(); v == 10 || v == 11 ) {
			if ( nif->isNiBlock( index, "bhkMoppBvTreeShape" ) ) {
				return ( nif->checkVersion( 0x14000004, 0x14000005 )
						|| nif->checkVersion( 0x14020007, 0x14020007 ) );
			}
		}

		return false;
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & iBlock ) override final
	{
		QPersistentModelIndex ibhkMoppBvTreeShape = iBlock;

		QModelIndex ibhkPackedNiTriStripsShape = nif->getBlockIndex( nif->getLink( ibhkMoppBvTreeShape, "Shape" ) );

		if ( !nif->isNiBlock( ibhkPackedNiTriStripsShape, "bhkPackedNiTriStripsShape" ) ) {
			Message::warning( nullptr, Spell::tr( "Only bhkPackedNiTriStripsShape is supported at this time." ) );
			return iBlock;
		}

		QModelIndex ihkPackedNiTriStripsData = nif->getBlockIndex( nif->getLink( ibhkPackedNiTriStripsShape, "Data" ) );

		if ( !nif->isNiBlock( ihkPackedNiTriStripsData, "hkPackedNiTriStripsData" ) )
			return iBlock;

		QVector<int> subshapeVerts;

		if ( nif->checkVersion( 0x14000004, 0x14000005 ) ) {
			int nSubShapes = nif->get<int>( ibhkPackedNiTriStripsShape, "Num Sub Shapes" );
			QModelIndex ihkSubShapes = nif->getIndex( ibhkPackedNiTriStripsShape, "Sub Shapes" );
			subshapeVerts.resize( nSubShapes );

			for ( int t = 0; t < nSubShapes; t++ ) {
				subshapeVerts[t] = nif->get<int>( nif->getIndex( ihkSubShapes, t ), "Num Vertices" );
			}
		} else if ( nif->checkVersion( 0x14020007, 0x14020007 ) ) {
			int nSubShapes = nif->get<int>( ihkPackedNiTriStripsData, "Num Sub Shapes" );
			QModelIndex ihkSubShapes = nif->getIndex( ihkPackedNiTriStripsData, "Sub Shapes" );
			subshapeVerts.resize( nSubShapes );

			for ( int t = 0; t < nSubShapes; t++ ) {
				subshapeVerts[t] = nif->get<int>( nif->getIndex( ihkSubShapes, t ), "Num Vertices" );
			}
		}

		QVector<Vector3> verts = nif->getArray<Vector3>( ihkPackedNiTriStripsData, "Vertices" );
		QVector<Triangle> triangles;

		int nTriangles = nif->get<int>( ihkPackedNiTriStripsData, "Num Triangles" );
		QModelIndex iTriangles = nif->getIndex( ihkPackedNiTriStripsData, "Triangles" );
		triangles.resize( nTriangles );

		for ( int t = 0; t < nTriangles; t++ ) {
			triangles[t] = nif->get<Triangle>( nif->getIndex( iTriangles, t ), "Triangle" );
		}

		if ( verts.isEmpty() || triangles.isEmpty() ) {
			Message::critical( nullptr, Spell::tr( "Insufficient data to calculate MOPP code" ),
				Spell::tr("Vertices: %1, Triangles: %2").arg( !verts.isEmpty() ).arg( !triangles.isEmpty() )
			);
			return iBlock;
		}

		if ( HavokMoppCode moppcode; moppcode.CalculateMoppCode( subshapeVerts, verts, triangles ) ) {
			auto iMoppCode = nif->getIndex( ibhkMoppBvTreeShape, "MOPP Code" );

			nif->set<Vector4>( nif->getIndex( iMoppCode, "Offset" ), Vector4( moppcode.origin, moppcode.scale ) );

			QModelIndex iCodeSize = nif->getIndex( iMoppCode, "Data Size" );
			QModelIndex iCode = nif->getIndex( nif->getIndex( iMoppCode, "Data" ), 0 );

			if ( iCodeSize.isValid() && iCode.isValid() ) {
				nif->set<int>( iCodeSize, moppcode.data.size() );
				nif->set<QByteArray>( iCode, moppcode.data );
			}
		} else {
			Message::critical( nullptr, Spell::tr( "Failed to generate MOPP code" ) );
		}

		return iBlock;
	}
};

REGISTER_SPELL( spMoppCode )

//! Update MOPP code on all shapes in this model
class spAllMoppCodes final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Update All MOPP Code" ); }
	QString page() const override final { return Spell::tr( "Batch" ); }

	bool isApplicable( const NifModel * nif, const QModelIndex & idx ) override final
	{
		if ( !nif || idx.isValid() )
			return false;

		if ( auto v = nif->getUserVersion(); v == 10 || v == 11 ) {
			return ( nif->checkVersion( 0x14000004, 0x14000005 )
					|| nif->checkVersion( 0x14020007, 0x14020007 ) );
		}

		return false;
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & ) override final
	{
		QList<QPersistentModelIndex> indices;

		spMoppCode TSpacer;

		for ( int n = 0; n < nif->getBlockCount(); n++ ) {
			QModelIndex idx = nif->getBlockIndex( n );

			if ( TSpacer.isApplicable( nif, idx ) )
				indices << idx;
		}

		for ( const QPersistentModelIndex& idx : indices ) {
			TSpacer.castIfApplicable( nif, idx );
		}

		return QModelIndex();
	}
};

REGISTER_SPELL( spAllMoppCodes )

