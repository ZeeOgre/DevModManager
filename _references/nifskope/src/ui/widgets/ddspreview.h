#ifndef DDSPREVIEW_H_INCLUDED
#define DDSPREVIEW_H_INCLUDED

#include <QPaintEvent>
#include <QSize>
#include <QWidget>

#include "ddstxt16.hpp"
#include "gamemanager.h"

class DDSTexturePreview : public QWidget
{
	Q_OBJECT

protected:
	const DDSTexture16 *	t = nullptr;
	// bit 0 = normal map, bit 1 = signed format, bit 2 = invert cube map Z axis
	unsigned short	textureFlags = 0;
	unsigned short	defaultSize = 512;
	float	mipLevel = 0.0f;

	static void threadFunction( DDSTexturePreview * p, std::uint32_t * imgBuf, int w, int h, int y0, int y1 );

public:
	DDSTexturePreview( QWidget * parent = nullptr );
	virtual ~DDSTexturePreview();

	void setTexture( const DDSTexture16 * txt, bool isNormalMap, bool invertCubeMapZAxis );

	QSize sizeHint() const override;

protected:
	void paintEvent( QPaintEvent * ) override;
};

class DDSTextureInfo : public QWidget
{
	Q_OBJECT

protected:
	DDSTexturePreview *	textureView = nullptr;
	DDSTexture16 *	t = nullptr;

public:
	DDSTextureInfo( Game::GameManager::GameResources & gameResources, const QString & filePath,
					QWidget * parent = nullptr );
	virtual ~DDSTextureInfo();
};

#endif
