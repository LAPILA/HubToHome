using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Cinemachine 테스트용 타겟 이동 컨트롤러.
/// 빈 게임오브젝트에 붙인 뒤, Cinemachine Virtual Camera의 'Follow'에 할당하세요.
/// </summary>
public class CameraTestMovement : MonoBehaviour
{
    [Title("⚙️ 자동 이동 설정")]
    [ToggleLeft]
    public bool EnableAutoMove = false;

    [Title("📏 이동 제한 구역 (초기 위치 기준)")]
    [MinMaxSlider(-20f, 20f, true)] 
    public Vector2 BoundX = new Vector2(-5f, 5f);
    
    [MinMaxSlider(-10f, 10f, true)] 
    public Vector2 BoundY = new Vector2(-3f, 3f);

    [Title("🏃 이동 속도")]
    public Vector2 MoveSpeed = new Vector2(4f, 2f);
    
    private Vector2 _currentVelocity;
    private Vector3 _startPos;

    private void Start()
    {
        _startPos = transform.position;
        SetRandomDirection();
    }

    private void Update()
    {
        if (!EnableAutoMove) return;

        Vector3 pos = transform.position;
        pos.x += _currentVelocity.x * Time.deltaTime;
        pos.y += _currentVelocity.y * Time.deltaTime;

        // X축 벽에 부딪히면 튕기기
        if (pos.x <= _startPos.x + BoundX.x)
        {
            pos.x = _startPos.x + BoundX.x;
            _currentVelocity.x = MoveSpeed.x;
        }
        else if (pos.x >= _startPos.x + BoundX.y)
        {
            pos.x = _startPos.x + BoundX.y;
            _currentVelocity.x = -MoveSpeed.x;
        }

        // Y축 벽에 부딪히면 튕기기
        if (pos.y <= _startPos.y + BoundY.x)
        {
            pos.y = _startPos.y + BoundY.x;
            _currentVelocity.y = MoveSpeed.y;
        }
        else if (pos.y >= _startPos.y + BoundY.y)
        {
            pos.y = _startPos.y + BoundY.y;
            _currentVelocity.y = -MoveSpeed.y;
        }

        transform.position = pos;
    }

    [ButtonGroup("Controls")]
    [Button("🔀 방향 랜덤 전환")]
    private void SetRandomDirection()
    {
        _currentVelocity = new Vector2(
            Random.value > 0.5f ? MoveSpeed.x : -MoveSpeed.x,
            Random.value > 0.5f ? MoveSpeed.y : -MoveSpeed.y
        );
    }

    [ButtonGroup("Controls")]
    [Button("🔄 원위치 복귀")]
    private void ResetPosition()
    {
        transform.position = _startPos;
    }
}