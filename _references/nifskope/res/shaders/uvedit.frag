#version 410 core

uniform sampler2D BaseMap;

//  0 = no color conversion
//  1 = sRGB texture
//  2 = unsigned BC5 normal map
//  3 = signed BC5 normal map
// +4 = disable alpha channel
uniform int textureColorMode;

uniform vec2 uvCenter;
uniform vec4 uvScaleAndOffset;
uniform float uvRotation;

uniform vec2 pixelScale;

uniform vec4 gridColors[3];
uniform vec3 gridLineWidths;
uniform bool gridEnabled[3];

uniform vec4 backgroundColor;
uniform vec2 textureColorScale;

in vec2 texCoord;

out vec4 fragColor;


vec4 srgbCompress( vec4 c )
{
	vec3	tmp = max( c.rgb, vec3( 0.0 ) ) + 0.000858025;
	vec3	tmp1 = tmp;
	vec3	tmp2 = tmp * tmp;
	tmp = inversesqrt( tmp ) * tmp1;
	tmp1 = tmp1 * 0.59302883 + 1.42598062;
	tmp2 = tmp2 * -0.18732371 - 0.04110602;
	tmp = ( tmp * -0.79095451 + tmp1 ) * tmp + tmp2;
	return vec4( tmp, c.a );
}

vec3 drawGridLines( vec3 color, float x, float s )
{
	vec3	f = vec3( x ) * vec3( 4.0, 16.0, 64.0 );
	f = abs( f - round( f ) ) * vec3( 0.25, 0.0625, 0.015625 ) * s;
	f = max( f - ( gridLineWidths * 0.5 - 0.5 ), vec3( 0.0 ) );
	if ( gridEnabled[0] && f.x < 1.0 )
		return mix( color, gridColors[0].rgb, ( cos( f.x * 3.14159265 ) * 0.5 + 0.5 ) * gridColors[0].a );
	if ( gridEnabled[1] && f.y < 1.0 )
		return mix( color, gridColors[1].rgb, ( cos( f.y * 3.14159265 ) * 0.5 + 0.5 ) * gridColors[1].a );
	if ( gridEnabled[2] && f.z < 1.0 )
		return mix( color, gridColors[2].rgb, ( cos( f.z * 3.14159265 ) * 0.5 + 0.5 ) * gridColors[2].a );
	return color;
}

void main()
{
	vec4	color = vec4( 0.0 );

	if ( texCoord.s >= -1.0 && texCoord.s <= 2.0 && texCoord.t >= -1.0 && texCoord.t <= 2.0 ) {
		vec2	offs = texCoord.st - uvCenter;
		float	r_c = cos( uvRotation );
		float	r_s = sin( uvRotation ) * -1.0;
		offs = vec2( offs.x * r_c - offs.y * r_s, offs.x * r_s + offs.y * r_c ) * uvScaleAndOffset.xy;
		offs = offs + uvCenter + uvScaleAndOffset.zw;

		color = texture( BaseMap, offs );

		if ( ( textureColorMode & 2 ) != 0 ) {
			// BC5 normal map
			if ( ( textureColorMode & 1 ) == 0 ) {
				// UNORM
				color = color * 2.0 - 1.0;
			}
			color.b = sqrt( max( 1.0 - dot( color.rg, color.rg ), 0.0 ) );
			color = color * 0.5 + 0.5;
		} else if ( ( textureColorMode & 1 ) != 0 ) {
			// convert to sRGB
			color = srgbCompress( color );
		}

		if ( ( textureColorMode & 4 ) != 0 )
			color.a = 1.0;

		if ( texCoord.s >= 0.0 && texCoord.s <= 1.0 && texCoord.t >= 0.0 && texCoord.t <= 1.0 )
			color.rgb *= textureColorScale.x;
		else
			color.rgb *= textureColorScale.y;
	}

	color = vec4( mix( backgroundColor.rgb, color.rgb, color.a ), 1.0 );

	color.rgb = drawGridLines( drawGridLines( color.rgb, texCoord.s, pixelScale.x ), texCoord.t, pixelScale.y );

	fragColor = color;
}
