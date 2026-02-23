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

#include <cstdlib>

#include "filebrowser.h"
#include "ddspreview.h"
#include "ba2file.hpp"

#include <QCoreApplication>
#include <QMessageBox>
#include <QProgressBar>
#include <QPushButton>

class NifModel;

class spResourceFileExtract
{
public:
	static std::string getOutputDirectory( const NifModel * nif = nullptr );
	static void writeFileWithPath( const std::string & fileName, const char * buf, qsizetype bufSize );
};

QTreeWidgetItem *	FileBrowserWidget::findDirectory( std::map< std::string_view, QTreeWidgetItem * > & dirMap, const std::string_view & d )
{
	std::map< std::string_view, QTreeWidgetItem * >::iterator	i = dirMap.find( d );
	if ( i != dirMap.end() )
		return i->second;
	size_t	n = std::string_view::npos;
	if ( d.length() >= 2 )
		n = d.rfind( '/', d.length() - 2 );
	if ( n == std::string_view::npos )
		n = 0;
	else
		n++;
	QTreeWidgetItem *	parent = nullptr;
	if ( n )
		parent = findDirectory( dirMap, std::string_view( d.data(), n ) );
	QTreeWidgetItem *	tmp;
	if ( !parent )
		tmp = new QTreeWidgetItem( treeWidget, -2 );
	else
		tmp = new QTreeWidgetItem( parent, -2 );
	dirMap.emplace( d, tmp );
	tmp->setText( 0, QString::fromUtf8( d.data() + n, qsizetype(d.length() - n) ) );
	return tmp;
}

void FileBrowserWidget::updateTreeWidget()
{
	treeWidget->clear();
	filesShown.clear();
	std::string	filterString( filter->text().trimmed().toStdString() );
	int	curFileIndex = -1;
	for ( const auto & i : fileSet ) {
		if ( currentFile && i == *currentFile ) {
			curFileIndex = int( filesShown.size() );
		} else if ( !filterString.empty() && i.find( filterString ) == std::string_view::npos ) {
			continue;
		}
		filesShown.push_back( &i );
	}

	std::map< std::string_view, QTreeWidgetItem * >	dirMap;
	std::map< QTreeWidgetItem *, QList< QTreeWidgetItem * > >	dirChildren;
	QTreeWidgetItem *	selectedItem = nullptr;
	std::string_view	d;
	size_t	numFiles = filesShown.size();
	for ( size_t i = 0; i < numFiles; i++ ) {
		const std::string_view &	fullPath( *(filesShown[i]) );
		size_t	n = std::string_view::npos;
		if ( numFiles > 100 ) {
			n = fullPath.rfind( '/' );
			if ( n == 31 && fullPath.length() == 57 && fullPath[10] == '/'
				&& fullPath.starts_with( "geometries" ) && fullPath.ends_with( ".mesh" ) ) {
				// work around performance issues with Starfield split SHA1 .mesh paths
				for ( size_t j = 11; true; j++ ) {
					char	c = fullPath[j];
					if ( ( c >= '0' && c <= '9' ) || ( c >= 'a' && c <= 'f' ) || j == 31 )
						continue;
					if ( j >= 52 )
						n = 10;
					break;
				}
			}
		}
		if ( n == std::string_view::npos )
			n = 0;
		else
			n++;
		QTreeWidgetItem *	parent = nullptr;
		if ( n ) {
			d = std::string_view( fullPath.data(), n );
			parent = findDirectory( dirMap, d );
		}
		QTreeWidgetItem *	tmp;
		if ( !parent ) {
			tmp = new QTreeWidgetItem( treeWidget, int(i) );
		} else {
			tmp = new QTreeWidgetItem( (QTreeWidgetItem *) nullptr, int(i) );
			dirChildren[parent].append( tmp );
		}
		tmp->setText( 0, QString::fromUtf8( fullPath.data() + n, qsizetype(fullPath.length() - n) ) );
		if ( treeWidget->columnCount() > 1 ) {
			for ( Game::GameManager::GameResources * r = gameResources; r; r = r->parent ) {
				if ( !r->ba2File )
					continue;
				// print packed size for games that use BSA archives
				std::int64_t	fileSize = r->ba2File->getFileSize( fullPath, ( r->game < Game::FALLOUT_4 ) );
				if ( fileSize >= 0 ) {
					tmp->setText( 1, QString::number( fileSize ) );
					break;
				}
			}
		}
		if ( i == size_t(curFileIndex) )
			selectedItem = tmp;
	}

	for ( auto i = dirChildren.begin(); i != dirChildren.end(); i++ )
		i->first->addChildren( i->second );

	if ( selectedItem )
		treeWidget->setCurrentItem( selectedItem );
}

void FileBrowserWidget::checkItemActivated( QTreeWidgetItem * i, [[maybe_unused]] int column )
{
	if ( !i )
		return;
	if ( int n = i->type(); n >= 0 && size_t(n) < filesShown.size() ) {
		selectedFileIndex = n;
		if ( treeWidget->columnCount() < 2 )
			accept();
	}
}

FileBrowserWidget::FileBrowserWidget(
	int w, int h, const char * titleString,
	const std::set< std::string_view > & files, const std::string_view & fileSelected,
	Game::GameManager::GameResources * archives, bool archiveExtractorMode )
	: fileSet( files ), currentFile( nullptr ), gameResources( archives )
{
	layout = new QGridLayout( this );
	layout->setColumnMinimumWidth( 0, w );
	layout->setColumnStretch( 0, 1 );
	layout->setRowMinimumHeight( 1, h );
	layout->addWidget( new QLabel( QString( titleString ), this ), 0, 0 );
	treeWidget = new QTreeWidget( this );
	if ( gameResources && archiveExtractorMode ) {
		treeWidget->setColumnCount( 2 );
		treeWidget->setColumnWidth( 0, w - 100 );
		treeWidget->setHeaderLabels( { "Path", ( gameResources->game < Game::FALLOUT_4 ? "Packed Size" : "Size" ) } );
	} else {
		treeWidget->setHeaderLabel( "Path" );
	}
	layout->addWidget( treeWidget, 1, 0 );
	QGridLayout *	layout2 = new QGridLayout();
	layout->addLayout( layout2, 2, 0 );
	layout2->setColumnMinimumWidth( 0, w - 200 );
	layout2->setColumnMinimumWidth( 1, ( !gameResources ? 200 : 100 ) );
	filter = new QLineEdit( this );
	layout2->addWidget( filter, 0, 0 );
	layout2->addWidget( new QLabel( QString( "Path Filter" ), this ), 0, 1 );

	if ( gameResources ) {
		treeWidget->setSelectionMode( QAbstractItemView::ExtendedSelection );
		layout2->setColumnMinimumWidth( 2, 100 );
		QPushButton *	b = new QPushButton( "&Extract Selected", this );
		layout2->addWidget( b, 0, 2 );
		b->setAutoDefault( false );
		QObject::connect( b, &QPushButton::clicked, this, &FileBrowserWidget::extractItemSelected );
		QObject::connect( treeWidget, &QTreeWidget::itemSelectionChanged, this, &FileBrowserWidget::showTextureInfo );
	}

	if ( !fileSelected.empty() )
		currentFile = &fileSelected;
	QObject::connect( filter, &QLineEdit::editingFinished, filter, [this]() { updateTreeWidget(); } );
	QObject::connect( treeWidget, &QTreeWidget::itemActivated, this, &FileBrowserWidget::checkItemActivated );

	updateTreeWidget();
}

FileBrowserWidget::~FileBrowserWidget()
{
	delete textureInfo;
}

const std::string_view * FileBrowserWidget::getItemSelected() const
{
	if ( selectedFileIndex >= 0 && selectedFileIndex < qsizetype( filesShown.size() ) )
		return filesShown[selectedFileIndex];
	return nullptr;
}

void FileBrowserWidget::showTextureInfo()
{
	if ( textureInfo ) {
		delete textureInfo;
		textureInfo = nullptr;
	}
	if ( !gameResources )
		return;

	const std::string_view *	filePath = nullptr;
	if ( QTreeWidgetItem * i = treeWidget->currentItem(); i ) {
		int	n = i->type();
		if ( n >= 0 && size_t(n) < filesShown.size() )
			filePath = filesShown[n];
	}
	if ( !( filePath && ( filePath->ends_with( ".dds" ) || filePath->ends_with( ".hdr" ) ) ) )
		return;

	try {
		textureInfo =
			new DDSTextureInfo( *gameResources, QString::fromUtf8( filePath->data(), qsizetype( filePath->length() ) ),
								this );
	} catch ( NifSkopeError & ) {
		return;
	}
	layout->addWidget( textureInfo, 1, 1 );
}

void FileBrowserWidget::findItemsSelected( std::set< std::string_view > & filesSelected, const QTreeWidgetItem * i )
{
	if ( !i )
		return;
	int	n = i->type();
	if ( n >= 0 && size_t(n) < filesShown.size() ) {
		filesSelected.insert( *(filesShown[n]) );
		return;
	}
	if ( n == -2 ) {
		for ( int j = 0; j < i->childCount(); j++ )
			findItemsSelected( filesSelected, i->child( j ) );
	}
}

void FileBrowserWidget::extractItemSelected()
{
	if ( !gameResources )
		return;
	std::set< std::string_view >	filesSelected;
	for ( QTreeWidgetItem * i : treeWidget->selectedItems() )
		findItemsSelected( filesSelected, i );
	if ( filesSelected.empty() )
		return;

	std::string	outDir = spResourceFileExtract::getOutputDirectory();
	if ( outDir.empty() )
		return;

	QDialog	dlg;
	QLabel *	lb = new QLabel( tr( "Extracting %1 Files..." ).arg( filesSelected.size() ), &dlg );
	QProgressBar *	pb = new QProgressBar( &dlg );
	pb->setMinimum( 0 );
	pb->setMaximum( int( filesSelected.size() ) );
	QPushButton *	cb = new QPushButton( tr( "Cancel" ), &dlg );
	QGridLayout *	grid = new QGridLayout( &dlg );
	grid->addWidget( lb, 0, 0, 1, 3 );
	grid->addWidget( pb, 1, 0, 1, 3 );
	grid->addWidget( cb, 2, 1, 1, 1 );
	QObject::connect( cb, &QPushButton::clicked, &dlg, &QDialog::reject );
	dlg.setModal( true );
	dlg.setResult( QDialog::Accepted );
	dlg.show();

	try {
		std::string	fullPath;
		QByteArray	fileData;
		int	n = 0;
		for ( const auto & i : filesSelected ) {
			QCoreApplication::processEvents();
			if ( dlg.result() == QDialog::Rejected )
				break;
			fullPath = outDir;
			fullPath += i;
			if ( gameResources->get_file( fileData, i ) )
				spResourceFileExtract::writeFileWithPath( fullPath, fileData.data(), fileData.size() );
			n++;
			pb->setValue( n );
		}
	} catch ( std::exception & e ) {
		QMessageBox::critical( this, "NifSkope error", QString( "Error extracting file: %1" ).arg( e.what() ) );
	}
}
