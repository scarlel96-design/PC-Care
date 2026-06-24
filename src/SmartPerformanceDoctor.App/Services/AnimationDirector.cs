using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace SmartPerformanceDoctor.App.Services;

public sealed class AnimationDirector
{
    public void ApplyCardHover(UIElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(0.5f, 0.5f, 0);
        // 실제 구현 단계에서 PointerEntered/Exited 이벤트와 Composition implicit animation을 연결한다.
    }
}
