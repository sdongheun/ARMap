using System;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ARDetailPanelDocumentController : MonoBehaviour
{
    public event Action OnClosed;
    public event Action OnPhoneRequested;
    public event Action OnCopyRequested;
    public event Action OnShareRequested;
    public event Action OnMapRequested;

    public bool IsVisible => _isVisible;

    private UIDocument _uiDocument;
    private VisualElement _root;
    private VisualElement _overlay;
    private VisualElement _sheet;
    private VisualElement _hoursRow;
    private VisualElement _addressRow;
    private VisualElement _phoneRow;
    private Label _titleLabel;
    private Label _subtitleLabel;
    private Label _addressLabel;
    private Label _hoursLabel;
    private Label _phoneLabel;
    private Button _closeButton;
    private Button _addressCopyButton;
    private bool _isVisible;
    private bool _isInitialized;

    void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        Initialize();
        HideImmediate();
    }

    void Initialize()
    {
        if (_isInitialized || _uiDocument == null)
        {
            return;
        }

        _root = _uiDocument.rootVisualElement;
        if (_root == null)
        {
            return;
        }

        _overlay = _root.Q<VisualElement>("detail-overlay");
        _sheet = _root.Q<VisualElement>("detail-sheet");
        _hoursRow = _root.Q<VisualElement>("hours-row");
        _addressRow = _root.Q<VisualElement>("address-row");
        _phoneRow = _root.Q<VisualElement>("phone-row");
        _titleLabel = _root.Q<Label>("detail-title");
        _subtitleLabel = _root.Q<Label>("detail-subtitle");
        _addressLabel = _root.Q<Label>("detail-address-value");
        _hoursLabel = _root.Q<Label>("detail-hours-value");
        _phoneLabel = _root.Q<Label>("detail-phone-value");
        _closeButton = _root.Q<Button>("close-button");
        _addressCopyButton = _root.Q<Button>("address-copy-button");

        if (_closeButton != null) _closeButton.clicked += Hide;
        if (_addressCopyButton != null) _addressCopyButton.clicked += () => OnCopyRequested?.Invoke();
        if (_overlay != null) _overlay.RegisterCallback<ClickEvent>(OnOverlayClicked);

        VisualElement sheet = _root.Q<VisualElement>("detail-sheet");
        if (sheet != null)
        {
            sheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
        }

        _isInitialized = true;
    }

    public void Show(BuildingData data)
    {
        Initialize();
        if (!_isInitialized) return;

        Bind(data);
        _isVisible = true;
        _root.style.display = DisplayStyle.Flex;
        _overlay?.RemoveFromClassList("open");
        _sheet?.RemoveFromClassList("open");
        _root.schedule.Execute(() =>
        {
            _overlay?.AddToClassList("open");
            _sheet?.AddToClassList("open");
        }).ExecuteLater(10);
    }

    public void ShowPreview(BuildingData data)
    {
        Initialize();
        if (!_isInitialized) return;

        Bind(data);
        _isVisible = true;
        _root.style.display = DisplayStyle.Flex;
        _overlay?.AddToClassList("open");
        _sheet?.AddToClassList("open");
    }

    public void Hide()
    {
        if (!_isInitialized) return;
        _isVisible = false;
        _overlay?.RemoveFromClassList("open");
        _sheet?.RemoveFromClassList("open");
        _root.schedule.Execute(() =>
        {
            if (!_isVisible)
            {
                HideImmediate();
                OnClosed?.Invoke();
            }
        }).ExecuteLater(380);
    }

    private void HideImmediate()
    {
        if (_root == null) return;
        _isVisible = false;
        _overlay?.RemoveFromClassList("open");
        _sheet?.RemoveFromClassList("open");
        _root.style.display = DisplayStyle.None;
    }

    private void Bind(BuildingData data)
    {
        if (data == null) return;

        if (_titleLabel != null)
        {
            _titleLabel.text = string.IsNullOrWhiteSpace(data.buildingName) ? "상세 정보" : data.buildingName;
        }

        if (_subtitleLabel != null)
        {
            bool hasCategory = !string.IsNullOrWhiteSpace(data.description);
            _subtitleLabel.style.display = hasCategory ? DisplayStyle.Flex : DisplayStyle.None;
            _subtitleLabel.text = hasCategory ? data.description : string.Empty;
        }

        if (_addressLabel != null)
        {
            _addressLabel.text = string.IsNullOrWhiteSpace(data.fetchedAddress) ? "주소 정보가 없습니다." : data.fetchedAddress;
        }

        if (_hoursLabel != null)
        {
            _hoursLabel.text = string.IsNullOrWhiteSpace(data.openingHours) ? "영업시간 정보가 없습니다." : data.openingHours;
        }

        if (_phoneLabel != null)
        {
            _phoneLabel.text = string.IsNullOrWhiteSpace(data.phoneNumber) ? "전화번호 정보가 없습니다." : data.phoneNumber;
        }

        if (_addressCopyButton != null)
        {
            _addressCopyButton.SetEnabled(!string.IsNullOrWhiteSpace(data.fetchedAddress));
        }

        bool hasAddress = !string.IsNullOrWhiteSpace(data.fetchedAddress);

        if (_addressRow != null)
        {
            _addressRow.style.display = hasAddress ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_phoneRow != null)
        {
            _phoneRow.style.display = DisplayStyle.Flex;
        }
        if (_hoursRow != null)
        {
            _hoursRow.style.display = DisplayStyle.Flex;
        }
    }

    private void OnOverlayClicked(ClickEvent evt)
    {
        if (evt.target == _overlay)
        {
            Hide();
        }
    }
}
