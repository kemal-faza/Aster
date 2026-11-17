using Aster.Core.Models;
using Avalonia.Controls;

namespace Aster.UI.Abstractions;

public interface IEffectLayer : IDisposable
{
    bool IsAvailable { get; }
    void Attach(Window island);
    void Update(EffectLayerState state);
}
