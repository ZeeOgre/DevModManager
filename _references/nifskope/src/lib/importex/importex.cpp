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

#include "nifskope.h"
#include "gl/glscene.h"
#include "glview.h"
#include "model/nifmodel.h"
#include "model/nifproxymodel.h"
#include "ui/widgets/nifview.h"

#include <QApplication>
#include <QDockWidget>
#include <QFileDialog>
#include <QFileInfo>
#include <QMenu>
#include <QModelIndex>
#include <QSettings>

#include <string>
#include <functional>

#define tr( x ) QApplication::tr( x )


void exportObj( const NifModel * nif, const Scene* scene, const QModelIndex & index );
void exportCol( const NifModel * nif, const Scene* scene, QFileInfo );
void importObjMain( NifModel * nif, const QModelIndex & index, bool collision = false );
void importObj( NifModel* nif, const QModelIndex& index );
void importObjAsCollision( NifModel * nif, const QModelIndex & index );
void import3ds( NifModel * nif, const QModelIndex & index );

void exportGltf( const NifModel* nif, const Scene* scene, const QModelIndex& index );
void importGltf( NifModel* nif, const QModelIndex& index );


struct ImportExportOption
{
	std::string name;
	std::function<void(NifModel*, const QModelIndex&)> importFn;
	std::function<void(const NifModel*, const Scene*, const QModelIndex&)> exportFn;
	quint32 minBSVersion = 0;
	quint32 maxBSVersion = 0xFFFFFFFF;
	quint32 minVersion = 0;
	quint32 maxVersion = 0x1404FFFF; // < 0x14050000 (20.5)
};


QVector<ImportExportOption> impexOptions{
	ImportExportOption{ ".OBJ", importObj, exportObj, 0, 169 },
	ImportExportOption{ ".OBJ as Collision", importObjAsCollision, nullptr, 0, 169 },
	ImportExportOption{ ".glTF", importGltf, exportGltf, 83 },
};


void NifSkope::fillImportExportMenus()
{
	int impexIndex = 0;
	for ( const auto& option : impexOptions ) {
		if ( option.exportFn ) {
			mExport->addAction(tr("Export %1").arg(option.name.c_str()));
			mExport->actions().last()->setData(impexIndex);
		}
		if ( option.importFn ) {
			mImport->addAction(tr("Import %1").arg(option.name.c_str()));
			mImport->actions().last()->setData(impexIndex);
		}
		impexIndex++;
	}
}

void NifSkope::updateImportExportMenu(const QMenu * menu)
{
	for ( const auto a : menu->actions() ) {
		a->setEnabled(false);
		auto impex = impexOptions.value(a->data().toInt(), {});
		if ( impex.importFn || impex.exportFn ) {
			auto nifver = nif->getVersionNumber();
			auto bsver = nif->getBSVersion();
			if ( (nifver >= impex.minVersion && nifver <= impex.maxVersion)
				&& (bsver >= impex.minBSVersion && bsver <= impex.maxBSVersion) )
			{
				a->setEnabled(true);
			}
		}
	}
}

void NifSkope::sltImport( QAction* a )
{
	// Do not require index.isValid(), let fn deal with it
	QModelIndex index = currentNifIndex();
	auto impex = impexOptions.value(a->data().toInt(), {});
	if ( impex.importFn ) { 
		impex.importFn(nif, index);
	}
}

void NifSkope::sltExport( QAction* a )
{
	// Do not require index.isValid(), let fn deal with it
	QModelIndex index = currentNifIndex();
	auto impex = impexOptions.value(a->data().toInt(), {});
	if ( impex.exportFn ) {
		impex.exportFn(nif, ogl->scene, index);
	}
}


QString getImportexFileName( const NifModel * nif, const char * fileType, bool isImport )
{
	QString fileTypeStr = QLatin1StringView(fileType);
	QString suffix( QChar('.') );
	suffix.append( fileTypeStr.toLower() );

	QString nifPath;
	if ( auto w = qobject_cast< const NifSkope * >( nif->getWindow() ); w ) {
		if ( nifPath = w->getCurrentFile(); !nifPath.isEmpty() ) {
			if ( nifPath.endsWith( QLatin1StringView(".nif"), Qt::CaseInsensitive )
				|| nifPath.endsWith( QLatin1StringView(".bto"), Qt::CaseInsensitive )
				|| nifPath.endsWith( QLatin1StringView(".btr"), Qt::CaseInsensitive ) ) {
				nifPath.chop( 4 );
			}
#ifdef Q_OS_WIN32
			nifPath.replace( QChar('\\'), QChar('/') );
#endif
			nifPath.remove( 0, nifPath.lastIndexOf( QChar('/') ) + 1 );
		}
	}

	QString	fileName = nif->getFolder();
	bool usingLastImportexPath = false;
	if ( fileName.isEmpty()
		|| fileName.contains( QLatin1StringView(".ba2/"), Qt::CaseInsensitive )
		|| fileName.contains( QLatin1StringView(".bsa/"), Qt::CaseInsensitive ) ) {
		QSettings	settings;
		if ( fileTypeStr.isEmpty() ) {
			fileName = settings.value( "Spells//Extract File/Last File Path", QString() ).toString();
		} else {
			fileName = settings.value( QString("Import-Export/%1/File Name").arg(fileTypeStr), QString() ).toString();
			if ( isImport || nifPath.isEmpty() ) {
				usingLastImportexPath = true;
			} else {
#ifdef Q_OS_WIN32
				fileName.replace( QChar('\\'), QChar('/') );
#endif
				fileName.truncate( fileName.lastIndexOf( QChar('/') ) + 1 );
			}
		}
	}
	if ( !usingLastImportexPath && !nifPath.isEmpty() ) {
		if ( !fileName.isEmpty() && !fileName.endsWith( QChar('/') ) )
			fileName.append( QChar('/') );
		fileName.append( nifPath );
		fileName.append( suffix );
	}

	QString fileFilter;
	if ( isImport && fileTypeStr == "glTF" )
		fileFilter = QLatin1StringView("glTF (*.glb *.gltf)");
	else
		fileFilter = QString( QLatin1StringView("%1 (*%2)") ).arg( fileTypeStr ).arg( suffix );

	if ( isImport ) {
		fileName = QFileDialog::getOpenFileName( qApp->activeWindow(),
													tr("Choose a .%1 file for import").arg(fileTypeStr),
													fileName, fileFilter );
	} else {
		fileName = QFileDialog::getSaveFileName( qApp->activeWindow(),
													tr("Choose a .%1 file for export").arg(fileTypeStr),
													fileName, fileFilter );
	}

	if ( !fileName.isEmpty() ) {
		QSettings settings;
		settings.setValue( QString("Import-Export/%1/File Name").arg(fileTypeStr), fileName );
	}

	return fileName;
}

