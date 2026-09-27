#ifndef HUB_TO_HOME_PIXEL_FOLIAGE_UV_INCLUDED
#define HUB_TO_HOME_PIXEL_FOLIAGE_UV_INCLUDED

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float _SwayPixels;
    float _VerticalPixels;
    float _SwaySpeed;
    float _WindScale;
    float _RootPin;
    float _PinFromTop;
    float _EdgeLockPixels;
CBUFFER_END

float4 _MainTex_TexelSize;
// Pixel coordinates of one sprite, or (0, 0, cell width, cell height) for tile sheets.
float4 _FoliageRegion;
// Pixels per unit, tiled sheet flag, enabled, phase.
float4 _FoliageLayout;

float2 PixelFoliageUV(float2 uv, float2 positionWS)
{
    if (_FoliageLayout.z < 0.5 || max(_SwayPixels, _VerticalPixels) <= 0.0)
        return uv;

    float2 textureSize = max(_MainTex_TexelSize.zw, float2(1.0, 1.0));
    float2 pixel = clamp(floor(uv * textureSize), 0.0, textureSize - 1.0);
    float2 size = max(_FoliageRegion.zw, float2(1.0, 1.0));
    float2 origin = _FoliageRegion.xy;
    if (_FoliageLayout.y > 0.5)
        origin = floor(pixel / size) * size;

    float2 localPixel = pixel - origin;
    float2 edgeDistance = min(localPixel, size - 1.0 - localPixel);
    float edgeWeight = 1.0;
    if (_EdgeLockPixels > 0.0)
        edgeWeight = saturate(min(edgeDistance.x, edgeDistance.y) / _EdgeLockPixels);
    float height = saturate(localPixel.y / max(1.0, size.y - 1.0));
    height = lerp(height, 1.0 - height, saturate(_PinFromTop));
    float rootWeight = lerp(1.0, height * height, saturate(_RootPin));

    float ppu = max(1.0, _FoliageLayout.x);
    float2 worldPixel = floor(positionWS * ppu) / ppu;
    float time = _Time.y * max(0.0, _SwaySpeed) * 6.28318530718;
    float phase = dot(worldPixel, float2(1.0, 0.61)) * max(0.01, _WindScale)
        + time + _FoliageLayout.w;
    float wave = sin(phase) * 0.75 + sin(phase * 1.37 + localPixel.y * 0.18) * 0.25;
    float2 offset = float2(wave * clamp(_SwayPixels, 0.0, 8.0),
        sin(phase * 0.73 + 1.1) * clamp(_VerticalPixels, 0.0, 8.0));
    offset *= edgeWeight * rootWeight;
    // Move the sampled image by WHOLE source pixels; never animate brightness or geometry.
    offset = sign(offset) * floor(abs(offset) + 0.5);
    float2 sourcePixel = clamp(pixel - offset, origin, origin + size - 1.0);
    return (sourcePixel + 0.5) / textureSize;
}

#endif
