#version 410 core

out vec3 vsLightDir;
out vec3 vsViewDir;

out vec4 vsColor;
out float vsParticleSize;

#include "uniforms.glsl"

uniform mat4 modelViewMatrix;

uniform vec4 vertexColorOverride;	// components greater than zero replace the vertex color

layout ( location = 0 ) in vec3 vertexPosition;
layout ( location = 1 ) in vec4 vertexColor;
// location 4 (bitangent) is used for sizes because the default is set to vec3(1, 0, 0)
layout ( location = 4 ) in float particleSize;

void main()
{
	vec4	v = modelViewMatrix * vec4( vertexPosition, 1.0 );

	gl_Position = v;

	if ( projectionMatrix[3][3] == 1.0 )
		vsViewDir = vec3(0.0, 0.0, 1.0);	// orthographic view
	else
		vsViewDir = -v.xyz;
	vsLightDir = lightSourcePosition[0].xyz;

	vsColor = mix( vertexColor, vertexColorOverride, greaterThan( vertexColorOverride, vec4( 0.0 ) ) );
	vsParticleSize = particleSize;
}
