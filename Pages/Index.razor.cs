namespace LiteDB.Studio.WebAssembly.Pages;

using System.Text;
using KristofferStrube.Blazor.FileSystemAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed partial class Index
{
  [Inject]
  private FileSystemAccessService _fileSysSvc { get; set; }

  private LiteDatabase _db;
  private string _info = string.Empty;

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
    _db = new LiteDatabase(strm);

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

  private static Stream GenerateStreamFromString(string str)
  {
    return new MemoryStream(Encoding.ASCII.GetBytes(str));
  }
}
