using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>메뉴 열기 1회만 배경을 캡처합니다. 메뉴 UI/글자는 캡처 뒤 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class FieldMenuBackdrop : MonoBehaviour
{
    private RawImage _picture;
    private Material _blur;
    private RenderTexture _snapshot;
    private Coroutine _capture;
    private int _width, _height;
    private Canvas _canvas;
    private static readonly int Direction = Shader.PropertyToID("_Direction");

    public void Initialize(RectTransform parent, Canvas canvas)
    {
        _canvas = canvas;
        var go = new GameObject("MenuBackdrop", typeof(RectTransform), typeof(RawImage));
        RectTransform rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
        _picture = go.GetComponent<RawImage>(); _picture.raycastTarget = false; _picture.color = Color.clear;
        var cover = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        RectTransform coverRect = cover.GetComponent<RectTransform>(); coverRect.SetParent(rect, false);
        coverRect.anchorMin = Vector2.zero; coverRect.anchorMax = Vector2.one; coverRect.offsetMin = coverRect.offsetMax = Vector2.zero;
        Image image = cover.GetComponent<Image>(); image.color = new Color(.015f, .02f, .035f, .62f); image.raycastTarget = true;
    }

    public void Prepare(Action ready)
    {
        Release();
        if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        { ready?.Invoke(); return; }
        _capture = StartCoroutine(Capture(ready));
    }

    private IEnumerator Capture(Action ready)
    {
        // The owning CanvasGroup is still transparent. Avoid capturing the inventory itself.
        yield return new WaitForEndOfFrame();
        _capture = null;
        TryCapture();
        ready?.Invoke();
    }

    private void TryCapture()
    {
        // One full-size transient copy is required by ScreenCapture; keep it bounded.
        if (Screen.width <= 0 || Screen.height <= 0 || (long)Screen.width * Screen.height > 8294400L) return;
        Material template = Resources.Load<Material>("HubToHome/FieldMenuBackdropBlur");
        if (template == null || template.shader == null || !template.shader.isSupported) return;
        RenderTexture full = null, low = null, scratch = null;
        try
        {
            _blur = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
            _width = Screen.width; _height = Screen.height;
            full = RenderTexture.GetTemporary(_width, _height, 0, RenderTextureFormat.ARGB32);
            int width = Mathf.Min(320, _width), height = Mathf.Max(1, Mathf.RoundToInt((float)_height * width / _width));
            low = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            scratch = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            full.filterMode = low.filterMode = scratch.filterMode = FilterMode.Bilinear;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
            Graphics.Blit(full, low);
            _blur.SetVector(Direction, new Vector4(1.5f / width, 0, 0, 0));
            Graphics.Blit(low, scratch, _blur);
            _snapshot = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            { name = "FieldMenuBackground", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            if (!_snapshot.Create()) throw new InvalidOperationException("background_texture_create_failed");
            _blur.SetVector(Direction, new Vector4(0, 1.5f / height, 0, 0));
            Graphics.Blit(scratch, _snapshot, _blur);
            Rect output = UIViewportService.CalculateContentScreenRect(UIViewportService.GetOutputRect(UIViewportService.GetOutputCamera(_canvas.rootCanvas)));
            _picture.uvRect = new Rect(output.x / _width, output.y / _height, output.width / _width, output.height / _height);
            _picture.texture = _snapshot; _picture.color = Color.white;
        }
        catch (Exception exception)
        {
            ReleaseResources();
            Debug.LogWarning("[FieldMenuBackdrop] 배경 흐림을 생략합니다: " + exception.GetType().Name, this);
        }
        finally
        {
            if (full != null) RenderTexture.ReleaseTemporary(full);
            if (low != null) RenderTexture.ReleaseTemporary(low);
            if (scratch != null) RenderTexture.ReleaseTemporary(scratch);
            if (_blur != null) { Destroy(_blur); _blur = null; }
        }
    }

    private void Update()
    {
        // A resize invalidates the captured crop. Do not recapture a visible menu.
        if (_snapshot != null && (_width != Screen.width || _height != Screen.height)) ReleaseResources();
    }

    public void Release()
    {
        CancelPendingCapture();
        ReleaseResources();
    }

    public void CancelPendingCapture()
    {
        if (_capture != null) { StopCoroutine(_capture); _capture = null; }
    }

    private void ReleaseResources()
    {
        if (_picture != null) { _picture.texture = null; _picture.color = Color.clear; }
        if (_snapshot != null) { _snapshot.Release(); Destroy(_snapshot); _snapshot = null; }
        if (_blur != null) { Destroy(_blur); _blur = null; }
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
