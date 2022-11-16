namespace LiteDB.Studio.WebAssembly.Pages;

using System.Text;
using System.Text.Json;
using BlazorMonaco;
using KristofferStrube.Blazor.FileSystemAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

public sealed partial class Index
{
  [Inject]
  private FileSystemAccessService _fileSysSvc { get; set; }

  [Inject]
  private IJSRuntime JSRuntime { get; set; }

  [Inject]
  private IDialogService _dlgSvc { get; set; }

  private LiteDatabase _db;
  private MemoryStream _strm;

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
    catch (JSException)
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

    _strm = new MemoryStream(data);
    _fileName = file.Name;
    _db = new LiteDatabase(_strm);
    _collections = _db.GetCollectionNames().ToHashSet();

    if (_tabs.Count == 0)
    {
      AddTab();
    }
  }

  private async Task OnDownload()
  {
    // Rebuild will flush changes
    _db.Rebuild();
    await JSRuntime.InvokeVoidAsync("BlazorDownloadFile", _fileName, "application/octet-stream", _strm.ToArray());
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
    var results = GetResults(sql, MaxResults);
    var options = new JsonSerializerOptions
    {
      WriteIndented = true
    };
    var resultsJson = GetResultsJson(results, options);

    activeTab.Results = results;
    activeTab.ResultsJson = resultsJson;
    activeTab.Parameters = System.Text.Json.JsonSerializer.Serialize(new { }, options);

    OnRefresh();
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

  private void OnDebug()
  {
    var page = _db.GetCollection($"$dump(0)").Query().FirstOrDefault();
    var dump = new HtmlPageDump(page);
    var body = dump.Render();
    var parameters = new DialogParameters
    {
      { nameof(Debug.Body), body }
    };

    var options = new DialogOptions
    {
      FullScreen = true,
      CloseOnEscapeKey = true,
      CloseButton = true,
      NoHeader = true
    };

    _dlgSvc.Show<Debug>("Debug", parameters, options);
  }

  #endregion

  private async Task UpdateQuery(string query)
  {
    var activeTab = _tabs[_activeTabIndex];
    await activeTab.Query.SetValue(query);
  }

  #region database context menus

  private async Task OnDatabaseInfo()
  {
    var sql = "SELECT $ FROM $database;";
    await UpdateQuery(sql);
  }

  private async Task OnImport()
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
    catch (JSException)
    {
      // Handle Exception or cancellation of File Access prompt
    }

    if (fileHandle is null)
    {
      return;
    }

    var file = await fileHandle.GetFileAsync();
    var json = await file.TextAsync();
    var docs = LiteDB.JsonSerializer.DeserializeArray(json).Select(x => x.AsDocument);
    var collName = Path.GetFileNameWithoutExtension(file.Name);
    var coll = _db.GetCollection(collName);
    coll.InsertBulk(docs);

    OnRefresh();
  }

  private async Task OnRebuild()
  {
    // encryption not supported on wasm
    var sql = "REBUILD { collation: 'en-US/IgnoreCase' };";
    await UpdateQuery(sql);
  }

  #endregion

  #region collection context menus

  private async Task OnQuery()
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
    var sql = $"SELECT $ FROM {_selColl};";
    var results = GetResults(sql, int.MaxValue);
    var options = new JsonSerializerOptions
    {
      WriteIndented = true
    };
    var resultsJson = GetResultsJson(results, options);
    var data = Encoding.UTF8.GetBytes(resultsJson);
    await JSRuntime.InvokeVoidAsync("BlazorDownloadFile", $"{_selColl}.json", "application/json", data);
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

  #region SQL execution

  private static string GetResultsJson(IEnumerable<BsonDocument> results, JsonSerializerOptions options)
  {
    var resultsDicStr = results
      .Select(bdoc =>
        bdoc.ToDictionary(
          bd => bd.Key,
          bd => bd.Value.ToString()));
    var resultsJson = JsonSerializer.Serialize(resultsDicStr, options);
    return resultsJson;
  }

  private List<BsonDocument> GetResults(string sql, int maxResults)
  {
    var results = new List<BsonDocument>();
    using var reader = _db.Execute(sql, new BsonDocument());
    while (reader.Read())
    {
      var val = reader.Current;
      if (val.IsDocument)
      {
        results.Add(val.AsDocument);
      }

      if (results.Count >= maxResults)
      {
        break;
      }
    }

    return results;
  }

  #endregion
}
