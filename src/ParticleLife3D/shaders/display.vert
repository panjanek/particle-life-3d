#version 430

struct Particle
{
   vec4 position;
   vec4 velocity;
   int species;
   int flags;
   int  _pad0;
   int  _pad1;
};

layout(std430, binding = 2) buffer OutputBuffer {
    Particle points[];
};

uniform mat4 view;
uniform mat4 projection;
uniform float paricleSize;
uniform vec2 viewportSize;
uniform vec4 torusOffset;

layout(location = 0) out vec3 vColor;
layout(location = 1) out float vDepth;
layout(location = 2) out float vFadingAlpha;
layout(location = 3) out vec3 vCenterView;
layout(location = 4) out vec2 vQuad;
layout(location = 5) in vec2 quadPos;
layout(location = 6) out vec3 vOffsetView;

float fading_alpha(float r2)
{
    float sigma2 = 500*500;
    float minAlpha = 0.5;

    float a = exp(-(r2) / sigma2);
    return max(a, minAlpha);
}

void main()
{
    float sphereRadius = 2 * paricleSize + (viewportSize.x/1920);

    uint id = gl_InstanceID;
    Particle p = points[id];
    p.position += torusOffset;

    //if tracking enabled - make everything around tracked particle fade away
    float fading = 1.0;
    if (p.position.w > 0)
        fading = fading_alpha(p.position.w);
    vFadingAlpha = fading;
    if (p.flags == 2)
        vFadingAlpha = 0;

    vec4 viewPos = view * vec4(p.position.xyz, 1.0);

    vCenterView = viewPos.xyz;
    vQuad = quadPos;

    // In VIEW SPACE the camera basis is fixed
    float inflate = 1.5;
    vec3 offset = vec3(quadPos * sphereRadius * inflate, 0.0);

    vOffsetView = offset;

    vec4 pos = viewPos + vec4(offset, 0.0);
    gl_Position = projection * pos;

    // species coloring as before
        const vec3 colors[] = vec3[](
        vec3(1.0, 1.0, 0.0), // yellow
        vec3(1.0, 0.0, 1.0), // magenta
        vec3(0.0, 1.0, 1.0), // cyan
        vec3(1.0, 0.0, 0.0), // red
        vec3(0.0, 1.0, 0.0), // green
        vec3(0.0, 0.0, 1.0), // blue
        vec3(1.0, 1.0, 1.0), // white
        vec3(0.5, 0.5, 0.5)  // gray
    );

    vColor = colors[p.species % 8];

    if (p.flags == 1)
        vColor = vColor*2;
}