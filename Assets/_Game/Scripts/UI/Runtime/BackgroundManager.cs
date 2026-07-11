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

        _layers = GetComponentsInChildren<ParallaxLayer>();
    }

    private void LateUpdate()
    {
        if (_mainCamera == null) return;

        Vector3 cameraDelta = _mainCamera.transform.position - _startCameraPos;

        foreach (var layer in _layers)
        {
            layer.ApplyEffect(_mainCamera.transform.position, cameraDelta);
        }
    }
}