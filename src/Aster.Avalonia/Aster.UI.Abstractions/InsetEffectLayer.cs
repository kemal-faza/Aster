using Aster.Core.Models;
using Avalonia.Controls;

namespace Aster.UI.Abstractions;

public sealed class InsetEffectLayer : IEffectLayer
{
    public bool IsAvailable => false;
    public void Attach(Window island) { }
    public void Update(EffectLayerState state) { }
    public void Dispose() { }
}
