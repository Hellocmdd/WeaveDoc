using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.App.ViewModels;

/// <summary>
/// BibTeX 文献库视图模型：导入 .bib、列出/搜索/删除条目、标记缺字段、补充著录项。
/// 沿用 RagTabViewModel 风格（手写 INotifyPropertyChanged + 公共方法供 code-behind 调用）。
/// 插入引用通过 <see cref="CitationInsertRequested"/> 事件向宿主（MainWindow）发起。
/// </summary>
public sealed class LiteratureViewModel : INotifyPropertyChanged
{
    private readonly ILiteratureRepository _repository;
    private readonly BibtexParser _parser = new();
    private string _lastImportedFile = string.Empty;
    private string _searchQuery = string.Empty;
    private string _statusText = "尚未导入文献";
    private bool _isBusy;

    public LiteratureViewModel(ILiteratureRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>当前显示的文献条目（可能是搜索过滤后的子集）。</summary>
    public ObservableCollection<LiteratureEntryViewModel> Entries { get; } = [];

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                IsBusyChanged?.Invoke();
            }
        }
    }

    /// <summary>条目总数（含未在当前过滤视图中的）。</summary>
    public int TotalCount { get; private set; }

    /// <summary>请求宿主向编辑器插入 [@key]。宿主（MainWindow）订阅。</summary>
    public event Action<string>? CitationInsertRequested;

    /// <summary>IsBusy 翻转时触发（供测试观测）。</summary>
    public event Action? IsBusyChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>从 .bib 文件路径导入（读取文本 → 解析 → 入库 → 刷新列表）。</summary>
    public async Task ImportBibAsync(string bibFilePath, CancellationToken ct = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var content = await File.ReadAllTextAsync(bibFilePath, ct);
            var entries = _parser.Parse(content);
            var sourceFile = Path.GetFileName(bibFilePath);
            await _repository.ImportAsync(entries, sourceFile, ct);
            _lastImportedFile = bibFilePath;
            await PopulateAsync(ct);
            StatusText = $"已导入 {entries.Count} 条文献（来源：{sourceFile}）";
        }
        catch (Exception ex)
        {
            StatusText = $"导入失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>从 .bib 文本直接导入（测试与重导入复用）。</summary>
    public async Task ImportBibTextAsync(string bibContent, string sourceFile, CancellationToken ct = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var entries = _parser.Parse(bibContent);
            await _repository.ImportAsync(entries, sourceFile, ct);
            await PopulateAsync(ct);
            StatusText = $"已导入 {entries.Count} 条文献（来源：{sourceFile}）";
        }
        catch (Exception ex)
        {
            StatusText = $"导入失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>从仓库重新加载全部条目（清空搜索）。</summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await PopulateAsync(ct);
            StatusText = $"文献库共 {TotalCount} 条";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SearchAsync(string query, CancellationToken ct = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _repository.InitializeAsync();
            var trimmed = (query ?? string.Empty).Trim();
            var results = string.IsNullOrEmpty(trimmed)
                ? await _repository.GetAllAsync(ct)
                : await _repository.FindAsync(trimmed, ct);
            TotalCount = results.Count;
            Entries.Clear();
            foreach (var e in results)
                Entries.Add(ToViewModel(e));
            StatusText = string.IsNullOrEmpty(trimmed)
                ? $"文献库共 {results.Count} 条"
                : $"搜索「{trimmed}」：{results.Count} 条";
        }
        catch (Exception ex)
        {
            StatusText = $"搜索失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteAsync(string citationKey, CancellationToken ct = default)
    {
        if (IsBusy || string.IsNullOrEmpty(citationKey)) return;
        IsBusy = true;
        try
        {
            await _repository.DeleteAsync(citationKey, ct);
            await PopulateAsync(ct);
            StatusText = $"已删除 {citationKey}";
        }
        catch (Exception ex)
        {
            StatusText = $"删除失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UpdateFieldAsync(string citationKey, string fieldName, string value, CancellationToken ct = default)
    {
        if (IsBusy || string.IsNullOrEmpty(citationKey)) return;
        IsBusy = true;
        try
        {
            await _repository.UpdateFieldAsync(citationKey, fieldName, value, ct);
            await PopulateAsync(ct);
            StatusText = $"已更新 {citationKey}.{fieldName}";
        }
        catch (Exception ex)
        {
            StatusText = $"更新失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>从仓库加载全部条目并填充列表（无 IsBusy 守卫，供已在持锁的公共方法复用）。</summary>
    private async Task PopulateAsync(CancellationToken ct = default)
    {
        await _repository.InitializeAsync();
        var all = await _repository.GetAllAsync(ct);
        TotalCount = all.Count;
        Entries.Clear();
        foreach (var e in all)
            Entries.Add(ToViewModel(e));
    }

    /// <summary>请求宿主向编辑器插入引用 [@key]。</summary>
    public void RequestInsertCitation(string citationKey)
    {
        if (!string.IsNullOrEmpty(citationKey))
            CitationInsertRequested?.Invoke(citationKey);
    }

    private static LiteratureEntryViewModel ToViewModel(LiteratureEntryRecord record)
    {
        var (required, _) = CitationFieldRules.Resolve(record.EntryType);
        var hasMissing = required.Any(f =>
            !record.Fields.ContainsKey(f) && !CitationFieldRules.HasAlternative(f, record.Fields));

        return new LiteratureEntryViewModel
        {
            CitationKey = record.CitationKey,
            EntryType = record.EntryType,
            Title = record.Title,
            Authors = record.Authors,
            Year = record.Year,
            HasMissingFields = hasMissing
        };
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>文献条目列表项（派生 HasMissingFields 供 UI 黄角标）。</summary>
public sealed class LiteratureEntryViewModel : INotifyPropertyChanged
{
    public string CitationKey { get; set; } = "";
    public string EntryType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Authors { get; set; } = "";
    public string Year { get; set; } = "";
    public bool HasMissingFields { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
