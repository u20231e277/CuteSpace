using CuteSpace.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CuteSpace.Services;

public sealed class ClipboardHistoryService
{
    private readonly List<ClipboardEntry> _history;
    private bool _isRestoring;

    public ClipboardHistoryService(List<ClipboardEntry> initialHistory)
    {
        _history = initialHistory;
    }

    public event EventHandler<ClipboardEntry>? EntryAdded;

    public void Start()
    {
        try
        {
            Clipboard.ContentChanged += OnClipboardContentChanged;
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(ClipboardHistoryService), ex.ToString());
        }
    }

    public void Stop()
    {
        try
        {
            Clipboard.ContentChanged -= OnClipboardContentChanged;
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(ClipboardHistoryService), ex.ToString());
        }
    }

    public async Task RestoreAsync(ClipboardEntry entry)
    {
        try
        {
            _isRestoring = true;
            var package = new DataPackage();

            if (entry.Kind == "image" && File.Exists(entry.Content))
            {
                var file = await StorageFile.GetFileFromPathAsync(entry.Content);
                package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
            }
            else
            {
                package.SetText(entry.Content);
            }

            Clipboard.SetContent(package);
            Clipboard.Flush();
            await Task.Delay(80);
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(ClipboardHistoryService), ex.ToString());
        }
        finally
        {
            _isRestoring = false;
        }
    }

    private async void OnClipboardContentChanged(object? sender, object e)
    {
        if (_isRestoring)
        {
            return;
        }

        try
        {
            var view = Clipboard.GetContent();
            ClipboardEntry? entry = null;

            if (view.Contains(StandardDataFormats.Text))
            {
                var text = await view.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    entry = CreateEntry("text", text.Trim(), text);
                }
            }
            else if (view.Contains(StandardDataFormats.WebLink))
            {
                var link = (await view.GetWebLinkAsync()).ToString();
                entry = CreateEntry("link", link, link);
            }
            else if (view.Contains(StandardDataFormats.Bitmap))
            {
                var imagePath = await SaveBitmapAsync(await view.GetBitmapAsync());
                entry = CreateEntry("image", "Image copied", imagePath);
            }

            if (entry is null || _history.FirstOrDefault()?.Content == entry.Content)
            {
                return;
            }

            _history.Insert(0, entry);
            if (_history.Count > 40)
            {
                _history.RemoveRange(40, _history.Count - 40);
            }

            EntryAdded?.Invoke(this, entry);
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(ClipboardHistoryService), ex.ToString());
        }
    }

    private static ClipboardEntry CreateEntry(string kind, string preview, string content)
    {
        return new ClipboardEntry
        {
            Kind = kind,
            Preview = preview.Length > 120 ? $"{preview[..120]}..." : preview,
            Content = content,
            CreatedAt = DateTimeOffset.Now
        };
    }

    private static async Task<string> SaveBitmapAsync(RandomAccessStreamReference reference)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CuteSpace",
            "clipboard-images");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{DateTimeOffset.Now:yyyyMMddHHmmssfff}.png");
        await using var source = (await reference.OpenReadAsync()).AsStreamForRead();
        await using var target = File.Create(path);
        await source.CopyToAsync(target);
        return path;
    }
}
