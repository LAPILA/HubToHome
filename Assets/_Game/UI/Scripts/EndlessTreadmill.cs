using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 물리적 러닝머신 무한 스크롤 시스템.
/// 게임 시작 시 2개의 복제본을 추가 생성하여 총 3개가 맞물려 돌아갑니다.
/// Instantiate 연산이 없으므로 모바일에서도 완벽한 최적화를 보장합니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EndlessTreadmill : MonoBehaviour
{
    [Title("Treadmill Settings")]
    [Tooltip("초당 이동 속도 (마이너스면 왼쪽, 플러스면 오른쪽으로 이동)")]
    public float ScrollSpeedX = -5f;

    private Transform[] _tiles;
    private float _spriteWidth;

    private void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null) return;

        // 스프라이트의 실제 가로 길이를 구합니다. (스케일 반영)
        _spriteWidth = sr.sprite.bounds.size.x * transform.localScale.x;

        // 본체를 포함해 총 3개의 타일 배열 생성
        _tiles = new Transform[3];
        _tiles[0] = transform;

        // 꼬리에 붙을 2개의 복제본을 '미리' 생성합니다.
        for (int i = 1; i < 3; i++)
        {
            GameObject clone = Instantiate(gameObject, transform.parent);
            // 복제본에는 이 스크립트가 작동하지 않도록 파괴
            Destroy(clone.GetComponent<EndlessTreadmill>()); 
            clone.name = $"{gameObject.name}_Clone_{i}";
            
            _tiles[i] = clone.transform;
            _tiles[i].position = _tiles[0].position + new Vector3(_spriteWidth * i, 0, 0);
        }
    }

    private void Update()
    {
        if (_tiles == null) return;

        // 1. 모든 타일 이동
        float moveStep = ScrollSpeedX * Time.deltaTime;
        for (int i = 0; i < 3; i++)
        {
            _tiles[i].Translate(Vector3.right * moveStep, Space.World);
        }

        // 2. 꼬리잡기 로직 (카메라 밖으로 나간 타일을 반대쪽 끝으로 텔레포트)
        if (ScrollSpeedX < 0) // 왼쪽으로 이동 중
        {
            // 첫 번째 타일이 완전히 왼쪽으로 넘어갔다면
            if (_tiles[0].position.x < transform.parent.position.x - _spriteWidth)
            {
                ShiftTiles(true);
            }
        }
        else // 오른쪽으로 이동 중
        {
            // 세 번째 타일이 완전히 오른쪽으로 넘어갔다면
            if (_tiles[2].position.x > transform.parent.position.x + _spriteWidth)
            {
                ShiftTiles(false);
            }
        }
    }

    /// <summary>배열 순서를 바꾸고 위치를 재배치합니다.</summary>
    private void ShiftTiles(bool movingLeft)
    {
        if (movingLeft)
        {
            // 맨 앞(0번)을 맨 뒤(2번)의 오른쪽으로 보냄
            Transform first = _tiles[0];
            first.position = _tiles[2].position + new Vector3(_spriteWidth, 0, 0);

            // 배열 밀어내기: 1->0, 2->1, 맨앞->2
            _tiles[0] = _tiles[1];
            _tiles[1] = _tiles[2];
            _tiles[2] = first;
        }
        else
        {
            // 맨 뒤(2번)를 맨 앞(0번)의 왼쪽으로 보냄
            Transform last = _tiles[2];
            last.position = _tiles[0].position - new Vector3(_spriteWidth, 0, 0);

            // 배열 당기기: 1->2, 0->1, 맨뒤->0
            _tiles[2] = _tiles[1];
            _tiles[1] = _tiles[0];
            _tiles[0] = last;
        }
    }
}