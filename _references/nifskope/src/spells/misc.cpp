#include "misc.h"
#include "model/undocommands.h"

#include <QAction>
#include <QDialog>
#include <QFileDialog>
#include <QLabel>
#include <QLayout>
#include <QMenu>
#include <QPushButton>
#include <QSpinBox>

// Brief description is deliberately not autolinked to class Spell
/*! \file misc.cpp
 * \brief Miscellaneous helper spells
 *
 * All classes here inherit from the Spell class.
 */

//! Update an array if eg. the size has changed
class spUpdateArray final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Update" ); }
	QString page() const override final { return Spell::tr( "Array" ); }
	QIcon icon() const override final { return QIcon( ":/img/update" ); }
	bool instant() const override final { return true; }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		if ( nif->isArray( index ) ) {
			//Check if array is of fixed size
			NifItem * item = static_cast<NifItem *>( index.internalPointer() );
			bool static1 = true;
			bool static2 = true;

			if ( item->arr1().isEmpty() == false ) {
				item->arr1().toInt( &static1 );
			}

			if ( item->arr2().isEmpty() == false ) {
				item->arr2().toInt( &static2 );
			}

			//Leave this commented out until a way for static arrays to be initialized to the right size is created.
			//if ( static1 && static2 )
			//{
			//	//Neither arr1 or arr2 is a variable name
			//	return false;
			//}

			//One of arr1 or arr2 is a variable name so the array is dynamic
			return true;
		}

		return false;
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		nif->undoStack->push( new ArrayUpdateCommand( index, nif ) );
		return index;
	}
};

REGISTER_SPELL( spUpdateArray )

//! Updates the header of the NifModel
class spUpdateHeader final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Update" ); }
	QString page() const override final { return Spell::tr( "Header" ); }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		auto block = nif->getTopItem( index );
		return ( block && block == nif->getHeaderItem() );
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		nif->updateHeader();
		return index;
	}
};

REGISTER_SPELL( spUpdateHeader )

//! Updates the footer of the NifModel
class spUpdateFooter final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Update" ); }
	QString page() const override final { return Spell::tr( "Footer" ); }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		auto block = nif->getTopItem( index );
		return ( block && block == nif->getFooterItem() );
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		nif->updateFooter();
		return index;
	}
};

REGISTER_SPELL( spUpdateFooter )

//! Follows a link
class spFollowLink final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Follow Link" ); }
	bool constant() const override final { return true; }
	bool instant() const override final { return true; }
	QIcon icon() const override final { return QIcon( ":/img/link" ); }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		return nif->isLink( index ) && nif->getLink( index ) >= 0;
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		QModelIndex idx = nif->getBlockIndex( nif->getLink( index ) );

		if ( idx.isValid() )
			return idx;

		return index;
	}
};

REGISTER_SPELL( spFollowLink )

//! Estimates the file offset of an item in a model
class spFileOffset final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "File Offset" ); }
	bool constant() const override final { return true; }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		return nif && index.isValid();
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		int ofs = nif->fileOffset( index );
		Message::info( nif->getWindow(),
			Spell::tr( "Estimated file offset is %1 (0x%2)" ).arg( ofs ).arg( ofs, 0, 16 ),
			Spell::tr( "Block: %1\nOffset: %2 (0x%3)" ).arg( index.data( Qt::DisplayRole ).toString() ).arg( ofs ).arg( ofs, 0, 16 )
		);
		return index;
	}
};

REGISTER_SPELL( spFileOffset )

//! Exports the binary data of a binary row to a file
class spExportBinary final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Export Binary" ); }
	bool constant() const override final { return true; }

	bool isApplicable( [[maybe_unused]] const NifModel * nif, const QModelIndex & index ) override final
	{
		NifItem * item = static_cast<NifItem *>(index.internalPointer());

		return item && (item->value().isByteArray() || (item->isBinary() && item->isArray())) && index.isValid();
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		NifItem * item = static_cast<NifItem *>(index.internalPointer());

		QByteArray data;

		NifItem * dataItem = item;
		if ( item->isArray() && item->isBinary() ) {
			dataItem = item->child( 0 );
		}

		if ( dataItem && dataItem->isByteArray() ) {
			auto bytes = dataItem->get<QByteArray *>();
			data.append( *bytes );
		}

		// Get parent block name and number
		int blockNum = nif->getBlockNumber( index );
		QString suffix = QString( "%1_%2" ).arg( nif->itemName( nif->getBlockIndex( blockNum ) ) ).arg( blockNum );
		QString filestring = QString( "%1-%2" ).arg( nif->getFilename() ).arg( suffix );

		QString filename = QFileDialog::getSaveFileName( qApp->activeWindow(), tr( "Export Binary File" ),
														 filestring, "*.*" );
		QFile file( filename );
		if ( file.open( QIODevice::WriteOnly ) ) {
			file.write( data );
			file.close();
		}

		return index;
	}
};

REGISTER_SPELL( spExportBinary )

//! Imports the binary data of a file to a binary row
class spImportBinary final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Import Binary" ); }

	bool isApplicable( [[maybe_unused]] const NifModel * nif, const QModelIndex & index ) override final
	{
		NifItem * item = static_cast<NifItem *>(index.internalPointer());

		return item && (item->value().isByteArray() || (item->isBinary() && item->isArray())) && index.isValid();
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		NifItem * item = static_cast<NifItem *>(index.internalPointer());
		NifItem * parent = item->parent();
		auto iParent = index.parent();

		auto idx = index;
		if ( item->isArray() && item->isBinary() ) {
			parent = item;
			iParent = index;
			idx = nif->getIndex( index, 0 );
		}

		QString filename = QFileDialog::getOpenFileName( qApp->activeWindow(), tr( "Import Binary File" ), "", "*.*" );
		QFile file( filename );
		if ( file.open( QIODevice::ReadOnly ) ) {
			QByteArray data = file.readAll();

			if ( parent->isArray() && parent->isBinary() ) {
				// NOTE: This will only work on byte arrays where the array length is not an expression
				nif->set<int>( iParent.parent(), parent->arr1(), int( data.size() ) );
				nif->updateArraySize( iParent );
			}

			nif->set<QByteArray>( idx, data );

			file.close();
		}

		return index;
	}
};

REGISTER_SPELL( spImportBinary )

// definitions for spCollapseArray moved to misc.h
bool spCollapseArray::isApplicable( const NifModel * nif, const QModelIndex & index )
{
	if ( nif->isArray( index ) && index.isValid() ) {
		if ( auto i = nif->getItem( index ); i && ( i->hasStrType( "Ref" ) || i->hasStrType( "Ptr" ) ) ) {
			// copy from spUpdateArray when that changes
			return true;
		}
	}

	return false;
}

QModelIndex spCollapseArray::cast( NifModel * nif, const QModelIndex & index )
{
	nif->updateArraySize( index );
	// There's probably an easier way of doing this hiding in NifModel somewhere
	NifItem * item = static_cast<NifItem *>( index.internalPointer() );
	QModelIndex size  = nif->getIndex( nif->getBlockIndex( index.parent() ), item->arr1() );
	QModelIndex array = static_cast<QModelIndex>( index );
	return numCollapser( nif, size, array );
}

QModelIndex spCollapseArray::numCollapser( NifModel * nif, QModelIndex & iNumElem, QModelIndex & iArray )
{
	if ( iNumElem.isValid() && iArray.isValid() && isApplicable( nif, iArray ) ) {
		QVector<qint32> links = nif->getLinkArray( iArray );

		qsizetype r = 0;
		for ( qsizetype i = 0; i < links.size(); i++ ) {
			if ( qint32 l = links.at( i ); l >= 0 ) {
				links[r] = l;
				r++;
			}
		}

		if ( r < nif->rowCount( iArray ) ) {
			nif->set<int>( iNumElem, int( r ) );
			nif->updateArraySize( iArray );
			if ( r > 0 ) {
				links.resize( r );
				nif->setLinkArray( iArray, links );
			}
		}
	}

	return iArray;
}

REGISTER_SPELL( spCollapseArray )


//! Move array items
class spMoveArrayItem : public Spell
{
public:
	static bool swapItems( NifModel * nif, NifItem * item1, NifItem * item2 );
	static int moveArrayItem( NifModel * nif, const QModelIndex & iArray, int srcRow, int dstRow );

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override
	{
		if ( index.isValid() && nif ) {
			if ( auto iParent = index.parent(); iParent.isValid() )
				return ( nif->isArray( iParent ) && nif->rowCount( iParent ) >= 2 );
		}
		return false;
	}
};

bool spMoveArrayItem::swapItems( NifModel * nif, NifItem * item1, NifItem * item2 )
{
	if ( !( item1 && item2 && item1->valueType() == item2->valueType() && item1->childCount() == item2->childCount() ) )
		return false;

	item1->invalidateVersionCondition();
	item1->invalidateCondition();
	item2->invalidateVersionCondition();
	item2->invalidateCondition();

	bool	r = true;
	for ( int i = 0; i < item1->childCount(); i++ ) {
		if ( !swapItems( nif, item1->child( i ), item2->child( i ) ) )
			r = false;
	}

	NifValue	tmp( item1->value() );
	nif->setItemValue( item1, item2->value() );
	nif->setItemValue( item2, tmp );

	return r;
}

int spMoveArrayItem::moveArrayItem( NifModel * nif, const QModelIndex & iArray, int srcRow, int dstRow )
{
	if ( !( iArray.isValid() && nif->isArray( iArray ) ) )
		return srcRow;
	int	n = nif->rowCount( iArray );
	if ( n < 2 )
		return srcRow;
	if ( srcRow < 0 )
		srcRow += n;
	if ( dstRow < 0 )
		dstRow += n;
	srcRow = std::clamp< int >( srcRow, 0, n - 1 );
	dstRow = std::clamp< int >( dstRow, 0, n - 1 );
	if ( srcRow == dstRow )
		return srcRow;
	int	d = dstRow - srcRow;
	if ( d == 1 || d == -1 ) {
		if ( swapItems( nif, nif->getItem( iArray, srcRow, false ), nif->getItem( iArray, dstRow, false ) ) )
			return dstRow;
		return srcRow;
	}

	d = ( d < 0 ? -1 : 1 );
	nif->setState( BaseModel::Processing );
	do {
		if ( !swapItems( nif, nif->getItem( iArray, srcRow, false ), nif->getItem( iArray, srcRow + d, false ) ) )
			break;
		srcRow += d;
	} while ( srcRow != dstRow );
	nif->restoreState();

	return srcRow;
}


class spMoveArrayItemUp final : public spMoveArrayItem
{
public:
	QString name() const override final { return Spell::tr( "Move Up" ); }
	QKeySequence hotkey() const override final { return { Qt::ControlModifier | Qt::Key_Up }; }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		if ( !spMoveArrayItem::isApplicable( nif, index ) )
			return false;
		return ( index.row() > 0 );
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		int	srcRow = index.row();
		return nif->getIndex( index.parent(), moveArrayItem( nif, index.parent(), srcRow, srcRow - 1 ) );
	}
};

REGISTER_SPELL( spMoveArrayItemUp )


class spMoveArrayItemDown final : public spMoveArrayItem
{
public:
	QString name() const override final { return Spell::tr( "Move Down" ); }
	QKeySequence hotkey() const override final { return { Qt::ControlModifier | Qt::Key_Down }; }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		if ( !spMoveArrayItem::isApplicable( nif, index ) )
			return false;
		return ( index.row() < ( nif->rowCount( index.parent() ) - 1 ) );
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		int	srcRow = index.row();
		return nif->getIndex( index.parent(), moveArrayItem( nif, index.parent(), srcRow, srcRow + 1 ) );
	}
};

REGISTER_SPELL( spMoveArrayItemDown )


class spMoveArrayItemTo final : public spMoveArrayItem
{
public:
	QString name() const override final { return Spell::tr( "Move To Row..." ); }

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		int	srcRow = index.row();
		int	dstRow = srcRow;

		{
			QDialog dlg;
			dlg.setWindowTitle( Spell::tr( "Move Array Item" ) );

			QGridLayout * grid = new QGridLayout( &dlg );

			QSpinBox * dstRowInput = new QSpinBox;
			dstRowInput->setRange( -131072, 131071 );
			dstRowInput->setValue( dstRow );

			grid->addWidget( new QLabel( Spell::tr( "Destination row (-1: end of array)" ) ), 0, 0 );
			grid->addWidget( dstRowInput, 0, 1 );

			QPushButton * btOk = new QPushButton( Spell::tr( "Move" ) );
			QObject::connect( btOk, &QPushButton::clicked, &dlg, &QDialog::accept );

			QPushButton * btCancel = new QPushButton( Spell::tr( "Cancel" ) );
			QObject::connect( btCancel, &QPushButton::clicked, &dlg, &QDialog::reject );

			grid->addWidget( btOk, 1, 0 );
			grid->addWidget( btCancel, 1, 1 );

			if ( dlg.exec() != QDialog::Accepted )
				return index;

			dstRow = dstRowInput->value();
		}

		return nif->getIndex( index.parent(), moveArrayItem( nif, index.parent(), srcRow, dstRow ) );
	}
};

REGISTER_SPELL( spMoveArrayItemTo )


//! Select a vertex attribute (same element of a different array)
class spSelectVertexAttr final : public Spell
{
public:
	QString name() const override final { return Spell::tr( "Vertex Attribute..." ); }
	QString page() const override final { return QString(); }
	bool constant() const override final { return true; }

	bool isApplicable( const NifModel * nif, const QModelIndex & index ) override final
	{
		if ( !( nif && index.isValid() ) )
			return false;
		const NifItem *	p = nif->getItem( index.parent(), false );
		if ( !( p && p->isArray() ) )
			return false;
		if ( nif->blockInherits( p, "NiGeometryData" ) ) {
			if ( p->hasName( "Vertices" ) || p->hasName( "Normals" ) || p->hasName( "Tangents" )
				|| p->hasName( "Bitangents" ) || p->hasName( "Vertex Colors" ) ) {
				return true;
			}
			return ( p->hasName( "UV Sets" ) && !nif->isArray( index ) );
		} else if ( nif->blockInherits( p, "BSGeometry" ) ) {
			return ( p->hasName( "Vertices" ) || p->hasName( "UVs" ) || p->hasName( "UVs 2" )
					|| p->hasName( "Vertex Colors" ) || p->hasName( "Normals" ) || p->hasName( "Tangents" )
					|| p->hasName( "Weights" ) );
		}
		return false;
	}

	QModelIndex cast( NifModel * nif, const QModelIndex & index ) override final
	{
		const NifItem *	attrItems[8] = {
			nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr
		};
		static const char *	attrNames[8] = {
			"Position", "Normal", "Tangent", "Bitangent", "Color", "UV 1", "UV 2", "Weights"
		};

		int	n = index.row();
		if ( nif->blockInherits( index, "NiGeometryData" ) ) {
			QModelIndex	iData = nif->getBlockIndex( index );
			if ( auto i = nif->getIndex( iData, "Vertices" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[0] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "Normals" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[1] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "Tangents" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[2] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "Bitangents" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[3] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "Vertex Colors" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[4] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "UV Sets" ); i.isValid() && nif->rowCount( i ) > 0 ) {
				if ( auto j = nif->getIndex( i, 0 ); j.isValid() && nif->rowCount( j ) > n )
					attrItems[5] = nif->getItem( nif->getIndex( j, n ), false );
				if ( nif->rowCount( i ) > 1 ) {
					if ( auto j = nif->getIndex( i, 1 ); j.isValid() && nif->rowCount( j ) > n )
						attrItems[6] = nif->getItem( nif->getIndex( j, n ), false );
				}
			}
		} else if ( nif->blockInherits( index, "BSGeometry" ) ) {
			QModelIndex	iData = index.parent().parent();
			int	d = std::max( nif->get<int>( iData, "Weights Per Vertex" ), 1 );
			if ( const NifItem * p = nif->getItem( index.parent() ); p && p->hasName( "Weights" ) )
				n = n / d;
			if ( auto i = nif->getIndex( iData, "Vertices" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[0] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "Normals" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[1] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "Tangents" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[2] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "Vertex Colors" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[4] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "UVs" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[5] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "UVs 2" ); i.isValid() && nif->rowCount( i ) > n )
				attrItems[6] = nif->getItem( nif->getIndex( i, n ), false );
			if ( auto i = nif->getIndex( iData, "Weights" ); i.isValid() && nif->rowCount( i ) > ( n * d ) )
				attrItems[7] = nif->getItem( nif->getIndex( i, n * d ), false );
		}

		QMenu	menu;
		for ( int i = 0; i < 8; i++ ) {
			if ( attrItems[i] ) {
				auto	a = menu.addAction( QString( attrNames[i] ) );
				a->setData( QVariant( i ) );
			}
		}

		if ( QAction * a = menu.exec( QCursor::pos() ); a )
			return nif->itemToIndex( attrItems[a->data().toInt()] );

		return index;
	}
};

REGISTER_SPELL( spSelectVertexAttr )
