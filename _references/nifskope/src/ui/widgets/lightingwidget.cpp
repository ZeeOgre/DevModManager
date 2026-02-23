#include "lightingwidget.h"
#include "ui_lightingwidget.h"

#include "glview.h"
#include "nifskope.h"

#include <QAction>
#include <QSettings>


// Slider lambda
auto sld = []( QSlider * slider, int min, int max, int val ) {
	slider->setSizePolicy( QSizePolicy::MinimumExpanding, QSizePolicy::Maximum );
	slider->setRange( min, max );
	slider->setSingleStep( max / 8 );
	slider->setPageStep( ( max - min ) / 4 );
	slider->setTickInterval( max / 2 );
	slider->setTickPosition( QSlider::TicksBelow );
	slider->setValue( val );
};

LightingWidget::LightingWidget( GLView * ogl, QWidget * parent ) : QWidget(parent),
	ui(new Ui::LightingWidget)
{
	ui->setupUi(this);

	setDefaults();

	// Disable Frontal checkbox (and sliders) when no lighting
	connect( ui->btnLighting, &QToolButton::toggled, ui->btnFrontal, &QToolButton::setEnabled );
	connect( ui->btnLighting, &QToolButton::toggled, [&]( bool checked ) {
		ui->sldEnvMapRotation->setEnabled( checked );
	} );

	// Inform ogl of changes
	connect( ui->sldDirectional, &QSlider::valueChanged, ogl, &GLView::setLightLevel );
	connect( ui->sldLightColor, &QSlider::valueChanged, ogl, &GLView::setLightColor );
	connect( ui->sldAmbient, &QSlider::valueChanged, ogl, &GLView::setAmbient );
	connect( ui->sldEnvMapRotation, &QSlider::valueChanged, ogl, &GLView::setEnvMapRotation );
	connect( ui->sldGlowScale, &QSlider::valueChanged, ogl, &GLView::setGlowScale );
	connect( ui->sldLightScale, &QSlider::valueChanged, ogl, &GLView::setBrightness );
	connect( ui->sldToneMapping, &QSlider::valueChanged, ogl, &GLView::setToneMapping );
	connect( ui->btnFrontal, &QToolButton::toggled, ogl, &GLView::setFrontalLight );
	connect( ogl, &GLView::frontalLightChanged, ui->btnFrontal, &QToolButton::setChecked );
	connect( ui->btnLoadCubeMap, &QPushButton::clicked, ogl, &GLView::selectPBRCubeMap );

	// Load default settings
	QSettings	settings;
	int	tmp = settings.value( "Settings/Render/Lighting/Directional Level", POS ).toInt();
	ui->sldDirectional->setValue( std::clamp< int >( tmp, 0, BRIGHT ) );
	tmp = settings.value( "Settings/Render/Lighting/Light Color", POS ).toInt();
	ui->sldLightColor->setValue( std::clamp< int >( tmp, 0, BRIGHT ) );
	tmp = settings.value( "Settings/Render/Lighting/Ambient Level", POS ).toInt();
	ui->sldAmbient->setValue( std::clamp< int >( tmp, 0, BRIGHT ) );
	tmp = settings.value( "Settings/Render/Lighting/Cube Map Rotation", 0 ).toInt();
	ui->sldEnvMapRotation->setValue( std::clamp< int >( tmp, -POS, POS ) );
	tmp = settings.value( "Settings/Render/Lighting/Glow Scale", POS ).toInt();
	ui->sldGlowScale->setValue( std::clamp< int >( tmp, 0, BRIGHT ) );
	tmp = settings.value( "Settings/Render/Lighting/Brightness Scale", POS ).toInt();
	ui->sldLightScale->setValue( std::clamp< int >( tmp, 0, BRIGHT ) );
	tmp = settings.value( "Settings/Render/Lighting/Tone Mapping", POS ).toInt();
	ui->sldToneMapping->setValue( std::clamp< int >( tmp, 0, BRIGHT ) );
	ui->btnFrontal->setChecked( settings.value( "Settings/Render/Lighting/Frontal Light", true ).toBool() );

	tmp = settings.value( "Lighting/Declination", 0 ).toInt();
	ogl->declination = float( tmp % int(POS) ) * ( 180.0f / float(POS) );
	tmp = settings.value( "Lighting/Planar Angle", 0 ).toInt();
	ogl->planarAngle = float( tmp % int(POS) ) * ( 180.0f / float(POS) );
}

LightingWidget::~LightingWidget()
{
}

void LightingWidget::setDefaults()
{
	sld( ui->sldDirectional, DirMin, DirMax, DirDefault );
	sld( ui->sldLightColor, LightColorMin, LightColorMax, LightColorDefault );
	ui->sldLightColor->setSingleStep( LightColorMax / 16 );
	ui->sldLightColor->setTickInterval( LightColorMax / 8 );
	sld( ui->sldAmbient, AmbientMin, AmbientMax, AmbientDefault );
	sld( ui->sldEnvMapRotation, EnvMapRotationMin, EnvMapRotationMax, EnvMapRotationDefault );
	sld( ui->sldGlowScale, GlowScaleMin, GlowScaleMax, GlowScaleDefault );
	sld( ui->sldLightScale, LightScaleMin, LightScaleMax, LightScaleDefault );
	sld( ui->sldToneMapping, ToneMappingMin, ToneMappingMax, ToneMappingDefault );
}

void LightingWidget::setActions( QVector<QAction *> atns )
{
	ui->btnLighting->setDefaultAction( atns.value(0) );
	ui->btnTextures->setDefaultAction( atns.value(1) );
	ui->btnVertexColors->setDefaultAction( atns.value(2) );
	ui->btnSpecular->setDefaultAction( atns.value(3) );
	ui->btnCubemap->setDefaultAction( atns.value(4) );
	ui->btnGlow->setDefaultAction( atns.value(5) );
	ui->btnLightingOnly->setDefaultAction( atns.value(6) );
	ui->btnSilhouette->setDefaultAction( atns.value(7) );

	connect( ui->btnLighting, &QToolButton::toggled, atns.value(3), &QAction::setEnabled );
}

void LightingWidget::saveSettings()
{
	QSettings	settings;
	settings.setValue( "Settings/Render/Lighting/Directional Level", ui->sldDirectional->value() );
	settings.setValue( "Settings/Render/Lighting/Light Color", ui->sldLightColor->value() );
	settings.setValue( "Settings/Render/Lighting/Ambient Level", ui->sldAmbient->value() );
	settings.setValue( "Settings/Render/Lighting/Cube Map Rotation", ui->sldEnvMapRotation->value() );
	settings.setValue( "Settings/Render/Lighting/Glow Scale", ui->sldGlowScale->value() );
	settings.setValue( "Settings/Render/Lighting/Brightness Scale", ui->sldLightScale->value() );
	settings.setValue( "Settings/Render/Lighting/Tone Mapping", ui->sldToneMapping->value() );
	settings.setValue( "Settings/Render/Lighting/Frontal Light", ui->btnFrontal->isChecked() );

	for ( QObject * o = parent(); o; o = o->parent() ) {
		auto	w = qobject_cast< NifSkope * >( o );
		if ( !w )
			continue;
		if ( auto v = w->getGLView(); v ) {
			float	scale = float( POS ) / 180.0f;
			settings.setValue( "Settings/Render/Lighting/Declination", roundFloat( v->declination * scale ) );
			settings.setValue( "Settings/Render/Lighting/Planar Angle", roundFloat( v->planarAngle * scale ) );
		}
		break;
	}
}
