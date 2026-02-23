#version 410 core

out vec4 C;

#include "uniforms.glsl"

uniform mat4 modelViewMatrix;

uniform vec4 vertexColorOverride;	// components greater than zero replace the vertex color
uniform vec4 highlightColor;

// bit 0 = Scene::selecting
// bit 1 = vertex mode (drawing points instead of triangles)
// bit 2 = triangle selection mode (primitive ID << 15 is added to the color)
// bit 3 = draw bone weights
// bits 8 to 15 = point size * 8 (0: do not draw smooth points)
uniform int selectionFlags;
// if Scene::selecting == false: vertex selected (-1: none)
// if Scene::selecting == true: value to add to color key (e.g. shapeID << 16)
uniform int selectionParam;

layout ( location = 0 ) in vec3	vertexPosition;
layout ( location = 1 ) in vec4	vertexColor;

#define BT_POSITION_ONLY 1
#include "bonetransform.glsl"

vec4 boneWeightsColor()
{
	vec4	vcSum = vec4( 0.0 );
	float	wSum = 0.0;
	for ( int i = 0; i < 8; i++ ) {
		float	w = boneWeights[i >> 2][i & 3];
		if ( !( w > 0.0 ) )
			break;
		int	b = int( w );
		w = fract( w );
		int	vc = ( b & 0x0049 ) | ( ( b & 0x0092 ) << 7 ) | ( ( b & 0x0124 ) << 14 );
		vc = ( ( vc & 0x00010101 ) << 7 ) | ( ( vc & 0x00080808 ) << 3 ) | ( ( vc & 0x00404040 ) >> 1 );
		vcSum += unpackUnorm4x8( uint( vc ) ^ 0xE0E0E0E0u ) * w;
		wSum += w;
	}
	if ( wSum > 0.0 )
		vcSum /= wSum;
	return vcSum;
}

void main()
{
	if ( ( selectionFlags & 1 ) != 0 ) {
		int	colorKey = selectionParam + 1;
		if ( ( selectionFlags & 2 ) != 0 )
			colorKey = colorKey + gl_VertexID;
		C = unpackUnorm4x8( uint( colorKey ) ) + vec4( 0.0005 );
	} else if ( !( gl_VertexID == selectionParam && ( selectionFlags & 2 ) != 0 ) ) {
		C = mix( vertexColor, vertexColorOverride, greaterThan( vertexColorOverride, vec4( 0.0 ) ) );
	} else {
		C = highlightColor;
	}

	vec4	v = vec4( vertexPosition, 1.0 );

	if ( boneWeights[0].x > 0.0 ) {
		if ( doSkinning )
			boneTransform( v );
		if ( ( selectionFlags & 8 ) != 0 )
			C = boneWeightsColor();
	}

	v = projectionMatrix * ( modelViewMatrix * v );

	gl_Position = v;
}
