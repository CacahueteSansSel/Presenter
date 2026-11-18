using Avalonia.Media.Imaging;

namespace Presenter.Views;

internal sealed class SlideImages(Dictionary<string, byte[]> data) : IDisposable
{
    private readonly Dictionary<string, Bitmap?> _bitmaps = new(StringComparer.Ordinal);

    public Dictionary<string, byte[]> Data { get; } = data;

    public void Add(string id, byte[] bytes, Bitmap bitmap)
    {
        Data.Add(id, bytes);
        _bitmaps.Add(id, bitmap);
    }

    public Bitmap? Resolve(string? id)
    {
        if (id == null)
            return null;

        if (_bitmaps.TryGetValue(id, out Bitmap? bitmap))
            return bitmap;

        if (!Data.TryGetValue(id, out byte[]? bytes) || bytes == null)
            return null;

        try
        {
            using MemoryStream stream = new(bytes, writable: false);
            bitmap = new Bitmap(stream);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceWarning($"Image {id} : {exception.Message}");
        }

        _bitmaps[id] = bitmap;

        return bitmap;
    }

    public void Dispose()
    {
        foreach (Bitmap? bitmap in _bitmaps.Values)
            bitmap?.Dispose();

        _bitmaps.Clear();
    }
}
