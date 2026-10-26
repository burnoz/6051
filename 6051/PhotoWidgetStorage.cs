using System.Text.Json;

namespace _6051;

public enum WidgetShape
{
    FullRectangle,
    RoundedRect,
    Circle,
    Clover,
    Scallop,
    Pill,
    Heart,
    Star,
    Hexagon,
    Flower
}

public static class PhotoWidgetStorage
{
    public static event Action<int>? WidgetSelectedFromIntent;

    public static void RaiseWidgetSelectedFromIntent(int widgetId)
    {
        WidgetSelectedFromIntent?.Invoke(widgetId);
    }

    private static string GetPhotosKey(int widgetId) => $"WidgetPhotosList_{widgetId}";
    private static string GetCurrentIndexKey(int widgetId) => $"WidgetCurrentPhotoIndex_{widgetId}";
    private static string GetShapeKey(int widgetId) => $"WidgetShape_{widgetId}";

    public static string GetWidgetPhotosDirectory(int widgetId) =>
        Path.Combine(FileSystem.AppDataDirectory, "WidgetPhotos", $"widget_{widgetId}");

    public static List<int> GetActiveWidgetIds()
    {
#if ANDROID
        var context = Android.App.Application.Context;
        var manager = Android.Appwidget.AppWidgetManager.GetInstance(context);
        if (manager != null)
        {
            var component = new Android.Content.ComponentName(
                context,
                Java.Lang.Class.FromType(typeof(_6051.Platforms.MyAppWidgetProvider)));
            var ids = manager.GetAppWidgetIds(component);
            if (ids != null && ids.Length > 0)
            {
                return ids.OrderBy(id => id).ToList();
            }
        }
#endif
        return new List<int>();
    }

    public static List<string> GetPhotoPaths(int widgetId)
    {
        var json = Preferences.Default.Get(GetPhotosKey(widgetId), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return list.Where(File.Exists).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void SavePhotoPaths(int widgetId, List<string> paths)
    {
        var json = JsonSerializer.Serialize(paths);
        Preferences.Default.Set(GetPhotosKey(widgetId), json);
    }

    public static int GetCurrentIndex(int widgetId)
    {
        return Preferences.Default.Get(GetCurrentIndexKey(widgetId), 0);
    }

    public static void SetCurrentIndex(int widgetId, int index)
    {
        Preferences.Default.Set(GetCurrentIndexKey(widgetId), index);
    }

    public static WidgetShape GetWidgetShape(int widgetId)
    {
        var raw = Preferences.Default.Get(GetShapeKey(widgetId), nameof(WidgetShape.FullRectangle));
        return Enum.TryParse<WidgetShape>(raw, out var shape) ? shape : WidgetShape.FullRectangle;
    }

    public static void SetWidgetShape(int widgetId, WidgetShape shape)
    {
        Preferences.Default.Set(GetShapeKey(widgetId), shape.ToString());
    }

    public static string? GetCurrentPhotoPath(int widgetId)
    {
        var photos = GetPhotoPaths(widgetId);
        if (photos.Count == 0)
            return null;

        var index = GetCurrentIndex(widgetId);
        if (index < 0 || index >= photos.Count)
        {
            index = 0;
            SetCurrentIndex(widgetId, 0);
        }

        return photos[index];
    }

    public static string? MoveNextPhoto(int widgetId)
    {
        var photos = GetPhotoPaths(widgetId);
        if (photos.Count == 0)
            return null;

        var nextIndex = (GetCurrentIndex(widgetId) + 1) % photos.Count;
        SetCurrentIndex(widgetId, nextIndex);
        return photos[nextIndex];
    }

    public static void DeleteWidgetData(int widgetId)
    {
        Preferences.Default.Remove(GetPhotosKey(widgetId));
        Preferences.Default.Remove(GetCurrentIndexKey(widgetId));
        Preferences.Default.Remove(GetShapeKey(widgetId));

        try
        {
            var dir = GetWidgetPhotosDirectory(widgetId);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // Ignorar errores al limpiar archivos
        }
    }

    public static void NotifyWidgetUpdate(int widgetId)
    {
#if ANDROID
        _6051.Platforms.MyAppWidgetProvider.UpdateSingleWidget(Android.App.Application.Context, widgetId);
#endif
    }
}
