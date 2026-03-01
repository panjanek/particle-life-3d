#version 430

layout(location = 0) in vec3 vColor;
layout(location = 1) in float vFadingAlpha;

out vec4 outputColor;

void main()
{
    vec2 p = gl_PointCoord * 2.0 - 1.0;

    float r = length(p);

    if (r > 1.0)
        discard;

    float softness = 3.0;
    float alpha = exp(-softness * r * r);

    alpha *= vFadingAlpha;

    outputColor = vec4(vColor * alpha, alpha);
    //outputColor = vec4(1,1,1,1);
}