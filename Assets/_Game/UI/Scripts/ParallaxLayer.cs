using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    [Title("1. 시차 (Parallax) 설정")]
    [ToggleLeft] public bool UseParallax = true;
    
    [ShowIf("UseParallax")]
    [Tooltip("1.0: 제자리 고정, 0.5: 원경, 1.2: 근경")]
    public Vector2 ParallaxMultiplier = new Vector2(0.5f, 0.5f);

    [Title("2. 바닥 왜곡 설정 (Ground 전용)")]
    [ToggleLeft] public bool UsePerspectiveStretch = false;
    
    [ShowIf("UsePerspectiveStretch")]
    [Tooltip("카메라 Y축 이동에 따른 바닥 스케일 변화량")]
    public float StretchSensitivity = 0.2f;

    [Title("3. 사물 바닥 앵커 설정 (Obj_Back, Obj_Front 전용)")]
    [ToggleLeft] public bool UseGroundAnchor = false;
    
    [ShowIf("UseGroundAnchor")]
    [Tooltip("바닥이 1만큼 줄어들 때 이 오브젝트가 아래로 내려갈 비율 (0.5~1.0 사이 추천)")]
    public float AnchorSensitivity = 0.2f;

    private Vector3 _startPos;
    private Vector3 _startScale;

    private void Start()
    {
        _startPos = transform.position;
        _startScale = transform.localScale;
    }

    /// <summary>BackgroundManager에서 카메라 이동량을 받아 매 프레임 호출</summary>
    public void ApplyEffect(Vector3 cameraDelta)
    {
        // 1. 패럴랙스 이동
        float targetX = _startPos.x;
        float targetY = _startPos.y;

        if (UseParallax)
        {
            targetX += cameraDelta.x * (1f - ParallaxMultiplier.x);
            targetY += cameraDelta.y * (1f - ParallaxMultiplier.y);
        }

        // 2. 바닥 위에 있는 사물 싱크 맞추기 (붕 뜨는 현상 방지)
        if (UseGroundAnchor)
        {
            targetY += (cameraDelta.y * AnchorSensitivity);
        }

        transform.position = new Vector3(targetX, targetY, _startPos.z);

        // 3. 림버스 스타일 바닥 원근 왜곡
        if (UsePerspectiveStretch)
        {
            float stretch = cameraDelta.y * StretchSensitivity;
            
            // 🚨 [수정된 부분] Mathf.Max 대신 Mathf.Clamp 사용
            // 최소 0.01배까지만 줄어들고, 최대치는 원래 스케일(_startScale.y)을 절대 넘지 못하게 가둡니다!
            float newScaleY = Mathf.Clamp(_startScale.y + stretch, 0.01f, _startScale.y); 
            
            transform.localScale = new Vector3(_startScale.x, newScaleY, _startScale.z);
        }
    }
}