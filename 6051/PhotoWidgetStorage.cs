using System.Text.Json;

namespace _6051;

public static class PhotoWidgetStorage
{
    private const string PhotosKey = "WidgetPhotosList";
    private const string CurrentIndexKey = "WidgetCurrentPhotoIndex";

    public static string PhotosDirectory =>
        Path.Combine(FileSystem.AppDataDirectory, "WidgetPhotos");

    public static List<string> GetPhotoPaths()
    {
        var json = Preferences.Default.Get(PhotosKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            // Filtrar solo los archivos que aún existen en disco
            return list.Where(File.Exists).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void SavePhotoPaths(List<string> paths)
    {
        var json = JsonSerializer.Serialize(paths);
        Preferences.Default.Set(PhotosKey, json);
    }

    public static int GetCurrentIndex()
    {
        return Preferences.Default.Get(CurrentIndexKey, 0);
    }

    public static void SetCurrentIndex(int index)
    {
        Preferences.Default.Set(CurrentIndexKey, index);
    }

    public static string? GetCurrentPhotoPath()
    {
        var photos = GetPhotoPaths();
        if (photos.Count == 0)
            return null;

        var index = GetCurrentIndex();
        if (index < 0 || index >= photos.Count)
        {
            index = 0;
            SetCurrentIndex(0);
        }

        return photos[index];
    }

    public static string? MoveNextPhoto()
    {
        var photos = GetPhotoPaths();
        if (photos.Count == 0)
            return null;

        var nextIndex = (GetCurrentIndex() + 1) % photos.Count;
        SetCurrentIndex(nextIndex);
        return photos[nextIndex];
    }

    public static string? MovePreviousPhoto()
    {
        var photos = GetPhotoPaths();
        if (photos.Count == 0)
            return null;

        var prevIndex = (GetCurrentIndex() - 1 + photos.Count) % photos.Count;
        SetCurrentIndex(prevIndex);
        return photos[prevIndex];
    }

    public static void NotifyWidgetUpdate()
    {
#if ANDROID
        _6051.Platforms.MyAppWidgetProvider.UpdateAllWidgets(Android.App.Application.Context);
#endif
    }
}
