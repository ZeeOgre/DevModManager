#version 410 core

uniform sampler2D BaseMap;
uniform sampler2D GreyscaleMap;
uniform samplerCube CubeMap;
uniform sampler2D NormalMap;
uniform sampler2D SpecularMap;

uniform bool hasSourceTexture;
uniform bool hasGreyscaleMap;
uniform bool hasCubeMap;
uniform bool hasNormalMap;
uniform bool hasEnvMask;

uniform bool greyscaleAlpha;
uniform bool greyscaleColor;

uniform bool useFalloff;
uniform bool hasRGBFalloff;

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

in vec3 LightDir;
in vec3 ViewDir;

in vec2 texCoord;

flat in vec4 A;
in vec4 C;
flat in vec4 D;

in mat3 btnMatrix;
flat in mat3 reflMatrix;

out vec4 fragColor;

vec3 ViewDir_norm = normalize( ViewDir );
mat3 btnMatrix_norm = mat3( normalize( btnMatrix[0] ), normalize( btnMatrix[1] ), normalize( btnMatrix[2] ) );


void main()
{
	vec2 offset = texCoord.st * uvScale + uvOffset;

	vec4 baseMap = texture( BaseMap, offset );
	vec4 normalMap = texture( NormalMap, offset );
	vec4 specMap = texture( SpecularMap, offset );

	vec3 normal = normalize(normalMap.rgb * 2.0 - 1.0);
	// Calculate missing blue channel
	normal.b = sqrt(max(1.0 - dot(normal.rg, normal.rg), 0.0));
	normal = normalize( btnMatrix_norm * normal );
	if ( !gl_FrontFacing )
		normal *= -1.0;

	vec3 L = normalize(LightDir);
	vec3 V = ViewDir_norm;
	vec3 R = reflect(-V, normal);
	vec3 H = normalize( L + V );

	float NdotL = max( dot(normal, L), 0.000001 );
	float NdotH = max( dot(normal, H), 0.000001 );
	float NdotV = max( dot(normal, V), 0.000001 );
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
		falloff = smoothstep( falloffParams.x, falloffParams.y, abs(dot(normal, V)) );
		falloff = mix( max(falloffParams.z, 0.0), min(falloffParams.w, 1.0), falloff );

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

	vec3 diffuse = A.rgb + (D.rgb * NdotL);
	color.rgb = mix( color.rgb, color.rgb * D.rgb, lightingInfluence );

	// Specular
	float g = 1.0;
	float s = 1.0;
	if ( hasEnvMask ) {
		g = specMap.r;
		s = specMap.g;
	}

	// Environment
	vec4 cube = texture( CubeMap, reflectedWS );
	if ( hasCubeMap ) {
		cube.rgb *= envReflection * s;
		cube.rgb = mix( cube.rgb, cube.rgb * D.rgb, lightingInfluence );

		color.rgb += cube.rgb * falloff;
	}

	fragColor = vec4( color.rgb * sqrt(D.a), color.a );
}
