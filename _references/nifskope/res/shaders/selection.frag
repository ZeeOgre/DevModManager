#version 410 core

in vec4 C;

// bit 0 = Scene::selecting
// bit 1 = vertex mode (drawing points instead of triangles)
// bit 2 = triangle selection mode (primitive ID << 15 is added to the color)
// bits 8 to 15 = point size * 8 (0: do not draw smooth points)
uniform int selectionFlags;

out vec4 fragColor;

void main()
{
	vec4	color = C;

	if ( ( selectionFlags & 2 ) != 0 ) {
		// draw points as circles
		vec2	d = gl_PointCoord - vec2( 0.5 );
		float	r2 = dot( d, d );
		if ( r2 > 0.25 )
			discard;

		// with anti-aliasing if enabled
		int	p = selectionFlags & 0xFF00;
		if ( p != 0 )
			color.a *= clamp( ( 0.5 - sqrt(r2) ) * float( p ) / 2048.0, 0.0, 1.0 );
	} else if ( ( selectionFlags & 4 ) != 0 ) {
		color = unpackUnorm4x8( packUnorm4x8( color ) + ( uint( gl_PrimitiveID ) << 15 ) ) + vec4( 0.0005 );
	}

	fragColor = color;
}
