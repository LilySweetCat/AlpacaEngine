#version 330

in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 worldNormal;

uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform int lightingEnabled;
uniform vec3 sunDirection;
uniform vec3 sunColor;
uniform float sunIntensity;
uniform float ambient;

out vec4 finalColor;

void main()
{
    vec4 surface = texture(texture0, fragTexCoord) * colDiffuse * fragColor;

    if (lightingEnabled == 0)
    {
        finalColor = surface;
        return;
    }

    // sunDirection follows the rays; the surface faces the opposite direction.
    float diffuse = 0.0;

    if (dot(worldNormal, worldNormal) > 0.000001)
        diffuse = max(dot(normalize(worldNormal), -sunDirection), 0.0);

    vec3 illumination = vec3(ambient) + sunColor * sunIntensity * diffuse;
    finalColor = vec4(surface.rgb * illumination, surface.a);
}
