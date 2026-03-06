#version 430

struct Particle
{
   vec4 position;
   vec4 velocity;
   int species;
   int flags;
   int  cellIndex;
   float xzAngle;
   float yAngle;
   int _pad0;
   int _pad1;
   int _pad2;
   vec4 direction;
};

layout(std430, binding = 2) buffer OutputBuffer {
    Particle points[];
};

uniform mat4 view;
uniform mat4 projection;
uniform float paricleSize;
uniform float fogDensity;
uniform vec4 torusOffset;

layout(location = 0) out vec3 vColor;
layout(location = 1) out float vFadingAlpha;

void main()
{
    uint id = gl_VertexID;
    Particle p = points[id];
    p.position += torusOffset;

    vec4 viewPos = view * vec4(p.position.xyz, 1.0);
    gl_Position = projection * viewPos;

    float distance = max(-viewPos.z, 0.001);

    float scale = projection[1][1];
    gl_PointSize = paricleSize * scale / distance;

    const vec3 colors[] = vec3[](
        vec3(1.0, 1.0, 0.2), // yellow   
        vec3(0.2, 1.0, 1.0), // cyan

        vec3(0.2, 1.0, 0.2), // green
        vec3(0.2, 0.2, 1.0), // blue
        vec3(1.0, 0.2, 1.0), // magenta
        vec3(1.0, 0.2, 0.2), // red
        vec3(1.0, 1.0, 0.2), // yellow        
        vec3(0.2, 1.0, 1.0), // cyan
        vec3(1.0, 1.0, 1.0), // white
        vec3(0.5, 0.5, 0.5)  // gray
    );

    vColor = colors[p.species % 8];
   
    vFadingAlpha = exp(-fogDensity * distance);
}