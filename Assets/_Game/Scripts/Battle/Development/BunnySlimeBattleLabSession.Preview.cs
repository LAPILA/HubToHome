using UnityEngine;

public sealed partial class BunnySlimeBattleLabSession
{
    private GameObject _partyPreviewRoot;
    private SpriteRenderer[] _partyPreviewSprites;
    private CameraController _previewCamera;
    private CameraDefaultTargetSnapshot _previewCameraSnapshot;

    // 실제 주인공 옆에 표시만 합니다. 전투 참가자/입력/충돌/저장 데이터는 생성하지 않습니다.
    private void SetPartyPreviewVisible(bool visible)
    {
        if (!visible)
        {
            if (_partyPreviewRoot != null)
                _partyPreviewRoot.SetActive(false);
            RestorePreviewCamera();
            return;
        }
        if (!_ownsSession || _leaving || _player == null || _data == null
            || _data.GetPartyCount(false) < 3)
            return;

        SpriteRenderer source = _player.GetComponent<SpriteRenderer>();
        if (source == null)
            return;

        if (_partyPreviewRoot == null)
        {
            _partyPreviewRoot = new GameObject("Lab Party Preview - Visual Only");
            _partyPreviewRoot.SetActive(false);
            _partyPreviewRoot.transform.SetParent(transform, false);
            _partyPreviewSprites = new SpriteRenderer[2];
            for (int i = 0; i < _partyPreviewSprites.Length; i++)
            {
                var companion = new GameObject("Party Preview " + (i + 1), typeof(SpriteRenderer));
                companion.transform.localScale = source.transform.lossyScale;
                companion.transform.SetParent(_partyPreviewRoot.transform, true);
                _partyPreviewSprites[i] = companion.GetComponent<SpriteRenderer>();
            }
        }

        Vector3 origin = _player.transform.position;
        _partyPreviewRoot.transform.position = origin + new Vector3(2.8f, 0.15f, 0f);
        for (int i = 0; i < _partyPreviewSprites.Length; i++)
        {
            SpriteRenderer preview = _partyPreviewSprites[i];
            preview.gameObject.layer = source.gameObject.layer;
            preview.sprite = source.sprite;
            preview.sharedMaterial = source.sharedMaterial;
            preview.sortingLayerID = source.sortingLayerID;
            preview.sortingOrder = source.sortingOrder;
            preview.flipX = source.flipX;
            preview.flipY = source.flipY;
            preview.color = _data.Party[i + 1].BattleSpriteTint;
            preview.transform.position = origin + new Vector3(2.8f * (i + 1), i == 0 ? -0.15f : 0f, 0f);
        }
        _partyPreviewRoot.SetActive(true);

        CameraController camera = CameraController.Instance;
        if (camera != null && _previewCamera == null)
        {
            _previewCamera = camera;
            _previewCameraSnapshot = camera.CaptureDefaultTarget();
            camera.SetDefaultTarget(_partyPreviewRoot.transform, true);
            camera.ResetCamera(0f);
        }
    }

    private void RestorePreviewCamera()
    {
        // 전투/다른 연출이 이미 소유권을 가져갔다면 그 타깃을 덮어쓰지 않습니다.
        if (_previewCamera != null && _partyPreviewRoot != null
            && _previewCamera.DefaultTarget == _partyPreviewRoot.transform)
            _previewCamera.RestoreDefaultTarget(_previewCameraSnapshot, 0f);
        _previewCamera = null;
        _previewCameraSnapshot = default;
    }
}
