#version 430

layout(location = 0) in vec3 vColor;
layout(location = 1) in float vDepth;
layout(location = 2) in float vFadingAlpha;
layout(location = 3) in vec3 vCenterView;
layout(location = 4) in vec2 vQuad;

out vec4 outputColor;

uniform vec3 lightDir = normalize(vec3(0.3, 0.5, 1.0));
uniform mat4 projection;
uniform float paricleSize;

void main()
{
    float sphereRadius = 2 * paricleSize;

    // Ray origin is camera in view space
    vec3 rayOrigin = vec3(0.0);

    // Ray direction goes from camera through the quad pixel
    vec3 rayDir = normalize(vCenterView + vec3(vQuad * sphereRadius, 0.0));

    // Sphere center in view space
    vec3 oc = rayOrigin - vCenterView;

    float b = dot(oc, rayDir);
    float c = dot(oc, oc) - sphereRadius * sphereRadius;
    float h = b*b - c;

    if (h < 0.0)
        discard;

    float t = -b - sqrt(h); // nearest intersection

    vec3 hit = rayOrigin + t * rayDir;
    vec3 normal = normalize(hit - vCenterView);

    // Lighting
    float diff = max(dot(normal, lightDir), 0.0);

    vec3 color = vColor * (0.3 + 0.7 * diff);

    // ---- CORRECT DEPTH ----
    vec4 clip = projection * vec4(hit, 1.0);
    float ndcDepth = clip.z / clip.w;
    gl_FragDepth = ndcDepth * 0.5 + 0.5;

    outputColor = vec4(color, 1.0);
}