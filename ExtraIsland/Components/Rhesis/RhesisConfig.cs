using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using ExtraIsland.Shared;

namespace ExtraIsland.Components;

// ReSharper disable once ClassNeverInstantiated.Global
public class RhesisConfig : ObservableObject {
    RhesisDataSource _dataSource = RhesisDataSource.SaintJinrishici;
    public RhesisDataSource DataSource { 
        get => _dataSource;
        set {
            if (_dataSource == value) return;
            _dataSource = value;
            OnPropertyChanged();
        } 
    }

    public string IgnoreListString { get; set; } = string.Empty;
    
    public string HitokotoProp  { get; set; } = string.Empty;
    
    public DateTime LastUpdate { get; set; } = DateTime.Today;

    public int LengthLimitation { get; set; }
    
    [JsonIgnore]
    public string HitokotoLengthArgs {
        get {
            return LengthLimitation switch {
                0 => string.Empty,
                _ => $"max_length={LengthLimitation}&"
            };
        }
    }

    public TimeSpan UpdateTimeGap { get; set; } = TimeSpan.FromSeconds(30);

    [JsonIgnore]
    public double UpdateTimeGapSeconds {
        get => UpdateTimeGap.TotalSeconds;
        set => UpdateTimeGap = TimeSpan.FromSeconds(value);
    }
    
    public string SainticProp  { get; set; } = string.Empty;
    
    public bool IsAnimationEnabled { get; set; } = true;
    
    public bool IsSwapAnimationEnabled { get; set; }

    public bool IsAuthorShowEnabled { get; set; }
    public bool IsTitleShowEnabled { get; set; }

    int _attributesShowingInterval = 3;
    public int AttributesShowingInterval {
        get => _attributesShowingInterval;
        set {
            if(_attributesShowingInterval == value) return;
            _attributesShowingInterval = value;
            OnPropertyChanged();
        }
    }

    AttributesDisplayRule _attributesRule = AttributesDisplayRule.Sametime;
    public AttributesDisplayRule AttributesRule {
        get => _attributesRule;
        set {
            if (value == _attributesRule) return;
            _attributesRule = value;
            OnPropertyChanged();
        }
    }

    public enum AttributesDisplayRule {
        [Description("同时展示")]
        Sametime,
        [Description("分开展示")]
        Separate
    }

    // AI 筛选相关
    bool _aiFilterEnabled;
    public bool AiFilterEnabled {
        get => _aiFilterEnabled;
        set {
            if (_aiFilterEnabled == value) return;
            _aiFilterEnabled = value;
            OnPropertyChanged();
        }
    }

    public string AiFilterEndpoint { get; set; } = "https://api.openai.com/v1";
    public string AiFilterApiKey { get; set; } = string.Empty;
    public string AiFilterModel { get; set; } = "gpt-4o-mini";
    string _aiFilterInstructions = string.Empty;
    public string AiFilterInstructions {
        get => string.IsNullOrWhiteSpace(_aiFilterInstructions)
            ? "你是一名内容审查员。请根据以下标准判断句子是否适合在教室大屏幕上展示：\n排除内容：暴力、色情、政治敏感、广告、歧视性言论。\n鼓励展示：积极健康、富有哲理、励志向上的名言。"
            : _aiFilterInstructions;
        set {
            if (_aiFilterInstructions == value) return;
            _aiFilterInstructions = value;
            OnPropertyChanged();
        }
    }

    AiFilterApiType _aiFilterApiType = AiFilterApiType.Chat;
    public AiFilterApiType AiFilterApiType {
        get => _aiFilterApiType;
        set {
            if (_aiFilterApiType == value) return;
            _aiFilterApiType = value;
            OnPropertyChanged();
        }
    }

    bool _aiFilterEnableWebSearch;
    public bool AiFilterEnableWebSearch {
        get => _aiFilterEnableWebSearch;
        set {
            if (_aiFilterEnableWebSearch == value) return;
            _aiFilterEnableWebSearch = value;
            OnPropertyChanged();
        }
    }

    bool _aiFilterDeepThinking;
    public bool AiFilterDeepThinking {
        get => _aiFilterDeepThinking;
        set {
            if (_aiFilterDeepThinking == value) return;
            _aiFilterDeepThinking = value;
            OnPropertyChanged();
        }
    }
}