using System.Collections.ObjectModel;

namespace _6051;

public class WidgetItem
{
    public int WidgetId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public override string ToString() => DisplayName;
}

public class ShapeOption
{
    public WidgetShape Shape { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public override string ToString() => DisplayName;
}

public partial class MainPage : ContentPage
{
    public ObservableCollection<string> Photos { get; } = new();
    private List<WidgetItem> _widgetItems = new();
    private readonly List<ShapeOption> _shapeOptions = new()
    {
        new ShapeOption { Shape = WidgetShape.FullRectangle, DisplayName = "Completo" },
        new ShapeOption { Shape = WidgetShape.RoundedRect, DisplayName = "Cuadro" },
        new ShapeOption { Shape = WidgetShape.Circle, DisplayName = "Círculo" },
        new ShapeOption { Shape = WidgetShape.Clover, DisplayName = "Trébol" },
        new ShapeOption { Shape = WidgetShape.Scallop, DisplayName = "Sello" },
        new ShapeOption { Shape = WidgetShape.Pill, DisplayName = "Cápsula" },
        new ShapeOption { Shape = WidgetShape.Flower, DisplayName = "Flor" },
        new ShapeOption { Shape = WidgetShape.Heart, DisplayName = "Corazón" },
        new ShapeOption { Shape = WidgetShape.Star, DisplayName = "Estrella" },
        new ShapeOption { Shape = WidgetShape.Hexagon, DisplayName = "Hexágono" }
    };

    private int? _selectedWidgetId;
    private bool _isUpdatingShapePicker;

    public MainPage()
    {
        InitializeComponent();
        PhotosCollectionView.ItemsSource = Photos;
        ShapePicker.ItemsSource = _shapeOptions;
        PhotoWidgetStorage.WidgetSelectedFromIntent += OnWidgetSelectedFromIntent;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        int? preferredWidgetId = null;
#if ANDROID
        if (MainActivity.LastTappedWidgetId != Android.Appwidget.AppWidgetManager.InvalidAppwidgetId)
        {
            preferredWidgetId = MainActivity.LastTappedWidgetId;
            MainActivity.LastTappedWidgetId = Android.Appwidget.AppWidgetManager.InvalidAppwidgetId;
        }
#endif
        RefreshActiveWidgets(preferredWidgetId ?? _selectedWidgetId);
    }

    private void OnWidgetSelectedFromIntent(int widgetId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshActiveWidgets(widgetId);
        });
    }

    private void OnRefreshWidgetsClicked(object? sender, EventArgs e)
    {
        RefreshActiveWidgets(_selectedWidgetId);
    }

    private void RefreshActiveWidgets(int? preferredWidgetId = null)
    {
        var ids = PhotoWidgetStorage.GetActiveWidgetIds();
        _widgetItems = ids.Select((id, idx) => new WidgetItem
        {
            WidgetId = id,
            DisplayName = $"Widget #{idx + 1} (ID: {id})"
        }).ToList();

        WidgetPicker.ItemsSource = _widgetItems;

        if (_widgetItems.Count == 0)
        {
            _selectedWidgetId = null;
            Photos.Clear();
            AddPhotosBtn.IsEnabled = false;
            ClearPhotosBtn.IsEnabled = false;
            ShapePicker.IsEnabled = false;
            EmptyTitleLabel.Text = "No tienes widgets activos";
            EmptySubtitleLabel.Text = "Ve a tu pantalla de inicio en Android, mantén presionado un espacio vacío, entra a 'Widgets' -> '6051 (Mis Fotos)' y coloca todos los widgets que quieras. Luego toca ↻ aquí.";
            return;
        }

        AddPhotosBtn.IsEnabled = true;
        ClearPhotosBtn.IsEnabled = true;
        ShapePicker.IsEnabled = true;
        EmptyTitleLabel.Text = "Este widget no tiene fotos todavía";
        EmptySubtitleLabel.Text = "Toca '+ Agregar fotos' para asignarle sus propias imágenes.";

        var targetItem = preferredWidgetId.HasValue
            ? _widgetItems.FirstOrDefault(w => w.WidgetId == preferredWidgetId.Value)
            : null;

        targetItem ??= _widgetItems.First();

        WidgetPicker.SelectedItem = targetItem;
        _selectedWidgetId = targetItem.WidgetId;
        LoadSelectedWidgetSettings();
    }

    private void OnWidgetPickerChanged(object? sender, EventArgs e)
    {
        if (WidgetPicker.SelectedItem is WidgetItem item)
        {
            _selectedWidgetId = item.WidgetId;
            LoadSelectedWidgetSettings();
        }
    }

    private void LoadSelectedWidgetSettings()
    {
        Photos.Clear();
        if (_selectedWidgetId == null)
            return;

        int widgetId = _selectedWidgetId.Value;

        // Cargar la forma actual de este widget
        _isUpdatingShapePicker = true;
        var currentShape = PhotoWidgetStorage.GetWidgetShape(widgetId);
        ShapePicker.SelectedItem = _shapeOptions.FirstOrDefault(s => s.Shape == currentShape) ?? _shapeOptions[0];
        _isUpdatingShapePicker = false;

        // Cargar las fotos de este widget
        foreach (var path in PhotoWidgetStorage.GetPhotoPaths(widgetId))
        {
            Photos.Add(path);
        }
    }

    private void OnShapePickerChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingShapePicker || _selectedWidgetId == null)
            return;

        if (ShapePicker.SelectedItem is ShapeOption option)
        {
            int widgetId = _selectedWidgetId.Value;
            PhotoWidgetStorage.SetWidgetShape(widgetId, option.Shape);
            PhotoWidgetStorage.NotifyWidgetUpdate(widgetId);
        }
    }

    private async void OnAddPhotosClicked(object? sender, EventArgs e)
    {
        if (_selectedWidgetId == null)
        {
            await DisplayAlertAsync("Sin widget", "Primero agrega un widget a tu pantalla de inicio.", "OK");
            return;
        }

        int widgetId = _selectedWidgetId.Value;

        try
        {
            var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Selecciona fotos para este widget"
            });

            if (results == null)
                return;

            var widgetDir = PhotoWidgetStorage.GetWidgetPhotosDirectory(widgetId);
            Directory.CreateDirectory(widgetDir);

            var currentList = PhotoWidgetStorage.GetPhotoPaths(widgetId);
            bool addedAny = false;

            foreach (var file in results)
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext))
                    ext = ".jpg";

                var destFileName = $"photo_{Guid.NewGuid():N}{ext}";
                var destPath = Path.Combine(widgetDir, destFileName);

                using (var sourceStream = await file.OpenReadAsync())
                using (var destStream = File.Create(destPath))
                {
                    await sourceStream.CopyToAsync(destStream);
                }

                currentList.Add(destPath);
                Photos.Add(destPath);
                addedAny = true;
            }

            if (addedAny)
            {
                PhotoWidgetStorage.SavePhotoPaths(widgetId, currentList);
                PhotoWidgetStorage.SetCurrentIndex(widgetId, currentList.Count - 1);
                PhotoWidgetStorage.NotifyWidgetUpdate(widgetId);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudieron cargar las fotos: {ex.Message}", "OK");
        }
    }

    private void OnRemovePhotoClicked(object? sender, EventArgs e)
    {
        if (_selectedWidgetId == null)
            return;

        int widgetId = _selectedWidgetId.Value;

        if (sender is Button btn && btn.CommandParameter is string path)
        {
            Photos.Remove(path);
            var list = PhotoWidgetStorage.GetPhotoPaths(widgetId);
            list.Remove(path);
            PhotoWidgetStorage.SavePhotoPaths(widgetId, list);

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ignorar si el archivo está en uso
            }

            PhotoWidgetStorage.NotifyWidgetUpdate(widgetId);
        }
    }

    private async void OnClearPhotosClicked(object? sender, EventArgs e)
    {
        if (_selectedWidgetId == null || Photos.Count == 0)
            return;

        int widgetId = _selectedWidgetId.Value;

        bool confirm = await DisplayAlertAsync("Limpiar fotos", "¿Quieres quitar todas las fotos de este widget?", "Sí", "Cancelar");
        if (!confirm)
            return;

        foreach (var path in Photos.ToList())
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        Photos.Clear();
        PhotoWidgetStorage.SavePhotoPaths(widgetId, new List<string>());
        PhotoWidgetStorage.SetCurrentIndex(widgetId, 0);
        PhotoWidgetStorage.NotifyWidgetUpdate(widgetId);
    }
}
