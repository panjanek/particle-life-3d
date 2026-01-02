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

layout(location=0) out vec3 vColor;

void main()
{
    uint id = gl_VertexID;

    vec4 pos = points[id].position;
    pos.w = 1.0;
    vec4 clip = projection * pos;
    gl_Position = clip;

    float baseSize = paricleSize;
    if (baseSize == 0)
        baseSize = 2.0;

    gl_PointSize = baseSize/ clip.w;

    if (points[id].flags == 1)
        gl_PointSize = baseSize*1.5;

    

    //gl_Position = projection * vec4(0, 0, 0, 1);
    gl_PointSize = 1000;

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
    //vColor = vec3(1.0, 1.0, 1.0);



    mat4 projection2 = mat4(
    1.7320508, 0.0,        0.0,             0.0,
    0.0,       1.7320508,  0.0,             0.0,
    0.0,       0.0,       -1.0000400,      -1.0,
    0.0,       0.0,       1999.8800,        2000.0
    );

    pos = points[id].position;
    pos.w = 1.0;
    clip = projection * pos;
    gl_Position = clip;
    gl_PointSize = 10000 * baseSize/ clip.w;

    
    //------------------------------------------------------
    /*
    vec3 p = points[id].position.xyz;

    // --- camera parameters (must match your test) ---
    float camZ = 2000.0;
    float fov  = radians(60.0);
    float scale = 1.0 / tan(fov * 0.5);

    // distance from camera
    float z = camZ - p.z;

    // clip particles behind camera
    if (z <= 0.0)
    {
        gl_Position = vec4(0, 0, 2, 1);
        return;
    }

    // manual perspective projection
    float x = p.x * scale / z;
    float y = p.y * scale / z;

    gl_Position = vec4(x, y, 0.0, 1.0);

    // --- distance-dependent size ---
    baseSize = 10000.0;
    gl_PointSize = baseSize * (scale / z);

    // clamp for safety
    gl_PointSize = clamp(gl_PointSize, 1.0, 64.0);


    gl_Position = vec4(x, y, 0.0, 1.0);*/

}