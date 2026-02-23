#version 410 core

#include "uniforms.glsl"

uniform samplerCube	CubeMap;
uniform bool	hasCubeMap;

in vec3 LightDir;
in vec3 ViewDir;

flat in mat3 reflMatrix;

out vec4 fragColor;

float LightingFuncGGX_REF( float LdotR, float roughness )
{
	float alpha = roughness * roughness;
	// D (GGX normal distribution)
	float alphaSqr = alpha * alpha;
	// denom = NdotH * NdotH * (alphaSqr - 1.0) + 1.0,
	// LdotR = NdotH * NdotH * 2.0 - 1.0
	float denom = LdotR * alphaSqr + alphaSqr + max(1.0 - LdotR, 0.0);
	// no pi because BRDF -> lighting
	return alphaSqr / (denom * denom);
}

vec3 tonemap(vec3 x, float y)
{
	float a = 0.15;
	float b = 0.50;
	float c = 0.10;
	float d = 0.20;
	float e = 0.02;
	float f = 0.30;

	vec3 z = x * (y * 4.22978723);
	z = (z * (a * z + b * c) + d * e) / (z * (a * z + b) + d * f) - e / f;
	return z / (y * 0.93333333);
}

void main()
{
	vec3	L = normalize( LightDir );
	vec3	V = normalize( -ViewDir );

	float	VdotL = dot( V, L );

	vec3	viewWS = reflMatrix * V;

	float	m = clamp( float(cubeBgndMipLevel), 0.0, 6.0 );

	// Environment
	vec3	color = lightSourceAmbient.rgb;
	if ( hasCubeMap ) {
		color *= textureLod( CubeMap, viewWS, m ).rgb;
	} else {
		color *= 0.08;
	}
	// Directional light
	if ( VdotL > 0.0 ) {
		float	roughness = ( 5.0 - sqrt( 25.0 - 4.0 * m ) ) / 4.0;
		color += lightSourceDiffuse[0].rgb * LightingFuncGGX_REF( VdotL, max(roughness, 0.02) ) * VdotL;
	}

	fragColor = vec4( tonemap( color * brightnessScale, toneMapScale ), 0.0 );
}
