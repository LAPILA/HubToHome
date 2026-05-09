using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    [Title("1. 시차 (Parallax) 설정")]
    [ToggleLeft] public bool UseParallax = true;
    [ShowIf("UseParallax")]
    [Tooltip("1.0: 제자리 고정, 0.5: 절반 속도(원경), 1.2: 근경")]
    public Vector2 ParallaxMultiplier = new Vector2(0.5f, 0.5f);

    [Title("2. 무한 스크롤 (Endless Treadmill)")]
    [ToggleLeft] public bool UseEndlessScroll = false;
    [ShowIf("UseEndlessScroll")]
    public float ScrollSpeedX = -5f;
    [ShowIf("UseEndlessScroll")]
    [Tooltip("틈새 방지를 위해 타일들을 겹치는 수치")]
    public float OverlapAmount = 0.05f;

    [Title("3. 바닥 왜곡 설정 (Ground 전용)")]
    [ToggleLeft] public bool UsePerspectiveStretch = false;
    [ShowIf("UsePerspectiveStretch")]
    [Tooltip("카메라 Y축 이동에 따른 바닥 스케일 변화량")]
    public float StretchSensitivity = 0.2f;

    [Title("4. 사물 바닥 앵커 설정 (오브젝트 전용)")]
    [ToggleLeft] public bool UseGroundAnchor = false;
    [ShowIf("UseGroundAnchor")]
    [Tooltip("바닥이 1만큼 줄어들 때 이 오브젝트가 아래로 내려갈 비율")]
    public float AnchorSensitivity = 0.2f;

    // ── 내부 상태 캐싱 ──
    private Vector3 _startPos;
    private Vector3 _startScale;
    private float _spriteWidth;
    private float _autoScrollX;
    
    // 🚨 탑 앵커(Top Anchor) 수학적 보정을 위한 캐시
    private float _localTopY; 

    private void Start()
    {
        _startPos = transform.position;
        _startScale = transform.localScale;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            // 스프라이트의 중심(Pivot) 기준, 맨 윗면(Top)까지의 로컬 거리를 계산하여 기억해둡니다.
            _localTopY = sr.sprite.bounds.max.y;
        }

        if (UseEndlessScroll) SetupEndlessTiles(sr);
    }

    private void SetupEndlessTiles(SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null) return;

        float baseWorldWidth = sr.sprite.bounds.size.x * _startScale.x;
        _spriteWidth = baseWorldWidth - OverlapAmount;

        transform.localScale = new Vector3(_startScale.x * 1.01f, _startScale.y, _startScale.z);

        for (int i = -1; i <= 1; i += 2) 
        {
            GameObject clone = new GameObject($"{gameObject.name}_Tile_{i}");
            clone.transform.SetParent(transform);
            
            float localOffsetX = (_spriteWidth / transform.localScale.x) * i;
            clone.transform.localPosition = new Vector3(localOffsetX, 0, 0);
            clone.transform.localScale = Vector3.one; 
            
            SpriteRenderer cloneSr = clone.AddComponent<SpriteRenderer>();
            cloneSr.sprite = sr.sprite;
            cloneSr.color = sr.color;
            cloneSr.sortingLayerID = sr.sortingLayerID;
            cloneSr.sortingOrder = sr.sortingOrder;
            cloneSr.material = sr.material;
        }
    }

    public void ApplyEffect(Vector3 cameraPos, Vector3 cameraDelta)
    {
        float targetX = _startPos.x;
        float targetY = _startPos.y;

        // ── 1. Y축 연산 (왜곡 및 앵커) ──
        if (UseGroundAnchor) targetY += (cameraDelta.y * AnchorSensitivity);
        
        if (UsePerspectiveStretch)
        {
            float stretch = cameraDelta.y * StretchSensitivity;
            float newScaleY = Mathf.Clamp(_startScale.y + stretch, 0.01f, _startScale.y);
            transform.localScale = new Vector3(transform.localScale.x, newScaleY, _startScale.z);

            // 🚨 [핵심 수학적 보정] 바닥 윗면(지평선) 고정 로직
            // 스케일이 줄어든 비율만큼 위치(Y)를 위로 밀어올려서, 스프라이트의 윗면이 허공에 뜨지 않고 제자리에 박혀있게 만듭니다.
            float scaleDiff = newScaleY - _startScale.y; 
            targetY -= scaleDiff * _localTopY; 
        }

        // ── 2. X축 연산 (시차 및 자동 이동) ──
        if (UseParallax)
        {
            targetX += cameraDelta.x * (1f - ParallaxMultiplier.x);
            targetY += cameraDelta.y * (1f - ParallaxMultiplier.y);
        }

        if (UseEndlessScroll)
        {
            _autoScrollX += ScrollSpeedX * Time.deltaTime;
            targetX += _autoScrollX;
        }

        transform.position = new Vector3(targetX, targetY, _startPos.z);

        // ── 3. 무한 스크롤 텔레포트 ──
        if (UseEndlessScroll && _spriteWidth > 0)
        {
            float distFromCam = cameraPos.x - transform.position.x;

            while (distFromCam > _spriteWidth)
            {
                _startPos.x += _spriteWidth;
                distFromCam -= _spriteWidth;
            }
            while (distFromCam < -_spriteWidth)
            {
                _startPos.x -= _spriteWidth;
                distFromCam += _spriteWidth;
            }
        }
    }
}