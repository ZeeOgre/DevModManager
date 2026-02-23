layout ( std140 ) uniform globalUniforms
{
	mat3	viewMatrix;					// rotation part of view transform
	mat3	envMapRotation;				// view space to environment map
	mat4	projectionMatrix;
	vec4	lightSourcePosition[3];
	vec4	lightSourceDiffuse[3];
	vec4	lightSourceAmbient;
	float	toneMapScale;				// 1.0 = full tone mapping
	float	brightnessScale;
	float	glowScale;
	float	glowScaleSRGB;
	ivec4	viewportDimensions;			// X, Y, width, height
	bool	doSkinning;
	int	sceneOptions;
	int	cubeBgndMipLevel;
	int	sfParallaxMaxSteps;
	float	sfParallaxScale;
	float	sfParallaxOffset;
	float	unusedUniform1;
	float	unusedUniform2;
};
