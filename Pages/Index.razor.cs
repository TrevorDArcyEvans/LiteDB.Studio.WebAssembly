namespace LiteDB.Studio.WebAssembly.Pages;

using BlazorMonaco;
using KristofferStrube.Blazor.FileSystemAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

public sealed partial class Index
{
  [Inject]
  private FileSystemAccessService _fileSysSvc { get; set; }

  private LiteDatabase _db;

  private List<TabView> _tabs = new();
  private int _allTabsCount;
  private int _activeTabIndex;

  private string _fileName;
  private HashSet<string> _collections = new();
  private string _selColl;
  private bool _enableCollMenu => _tabs.Count > 0;
  private bool _disableMainMenu => _db is null;

  private static StandaloneEditorConstructionOptions EditorOptions(string language, bool readOnly = false, string text = "")
  {
    return new StandaloneEditorConstructionOptions
    {
      Language = language,
      AutomaticLayout = true,
      RenderIndentGuides = false,
      RenderFinalNewline = false,
      ColorDecorators = true,
      OccurrencesHighlight = true,
      ReadOnly = readOnly,
      Value = text
    };
  }

  private static StandaloneEditorConstructionOptions SQL_EditorOptions(MonacoEditor editor)
  {
    return EditorOptions("sql");
  }

  private void OnCollectionSelected(string coll)
  {
    _selColl = coll;
  }

  #region tab handling

  private void AddTab()
  {
    var item = new TabView
    {
      Name = $"{++_allTabsCount}",
      Id = Guid.NewGuid()
    };
    _tabs.Add(item);
  }

  private void CloseTab(MudTabPanel panel)
  {
    var tabView = _tabs.FirstOrDefault(x => x.Id == (Guid)panel.Tag);
    if (tabView is not null)
    {
      _tabs.Remove(tabView);
    }
  }

  #endregion

  #region toolbar menus

  private async Task OnOpen()
  {
    FileSystemFileHandle? fileHandle = null;
    try
    {
      OpenFilePickerOptionsStartInWellKnownDirectory options = new()
      {
        Multiple = false,
        StartIn = WellKnownDirectory.Downloads
      };
      var fileHandles = await _fileSysSvc.ShowOpenFilePickerAsync(options);
      fileHandle = fileHandles.Single();
    }
    catch (JSException ex)
    {
      // Handle Exception or cancellation of File Access prompt
    }

    if (fileHandle is null)
    {
      return;
    }

    var file = await fileHandle.GetFileAsync();

    // do not construct stream via text as this will give an incompatible stream
    var data = await file.ArrayBufferAsync();

    var strm = new MemoryStream(data);
    _fileName = file.Name;
    _db = new LiteDatabase(strm);
    _collections = _db.GetCollectionNames().ToHashSet();
  }

  private void OnRefresh()
  {
    _collections = _db.GetCollectionNames().ToHashSet();
  }

  private async Task OnRun()
  {
    const int MaxResults = 100;

    var activeTab = _tabs[_activeTabIndex];
    var sql = await activeTab.Query.GetValue();
    var results = new List<BsonDocument>();
    using var reader = _db.Execute(sql, new BsonDocument());
    while (reader.Read())
    {
      var val = reader.Current;
      if (val.IsDocument)
      {
        results.Add(val.AsDocument);
      }

      if (results.Count >= MaxResults)
      {
        break;
      }
    }

    var resultsDicStr = results
      .Select(bdoc =>
        bdoc.ToDictionary(
          bd => bd.Key,
          bd => bd.Value.ToString()));
    var options = new System.Text.Json.JsonSerializerOptions
    {
      WriteIndented = true
    };
    var resultsJson = System.Text.Json.JsonSerializer.Serialize(resultsDicStr, options);

    activeTab.Results = results;
    activeTab.ResultsJson = resultsJson;
    activeTab.Parameters = System.Text.Json.JsonSerializer.Serialize(new { }, options);

    _collections = _db.GetCollectionNames().ToHashSet();
  }

  private void OnBegin()
  {
    _ = _db.BeginTrans();
  }

  private void OnCommit()
  {
    _ = _db.Commit();
  }

  private void OnRollback()
  {
    _ = _db.Rollback();
  }

  private void OnCheckpoint()
  {
    _db.Checkpoint();
  }

  #endregion

  #region collection context menus

  private async Task UpdateQuery(string query)
  {
    var activeTab = _tabs[_activeTabIndex];
    await activeTab.Query.SetValue(query);
  }

  private async Task OnAll()
  {
    var sql = $"SELECT $ FROM {_selColl};";
    await UpdateQuery(sql);
  }

  private async Task OnCount()
  {
    var sql = $"SELECT COUNT(*) FROM {_selColl};";
    await UpdateQuery(sql);
  }

  private async Task OnExplainPlan()
  {
    var sql = $"EXPLAIN SELECT $ FROM {_selColl};";
    await UpdateQuery(sql);
  }

  private async Task OnIndexes()
  {
    var sql = $"SELECT $ FROM $indexes WHERE collection = \"{_selColl}\";";
    await UpdateQuery(sql);
  }

  private async Task OnExport()
  {
    var sql = $"SELECT ${Environment.NewLine}  INTO $file(\'C:/temp/{_selColl}.json\'){Environment.NewLine}  FROM {_selColl};";
    await UpdateQuery(sql);
  }

  private async Task OnAnalyse()
  {
    var sql = $"ANALYZE {_selColl};";
    await UpdateQuery(sql);
  }

  private async Task OnRename()
  {
    var sql = $"RENAME COLLECTION {_selColl} TO new_name;";
    await UpdateQuery(sql);
  }

  private async Task OnDrop()
  {
    var sql = $"DROP COLLECTION {_selColl};";
    await UpdateQuery(sql);
  }

  #endregion
}
