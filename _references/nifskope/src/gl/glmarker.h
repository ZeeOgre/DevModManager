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

#ifndef GLMARKER_H_INCLUDED
#define GLMARKER_H_INCLUDED

class Scene;

struct GLMarker
{
	int nv;
	int nf;
	const float * verts;
	const unsigned short * faces;

	void drawMarker( Scene * scene, bool solid = false ) const;

	static const float FurnitureMarker01Verts[288];
	static const unsigned short FurnitureMarker01Faces[198];
	static const GLMarker FurnitureMarker01;

	static const float FurnitureMarker03Verts[228];
	static const unsigned short FurnitureMarker03Faces[168];
	static const GLMarker FurnitureMarker03;

	static const float FurnitureMarker04Verts[228];
	static const unsigned short FurnitureMarker04Faces[168];
	static const GLMarker FurnitureMarker04;

	static const float FurnitureMarker11Verts[324];
	static const unsigned short FurnitureMarker11Faces[228];
	static const GLMarker FurnitureMarker11;

	static const float FurnitureMarker13Verts[324];
	static const unsigned short FurnitureMarker13Faces[228];
	static const GLMarker FurnitureMarker13;

	static const float FurnitureMarker14Verts[324];
	static const unsigned short FurnitureMarker14Faces[228];
	static const GLMarker FurnitureMarker14;

	static const float BedLeftVerts[288];
	static const unsigned short BedLeftFaces[198];
	static const GLMarker BedLeft;

	static const float ChairLeftVerts[324];
	static const unsigned short ChairLeftFaces[228];
	static const GLMarker ChairLeft;

	static const float ChairFrontVerts[324];
	static const unsigned short ChairFrontFaces[228];
	static const GLMarker ChairFront;

	static const float ChairBehindVerts[324];
	static const unsigned short ChairBehindFaces[228];
	static const GLMarker ChairBehind;

	static const float BumperMarker01Verts[288];
	static const unsigned short BumperMarker01Faces[432];
	static const GLMarker BumperMarker01;
};

#endif
