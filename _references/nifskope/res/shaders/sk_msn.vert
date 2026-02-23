#version 410 core

out vec3 LightDir;
out vec3 ViewDir;

out vec2 texCoord;

flat out vec4 A;
out vec4 C;
flat out vec4 D;

#include "uniforms.glsl"

uniform mat4 modelViewMatrix;

uniform vec4 vertexColorOverride;	// components greater than zero replace the vertex color

layout ( location = 0 ) in vec3	vertexPosition;
layout ( location = 1 ) in vec4	vertexColor;
layout ( location = 7 ) in vec2	multiTexCoord0;

#define BT_POSITION_ONLY 1
#include "bonetransform.glsl"

void main()
{
	vec4	v = vec4( vertexPosition, 1.0 );

	if ( boneWeights[0].x > 0.0 && doSkinning )
		boneTransform( v );

	v = modelViewMatrix * v;
	gl_Position = projectionMatrix * v;
	texCoord = multiTexCoord0;

	if ( projectionMatrix[3][3] == 1.0 )
		ViewDir = vec3(0.0, 0.0, 1.0);	// orthographic view
	else
		ViewDir = -v.xyz;
	LightDir = lightSourcePosition[0].xyz;

	A = vec4( sqrt(lightSourceAmbient.rgb) * 0.375, toneMapScale );
	C = mix( vertexColor, vertexColorOverride, greaterThan( vertexColorOverride, vec4( 0.0 ) ) );
	D = vec4( sqrt(lightSourceDiffuse[0].rgb), brightnessScale );
}
