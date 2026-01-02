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

layout(std430, binding = 3) buffer OutputBuffer {
    Particle points[];
};

uniform mat4 projection;
uniform float paricleSize;
uniform vec2 viewportSize;

layout(location = 0) out vec3 vColor;
layout(location = 1) out float vDepth;

void main()
{
    uint id = gl_VertexID;

    vec4 pos = points[id].position;
    pos.w = 1.0;
    if (points[id].flags == 2)
        pos.w = 0;


    vec4 clip = projection * pos;
    gl_Position = clip;

    float baseSize = paricleSize;
    if (baseSize == 0)
        baseSize = 2.0;

    gl_PointSize = baseSize / clip.w;

    if (points[id].flags == 1)
        baseSize = baseSize*1.5;

    gl_PointSize = viewportSize.x * 5 * baseSize / clip.w;

    float depth = clip.w;
    vDepth = depth;

    uint spec = points[id].species;
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

    vColor = colors[spec%8];

    if (points[id].flags == 2)
        vColor = vec3(0,0,0);

    if (points[id].flags == 1)
        vColor = vColor*1.25;
}