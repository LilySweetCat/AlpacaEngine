#version 330

in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;
uniform vec3 normalRow0;
uniform vec3 normalRow1;
uniform vec3 normalRow2;

out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 worldNormal;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    worldNormal = vec3(dot(normalRow0, vertexNormal),
                       dot(normalRow1, vertexNormal),
                       dot(normalRow2, vertexNormal));
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
