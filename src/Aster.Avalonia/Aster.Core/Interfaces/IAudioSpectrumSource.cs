using Aster.Core.Models;

namespace Aster.Core.Interfaces;

public interface IAudioSpectrumSource : IDisposable
{
    bool IsAvailable { get; }
    SpectrumFrame Latest { get; }
    void Start();
    void Stop();
}
