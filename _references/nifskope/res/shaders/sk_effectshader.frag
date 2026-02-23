#version 410 core

#include "uniforms.glsl"

uniform sampler2D BaseMap;
uniform sampler2D GreyscaleMap;

uniform bool hasSourceTexture;
uniform bool hasGreyscaleMap;
uniform bool greyscaleAlpha;
uniform bool greyscaleColor;

uniform bool useFalloff;
uniform bool vertexColors;
uniform bool vertexAlpha;

uniform bool hasWeaponBlood;

uniform vec4 glowColor;
uniform float glowMult;

uniform int alphaFlags;			// bits 0 to 2: alpha test mode, bit 3: alpha blending enabled
uniform float alphaThreshold;

uniform vec2 uvScale;
uniform vec2 uvOffset;

uniform vec4 falloffParams;
uniform float falloffDepth;

in vec3 LightDir;
in vec3 ViewDir;

in vec2 texCoord;

in vec4 C;

in vec3 N;

out vec4 fragColor;

vec4 colorLookup( float x, float y ) {

	return texture( GreyscaleMap, vec2( clamp(x, 0.0, 1.0), clamp(y, 0.0, 1.0)) );
}

void main()
{
	vec4 baseMap = texture( BaseMap, texCoord.st * uvScale + uvOffset );

	vec4 color;

	vec3 normal = normalize( N );

	// Reconstructed normal
	//normal = normalize(cross(dFdy(v.xyz), dFdx(v.xyz)));

	vec3 E = normalize(ViewDir);

	float tmp2 = falloffDepth; // Unused right now

	// Falloff
	float falloff = 1.0;
	if ( useFalloff ) {
		float startO = min(falloffParams.z, 1.0);
		float stopO = max(falloffParams.w, 0.0);

		// TODO: When X and Y are both 0.0 or both 1.0 the effect is reversed.
		falloff = smoothstep( falloffParams.y, falloffParams.x, abs(E.b));

		falloff = mix( max(falloffParams.w, 0.0), min(falloffParams.z, 1.0), falloff );
	}

	float alphaMult = glowColor.a * glowColor.a;

	color = baseMap;

	if ( hasWeaponBlood ) {
		color.rgb = vec3( 1.0, 0.0, 0.0 ) * baseMap.r;
		color.a = baseMap.a * baseMap.g;
	}

	color *= C;
	color.rgb *= glowColor.rgb;
	color.a *= falloff * alphaMult;

	if ( greyscaleColor ) {
		// Only Red emissive channel is used
		float emRGB = glowColor.r;

		vec4 luG = colorLookup( baseMap.g, C.g * falloff * emRGB );

		color.rgb = luG.rgb;
	}

	if ( greyscaleAlpha ) {
		vec4 luA = colorLookup( baseMap.a, C.a * falloff * alphaMult );

		color.a = luA.a;
	}

	if ( alphaFlags > 0 ) {
		// 0: always, 1: <, 2: ==, 3: <=, 4: >, 5: !=, 6: >=, 7: never
		int	m = ( color.a < alphaThreshold ? 0x2B2B : ( color.a > alphaThreshold ? 0x7171 : 0x4D4D ) );
		if ( ( m & ( 1 << alphaFlags ) ) == 0 )
			discard;
	}
	if ( alphaFlags < 8 )
		color.a = 1.0;

	fragColor = vec4( color.rgb * ( glowMult * sqrt(brightnessScale) ), color.a );
}
