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

#ifndef FILEBROWSER_H_INCLUDED
#define FILEBROWSER_H_INCLUDED

#include <string>
#include <vector>
#include <set>
#include <map>

#include <QDialog>
#include <QLabel>
#include <QLayout>
#include <QLineEdit>
#include <QTreeWidget>

#include "gamemanager.h"

class DDSTextureInfo;

class FileBrowserWidget : public QDialog
{
	Q_OBJECT

protected:
	QGridLayout *	layout;
	QTreeWidget *	treeWidget;
	QLineEdit *	filter;
	const std::set< std::string_view > &	fileSet;
	const std::string_view *	currentFile;
	qsizetype	selectedFileIndex = -1;
	std::vector< const std::string_view * >	filesShown;
	DDSTextureInfo *	textureInfo = nullptr;
	Game::GameManager::GameResources *	gameResources;
	QTreeWidgetItem *	findDirectory( std::map< std::string_view, QTreeWidgetItem * > & dirMap, const std::string_view & d );
	void updateTreeWidget();
	void findItemsSelected( std::set< std::string_view > & filesSelected, const QTreeWidgetItem * i );

public:
	// texture preview is enabled if 'archives' is not nullptr
	FileBrowserWidget( int w, int h, const char * titleString,
						const std::set< std::string_view > & files, const std::string_view & fileSelected,
						Game::GameManager::GameResources * archives = nullptr, bool archiveExtractorMode = false );
	virtual ~FileBrowserWidget();
	const std::string_view *	getItemSelected() const;

public slots:
	virtual void checkItemActivated( QTreeWidgetItem *, int );
	virtual void showTextureInfo();
	virtual void extractItemSelected();
};

#endif
