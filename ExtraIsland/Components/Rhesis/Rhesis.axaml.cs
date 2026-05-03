using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ExtraIsland.Shared;

namespace ExtraIsland.Components;

[ComponentInfo(
                  "FBB380C2-5480-4FED-8349-BA5F4EAD2688",
                  "名句一言",
                  "\uE3F4",
                  "显示一句古今名言,可使用三个API,支持AI筛选"
              )]
public partial class Rhesis : ComponentBase<RhesisConfig> {
    public Rhesis(ILessonsService lessonsService) {
        _authorLabel = new Label {
            Content = Title,
            Margin = new Thickness(0,4,0,0),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(_authorLabel,0);
        _authorLabel.Bind(FontSizeProperty,new DynamicResourceExtension("MainWindowSecondaryFontSize"));

        _titleLabel = new Label {
            Content = Author,
            Margin = new Thickness(0,0,0,4),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(_titleLabel,1);
        _titleLabel.Bind(FontSizeProperty,new DynamicResourceExtension("MainWindowSecondaryFontSize"));

        _infoGrid = new Grid {
            VerticalAlignment = VerticalAlignment.Center,
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            Children = {
                _authorLabel,
                _titleLabel
            }
        };

        LessonsService = lessonsService;
        InitializeComponent();
        _mainLabelAnimator = new Animators.GenericContentSwapAnimator(MainLabel);
        _subLabelAnimator = new Animators.GenericContentSwapAnimator(SubLabel);
    }

    ILessonsService LessonsService { get; }

    public string Showing { get; private set; } = "-----------------";
    public string Author { get; private set; } = "";
    public string Title { get; private set; } = "";
    readonly RhesisHandler.Instance _rhesisHandler = new RhesisHandler.Instance();
    readonly AiRhesisFilter _aiFilter = new AiRhesisFilter();
    readonly Animators.GenericContentSwapAnimator _mainLabelAnimator;
    readonly Animators.GenericContentSwapAnimator _subLabelAnimator;
    readonly Grid _infoGrid;
    readonly Label _titleLabel;
    readonly Label _authorLabel;
    volatile bool _isUpdating;

    void OnAttachedToVisualTree(object? sender,VisualTreeAttachmentEventArgs visualTreeAttachmentEventArgs) {
        Settings.LastUpdate = DateTime.Now;
        _ = RunUpdateAsync();
        LessonsService.PostMainTimerTicked += UpdateEvent;
    }

    void OnDetachedFromVisualTree(object? sender,VisualTreeAttachmentEventArgs visualTreeAttachmentEventArgs) {
        LessonsService.PostMainTimerTicked -= UpdateEvent;
    }

    void UpdateEvent(object? sender,EventArgs eventArgs) {
        if (_isUpdating) return;
        if (Settings.UpdateTimeGapSeconds == 0) return;
        if (EiUtils.GetDateTimeSpan(Settings.LastUpdate,DateTime.Now) < Settings.UpdateTimeGap) return;
        _isUpdating = true;
        _ = RunUpdateAsync();
    }

    async Task RunUpdateAsync() {
        try {
            RhesisData data = await FetchFilteredQuoteAsync();
            Settings.LastUpdate = DateTime.Now;
            Showing = data.Content;
            Title = data.Title;
            Author = data.Author;
            await Dispatcher.UIThread.InvokeAsync(() => {
                object subObj;
                if (Settings.IsAuthorShowEnabled & Settings.IsTitleShowEnabled) {
                    subObj = Settings.AttributesShowingInterval == 0
                        ? _infoGrid
                        : $"{Author} {Title}";
                } else if (Settings.IsAuthorShowEnabled) {
                    subObj = Author;
                } else if (Settings.IsTitleShowEnabled) {
                    subObj = Title;
                } else {
                    subObj = string.Empty;
                }

                _titleLabel.Content = Title;
                _authorLabel.Content = Author;
                _mainLabelAnimator.Update(Showing,Settings.IsAnimationEnabled,Settings.IsSwapAnimationEnabled);
                if (Settings.IsAuthorShowEnabled | Settings.IsTitleShowEnabled) {
                    if (Settings.AttributesRule == RhesisConfig.AttributesDisplayRule.Sametime) {
                        SubLabel.IsVisible =  true;
                        _subLabelAnimator.Update(subObj,Settings.IsAnimationEnabled,Settings.IsSwapAnimationEnabled);
                    } else {
                        SubLabel.IsVisible = false;
                        if (Math.Abs(Settings.AttributesShowingInterval - Settings.UpdateTimeGapSeconds) < 0.5) {
                            _mainLabelAnimator.Update(subObj,Settings.IsAnimationEnabled,false);
                            return;
                        }
                        new Thread(() => {
                            Thread.Sleep((int)((Settings.UpdateTimeGapSeconds - Settings.AttributesShowingInterval) * 1000));
                            _mainLabelAnimator.Update(subObj,Settings.IsAnimationEnabled,false);
                        }).Start();
                    }
                } else {
                    SubLabel.IsVisible =  false;
                }
            });
        } finally {
            _isUpdating = false;
        }
    }

    async Task<RhesisData> FetchFilteredQuoteAsync() {
        int maxRetries = 50;
        for (int i = 0; i < maxRetries; i++) {
            RhesisData data = _rhesisHandler.LegacyGet(Settings.DataSource,
                Settings.HitokotoProp switch {
                    "" => "https://v1.hitokoto.cn/",
                    _ => $"https://v1.hitokoto.cn/?{Settings.HitokotoLengthArgs}{Settings.HitokotoProp}"
                },
                Settings.SainticProp switch {
                    "" => "https://hub.saintic.com/openservice/sentence/all.json",
                    _ => $"https://hub.saintic.com/openservice/sentence/{Settings.SainticProp}.json"
                },
                Settings.LengthLimitation);

            if (Settings.IgnoreListString.Split("\r\n")
                .Any(keyWord => data.Content.Contains(keyWord) && keyWord != ""))
                continue;

            if (Settings.AiFilterEnabled && !string.IsNullOrWhiteSpace(Settings.AiFilterApiKey)) {
                bool approved = await _aiFilter.CheckQuoteAsync(
                    Settings.AiFilterEndpoint,
                    Settings.AiFilterApiKey,
                    Settings.AiFilterModel,
                    Settings.AiFilterInstructions,
                    Settings.AiFilterApiType,
                    data.Content,
                    data.Author,
                    data.Title,
                    Settings.AiFilterEnableWebSearch,
                    Settings.AiFilterDeepThinking);
                if (!approved) continue;
            }

            return data;
        }

        return new RhesisData { Content = "无法获取符合条件的句子" };
    }
}
