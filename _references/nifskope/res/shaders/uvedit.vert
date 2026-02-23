#version 410 core

out vec2 texCoord;

uniform vec4 viewScaleAndOffset;

layout ( location = 7 ) in vec2	multiTexCoord0;

void main()
{
	vec2	offs = multiTexCoord0;

	gl_Position = vec4( offs * vec2( 2.0, -2.0 ) + vec2( -1.0, 1.0 ), vec2( 1.0 ) );

	texCoord = offs * viewScaleAndOffset.xy + viewScaleAndOffset.zw;
}
