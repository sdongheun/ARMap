using UnityEngine;
using UnityEngine.UIElements;
using UITKButton = UnityEngine.UIElements.Button;

public partial class ARUIManager
{
    private const float ToolkitPortraitButtonCornerRadius = 9f;
    private const float ToolkitLandscapeButtonCornerRadius = 12f;
    private const float ToolkitButtonHorizontalMargin = 3f;
    private const float ToolkitPortraitBarBottomMargin = 24f;
    private const float ToolkitPortraitRootHorizontalPadding = 18f;
    private const float ToolkitLandscapeBarBottomMargin = 18f;
    private const float ToolkitLandscapeRootRightPadding = 18f;
    private const float ToolkitPortraitBarHeight = 50f;
    private const float ToolkitPortraitBarPaddingHorizontal = 8f;
    private const float ToolkitPortraitBarPaddingVertical = 6f;
    private const float ToolkitPortraitButtonWidth = 64f;
    private const float ToolkitPortraitButtonHeight = 28f;
    private const float ToolkitPortraitButtonFontSize = 12f;
    private const float ToolkitLandscapeBarHeight = 32f;
    private const float ToolkitLandscapeBarPaddingHorizontal = 6f;
    private const float ToolkitLandscapeBarPaddingVertical = 5f;
    private const float ToolkitLandscapeButtonWidth = 64f;
    private const float ToolkitLandscapeButtonHeight = 24f;
    private const float ToolkitLandscapeButtonFontSize = 8f;

    private UIDocument _bottomBarToolkitDocument;
    private VisualElement _bottomBarToolkitRoot;
    private VisualElement _bottomBarToolkitBar;
    private UITKButton _toolkitNavigateButton;
    private UITKButton _toolkitLandscapeButton;
    private UITKButton _toolkitDetailButton;
    private bool _toolkitDetailButtonEnabled;
    private bool _toolkitBottomBarNavigationMode;

    // UI Toolkit 기반 하단 액션바 문서를 생성하고 버튼을 구성한다.
    void EnsureBottomActionBarToolkit()
    {
        if (_bottomBarToolkitDocument != null && _bottomBarToolkitBar != null)
        {
            return;
        }

        PanelSettings panelSettings = uiToolkitDetailPanel != null
            ? uiToolkitDetailPanel.GetComponent<UIDocument>()?.panelSettings
            : null;
        if (panelSettings == null)
        {
            return;
        }

        GameObject documentObject = new GameObject("ARBottomActionBarUIDocument");
        documentObject.transform.SetParent(transform, false);

        _bottomBarToolkitDocument = documentObject.AddComponent<UIDocument>();
        _bottomBarToolkitDocument.panelSettings = panelSettings;

        _bottomBarToolkitRoot = _bottomBarToolkitDocument.rootVisualElement;
        if (_bottomBarToolkitRoot == null)
        {
            return;
        }

        _bottomBarToolkitRoot.style.position = Position.Absolute;
        _bottomBarToolkitRoot.style.left = 0f;
        _bottomBarToolkitRoot.style.right = 0f;
        _bottomBarToolkitRoot.style.top = 0f;
        _bottomBarToolkitRoot.style.bottom = 0f;
        _bottomBarToolkitRoot.style.justifyContent = Justify.FlexEnd;
        _bottomBarToolkitRoot.style.alignItems = Align.Center;
        _bottomBarToolkitRoot.pickingMode = PickingMode.Ignore;

        _bottomBarToolkitBar = new VisualElement();
        _bottomBarToolkitBar.name = "bottom-action-bar-toolkit";
        _bottomBarToolkitBar.pickingMode = PickingMode.Position;
        _bottomBarToolkitBar.style.position = Position.Absolute;
        _bottomBarToolkitBar.style.flexDirection = FlexDirection.Row;
        _bottomBarToolkitBar.style.alignItems = Align.Center;
        _bottomBarToolkitBar.style.justifyContent = Justify.Center;
        _bottomBarToolkitBar.style.backgroundColor = new StyleColor(new Color(0.07f, 0.10f, 0.15f, 0.88f));
        _bottomBarToolkitBar.style.borderTopLeftRadius = bottomActionBarCornerRadius;
        _bottomBarToolkitBar.style.borderTopRightRadius = bottomActionBarCornerRadius;
        _bottomBarToolkitBar.style.borderBottomLeftRadius = bottomActionBarCornerRadius;
        _bottomBarToolkitBar.style.borderBottomRightRadius = bottomActionBarCornerRadius;
        _bottomBarToolkitBar.style.paddingLeft = ToolkitPortraitBarPaddingHorizontal;
        _bottomBarToolkitBar.style.paddingRight = ToolkitPortraitBarPaddingHorizontal;
        _bottomBarToolkitBar.style.paddingTop = ToolkitPortraitBarPaddingVertical;
        _bottomBarToolkitBar.style.paddingBottom = ToolkitPortraitBarPaddingVertical;
        _toolkitNavigateButton = CreateToolkitActionButton("길찾기", () => OnNavigateRequested?.Invoke());
        _toolkitLandscapeButton = CreateToolkitActionButton("가로모드", OnToolkitLandscapeButtonClicked);
        _toolkitDetailButton = CreateToolkitActionButton("상세정보", OnToolkitDetailButtonClicked);

        _bottomBarToolkitBar.Add(_toolkitNavigateButton);
        _bottomBarToolkitBar.Add(_toolkitLandscapeButton);
        _bottomBarToolkitBar.Add(_toolkitDetailButton);
        _bottomBarToolkitRoot.Add(_bottomBarToolkitBar);

        UpdateToolkitLandscapeButtonState(_isLandscapeModeEnabled);
        UpdateToolkitDetailButtonState(_worldInfoButtonData, false);
    }

    // 공통 스타일을 적용한 UI Toolkit 액션 버튼을 생성한다.
    UITKButton CreateToolkitActionButton(string label, System.Action onClick)
    {
        UITKButton button = new UITKButton();
        button.text = label;
        if (onClick != null)
        {
            button.clicked += onClick;
        }

        button.style.height = ToolkitPortraitButtonHeight;
        button.style.minHeight = ToolkitPortraitButtonHeight;
        button.style.minWidth = ToolkitPortraitButtonWidth;
        button.style.paddingLeft = 8f;
        button.style.paddingRight = 8f;
        button.style.marginLeft = ToolkitButtonHorizontalMargin;
        button.style.marginRight = ToolkitButtonHorizontalMargin;
        button.style.borderTopLeftRadius = ToolkitPortraitButtonCornerRadius;
        button.style.borderTopRightRadius = ToolkitPortraitButtonCornerRadius;
        button.style.borderBottomLeftRadius = ToolkitPortraitButtonCornerRadius;
        button.style.borderBottomRightRadius = ToolkitPortraitButtonCornerRadius;
        button.style.borderLeftWidth = 0f;
        button.style.borderRightWidth = 0f;
        button.style.borderTopWidth = 0f;
        button.style.borderBottomWidth = 0f;
        button.style.backgroundColor = new StyleColor(new Color(0.28f, 0.32f, 0.38f, 0.9f));
        button.style.color = new StyleColor(new Color(0.82f, 0.86f, 0.9f, 0.9f));
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = ToolkitPortraitButtonFontSize;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.pickingMode = PickingMode.Position;
        return button;
    }

    // 현재 화면 방향과 상태에 맞게 UI Toolkit 액션바 레이아웃을 갱신한다.
    void RefreshBottomActionBarToolkitLayout()
    {
        EnsureBottomActionBarToolkit();
        if (_bottomBarToolkitRoot == null || _bottomBarToolkitBar == null)
        {
            return;
        }

        bool isLandscapeLike = Screen.width > Screen.height;

        _bottomBarToolkitRoot.style.paddingBottom = 0f;
        _bottomBarToolkitRoot.style.paddingLeft = 0f;
        _bottomBarToolkitRoot.style.paddingRight = 0f;
        _bottomBarToolkitRoot.style.alignItems = Align.Center;

        _bottomBarToolkitBar.style.display = DisplayStyle.Flex;
        _bottomBarToolkitBar.style.marginBottom = 0f;
        _bottomBarToolkitBar.style.left = StyleKeyword.Null;
        _bottomBarToolkitBar.style.right = StyleKeyword.Null;
        _bottomBarToolkitBar.style.bottom = StyleKeyword.Null;
        _bottomBarToolkitBar.style.width = StyleKeyword.Null;

        if (isLandscapeLike)
        {
            _bottomBarToolkitBar.style.alignSelf = Align.Auto;
            _bottomBarToolkitBar.style.right = ToolkitLandscapeRootRightPadding;
            _bottomBarToolkitBar.style.bottom = ToolkitLandscapeBarBottomMargin;
            _bottomBarToolkitBar.style.height = ToolkitLandscapeBarHeight;
            _bottomBarToolkitBar.style.paddingLeft = ToolkitLandscapeBarPaddingHorizontal;
            _bottomBarToolkitBar.style.paddingRight = ToolkitLandscapeBarPaddingHorizontal;
            _bottomBarToolkitBar.style.paddingTop = ToolkitLandscapeBarPaddingVertical;
            _bottomBarToolkitBar.style.paddingBottom = ToolkitLandscapeBarPaddingVertical;

            if (_toolkitNavigateButton != null)
            {
                _toolkitNavigateButton.style.display = DisplayStyle.None;
            }

            ApplyToolkitButtonSize(_toolkitLandscapeButton, ToolkitLandscapeButtonWidth, ToolkitLandscapeButtonHeight, ToolkitLandscapeButtonFontSize, ToolkitLandscapeButtonCornerRadius);
            ApplyToolkitButtonSize(_toolkitDetailButton, ToolkitLandscapeButtonWidth, ToolkitLandscapeButtonHeight, ToolkitLandscapeButtonFontSize, ToolkitLandscapeButtonCornerRadius);
        }
        else
        {
            _bottomBarToolkitBar.style.alignSelf = Align.Auto;
            _bottomBarToolkitBar.style.left = ToolkitPortraitRootHorizontalPadding;
            _bottomBarToolkitBar.style.right = ToolkitPortraitRootHorizontalPadding;
            _bottomBarToolkitBar.style.bottom = ToolkitPortraitBarBottomMargin;
            _bottomBarToolkitBar.style.height = ToolkitPortraitBarHeight;
            _bottomBarToolkitBar.style.paddingLeft = ToolkitPortraitBarPaddingHorizontal;
            _bottomBarToolkitBar.style.paddingRight = ToolkitPortraitBarPaddingHorizontal;
            _bottomBarToolkitBar.style.paddingTop = ToolkitPortraitBarPaddingVertical;
            _bottomBarToolkitBar.style.paddingBottom = ToolkitPortraitBarPaddingVertical;

            if (_toolkitNavigateButton != null)
            {
                _toolkitNavigateButton.style.display = _toolkitBottomBarNavigationMode ? DisplayStyle.None : DisplayStyle.Flex;
            }

            ApplyToolkitButtonSize(_toolkitNavigateButton, ToolkitPortraitButtonWidth, ToolkitPortraitButtonHeight, ToolkitPortraitButtonFontSize, ToolkitPortraitButtonCornerRadius);
            ApplyToolkitButtonSize(_toolkitLandscapeButton, ToolkitPortraitButtonWidth, ToolkitPortraitButtonHeight, ToolkitPortraitButtonFontSize, ToolkitPortraitButtonCornerRadius);
            ApplyToolkitButtonSize(_toolkitDetailButton, ToolkitPortraitButtonWidth, ToolkitPortraitButtonHeight, ToolkitPortraitButtonFontSize, ToolkitPortraitButtonCornerRadius);
        }

        if (_toolkitDetailButton != null)
        {
            _toolkitDetailButton.style.display = _toolkitBottomBarNavigationMode ? DisplayStyle.None : DisplayStyle.Flex;
        }

        UpdateToolkitLandscapeButtonState(_isLandscapeModeEnabled);
        UpdateToolkitDetailButtonState(_worldInfoButtonData, _toolkitDetailButtonEnabled);
    }

    // 상세 패널 등 전체 화면 UI가 올라올 때 하단 액션바를 숨기거나 다시 표시한다.
    void SetBottomActionBarToolkitVisible(bool visible)
    {
        EnsureBottomActionBarToolkit();
        if (_bottomBarToolkitBar == null)
        {
            return;
        }

        _bottomBarToolkitBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // 버튼 크기와 글자 크기를 한 번에 맞춘다.
    void ApplyToolkitButtonSize(UITKButton button, float width, float height, float fontSize, float cornerRadius)
    {
        if (button == null)
        {
            return;
        }

        button.style.minWidth = width;
        button.style.width = width;
        button.style.height = height;
        button.style.minHeight = height;
        button.style.fontSize = fontSize;
        button.style.borderTopLeftRadius = cornerRadius;
        button.style.borderTopRightRadius = cornerRadius;
        button.style.borderBottomLeftRadius = cornerRadius;
        button.style.borderBottomRightRadius = cornerRadius;
    }

    // UI Toolkit 가로모드 버튼을 눌렀을 때 기존 토글 흐름을 재사용한다.
    void OnToolkitLandscapeButtonClicked()
    {
        _isLandscapeModeEnabled = !_isLandscapeModeEnabled;
        ApplyLandscapeScreenOrientation(_isLandscapeModeEnabled);
        SetLandscapeModeButtonState(_isLandscapeModeEnabled);
        OnLandscapeModeToggleRequested?.Invoke(_isLandscapeModeEnabled);
        RefreshFloatingButtonLayout(force: true);
    }

    // UI Toolkit 상세 버튼을 눌렀을 때 현재 월드 선택 대상의 상세 패널을 연다.
    void OnToolkitDetailButtonClicked()
    {
        if (_worldInfoButtonData != null)
        {
            OpenDetailView(_worldInfoButtonData);
        }
    }

    // UI Toolkit 가로모드 버튼의 텍스트와 색상을 현재 상태에 맞게 갱신한다.
    void UpdateToolkitLandscapeButtonState(bool enabled)
    {
        if (_toolkitLandscapeButton == null)
        {
            return;
        }

        _toolkitLandscapeButton.text = enabled ? "세로 복귀" : "가로모드";
        _toolkitLandscapeButton.style.backgroundColor = new StyleColor(enabled
            ? new Color(0.08f, 0.78f, 0.96f, 1f)
            : new Color(0.28f, 0.32f, 0.38f, 0.9f));
        _toolkitLandscapeButton.style.color = new StyleColor(enabled
            ? Color.white
            : new Color(0.82f, 0.86f, 0.9f, 0.9f));
    }

    // UI Toolkit 상세 버튼의 활성 상태와 색상을 현재 선택 상태에 맞게 갱신한다.
    void UpdateToolkitDetailButtonState(BuildingData data, bool active)
    {
        if (_toolkitDetailButton == null)
        {
            return;
        }

        bool interactable = active && data != null;
        _toolkitDetailButtonEnabled = interactable;
        _toolkitDetailButton.SetEnabled(interactable);
        _toolkitDetailButton.style.backgroundColor = new StyleColor(interactable
            ? new Color(0.08f, 0.78f, 0.96f, 1f)
            : new Color(0.28f, 0.32f, 0.38f, 0.9f));
        _toolkitDetailButton.style.color = new StyleColor(interactable
            ? Color.white
            : new Color(0.82f, 0.86f, 0.9f, 0.9f));
    }
}
