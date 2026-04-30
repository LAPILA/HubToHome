using UnityEngine;

public class BackgroundManager : MonoBehaviour
{
    [SerializeField] private Camera _mainCamera;
    
    private Vector3 _startCameraPos;
    private ParallaxLayer[] _layers;

    private void Start()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        _startCameraPos = _mainCamera.transform.position;

        // 하위에 있는 모든 ParallaxLayer를 찾아 캐싱 (Update 연산 최소화)
        _layers = GetComponentsInChildren<ParallaxLayer>();
    }

    private void LateUpdate()
    {
        if (_mainCamera == null) return;

        // 카메라가 최초 위치에서 얼마나 벗어났는지 계산
        Vector3 cameraDelta = _mainCamera.transform.position - _startCameraPos;

        // 캐싱된 레이어들에게 변화량 전달
        foreach (var layer in _layers)
        {
            layer.ApplyEffect(cameraDelta);
        }
    }
}