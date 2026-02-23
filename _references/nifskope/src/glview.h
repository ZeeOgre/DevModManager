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

#ifndef GLVIEW_H_INCLUDED
#define GLVIEW_H_INCLUDED

#include "gl/glscene.h"
#include "model/nifmodel.h"

#include <QOpenGLWindow> // Inherited
#include <QPersistentModelIndex>
#include <chrono>


//! @file glview.h GLView

class NifSkope;
class QTimer;


//! The main [Viewport](@ref viewport_details) class
class GLView final : public QOpenGLWindow
{
	Q_OBJECT

	friend class NifSkope;

public:
	GLView( QWindow * parent );
	~GLView();
	QWidget * createWindowContainer( QWidget * parent );

	NifSkopeOpenGLContext * glContext = nullptr;

	float	toneMapping = 0.23641851f;	// 0.05 to 1.0
	float	brightnessScale = 1.0f;		// overall brightness
	float	glowScale = 1.0f;
	float	ambient = 1.0f;				// environment map / ambient light level
	float	brightnessL = 1.0f;			// directional light intensity,
	float	lightColor = 0.0f;			// and color temperature (-1.0 to 1.0)
	float	declination = 0.0f;
	float	planarAngle = 0.0f;
	float	envMapRotation = 0.0f;
	bool	frontalLight = true;

	enum AnimationStates
	{
		AnimDisabled = 0x0,
		AnimEnabled = 0x1,
		AnimPlay = 0x2,
		AnimLoop = 0x4,
		AnimSwitch = 0x8
	};
	Q_DECLARE_FLAGS( AnimationState, AnimationStates );

	enum ViewState : unsigned char
	{
		ViewDefault,
		ViewTop,
		ViewBottom,
		ViewLeft,
		ViewRight,
		ViewFront,
		ViewBack,
		ViewWalk,
		ViewUser
	};

	enum DebugMode : unsigned char
	{
		DbgNone = 0,
		DbgColorPicker = 1,
		DbgBounds = 2
	};

	enum UpAxis : unsigned char
	{
		XAxis = 0,
		YAxis = 1,
		ZAxis = 2
	};

	void setNif( NifModel * );

	Scene * getScene();
	void updateShaders();
	void updateViewpoint();

	void flush();

	void center();
	void move( float, float, float );
	void rotate( float, float, float );
	void rotateLight( float, float );

	void setCenter();
	void setDistance( float );
	void setPosition( float, float, float );
	void setPosition( const Vector3 & );
	void setProjection( bool );
	void setRotation( float, float, float );
	void setZoom( float );

	void setOrientation( GLView::ViewState, bool recenter = true );
	void flipOrientation();

	void setDebugMode( DebugMode );
	static bool selectPBRCubeMapForGame( quint32 bsVersion );

	// Starfield: 1 unit = 1 meter
	// older games: 64 units = 1 yard = 0.9144 m
	float scale() { return (scene->nifModel && scene->nifModel->getBSVersion() >= 170) ? float(1.0 / 64.0) : 1.0f; };

	Color4 clearColor() const;


	QModelIndex indexAt( const QPointF & p, bool shiftModifier = false );

public slots:
	void update();
	void setCurrentIndex( const QModelIndex & );
	void setSceneTime( float );
	void setSceneSequence( const QString & );
	void saveUserView();
	void loadUserView();
	void setBrightness( int );
	void setLightLevel( int );
	void setLightColor( int );
	void setToneMapping( int );
	void setAmbient( int );
	void setEnvMapRotation( int );
	void setGlowScale( int );
	void setFrontalLight( bool );
	void updateScene();
	void updateAnimationState( bool checked );
	void setVisMode( Scene::VisMode, bool checked = true );
	void updateSettings();
	void update3D();
	void selectPBRCubeMap();
	void update_GL( [[maybe_unused]] int tmp ) { update(); }

signals:
	void clicked( const QModelIndex & );
	void paintUpdate();
	void sceneTimeChanged( float t, float mn, float mx );
	void viewpointChanged();
	void frontalLightChanged( bool isFrontal );

	void sequenceStopped();
	void sequenceChanged( const QString & );
	void sequencesUpdated();
	void sequencesDisabled( bool );

protected:
	//! Sets up the OpenGL rendering context, defines display lists, etc.
	void initializeGL() override final;
	//! Sets up the OpenGL viewport, projection, etc.
	void resizeGL( int width, int height ) override final;
	void resizeEvent( QResizeEvent * event ) override final;
	//! Renders the OpenGL scene.
	void paintGL() override final;
	void glProjection( int x = -1, int y = -1 );

	// QWidget Event Handlers

	void contextMenuEvent( QContextMenuEvent * );
	void dragEnterEvent( QDragEnterEvent * );
	void dragLeaveEvent( QDragLeaveEvent * );
	void dragMoveEvent( QDragMoveEvent * );
	void dropEvent( QDropEvent * );
	void focusOutEvent( QFocusEvent * ) override final;
	void keyPressEvent( QKeyEvent * ) override final;
	void keyReleaseEvent( QKeyEvent * ) override final;
	void mouseDoubleClickEvent( QMouseEvent * ) override final;
	void mouseMoveEvent( QMouseEvent * ) override final;
	void mousePressEvent( QMouseEvent * ) override final;
	void mouseReleaseEvent( QMouseEvent * ) override final;
	void wheelEvent( QWheelEvent * ) override final;

protected slots:
	void saveImage();

private:
	static const Vector3 viewRotations[6];

	NifModel * model;
	Scene * scene = nullptr;

	ViewState view;
	DebugMode debugMode;
	bool perspectiveMode;
	bool contextMenuShiftModifier;

	AnimationState animState;

	class TexCache * textures;

	QTimer * timer;
	std::chrono::steady_clock::time_point lastTime;
	float time;

	float Dist;
	Vector3 Pos;
	Vector3 Rot;
	GLdouble Zoom;
	GLdouble axis;

	GLdouble aspect;

	std::uint64_t kbdState = 0;
	QPointF lastPos;
	QPointF pressPos;
	Vector3 mouseMov;
	Vector3 mouseRot;
	std::uint32_t mouseButtonState = 0;

	QPersistentModelIndex iDragTarget;
	QString fnDragTex, fnDragTexOrg;

	bool isDisabled = false;
	unsigned char doCompile = 0;
	bool doCenter = false;
	unsigned char updatePending = 0;

	QTimer * lightVisTimer;
	int lightVisTimeout;

	int pixelWidth = 640;
	int pixelHeight = 480;

	QWidget * graphicsView = nullptr;

	enum Key : unsigned char
	{
		Key_CenterView = 1,
		Key_FrontView = 2,
		Key_LeftView = 3,
		Key_MoveBack = 4,
		Key_MoveCam = 5,
		Key_MoveDown = 6,
		Key_MoveForward = 7,
		Key_MoveLeft = 8,
		Key_MoveRight = 9,
		Key_MoveUp = 10,
		Key_Perspective = 11,
		Key_RotateDown = 12,
		Key_RotateLeft = 13,
		Key_RotateRight = 14,
		Key_RotateUp = 15,
		Key_Shift = 16,
		Key_ToggleGrid = 17,
		Key_TopView = 18,
		Key_Update = 19,
		Key_ZoomIn = 20,
		Key_ZoomOut = 21,
		Key_RotateXY = 22,
		Key_RotateZ = 23,
		Key_Scale = 24,
		Key_TranslateXY = 25
	};

	int convertKeyCode( int n ) const;
	inline bool kbd( int n ) const;
	void transformItem( float dx, float dy );

public:
	struct Settings
	{
		Color4 background;
		float fov = 60.0f;
		float moveSpd = 350.0f;
		float rotSpd = 45.0f;

		UpAxis upAxis = ZAxis;
		ViewState startupDirection = ViewFront;

		static float	vertexPointSize;
		static float	tbnPointSize;
		static float	vertexSelectPointSize;
		static float	vertexPointSizeSelected;
		static float	lineWidthAxes;
		static float	lineWidthWireframe;
		static float	lineWidthHighlight;
		static float	lineWidthGrid;
		static float	lineWidthSelect;
		static float	zoomInScale;
		static float	zoomOutScale;
	} cfg;

	//! Returns the actual dimensions in pixels
	QSize getSizeInPixels() const
	{
		return QSize( pixelWidth, pixelHeight );
	}

	inline void setDisabled( bool n )
	{
		isDisabled = n;
	}

	inline QOpenGLContext * pushGLContext();
	inline void popGLContext( QOpenGLContext * prvContext );
	static const char * getGLErrorString( int err );
	inline TexCache * getTexCache()
	{
		return textures;
	}

private slots:
	void advanceGears();

	void dataChanged( const QModelIndex &, const QModelIndex & );
	void modelChanged();
	void modelLinked();
	void modelDestroyed();

private:
	QStringList draggedNifs;
};

Q_DECLARE_OPERATORS_FOR_FLAGS( GLView::AnimationState )

inline QOpenGLContext * GLView::pushGLContext()
{
	QOpenGLContext *	prvContext = QOpenGLContext::currentContext();
	if ( context() != prvContext )
		makeCurrent();
	return prvContext;
}

inline void GLView::popGLContext( QOpenGLContext * prvContext )
{
	if ( !prvContext )
		doneCurrent();
	else if ( prvContext != context() )
		prvContext->makeCurrent( prvContext->surface() );
}

#endif
