namespace LiteDB.Studio.WebAssembly.Pages;

using System.Text;
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

  private void AddTabCallback()
  {
    var item = new TabView
    {
      Name = $"{++_allTabsCount}",
      Id = Guid.NewGuid()
    };
    _tabs.Add(item);
  }

  private void CloseTabCallback(MudTabPanel panel)
  {
    var tabView = _tabs.FirstOrDefault(x => x.Id == (Guid) panel.Tag);
    if (tabView is not null)
    {
      _tabs.Remove(tabView);
    }
  }

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
    var dbStr = await file.TextAsync();
    var strm = GenerateStreamFromString(dbStr);
    _fileName = file.Name;
    _db = new LiteDatabase(strm);
    _collections = _db.GetCollectionNames().ToHashSet();
  }

  private void OnRefresh()
  {
  }

  private void OnRun()
  {
    var results = Enumerable.Range(0, 10)
      .Select(_ =>
      {
        var bval1 = new BsonValue($"sign {Guid.NewGuid().ToString()}");
        var bval2 = new BsonValue(Guid.NewGuid());
        var dict = new Dictionary<string, BsonValue>
        {
          {"sign", bval1},
          {"name", bval2}
        };
        var bdoc = new BsonDocument(dict);
        return bdoc;
      })
      .ToList();
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
    var activeTab = _tabs[_activeTabIndex];
    activeTab.Results = results;
    activeTab.ResultsJson = resultsJson;
    activeTab.Parameters = System.Text.Json.JsonSerializer.Serialize(new { }, options);
  }

  private void OnBegin()
  {
  }

  private void OnCommit()
  {
  }

  private void OnRollback()
  {
  }

  private void OnCheckpoint()
  {
  }

  #endregion

  private void OnCollectionSelected(string coll)
  {
    _selColl = coll;
  }

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

  private static Stream GenerateStreamFromString(string str)
  {
    return new MemoryStream(Encoding.ASCII.GetBytes(str));
  }
}
