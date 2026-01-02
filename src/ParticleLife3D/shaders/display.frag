#version 430

layout(location = 0) in vec3 vColor;
layout(location = 1) in float vDepth;
layout(location = 2) in float vFadingAlpha;

out vec4 outputColor;

uniform vec3 lightDir = normalize(vec3(0.3, 0.5, 1.0));

void main()
{

    vec2 p = gl_PointCoord * 2.0 - 1.0;
    float r2 = dot(p, p);
    if (r2 > 1.0)
        discard;
    float z = sqrt(1.0 - r2);
    vec3 normal = normalize(vec3(p, z));
    float diffuse = max(dot(normal, lightDir), 0.0);
    vec3 viewDir = vec3(0.0, 0.0, 1.0);
    vec3 halfDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(normal, halfDir), 0.0), 32.0);

    vec3 color =
        vColor * (0.3 + 0.7 * diffuse) +
        vec3(1.0) * spec * 0.25;

    float fogDensity = 0.0005;  
    float fog = exp(-fogDensity * vDepth);
    fog = clamp(fog, 0.0, 1.0);
    

    outputColor = vec4(color*fog*vFadingAlpha, fog*vFadingAlpha);

}