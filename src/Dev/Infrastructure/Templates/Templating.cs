namespace Dev.Infrastructure.Templates;

using System.Collections.Concurrent;
using Scriban;

public class Templating
{
    private ConcurrentDictionary<string, Template> _templates = new();
    private object _lock = new();

    public string Render(string filePath, object? model = null)
    {
        if (!_templates.TryGetValue(filePath, out var template))
        {
            lock (_lock)
            {
                var path = Path.Combine(FolderHelpers.BaseDirectory, filePath);
                if (!File.Exists(path)) throw new Exception($"cannot find template {filePath}");
                var contents = File.ReadAllText(path);
                template = Template.Parse(contents);
                _templates.TryAdd(filePath, template);
            }
        }

        return template.Render(model: model);
    }
    
}