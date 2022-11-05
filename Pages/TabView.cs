namespace LiteDB.Studio.WebAssembly.Pages;

using BlazorMonaco;

internal class TabView
{
  public string Name { get; set; }
  public MonacoEditor Query { get; set; }
  public List<BsonDocument> Results { get; set; } = new();
  public Guid Id { get; set; }
}
