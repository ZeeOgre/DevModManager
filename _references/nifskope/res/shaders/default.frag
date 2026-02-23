#version 410 core

struct Texture {
	vec2 uvCenter;
	vec2 uvScale;
	vec2 uvOffset;
	float uvRotation;
	int coordSet;
	int textureUnit;
};

#include "uniforms.glsl"

uniform sampler2D textureUnits[10];
uniform Texture textures[10];

// bits 0 to 2: color mode
// bit 3: lighting mode
// bits 4 to 5: vertex mode
uniform int vertexColorFlags;

uniform float alpha;
uniform int alphaFlags;			// bits 0 to 2: alpha test mode, bit 3: alpha blending enabled
uniform float alphaThreshold;

uniform vec4 frontMaterialDiffuse;
uniform vec4 frontMaterialSpecular;
uniform vec4 frontMaterialAmbient;
uniform vec4 frontMaterialEmission;
uniform float frontMaterialShininess;

in vec3 LightDir;
in vec3 ViewDir;

in vec2 texCoords[9];

flat in vec4 A;
in vec4 C;
flat in vec4 D;

in vec3 N;

out vec4 fragColor;


vec4 getTexture( int n )
{
	float	r_c = cos( textures[n].uvRotation );
	float	r_s = sin( textures[n].uvRotation ) * -1.0;
	vec2	offs = texCoords[textures[n].coordSet].st - textures[n].uvCenter;
	offs = vec2( offs.x * r_c - offs.y * r_s, offs.x * r_s + offs.y * r_c );
	offs = offs * textures[n].uvScale + textures[n].uvCenter + textures[n].uvOffset;

	return texture( textureUnits[textures[n].textureUnit - 1], offs );
}

void main()
{
	vec3 L = normalize( LightDir );
	vec3 E = normalize( ViewDir );

	vec4 color = vec4( 1.0 );

	vec3 normal = normalize( N );

	float NdotL = max( dot(normal, L), 0.0 );

	if ( ( vertexColorFlags & 0x28 ) == 0x28 ) {
		color *= C;
		color.rgb *= A.rgb + ( D.rgb * NdotL );
	} else if ( ( vertexColorFlags & 0x08 ) != 0 ) {
		color.rgb *= ( A.rgb * frontMaterialAmbient.rgb ) + ( D.rgb * frontMaterialDiffuse.rgb * NdotL );
		color.a *= min( frontMaterialAmbient.a + frontMaterialDiffuse.a, 1.0 );
	} else {
		color.rgb *= A.rgb + ( D.rgb * NdotL );
	}

	// Emissive
	vec3 emissive = frontMaterialEmission.rgb * frontMaterialEmission.a;
	if ( ( vertexColorFlags & 0x10 ) != 0 )
		emissive = C.rgb * C.a;
	color.rgb += emissive * glowScaleSRGB;

	color.a *= alpha;

	// Texturing
	if ( textures[0].textureUnit > 0 ) {
		// base
		color *= getTexture( 0 );
	}
	if ( textures[1].textureUnit > 0 ) {
		// dark
		color *= getTexture( 1 );
	}
	if ( textures[2].textureUnit > 0 ) {
		// detail
		color *= getTexture( 2 );
		color.rgb *= 2.0;
	}
	if ( textures[6].textureUnit > 0 ) {
		// decal 0
		vec4	tmp = getTexture( 6 );
		color.rgb = mix( color.rgb, tmp.rgb, tmp.a );
	}
	if ( textures[7].textureUnit > 0 ) {
		// decal 1
		vec4	tmp = getTexture( 7 );
		color.rgb = mix( color.rgb, tmp.rgb, tmp.a );
	}
	if ( textures[8].textureUnit > 0 ) {
		// decal 2
		vec4	tmp = getTexture( 8 );
		color.rgb = mix( color.rgb, tmp.rgb, tmp.a );
	}
	if ( textures[9].textureUnit > 0 ) {
		// decal 3
		vec4	tmp = getTexture( 9 );
		color.rgb = mix( color.rgb, tmp.rgb, tmp.a );
	}
	if ( textures[4].textureUnit > 0 ) {
		// glow
		color.rgb += getTexture( 4 ).rgb * glowScaleSRGB;
	}

	// Specular
#if 0
	if ( NdotL > 0.0 ) {
		vec3 H = normalize( L + E );
		float NdotH = dot( normal, H );
		if ( NdotH > 0.0 ) {
			vec3 spec = frontMaterialSpecular.rgb * pow( NdotH, frontMaterialShininess );
			color.rgb += spec * frontMaterialSpecular.a;
		}
	}
#endif

	float	a = 1.0;
	if ( alphaFlags > 0 ) {
		// 0: always, 1: <, 2: ==, 3: <=, 4: >, 5: !=, 6: >=, 7: never
		int	m = ( color.a < alphaThreshold ? 0x2B2B : ( color.a > alphaThreshold ? 0x7171 : 0x4D4D ) );
		if ( ( m & ( 1 << alphaFlags ) ) == 0 )
			discard;
		if ( ( alphaFlags & 8 ) != 0 )
			a = color.a;
	}

	fragColor = vec4( color.rgb * D.a, a );
}
