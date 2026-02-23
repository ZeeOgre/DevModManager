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

#ifndef GLSHADER_H
#define GLSHADER_H

#include <data/niftypes.h>

#include <QCoreApplication>
#include <QMap>
#include <QVector>
#include <QString>

#include <array>
#include <string>

#include "glcontext.hpp"
#include "material.hpp"

//! @file renderer.h Renderer, Renderer::ConditionSingle, Renderer::ConditionGroup, Renderer::Shader, Renderer::Program

class NifModel;
class Scene;
class Shape;

typedef unsigned int GLenum;
typedef unsigned int GLuint;

//! Manages rendering and shaders
class Renderer : public QObject, public NifSkopeOpenGLContext
{
	Q_OBJECT

public:
	Renderer( QOpenGLContext * c );
	~Renderer();

	//! Set up shader program
	Program * setupProgram( Shape *, Program * hint = nullptr );

public slots:
	void updateSettings();

protected:
	// Starfield
	bool setupProgramCE2( const NifModel *, Program *, Shape * );
	// Skyrim, Fallout 4, Fallout 76
	bool setupProgramCE1( const NifModel *, Program *, Shape * );
	// Oblivion, Fallout 3/New Vegas
	bool setupProgramFO3( const NifModel *, Program *, Shape * );
	// other games
	void setupFixedFunction( Shape * );

	struct Settings
	{
		std::uint8_t	meshCacheSize = 16;		// in units of 8 MiB
		QString	cubeMapPathFO76;
		QString	cubeMapPathSTF;
	} cfg;

public:
	bool drawSkyBox( Scene * scene );
};

#endif
