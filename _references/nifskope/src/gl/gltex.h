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

#ifndef GLTEX_H_INCLUDED
#define GLTEX_H_INCLUDED

#include "gl/glcontext.hpp"

#include <QObject> // Inherited
#include <QByteArray>
#include <QHash>
#include <QPersistentModelIndex>
#include <QString>
#include <QStringView>


//! @file gltex.h TexCache etc. header

class NifModel;
class QSettings;

typedef unsigned int GLuint;
typedef unsigned int GLenum;

/*! A class for handling OpenGL textures.
 *
 * This class stores information on all loaded textures.
 */
class TexCache final : public QObject
{
	Q_OBJECT

public:
	struct TexFmt
	{
		enum {
			// for imageFormat
			TEXFMT_UNKNOWN = 0,
			TEXFMT_BMP = 1,
			TEXFMT_DDS = 2,
			TEXFMT_NIF = 3,
			TEXFMT_TGA = 4,
			// flags for imageEncoding
			TEXFMT_DXT1 = 8,
			TEXFMT_DXT3 = 16,
			TEXFMT_DXT5 = 32,
			TEXFMT_GRAYSCALE = 256,
			TEXFMT_GRAYSCALE_ALPHA = 512,
			TEXFMT_PAL8 = 1024,
			TEXFMT_RGB8 = 2048,
			TEXFMT_RGBA8 = 4096,
			TEXFMT_RLE = 8192
		};
		GLuint internalFormat = 0;	// OpenGL internal format
		bool isCompressed = false;
		unsigned char imageFormat = 0;
		unsigned short imageEncoding = 0;
		QString toString() const;
	};

	//! A structure for storing information on a single texture.
	struct Tex
	{
		struct ImageInfo
		{
			//! The texture file name.
			QString filename;
			//! The texture file path.
			QString filepath;
			//! Width of the texture
			GLuint width = 0;
			//! Height of the texture
			GLuint height = 0;
			//! Number of mipmaps present
			GLuint mipmaps = 0;
			//! Format of the texture
			TexFmt format;
			//! Status messages
			QString status;

			//! Save the texture as pixel data
			bool savePixelData( TexCache & t, NifModel * nif, QModelIndex & iData ) const;
		};

		const QChar *	nameData;
		std::uint16_t	nameLen;
		std::uint16_t	mipmaps;
		//! The format target
		GLenum	target;
		//! IDs for use with GL texture functions
		GLuint	id[2];
		//! Detailed information about the image file
		ImageInfo *	imageInfo;

		inline Tex()
		{
			nameData = nullptr;
			nameLen = 0;
			mipmaps = 0;
			target = 0;	// = 0x0DE1; // GL_TEXTURE_2D
			id[0] = 0;
			id[1] = 0;
			imageInfo = nullptr;
		}

		inline QStringView filename() const
		{
			return QStringView( nameData, nameLen );
		}

		//! Returns true if loadTex() was called and at least one valid texture ID was generated
		inline bool isLoaded() const
		{
			return bool( ( id[0] + 1U ) & ~1U );
		}

		//! Save the texture as a file
		bool saveAsFile( TexCache & t, const QModelIndex & index, QString & savepath );
	};

	TexCache( QObject * parent = nullptr );
	~TexCache();

	//! Bind a texture from filename
	int bind( const QStringView & fname, const NifModel * nif );
	//! Bind a cube map from filename
	bool bindCube( const QString & fname, const NifModel * nif, bool useSecondTexture );
	//! Bind a texture from pixel data
	int bind( const QModelIndex & iSource );

	//! Debug function for getting info about a texture
	QString info( const QModelIndex & iSource );

	//! Export pixel data to a file
	bool exportFile( const QModelIndex & iSource, QString & filepath );
	//! Import pixel data from a file (not implemented yet)
	bool importFile( NifModel * nif, const QModelIndex & iSource, QModelIndex & iData );

	//! Find a texture based on its filename
	static QString find( const QString & file, const NifModel * nif );
	//! Remove the path from a filename
	static QString stripPath( const QString & file, const QString & nifFolder );
	//! Checks whether the given file can be loaded
	static bool canLoad( const QString & file );
	//! Checks whether the extension is supported
	static bool isSupported( const QString & file );

	//! Number of texture units
	enum	{ maxTextureUnits = 32 };
	static int	num_texture_units;	// for glActiveTexture()
	static int	pbrCubeMapResolution;
	static int	pbrImportanceSamples;
	static int	hdrToneMapLevel;

signals:
	void sigRefresh();

public slots:
	void flush();

	/*! Set the folder to read textures from
	 *
	 * If this is not set, relative paths won't resolve. The standard usage
	 * is to give NifModel::getFolder() as the argument.
	 */
	void setNifFolder( const QString & );

protected:
	Tex * textures;
	std::uint32_t textureHashMask;
	std::uint32_t textureCount;
	NifSkopeOpenGLContext::GLFunctions * fn;
	QHash<QModelIndex, Tex> embedTextures;

	template< typename T > inline Tex * insertTex( const T & file );
	Tex * rehashTextures( Tex * p = nullptr );
	//! Load the texture
	std::uint16_t loadTex( Tex & tx, const NifModel * nif );

public:
	void setOpenGLContext( NifSkopeOpenGLContext * context );

	const Tex::ImageInfo * getTextureInfo( const QStringView & file ) const;

	// returns true if the settings have changed
	static bool loadSettings( QSettings & settings );
	static void clearCubeCache();
	static void set_max_anisotropy();
	static float get_max_anisotropy();

	bool activateTextureUnit( int x );

	//! Texture loading functions

	/*! A function for loading textures.
	 *
	 * Loads a texture pointed to by filepath.
	 * Returns the number of mipmaps on success, and throws a QString otherwise.
	 * The parameters format, width and height will be filled with information about the loaded texture.
	 *
	 * @param filepath	The full path to the texture that must be loaded. Can also be a color in the format
	 *                  "#AABBGGRR", "#AABBGGRRs" or "#AABBGGRRn" (hexadecimal) to generate a 1x1 texture
	 *                  from a solid color. Adding the 's' or 'n' suffix creates an sRGB or signed texture
	 *                  from the color, respectively.
	 * @param format	Contain the format, for instance "DDS (DXT3)" or "TGA", on successful load.
	 * @param width		Contains the texture width on successful load.
	 * @param height	Contains the texture height on successful load.
	 * @return			The number of mipmaps on successful load, 0 otherwise.
	 */
	GLuint texLoad( const NifModel * nif, const QString & filepath, TexFmt & format,
					GLenum & target, GLuint & width, GLuint & height, GLuint * id );

	/*! A function for loading textures.
	 *
	 * Loads a texture pointed to by model index.
	 * Returns the number of mipmaps on success, and throws a QString otherwise.
	 * The parameters format, width and height will be filled with information about the loaded texture.
	 *
	 * @param iData		Reference to pixel data block
	 * @param format	Contain the format, for instance "DDS (DXT3)" or "TGA", on successful load.
	 * @param width		Contains the texture width on successful load.
	 * @param height	Contains the texture height on successful load.
	 * @return			The number of mipmaps on successful load, 0 otherwise.
	 */
	GLuint texLoad( const QModelIndex & iData, TexFmt & format,
					GLenum & target, GLuint & width, GLuint & height, GLuint * id );

	/*! A function which checks whether the given file can be loaded.
	 *
	 * The function checks whether the file exists, is readable, and whether its extension
	 * is that of a supported file format (dds, tga, or bmp).
	 *
	 * @param filepath The full path to the texture that must be checked.
	 */
	static bool texCanLoad( const QString & filepath );

	/*! A function which checks whether the given file is supported.
	*
	* The function checks whether its extension
	* is that of a supported file format (dds, tga, or bmp).
	*
	* @param filepath The full path to the texture that must be checked.
	*/
	static bool texIsSupported( const QString & filepath );

	/*! Save pixel data to a DDS file
	 *
	 * @param index		Reference to pixel data
	 * @param filepath	The filepath to write
	 * @param width		The width of the texture
	 * @param height	The height of the texture
	 * @param mipmaps	The number of mipmaps present
	 * @return			True if the save was successful, false otherwise
	 */
	static bool texSaveDDS( const QModelIndex & index, const QString & filepath,
							const GLuint & width, const GLuint & height, const GLuint & mipmaps );

	/*! Save pixel data to a TGA file
	 *
	 * @param index		Reference to pixel data
	 * @param filepath	The filepath to write
	 * @param width		The width of the texture
	 * @param height	The height of the texture
	 * @return			True if the save was successful, false otherwise
	 */
	static bool texSaveTGA( const QModelIndex & index, const QString & filepath,
							const GLuint & width, const GLuint & height );

	/*! Save a file to pixel data
	 *
	 * @param filepath	The source texture to convert
	 * @param iData		The pixel data to write
	 */
	bool texSaveNIF( class NifModel * nif, const QString & filepath, QModelIndex & iData );

protected:
	GLuint texLoadDDS( const QString & filepath, GLenum & target, QByteArray & data, GLuint * id );
	GLuint texLoadPBRCubeMap( const NifModel * nif, const QString & filepath,
								GLenum & target, QByteArray & data, GLuint * id );
	GLuint texLoadColor( const NifModel * nif, const QString & filepath,
							GLenum & target, GLuint & width, GLuint & height, QByteArray & data, GLuint * id );
	//! Load NiPixelData or NiPersistentSrcTextureRendererData from a NifModel
	GLuint texLoadNIF( QIODevice & f, TexFmt & texformat,
						GLenum & target, GLuint & width, GLuint & height, GLuint * id );
};

#endif
