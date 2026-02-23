uniform float lineWidth;

void drawLine( vec4 p0, vec4 p1 )
{
	float	p0zw = p0.z + p0.w;
	float	p1zw = p1.z + p1.w;
	if ( !( p0zw > 0.0 && p1zw > 0.0 ) ) {
		if ( p0zw > 0.000001 )
			p1 = mix( p0, p1, p0zw / ( p0zw - p1zw ) );
		else if ( p1zw > 0.000001 )
			p0 = mix( p1, p0, p1zw / ( p1zw - p0zw ) );
		else
			return;
	}

	vec3	p0_ndc = p0.xyz / p0.w;
	vec3	p1_ndc = p1.xyz / p1.w;

	vec2	vpScale = vec2( viewportDimensions.zw ) * 0.5;
	vec2	vpOffs = vec2( viewportDimensions.xy ) + vpScale;

	vec2	p0_ss = p0_ndc.xy * vpScale;
	vec2	p1_ss = p1_ndc.xy * vpScale;

	vec2	d = normalize( p1_ss - p0_ss ) * max( lineWidth * 0.5, 0.5 );
	vec2	n = vec2( -d.y, d.x ) / vpScale;

	gl_Position = vec4( p0_ndc.xy + n, p0_ndc.z, 1.0 );
	EmitVertex();
	gl_Position = vec4( p0_ndc.xy - n, p0_ndc.z, 1.0 );
	EmitVertex();
	gl_Position = vec4( p1_ndc.xy + n, p1_ndc.z, 1.0 );
	EmitVertex();
	gl_Position = vec4( p1_ndc.xy - n, p1_ndc.z, 1.0 );
	EmitVertex();

	EndPrimitive();
}
