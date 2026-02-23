#version 410 core

out vec3 LightDir;
out vec3 ViewDir;

flat out mat3 reflMatrix;

#include "uniforms.glsl"

uniform bool invertZAxis;

layout ( location = 0 ) in vec3 vertexPosition;

void main()
{
	vec4 v = vec4( viewMatrix * vertexPosition, 1.0 );

	ViewDir = vec3(-v.xy, 1.0);
	LightDir = lightSourcePosition[0].xyz;

	reflMatrix = envMapRotation;
	if ( invertZAxis ) {
		reflMatrix[0][2] *= -1.0;
		reflMatrix[1][2] *= -1.0;
		reflMatrix[2][2] *= -1.0;
	}

	if ( projectionMatrix[3][3] == 1.0 )
		gl_Position = vec4(0.0, 0.0, 2.0, 1.0);	// orthographic view is not supported
	else
		gl_Position = vec4( ( projectionMatrix * v ).xy / clamp( projectionMatrix[0][0], 0.001, 1.0 ), 1.0, 1.0 );
}
