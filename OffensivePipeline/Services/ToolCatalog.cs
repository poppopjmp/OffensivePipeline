using OffensivePipeline.Ui;

namespace OffensivePipeline.Services;

/// <summary>Implements the <c>list</c> verb.</summary>
internal sealed class ToolCatalog(IConsoleUi ui, YmlHelpers ymlHelpers)
{
    public void List()
    {
        List<ToolConfig> tools = ymlHelpers.ReadYmls();
        int index = 1;
        foreach (ToolConfig tool in tools)
        {
            ui.Heading($"[{index}/{tools.Count}] {tool.Name} - <{tool.Language}>:");
            ui.Plain($"\t> Description: {tool.Description}");
            ui.Plain($"\t> Link: {tool.GitLink}");
            ui.Phase($"\t> Plugins: {string.Join(", ", tool.Plugins)}");
            index++;
        }
    }
}
