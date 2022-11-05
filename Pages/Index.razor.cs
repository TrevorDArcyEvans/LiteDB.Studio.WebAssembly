using System.Security.AccessControl;

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
  private string _info = string.Empty;

  private List<TabView> _tabs = new();
  private int _allTabsCount;
  private int _activeTabIndex;

  private string _fileName;
  private HashSet<string> _items = new();

  private async Task OpenAndReadFile()
  {
    _info = string.Empty;
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
      _info = ex.Message;
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
    _items = _db.GetCollectionNames().ToHashSet();

    var sb = new StringBuilder();
    sb.AppendLine($"Filename: {file.Name}");
    sb.AppendLine($"UserVersion: {_db.UserVersion}");
    sb.AppendLine($"CollectionNames:");
    foreach (var name in _db.GetCollectionNames())
    {
      sb.AppendLine($"  {name}");
    }

    sb.AppendLine($"UtcDate: {_db.UtcDate}");
    sb.AppendLine($"LimitSize: {_db.LimitSize}");
    sb.AppendLine($"CheckpointSize: {_db.CheckpointSize}");
    sb.AppendLine($"Collation: {_db.Collation}");

    _info = sb.ToString();
  }

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
    _tabs[_activeTabIndex].Results = results;
    _tabs[_activeTabIndex].ResultsJson = resultsJson;
  }

  private static Stream GenerateStreamFromString(string str)
  {
    return new MemoryStream(Encoding.ASCII.GetBytes(str));
  }
}
