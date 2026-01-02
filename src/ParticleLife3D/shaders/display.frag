#version 430

layout(location=0) in vec3 vColor;
out vec4 outputColor;

void main()
{
    vec2 uv = gl_PointCoord * 2.0 - 1.0; 
    float r = length(uv); 
    if (r > 1)
        discard;

    float m = r > 0.8 ? 1 : 0.8;

    outputColor = vec4(vColor*m, 1.0);
}