using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class ShopDialogueEntry
{
    [HorizontalGroup("Identity", Width = 0.35f)]
    [SerializeField, LabelText("주제 ID")]
    private string _topicId;

    [HorizontalGroup("Identity", Width = 0.65f)]
    [SerializeField, LabelText("메뉴 이름")]
    private string _displayName;

    [SerializeField, LabelText("대화 데이터 (선택)")]
    [Tooltip("긴 대사/여러 노드/분기는 기존 대화 메이커에서 만든 DialogueData를 연결합니다. 비워 두면 아래 기본 대사를 사용합니다.")]
    private DialogueData _dialogue;

    [SerializeField, LabelText("대화 중 상점 이미지 (선택)")]
    [Tooltip("이 주제를 대화하는 동안 표시할 전체 상점 이미지입니다. 비우면 기본 이미지를 유지하고, 대화가 끝나면 기본 이미지로 돌아갑니다.")]
    private Sprite _portraitOverride;

    [SerializeField, LabelText("표정")]
    private EmotionType _emotion = EmotionType.Normal;

    [SerializeField, LabelText("현지화 키")]
    private string _localizationKey;

    [SerializeField, TextArea(2, 4), LabelText("기본 대사")]
    private string _defaultText;

    [SerializeField, LabelText("이벤트 ID")]
    [Tooltip("기본 대사 시작 이벤트입니다. 상점의 '대사 이벤트 반응'에 같은 ID를 등록하면 이미지 교체·흔들림을 실행합니다. 대화 데이터 연결 시에는 각 노드의 EventTriggerID를 사용합니다.")]
    private string _eventTriggerId;

    public string TopicId => Normalize(_topicId);
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? TopicId : _displayName.Trim();
    public EmotionType Emotion => _emotion;
    public DialogueData Dialogue => _dialogue;
    public Sprite PortraitOverride => _portraitOverride;
    public string LocalizationKey => Normalize(_localizationKey);
    public string DefaultText => _defaultText ?? string.Empty;
    public string EventTriggerId => Normalize(_eventTriggerId);

    public ShopDialogueEntry()
    {
    }

    public ShopDialogueEntry(
        string topicId,
        string displayName,
        EmotionType emotion,
        string localizationKey,
        string defaultText,
        string eventTriggerId = null)
    {
        _topicId = Normalize(topicId);
        _displayName = Normalize(displayName);
        _emotion = emotion;
        _localizationKey = Normalize(localizationKey);
        _defaultText = defaultText ?? string.Empty;
        _eventTriggerId = Normalize(eventTriggerId);
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(TopicId))
        {
            error = "대화 주제 ID가 비어 있습니다.";
            return false;
        }

        if (string.IsNullOrEmpty(DisplayName))
        {
            error = $"{TopicId}: 메뉴 이름이 비어 있습니다.";
            return false;
        }

        if (_dialogue != null && !DialoguePlaybackPolicy.TryValidate(_dialogue, out error))
            return false;

        if (_dialogue == null && string.IsNullOrWhiteSpace(DefaultText) && string.IsNullOrEmpty(LocalizationKey))
        {
            error = $"{TopicId}: 현지화 키 또는 기본 대사가 필요합니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

[Serializable]
public sealed class ShopPresentationReaction
{
    [SerializeField, LabelText("대사 이벤트 ID")]
    private string _eventId;

    [SerializeField, LabelText("바꿀 상점 이미지 (선택)")]
    [Tooltip("얼굴 초상이 아닌 전체 상점 이미지입니다. 대화 종료 시 기본 이미지로 복구합니다.")]
    private Sprite _portrait;

    [SerializeField, LabelText("흔들림")]
    private bool _shake;

    [SerializeField, ShowIf("_shake"), Min(0f), LabelText("흔들림 시간")]
    private float _shakeDuration = 0.28f;

    [SerializeField, ShowIf("_shake"), Range(0f, 2f), LabelText("흔들림 강도")]
    private float _shakeStrength = 1f;

    public string EventId => string.IsNullOrWhiteSpace(_eventId) ? string.Empty : _eventId.Trim();
    public Sprite Portrait => _portrait;
    public bool Shake => _shake;
    public float ShakeDuration => Mathf.Max(0f, _shakeDuration);
    public float ShakeStrength => Mathf.Clamp(_shakeStrength, 0f, 2f);

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(EventId))
            error = "대사 이벤트 ID가 비어 있습니다.";
        else if (_portrait == null && !_shake)
            error = $"{EventId}: 이미지 또는 흔들림을 설정하세요.";
        else if (float.IsNaN(_shakeDuration) || float.IsInfinity(_shakeDuration) || _shakeDuration < 0f
            || float.IsNaN(_shakeStrength) || float.IsInfinity(_shakeStrength) || _shakeStrength < 0f)
            error = $"{EventId}: 흔들림 시간과 강도는 유효한 양수 또는 0이어야 합니다.";
        else
        {
            error = string.Empty;
            return true;
        }
        return false;
    }
}

[Serializable]
public sealed class ShopServiceEntry
{
    [HorizontalGroup("Identity", Width = 0.35f)]
    [SerializeField, LabelText("서비스 ID")]
    private string _serviceId;

    [HorizontalGroup("Identity", Width = 0.65f)]
    [SerializeField, LabelText("메뉴 이름")]
    private string _displayName;

    [SerializeField, TextArea(2, 3), LabelText("설명")]
    private string _description;

    [SerializeField, Min(0), LabelText("가격")]
    [Tooltip("0이면 무료입니다.")]
    private int _price;

    [SerializeField, LabelText("HP 완전 회복")]
    private bool _restoreHp = true;

    [SerializeField, LabelText("AP 완전 회복")]
    private bool _restoreAp;

    public string ServiceId => Normalize(_serviceId);
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? ServiceId : _displayName.Trim();
    public string Description => _description ?? string.Empty;
    public int Price => Mathf.Max(0, _price);
    public bool RestoreHp => _restoreHp;
    public bool RestoreAp => _restoreAp;

    public ShopServiceEntry()
    {
    }

    public ShopServiceEntry(
        string serviceId,
        string displayName,
        string description,
        int price,
        bool restoreHp,
        bool restoreAp)
    {
        _serviceId = Normalize(serviceId);
        _displayName = Normalize(displayName);
        _description = description ?? string.Empty;
        _price = Mathf.Max(0, price);
        _restoreHp = restoreHp;
        _restoreAp = restoreAp;
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(ServiceId))
        {
            error = "서비스 ID가 비어 있습니다.";
            return false;
        }

        if (string.IsNullOrEmpty(DisplayName))
        {
            error = $"{ServiceId}: 메뉴 이름이 비어 있습니다.";
            return false;
        }

        if (!RestoreHp && !RestoreAp)
        {
            error = $"{ServiceId}: 회복 대상 HP 또는 AP를 하나 이상 선택하세요.";
            return false;
        }

        if (_price < 0)
        {
            error = $"{ServiceId}: 가격은 0 이상이어야 합니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

[Serializable]
public sealed class ShopEntry
{
    [HorizontalGroup("Identity", Width = 0.42f)]
    [SerializeField, LabelText("Entry ID")]
    private string _entryId;

    [HorizontalGroup("Identity", Width = 0.58f)]
    [SerializeField, Required, LabelText("Item")]
    private ItemData _item;

    [HorizontalGroup("Purchase", Width = 0.34f)]
    [SerializeField, Min(0), LabelText("단가")]
    private int _price;

    [HorizontalGroup("Purchase", Width = 0.33f)]
    [SerializeField, Min(1), LabelText("1회 수량")]
    private int _quantity = 1;

    [HorizontalGroup("Purchase", Width = 0.33f)]
    [SerializeField, Min(0), LabelText("구매 제한")]
    [Tooltip("0은 무제한입니다. 제한은 아이템 개수가 아니라 구매 횟수 기준입니다.")]
    private int _purchaseLimit;

    [SerializeField, LabelText("구매 카운터 Flag")]
    private string _purchaseCounterFlag;

    public string EntryId => Normalize(_entryId);
    public ItemData Item => _item;
    public int Price => _price;
    public int Quantity => _quantity;
    public int PurchaseLimit => _purchaseLimit;
    public string PurchaseCounterFlag => Normalize(_purchaseCounterFlag);

    public ShopEntry()
    {
    }

    public ShopEntry(
        string entryId,
        ItemData item,
        int price,
        int quantity,
        int purchaseLimit,
        string purchaseCounterFlag)
    {
        _entryId = Normalize(entryId);
        _item = item;
        _price = price;
        _quantity = quantity;
        _purchaseLimit = purchaseLimit;
        _purchaseCounterFlag = Normalize(purchaseCounterFlag);
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(EntryId))
        {
            error = "Entry ID가 비어 있습니다.";
            return false;
        }

        if (_item == null)
        {
            error = $"{EntryId}: ItemData가 비어 있습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_item.ItemID))
        {
            error = $"{EntryId}: ItemData의 Item ID가 비어 있습니다.";
            return false;
        }

        if (_price < 0)
        {
            error = $"{EntryId}: 단가는 0 이상이어야 합니다.";
            return false;
        }

        if (_quantity <= 0)
        {
            error = $"{EntryId}: 1회 구매 수량은 1 이상이어야 합니다.";
            return false;
        }

        if (_purchaseLimit < 0)
        {
            error = $"{EntryId}: 구매 제한은 0 이상이어야 합니다.";
            return false;
        }

        if (string.IsNullOrEmpty(PurchaseCounterFlag))
        {
            error = $"{EntryId}: 구매 카운터 Flag가 비어 있습니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

[CreateAssetMenu(fileName = "ShopDefinition", menuName = "Hub To Home/아이템/상점 데이터")]
public sealed class ShopDefinition : ScriptableObject
{
    [TitleGroup("기본 정보")]
    [SerializeField, Required, LabelText("Shop ID")]
    private string _shopId;

    [TitleGroup("기본 정보")]
    [SerializeField, LabelText("표시 이름")]
    private string _displayName;

    [TitleGroup("판매 목록")]
    [SerializeField, ListDrawerSettings(ShowIndexLabels = true), LabelText("Entries")]
    private List<ShopEntry> _entries = new List<ShopEntry>();

    [TitleGroup("상인 연출")]
    [SerializeField, LabelText("상인 이미지")]
    [PreviewField(100, ObjectFieldAlignment.Center)]
    private Sprite _vendorPortrait;

    [TitleGroup("상인 연출")]
    [SerializeField, LabelText("상인 표시 이름")]
    private string _vendorDisplayName;

    [TitleGroup("상인 연출")]
    [SerializeField, LabelText("기본 대사 화자 (선택)")]
    [Tooltip("연결하면 기본 대사에서 이 화자의 이름·목소리·얼굴 초상을 사용합니다. 긴 DialogueData는 각 노드의 화자를 그대로 사용합니다.")]
    private SpeakerData _vendorSpeaker;

    [TitleGroup("상인 연출")]
    [SerializeField, LabelText("서비스 메뉴 이름")]
    private string _serviceMenuLabel = "서비스";

    [TitleGroup("상인 연출")]
    [SerializeField, ListDrawerSettings(ShowIndexLabels = true), LabelText("대사 이벤트 반응")]
    private List<ShopPresentationReaction> _reactions = new List<ShopPresentationReaction>();

    [TitleGroup("상인 연출")]
    [SerializeField, LabelText("상점 배경음악")]
    [Tooltip("비우면 현재 음악을 유지합니다. 상점을 닫으면 입장 전 음악으로 복구합니다.")]
    private AudioClip _backgroundMusic;

    [TitleGroup("상인 연출")]
    [SerializeField, Min(0f), LabelText("입장 암전 시간")]
    private float _entryFadeOutDuration = 0.12f;

    [TitleGroup("상인 연출")]
    [SerializeField, Min(0f), LabelText("입장 밝아지는 시간")]
    private float _entryFadeInDuration = 0.3f;

    [TitleGroup("상인 연출")]
    [SerializeField, Min(0f), LabelText("음악 전환 시간")]
    private float _musicFadeDuration = 0.35f;

    [TitleGroup("상인 연출")]
    [SerializeField, ListDrawerSettings(ShowIndexLabels = true), LabelText("대화 주제")]
    private List<ShopDialogueEntry> _dialogues = new List<ShopDialogueEntry>();

    [TitleGroup("상인 연출")]
    [SerializeField, ListDrawerSettings(ShowIndexLabels = true), LabelText("회복 서비스")]
    private List<ShopServiceEntry> _services = new List<ShopServiceEntry>();

    public string ShopId => Normalize(_shopId);
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? ShopId : _displayName.Trim();
    public IReadOnlyList<ShopEntry> Entries => _entries ?? (IReadOnlyList<ShopEntry>)Array.Empty<ShopEntry>();
    public Sprite VendorPortrait => _vendorPortrait;
    public AudioClip BackgroundMusic => _backgroundMusic;
    public float EntryFadeOutDuration => SafeDuration(_entryFadeOutDuration, 0.12f);
    public float EntryFadeInDuration => SafeDuration(_entryFadeInDuration, 0.3f);
    public float MusicFadeDuration => SafeDuration(_musicFadeDuration, 0.35f);
    public string VendorDisplayName => !string.IsNullOrWhiteSpace(_vendorDisplayName)
        ? _vendorDisplayName.Trim()
        : _vendorSpeaker != null && !string.IsNullOrWhiteSpace(_vendorSpeaker.DisplayName)
            ? _vendorSpeaker.DisplayName : DisplayName;
    public SpeakerData VendorSpeaker => _vendorSpeaker;
    public string ServiceMenuLabel => string.IsNullOrWhiteSpace(_serviceMenuLabel) ? "서비스" : _serviceMenuLabel.Trim();
    public IReadOnlyList<ShopPresentationReaction> Reactions => _reactions ?? (IReadOnlyList<ShopPresentationReaction>)Array.Empty<ShopPresentationReaction>();
    public IReadOnlyList<ShopDialogueEntry> Dialogues => _dialogues ?? (IReadOnlyList<ShopDialogueEntry>)Array.Empty<ShopDialogueEntry>();
    public IReadOnlyList<ShopServiceEntry> Services => _services ?? (IReadOnlyList<ShopServiceEntry>)Array.Empty<ShopServiceEntry>();

    public void Configure(string shopId, string displayName, IEnumerable<ShopEntry> entries)
    {
        _shopId = Normalize(shopId);
        _displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
        _entries = entries != null ? new List<ShopEntry>(entries) : new List<ShopEntry>();
    }

    private static float SafeDuration(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0f, value);
    }

    public bool TryFindUniqueEntry(
        string entryId,
        out ShopEntry entry,
        out int matchCount)
    {
        entry = null;
        matchCount = 0;
        string normalizedId = Normalize(entryId);
        if (string.IsNullOrEmpty(normalizedId) || _entries == null)
            return false;

        for (int i = 0; i < _entries.Count; i++)
        {
            ShopEntry candidate = _entries[i];
            if (candidate == null
                || !string.Equals(candidate.EntryId, normalizedId, StringComparison.Ordinal))
            {
                continue;
            }

            matchCount++;
            if (entry == null)
                entry = candidate;
        }

        return matchCount == 1;
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(ShopId))
        {
            error = "Shop ID가 비어 있습니다.";
            return false;
        }

        if (Entries.Count == 0 && Dialogues.Count == 0 && Services.Count == 0)
        {
            error = $"{ShopId}: 판매 상품, 대화 주제 또는 회복 서비스가 하나 이상 필요합니다.";
            return false;
        }

        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < Entries.Count; i++)
        {
            ShopEntry entry = Entries[i];
            if (entry == null)
            {
                error = $"Entry #{i + 1}이 비어 있습니다.";
                return false;
            }

            if (!entry.TryValidate(out string entryError))
            {
                error = $"Entry #{i + 1}: {entryError}";
                return false;
            }

            if (!entryIds.Add(entry.EntryId))
            {
                error = $"Entry ID가 중복됩니다: {entry.EntryId}";
                return false;
            }
        }

        var dialogueIds = new HashSet<string>(StringComparer.Ordinal);
        if (_dialogues != null)
        {
            for (int i = 0; i < _dialogues.Count; i++)
            {
                ShopDialogueEntry dialogue = _dialogues[i];
                if (dialogue == null)
                {
                    error = $"대화 주제 #{i + 1}이 비어 있습니다.";
                    return false;
                }

                if (!dialogue.TryValidate(out string dialogueError))
                {
                    error = $"대화 주제 #{i + 1}: {dialogueError}";
                    return false;
                }

                if (!dialogueIds.Add(dialogue.TopicId))
                {
                    error = $"대화 주제 ID가 중복됩니다: {dialogue.TopicId}";
                    return false;
                }
            }
        }

        var reactionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ShopPresentationReaction reaction in Reactions)
        {
            if (reaction == null)
            {
                error = "비어 있는 대사 이벤트 반응이 있습니다.";
                return false;
            }
            if (!reaction.TryValidate(out error))
                return false;
            if (!reactionIds.Add(reaction.EventId))
            {
                error = $"대사 이벤트 ID가 중복됩니다: {reaction.EventId}";
                return false;
            }
        }

        var serviceIds = new HashSet<string>(StringComparer.Ordinal);
        if (_services != null)
        {
            for (int i = 0; i < _services.Count; i++)
            {
                ShopServiceEntry service = _services[i];
                if (service == null)
                {
                    error = $"서비스 #{i + 1}이 비어 있습니다.";
                    return false;
                }

                if (!service.TryValidate(out string serviceError))
                {
                    error = $"서비스 #{i + 1}: {serviceError}";
                    return false;
                }

                if (!serviceIds.Add(service.ServiceId))
                {
                    error = $"서비스 ID가 중복됩니다: {service.ServiceId}";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    [TitleGroup("검증")]
    [Button("Shop 검증")]
    private void ValidateAndLog()
    {
        if (TryValidate(out string error))
            Debug.Log($"[ShopDefinition] 검증 통과: {ShopId}", this);
        else
            Debug.LogError("[ShopDefinition] " + error, this);
    }

    private void OnValidate()
    {
        // Validation intentionally reports bad authoring data without rewriting it.
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
