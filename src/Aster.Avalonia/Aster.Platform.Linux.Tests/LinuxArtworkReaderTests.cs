using Aster.Platform.Linux.Media;

namespace Aster.Platform.Linux.Tests;

public class LinuxArtworkReaderTests
{
    [Fact]
    public async Task OversizedDataUrlIsRejected()
    {
        var payload = new byte[5 * 1024 * 1024 + 1];
        var url = "data:image/png;base64," + Convert.ToBase64String(payload);

        Assert.Null(await LinuxArtworkReader.ReadAsync(url));
    }

    [Fact]
    public async Task SmallDataUrlIsDecoded()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var url = "data:image/png;base64," + Convert.ToBase64String(payload);

        Assert.Equal(payload, await LinuxArtworkReader.ReadAsync(url));
    }
}
