using UnityEngine;

public class DistrictInteractable : MonoBehaviour
{
    [HideInInspector]
    public string districtId;

    public static Material DefaultMaterial;
    public static Material HoverMaterial;
    public static Material SelectedMaterial;

    private const float LiftHeight = 5f;
    private const float LiftSpeed = 8f;

    private Vector3 _baseLocalPos;
    private Vector3 _targetLocalPos;
    private bool _isHovered;
    private bool _isSelected;
    private Renderer _renderer;

    private void Start()
    {
        _baseLocalPos = transform.localPosition;
        _targetLocalPos = _baseLocalPos;
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && DefaultMaterial != null)
            _renderer.sharedMaterial = DefaultMaterial;
    }

    private void Update()
    {
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            _targetLocalPos,
            Time.deltaTime * LiftSpeed
        );
    }

    public void HoverEnter()
    {
        _isHovered = true;
        _targetLocalPos = _baseLocalPos + Vector3.up * LiftHeight;
        if (_renderer != null && !_isSelected && HoverMaterial != null)
            _renderer.sharedMaterial = HoverMaterial;
    }

    public void HoverExit()
    {
        _isHovered = false;
        if (!_isSelected)
        {
            _targetLocalPos = _baseLocalPos;
            if (_renderer != null && DefaultMaterial != null)
                _renderer.sharedMaterial = DefaultMaterial;
        }
    }

    public void Select()
    {
        _isSelected = true;
        _targetLocalPos = _baseLocalPos + Vector3.up * LiftHeight;
        if (_renderer != null && SelectedMaterial != null)
            _renderer.sharedMaterial = SelectedMaterial;
    }

    public void Deselect()
    {
        _isSelected = false;
        if (!_isHovered)
        {
            _targetLocalPos = _baseLocalPos;
            if (_renderer != null && DefaultMaterial != null)
                _renderer.sharedMaterial = DefaultMaterial;
        }
        else
        {
            if (_renderer != null && HoverMaterial != null)
                _renderer.sharedMaterial = HoverMaterial;
        }
    }
}
