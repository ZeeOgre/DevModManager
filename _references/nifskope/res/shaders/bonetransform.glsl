layout ( std140 ) uniform skinningUniforms
{
	mat3x4	boneTransforms[256];		// bone transforms in row-major order
};

layout ( location = 5 ) in vec4	boneWeights[2];

#ifdef BT_POSITION_ONLY
void boneTransform( inout vec4 v )
#elif defined( BT_NO_TANGENTS )
void boneTransform( inout vec4 v, inout vec3 n )
#else
void boneTransform( inout vec4 v, inout vec3 n, inout vec3 t, inout vec3 b )
#endif
{
#if defined( BT_POSITION_ONLY ) || defined( BT_NO_TANGENTS )
	vec3	vTmp = vec3( 0.0 );
#ifndef BT_POSITION_ONLY
	vec3	nTmp = vec3( 0.0 );
#endif
#else
	mat3x4	mTmp = mat3x4( 0.0 );
#endif
	float	wSum = 0.0;
	for ( int i = 0; i < 8; i++ ) {
		float	bw = boneWeights[i >> 2][i & 3];
		if ( !( bw > 0.0 ) )
			break;
		int	bone = int( bw ) & 0xFF;
		float	w = fract( bw );
#if defined( BT_POSITION_ONLY ) || defined( BT_NO_TANGENTS )
		mat3x4	m = boneTransforms[bone];
		vTmp += v * m * w;
#ifndef BT_POSITION_ONLY
		nTmp += n * mat3( m ) * w;
#endif
#else
		mTmp += boneTransforms[bone] * w;
#endif
		wSum += w;
	}
	if ( wSum > 0.0 ) {
#if defined( BT_POSITION_ONLY ) || defined( BT_NO_TANGENTS )
		v = vec4( vTmp / wSum, 1.0 );
#ifndef BT_POSITION_ONLY
		n = nTmp;
#endif
#else
		v = vec4( v * mTmp / wSum, 1.0 );
		mat3	r = mat3( mTmp );
		n = n * r;
		t = t * r;
		b = b * r;
#endif
	}
}
