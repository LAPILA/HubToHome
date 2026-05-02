using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(SpriteRenderer))]
public class EndlessTreadmill : MonoBehaviour
{
    [Title("Treadmill Settings")]
    public float ScrollSpeedX = -5f;
    
    // [중요] 타일들을 0.05 단위로 강제로 겹쳐버립니다.
    [Tooltip("틈새 방지를 위해 강제로 겹치는 수치 (틈이 보이면 0.05 정도로 높이세요)")]
    public float overlapAmount = 0.05f; 

    private Transform[] _tiles;
    private float _spriteWidth;

    private void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null) return;

        // 원본 스케일을 저장해둡니다.
        Vector3 originalScale = transform.localScale;

        // 🚨 틈새 방지 1: 스프라이트 크기를 계산할 때 overlapAmount만큼 미리 뺍니다.
        // 이렇게 하면 타일들이 overlapAmount만큼 안으로 파고들어 배치됩니다.
        _spriteWidth = (sr.sprite.bounds.size.x / originalScale.x) * originalScale.x - overlapAmount;

        _tiles = new Transform[3];
        _tiles[0] = transform;

        // 🚨 틈새 방지 2: 원본 타일의 X 스케일을 미세하게 늘려서 빈틈을 가립니다.
        // 예를 들어 스케일이 1이면 1.01로 만듭니다. (픽셀 아트가 살짝 늘어나도 티 안 남)
        transform.localScale = new Vector3(originalScale.x * 1.01f, originalScale.y, originalScale.z);

        for (int i = 1; i < 3; i++)
        {
            GameObject clone = Instantiate(gameObject, transform.parent);
            Destroy(clone.GetComponent<EndlessTreadmill>()); 
            clone.name = $"{gameObject.name}_Clone_{i}";
            
            _tiles[i] = clone.transform;
            // 복제본의 스케일도 미세하게 늘린 상태 적용
            _tiles[i].localScale = new Vector3(originalScale.x * 1.01f, originalScale.y, originalScale.z);
            
            // 위치 배치 (파고든 상태로 배치됨)
            _tiles[i].position = _tiles[0].position + new Vector3(_spriteWidth * i, 0, 0);
        }
    }

    private void LateUpdate()
    {
        if (_tiles == null) return;

        float moveStep = ScrollSpeedX * Time.deltaTime;
        
        for (int i = 0; i < 3; i++)
        {
            _tiles[i].Translate(Vector3.right * moveStep, Space.World);
        }

        float parentX = transform.parent.position.x;

        if (ScrollSpeedX < 0) 
        {
            if (_tiles[0].position.x < parentX - _spriteWidth)
            {
                ShiftTiles(true);
            }
        }
        else 
        {
            if (_tiles[2].position.x > parentX + _spriteWidth)
            {
                ShiftTiles(false);
            }
        }
    }

    private void ShiftTiles(bool movingLeft)
    {
        if (movingLeft)
        {
            Transform first = _tiles[0];
            // 다시 배치할 때도 파고든 거리(_spriteWidth)만큼만 더합니다.
            first.position = _tiles[2].position + new Vector3(_spriteWidth, 0, 0);

            _tiles[0] = _tiles[1];
            _tiles[1] = _tiles[2];
            _tiles[2] = first;
        }
        else
        {
            Transform last = _tiles[2];
            last.position = _tiles[0].position - new Vector3(_spriteWidth, 0, 0);

            _tiles[2] = _tiles[1];
            _tiles[1] = _tiles[0];
            _tiles[0] = last;
        }
    }
}