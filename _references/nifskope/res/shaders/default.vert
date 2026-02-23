#version 410 core

out vec3 LightDir;
out vec3 ViewDir;

out vec2 texCoords[9];

flat out vec4 A;
out vec4 C;
flat out vec4 D;

out vec3 N;

#include "uniforms.glsl"

uniform mat3 normalMatrix;			// in row-major order
uniform mat4 modelViewMatrix;

uniform vec4 vertexColorOverride;	// components greater than zero replace the vertex color

layout ( location = 0 ) in vec3 vertexPosition;
layout ( location = 1 ) in vec4 vertexColor;
layout ( location = 2 ) in vec3 normalVector;
layout ( location = 7 ) in vec2 multiTexCoord0;
layout ( location = 8 ) in vec2 multiTexCoord1;
layout ( location = 9 ) in vec2 multiTexCoord2;
layout ( location = 10 ) in vec2 multiTexCoord3;
layout ( location = 11 ) in vec2 multiTexCoord4;
layout ( location = 12 ) in vec2 multiTexCoord5;
layout ( location = 13 ) in vec2 multiTexCoord6;
layout ( location = 14 ) in vec2 multiTexCoord7;
layout ( location = 15 ) in vec2 multiTexCoord8;

#define BT_NO_TANGENTS 1
#include "bonetransform.glsl"

void main()
{
	vec4	v = vec4( vertexPosition, 1.0 );
	vec3	n = normalVector;

	if ( boneWeights[0].x > 0.0 && doSkinning )
		boneTransform( v, n );

	v = modelViewMatrix * v;
	gl_Position = projectionMatrix * v;
	texCoords[0] = multiTexCoord0;
	texCoords[1] = multiTexCoord1;
	texCoords[2] = multiTexCoord2;
	texCoords[3] = multiTexCoord3;
	texCoords[4] = multiTexCoord4;
	texCoords[5] = multiTexCoord5;
	texCoords[6] = multiTexCoord6;
	texCoords[7] = multiTexCoord7;
	texCoords[8] = multiTexCoord8;

	N = normalize( n * normalMatrix );

	if ( projectionMatrix[3][3] == 1.0 )
		ViewDir = vec3(0.0, 0.0, 1.0);	// orthographic view
	else
		ViewDir = -v.xyz;
	LightDir = lightSourcePosition[0].xyz;

	A = vec4( sqrt(lightSourceAmbient.rgb) * 0.375, toneMapScale );
	C = mix( vertexColor, vertexColorOverride, greaterThan( vertexColorOverride, vec4( 0.0 ) ) );
	D = sqrt( vec4(lightSourceDiffuse[0].rgb, brightnessScale) );
}
