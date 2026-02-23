#version 410 core

out vec3 LightDir;
out vec3 ViewDir;

out vec2 texCoord;

out mat3 btnMatrix;

flat out vec4 A;
out vec4 C;
flat out vec4 D;

flat out mat3 reflMatrix;

#include "uniforms.glsl"

uniform mat3 normalMatrix;			// in row-major order
uniform mat4 modelViewMatrix;

uniform vec4 vertexColorOverride;	// components greater than zero replace the vertex color

layout ( location = 0 ) in vec3	vertexPosition;
layout ( location = 1 ) in vec4	vertexColor;
layout ( location = 2 ) in vec3	normalVector;
layout ( location = 3 ) in vec3	tangentVector;
layout ( location = 4 ) in vec3	bitangentVector;
layout ( location = 7 ) in vec2	multiTexCoord0;

#include "bonetransform.glsl"

void main()
{
	vec4	v = vec4( vertexPosition, 1.0 );
	vec3	n = normalVector;
	vec3	t = tangentVector;
	vec3	b = bitangentVector;

	if ( boneWeights[0].x > 0.0 && doSkinning )
		boneTransform( v, n, t, b );

	v = modelViewMatrix * v;
	gl_Position = projectionMatrix * v;
	texCoord = multiTexCoord0;

	btnMatrix[2] = normalize( n * normalMatrix );
	btnMatrix[1] = normalize( t * normalMatrix );
	btnMatrix[0] = normalize( b * normalMatrix );

	reflMatrix = envMapRotation;
	reflMatrix[0][2] *= -1.0;
	reflMatrix[1][2] *= -1.0;
	reflMatrix[2][2] *= -1.0;

	if ( projectionMatrix[3][3] == 1.0 )
		ViewDir = vec3(0.0, 0.0, 1.0);	// orthographic view
	else
		ViewDir = -v.xyz;
	LightDir = lightSourcePosition[0].xyz;

	A = vec4( sqrt(lightSourceAmbient.rgb) * 0.375, toneMapScale );
	C = mix( vertexColor, vertexColorOverride, greaterThan( vertexColorOverride, vec4( 0.0 ) ) );
	D = vec4( sqrt(lightSourceDiffuse[0].rgb), brightnessScale );
}
