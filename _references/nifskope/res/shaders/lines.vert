#version 410 core

out vec4 vsColor;

uniform mat4 modelViewMatrix;

uniform vec4 vertexColorOverride;	// components greater than zero replace the vertex color
uniform vec4 highlightColor;

layout ( location = 0 ) in vec3	vertexPosition;
layout ( location = 1 ) in vec4	vertexColor;

void main()
{
	vec4	v = vec4( vertexPosition, 1.0 );

	gl_Position = modelViewMatrix * v;

	vsColor = mix( vertexColor, vertexColorOverride, greaterThan( vertexColorOverride, vec4( 0.0 ) ) );
}
