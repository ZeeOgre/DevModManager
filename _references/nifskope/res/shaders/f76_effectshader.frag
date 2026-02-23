#version 410 core

#include "uniforms.glsl"

uniform sampler2D BaseMap;
uniform sampler2D GreyscaleMap;
uniform samplerCube CubeMap;
uniform sampler2D NormalMap;
uniform sampler2D EnvironmentMap;
uniform sampler2D ReflMap;
uniform sampler2D LightingMap;

uniform bool hasSourceTexture;
uniform bool hasGreyscaleMap;
uniform bool hasCubeMap;
uniform bool hasNormalMap;
uniform bool hasEnvMask;
uniform bool hasSpecularMap;

uniform bool greyscaleAlpha;
uniform bool greyscaleColor;

uniform bool useFalloff;
uniform bool hasRGBFalloff;
uniform bool isGlass;

uniform bool hasWeaponBlood;

uniform vec4 glowColor;
uniform float glowMult;

uniform int alphaFlags;			// bits 0 to 2: alpha test mode, bit 3: alpha blending enabled
uniform float alphaThreshold;

uniform vec2 uvScale;
uniform vec2 uvOffset;

uniform vec4 falloffParams;
uniform float falloffDepth;

uniform float lightingInfluence;
uniform float envReflection;

uniform float fLumEmittance;

in vec3 LightDir;
in vec3 ViewDir;

in vec2 texCoord;

in vec4 C;

in mat3 btnMatrix;
flat in mat3 reflMatrix;

out vec4 fragColor;

vec3 ViewDir_norm = normalize( ViewDir );
mat3 btnMatrix_norm = mat3( normalize( btnMatrix[0] ), normalize( btnMatrix[1] ), normalize( btnMatrix[2] ) );

vec3 fresnelSchlickRoughness(float NdotV, vec3 F0, float roughness)
{
	return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(1.0 - NdotV, 5.0);
}


void main()
{
	vec2 offset = texCoord.st * uvScale + uvOffset;

	vec4 baseMap = texture( BaseMap, offset );
	vec4 normalMap = texture( NormalMap, offset );
	vec4 reflMap = texture(ReflMap, offset);

	vec3 normal = normalMap.rgb;
	// Calculate missing blue channel
	normal.b = sqrt(max(1.0 - dot(normal.rg, normal.rg), 0.0));
	normal = normalize( btnMatrix_norm * normal );
	if ( !gl_FrontFacing )
		normal *= -1.0;

	vec3 f0 = reflMap.rgb;
	vec3 L = normalize(LightDir);
	vec3 V = ViewDir_norm;
	vec3 R = reflect(-V, normal);
	vec3 H = normalize( L + V );

	float NdotL = max( dot(normal, L), 0.000001 );
	float NdotH = max( dot(normal, H), 0.000001 );
	float NdotV = max( abs(dot(normal, V)), 0.000001 );
	float LdotH = max( dot(L, H), 0.000001 );
	float NdotNegL = max( dot(normal, -L), 0.000001 );

	vec3 reflectedWS = reflMatrix * R;

	if ( greyscaleAlpha )
		baseMap.a = 1.0;

	vec4 baseColor = glowColor;
	if ( !greyscaleColor )
		baseColor.rgb *= glowMult;

	// Falloff
	float falloff = 1.0;
	if ( useFalloff || hasRGBFalloff ) {
		if ( falloffParams.y > (falloffParams.x + 0.000001) )
			falloff = smoothstep(falloffParams.x, falloffParams.y, NdotV);
		else if ( falloffParams.x > (falloffParams.y + 0.000001) )
			falloff = 1.0 - smoothstep(falloffParams.y, falloffParams.x, NdotV);
		else
			falloff = 0.5;
		falloff = clamp(mix(falloffParams.z, falloffParams.w, falloff), 0.0, 1.0);

		if ( useFalloff )
			baseMap.a *= falloff;

		if ( hasRGBFalloff )
			baseMap.rgb *= falloff;
	}

	float alphaMult = baseColor.a * baseColor.a;

	vec4 color = baseMap * C;
	color.rgb *= baseColor.rgb;
	color.a *= alphaMult;

	if ( greyscaleColor )
		color.rgb = textureLod( GreyscaleMap, vec2( texture( BaseMap, offset ).g, baseColor.r * C.r * falloff ), 0.0 ).rgb;

	if ( greyscaleAlpha )
		color.a = textureLod( GreyscaleMap, vec2( texture( BaseMap, offset ).a, color.a ), 0.0 ).a;

	if ( alphaFlags > 0 ) {
		// 0: always, 1: <, 2: ==, 3: <=, 4: >, 5: !=, 6: >=, 7: never
		int	m = ( color.a < alphaThreshold ? 0x2B2B : ( color.a > alphaThreshold ? 0x7171 : 0x4D4D ) );
		if ( ( m & ( 1 << alphaFlags ) ) == 0 )
			discard;
	}
	if ( alphaFlags < 8 )
		color.a = 1.0;

	vec3 diffuse = lightSourceAmbient.rgb + (lightSourceDiffuse[0].rgb * NdotL);
	color.rgb = mix( color.rgb, color.rgb * lightSourceDiffuse[0].rgb, lightingInfluence );

	// Specular
	float g = 1.0;
	float s = 1.0;
	if ( hasSpecularMap && !isGlass ) {
		vec4 lightingMap = texture(LightingMap, offset);
		s = lightingMap.r;
		g = lightingMap.g;
	}
	float roughness = 1.0 - s;

	// Environment
	if ( hasCubeMap ) {
		float	m = roughness * (roughness * -4.0 + 10.0);
		vec3	cube = textureLod( CubeMap, reflectedWS, max(m, 0.0) ).rgb;
		cube *= envReflection * g;
		cube = mix( cube, cube * lightSourceDiffuse[0].rgb, lightingInfluence );
		if ( hasEnvMask )
			cube *= texture( EnvironmentMap, offset ).rgb;
		color.rgb += cube * falloff;
	}

	fragColor = vec4( color.rgb * brightnessScale, color.a );
}
