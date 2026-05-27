using Microsoft.AspNetCore.Mvc;

namespace TildeCompare.ViewComponents;

public class TildeProbeViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string source, string route)
        => View(new TildeProbeModel(source, route));
}

public record TildeProbeModel(string Source, string Route);
