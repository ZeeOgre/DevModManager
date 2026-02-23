#version 410 core

layout ( points ) in;
layout ( triangle_strip, max_vertices = 4 ) out;

#include "uniforms.glsl"

uniform int btnSelection;				// 0: draw bitangent, 1: draw tangent, 2: draw normal
uniform float normalLineLength;			// line length in view space

in mat3 btnMatrix[];

in vec4 vsColor[];
out vec4 C;

#include "drawline.glsl"

void main()
{
	C = vec4( vsColor[0].rgb, vsColor[0].a * min( lineWidth, 1.0 ) );

	vec4	d = vec4( btnMatrix[0][clamp( btnSelection, 0, 2 )] * normalLineLength, 0.0 );
	vec4	p0 = projectionMatrix * ( gl_in[0].gl_Position - d * 0.2 );
	vec4	p1 = projectionMatrix * ( gl_in[0].gl_Position + d * 0.8 );

	drawLine( p0, p1 );
}
