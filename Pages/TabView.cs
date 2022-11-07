namespace LiteDB.Studio.WebAssembly.Pages;

using BlazorMonaco;
using System.Reflection.PortableExecutable;

internal sealed class TabView
{
  public string Name { get; set; }
  public MonacoEditor Query { get; set; }
  public List<BsonDocument> Results { get; set; } = new();
  public string ResultsJson { get; set; }
  public string Parameters { get; set; }
  public Guid Id { get; set; }
}
