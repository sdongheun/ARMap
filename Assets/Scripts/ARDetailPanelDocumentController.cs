using System;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ARDetailPanelDocumentController : MonoBehaviour
{
    private class DetailPlaceOption
    {
        public string name;
        public string category;
        public string phone;
        public string placeUrl;
    }

    public event Action OnClosed;
    public event Action OnPhoneRequested;
    public event Action OnCopyRequested;
    public event Action OnShareRequested;
    public event Action OnMapRequested;

    public bool IsVisible => _isVisible;
    public string CurrentDisplayedPlaceName
    {
        get
        {
            DetailPlaceOption option = GetCurrentPlaceOption();
            return option != null
                ? (option.name ?? string.Empty)
                : (_currentData?.buildingName ?? string.Empty);
        }
    }

    public string CurrentDisplayedCategory
    {
        get
        {
            DetailPlaceOption option = GetCurrentPlaceOption();
            return option != null
                ? (option.category ?? string.Empty)
                : (_currentData?.description ?? string.Empty);
        }
    }

    public string CurrentDisplayedPhoneNumber
    {
        get
        {
            DetailPlaceOption option = GetCurrentPlaceOption();
            return option != null
                ? (option.phone ?? string.Empty)
                : (_currentData?.phoneNumber ?? string.Empty);
        }
    }

    public string CurrentDisplayedPlaceUrl
    {
        get
        {
            DetailPlaceOption option = GetCurrentPlaceOption();
            return option != null
                ? (option.placeUrl ?? string.Empty)
                : (_currentData?.placeUrl ?? string.Empty);
        }
    }

    private UIDocument _uiDocument;
    private VisualElement _root;
    private VisualElement _overlay;
    private VisualElement _sheet;
    private VisualElement _facilitySelectorSection;
    private VisualElement _facilityToggleButton;
    private ScrollView _facilityList;
    private VisualElement _addressRow;
    private VisualElement _phoneRow;
    private VisualElement _mapActionRow;
    private Label _titleLabel;
    private Label _subtitleLabel;
    private Label _facilityToggleLabel;
    private Label _facilitySelectedNameLabel;
    private Label _addressLabel;
    private Label _phoneLabel;
    private Button _closeButton;
    private Button _addressCopyButton;
    private Button _phoneCallButton;
    private Button _mapOpenButton;
    private VisualElement _facilityToggleChevronIcon;
    private bool _isVisible;
    private bool _isInitialized;
    private BuildingData _currentData;
    private readonly System.Collections.Generic.List<DetailPlaceOption> _placeOptions = new System.Collections.Generic.List<DetailPlaceOption>();
    private int _selectedPlaceIndex;
    private bool _isFacilityListExpanded;

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
        _facilitySelectorSection = _root.Q<VisualElement>("facility-selector-section");
        _facilityToggleButton = _root.Q<VisualElement>("facility-toggle-button");
        _facilityList = _root.Q<ScrollView>("facility-list");
        _addressRow = _root.Q<VisualElement>("address-row");
        _phoneRow = _root.Q<VisualElement>("phone-row");
        _mapActionRow = _root.Q<VisualElement>("map-action-row");
        _titleLabel = _root.Q<Label>("detail-title");
        _subtitleLabel = _root.Q<Label>("detail-subtitle");
        _facilityToggleLabel = _root.Q<Label>("facility-toggle-label");
        _facilitySelectedNameLabel = _root.Q<Label>("facility-selected-name");
        _facilityToggleChevronIcon = _root.Q<VisualElement>("facility-toggle-chevron");
        _addressLabel = _root.Q<Label>("detail-address-value");
        _phoneLabel = _root.Q<Label>("detail-phone-value");
        _closeButton = _root.Q<Button>("close-button");
        _addressCopyButton = _root.Q<Button>("address-copy-button");
        _phoneCallButton = _root.Q<Button>("phone-call-button");
        _mapOpenButton = _root.Q<Button>("map-open-button");

        if (_closeButton != null) _closeButton.clicked += Hide;
        if (_addressCopyButton != null) _addressCopyButton.clicked += () => OnCopyRequested?.Invoke();
        if (_phoneCallButton != null) _phoneCallButton.clicked += () => OnPhoneRequested?.Invoke();
        if (_mapOpenButton != null) _mapOpenButton.clicked += () => OnMapRequested?.Invoke();
        if (_facilityToggleButton != null) _facilityToggleButton.RegisterCallback<ClickEvent>(OnFacilityToggleClicked);
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

        _currentData = data;
        BuildPlaceOptions(data);
        _selectedPlaceIndex = 0;
        SetFacilityListExpanded(false);

        RefreshFacilitySelector();
        BindCurrentPlace(data);
    }

    private void BindCurrentPlace(BuildingData data)
    {
        DetailPlaceOption currentPlace = GetCurrentPlaceOption();
        string currentCategory = currentPlace?.category ?? string.Empty;
        string currentPhone = currentPlace?.phone ?? string.Empty;
        bool hasMultiplePlaces = _placeOptions.Count > 1;

        if (_titleLabel != null)
        {
            _titleLabel.style.display = hasMultiplePlaces ? DisplayStyle.None : DisplayStyle.Flex;
            _titleLabel.text = string.IsNullOrWhiteSpace(CurrentDisplayedPlaceName) ? "상세 정보" : CurrentDisplayedPlaceName;
        }

        if (_subtitleLabel != null)
        {
            bool hasCategory = !string.IsNullOrWhiteSpace(currentCategory);
            _subtitleLabel.style.display = hasCategory ? DisplayStyle.Flex : DisplayStyle.None;
            _subtitleLabel.text = hasCategory ? currentCategory : string.Empty;
        }

        if (_addressLabel != null)
        {
            _addressLabel.text = string.IsNullOrWhiteSpace(data.fetchedAddress) ? "주소 정보가 없습니다." : data.fetchedAddress;
        }

        if (_phoneLabel != null)
        {
            _phoneLabel.text = string.IsNullOrWhiteSpace(currentPhone) ? "전화번호 정보가 없습니다." : currentPhone;
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

        bool hasPhone = !string.IsNullOrWhiteSpace(currentPhone);
        if (_phoneRow != null)
        {
            _phoneRow.style.display = DisplayStyle.Flex;
        }
        if (_phoneCallButton != null)
        {
            _phoneCallButton.style.display = hasPhone ? DisplayStyle.Flex : DisplayStyle.None;
            _phoneCallButton.SetEnabled(hasPhone);
        }

        bool hasMap = !string.IsNullOrWhiteSpace(CurrentDisplayedPlaceUrl);
        if (_mapActionRow != null)
        {
            _mapActionRow.style.display = hasMap ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (_mapOpenButton != null)
        {
            _mapOpenButton.SetEnabled(hasMap);
        }
    }

    private void BuildPlaceOptions(BuildingData data)
    {
        _placeOptions.Clear();
        if (data == null)
        {
            return;
        }

        AddPlaceOption(data.buildingName, data.description, data.phoneNumber, data.placeUrl);

        if (data.facilities == null)
        {
            return;
        }

        foreach (FacilityInfo facility in data.facilities)
        {
            if (facility == null)
            {
                continue;
            }

            AddPlaceOption(facility.name, facility.category, facility.phone, facility.placeUrl);
        }
    }

    private void AddPlaceOption(string name, string category, string phone, string placeUrl)
    {
        if (string.IsNullOrWhiteSpace(name) &&
            string.IsNullOrWhiteSpace(category) &&
            string.IsNullOrWhiteSpace(phone) &&
            string.IsNullOrWhiteSpace(placeUrl))
        {
            return;
        }

        string candidateKey = BuildPlaceOptionKey(name, category, phone, placeUrl);
        foreach (DetailPlaceOption existing in _placeOptions)
        {
            if (BuildPlaceOptionKey(existing.name, existing.category, existing.phone, existing.placeUrl) == candidateKey)
            {
                return;
            }
        }

        _placeOptions.Add(new DetailPlaceOption
        {
            name = name,
            category = category,
            phone = phone,
            placeUrl = placeUrl
        });
    }

    private string BuildPlaceOptionKey(string name, string category, string phone, string placeUrl)
    {
        return $"{NormalizeValue(name)}|{NormalizeValue(category)}|{NormalizeValue(phone)}|{NormalizeValue(placeUrl)}";
    }

    private string NormalizeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
    }

    private DetailPlaceOption GetCurrentPlaceOption()
    {
        if (_selectedPlaceIndex < 0 || _selectedPlaceIndex >= _placeOptions.Count)
        {
            return null;
        }

        return _placeOptions[_selectedPlaceIndex];
    }

    private void RefreshFacilitySelector()
    {
        if (_facilitySelectorSection == null || _facilityList == null)
        {
            return;
        }

        bool hasMultiplePlaces = _placeOptions.Count > 1;
        _facilitySelectorSection.style.display = hasMultiplePlaces ? DisplayStyle.Flex : DisplayStyle.None;

        if (_facilityToggleLabel != null)
        {
            _facilityToggleLabel.text = "현재 장소";
        }

        DetailPlaceOption currentPlace = GetCurrentPlaceOption();
        if (_facilitySelectedNameLabel != null)
        {
            _facilitySelectedNameLabel.text = string.IsNullOrWhiteSpace(currentPlace?.name)
                ? "대표 장소"
                : currentPlace.name;
        }

        PopulateFacilityList();
        UpdateFacilityChevron();
    }

    private void PopulateFacilityList()
    {
        if (_facilityList == null)
        {
            return;
        }

        _facilityList.Clear();
        for (int i = 0; i < _placeOptions.Count; i++)
        {
            DetailPlaceOption option = _placeOptions[i];
            int optionIndex = i;

            Button itemButton = new Button(() => OnFacilityOptionSelected(optionIndex))
            {
                focusable = false
            };
            itemButton.AddToClassList("facility-list-item");
            if (optionIndex == _selectedPlaceIndex)
            {
                itemButton.AddToClassList("facility-list-item--selected");
            }

            Label nameLabel = new Label(string.IsNullOrWhiteSpace(option.name) ? "이름 없음" : option.name);
            nameLabel.AddToClassList("facility-list-item-name");
            itemButton.Add(nameLabel);

            if (!string.IsNullOrWhiteSpace(option.category))
            {
                Label categoryLabel = new Label(option.category);
                categoryLabel.AddToClassList("facility-list-item-category");
                itemButton.Add(categoryLabel);
            }

            _facilityList.Add(itemButton);
        }
    }

    private void OnFacilityToggleClicked(ClickEvent evt)
    {
        if (_placeOptions.Count <= 1)
        {
            return;
        }

        SetFacilityListExpanded(!_isFacilityListExpanded);
        evt.StopPropagation();
    }

    private void OnFacilityOptionSelected(int index)
    {
        if (index < 0 || index >= _placeOptions.Count || _currentData == null)
        {
            return;
        }

        _selectedPlaceIndex = index;
        BindCurrentPlace(_currentData);
        RefreshFacilitySelector();
        SetFacilityListExpanded(false);
    }

    private void SetFacilityListExpanded(bool expanded)
    {
        _isFacilityListExpanded = expanded && _placeOptions.Count > 1;
        if (_facilityList != null)
        {
            _facilityList.style.display = _isFacilityListExpanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        UpdateFacilityChevron();
    }

    private void UpdateFacilityChevron()
    {
        if (_facilityToggleChevronIcon != null)
        {
            if (_isFacilityListExpanded)
            {
                _facilityToggleChevronIcon.AddToClassList("facility-toggle-chevron-icon--expanded");
            }
            else
            {
                _facilityToggleChevronIcon.RemoveFromClassList("facility-toggle-chevron-icon--expanded");
            }
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
