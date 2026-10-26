using System.Collections.ObjectModel;

namespace _6051;

public partial class MainPage : ContentPage
{
    public ObservableCollection<string> Photos { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        PhotosCollectionView.ItemsSource = Photos;
        LoadSavedPhotos();
    }

    private void LoadSavedPhotos()
    {
        Photos.Clear();
        foreach (var path in PhotoWidgetStorage.GetPhotoPaths())
        {
            Photos.Add(path);
        }
        UpdateStatusText();
    }

    private async void OnAddPhotosClicked(object? sender, EventArgs e)
    {
        try
        {
            var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Selecciona fotos para tu widget"
            });

            if (results == null)
                return;

            Directory.CreateDirectory(PhotoWidgetStorage.PhotosDirectory);
            var currentList = PhotoWidgetStorage.GetPhotoPaths();
            bool addedAny = false;

            foreach (var file in results)
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext))
                    ext = ".jpg";

                var destFileName = $"photo_{Guid.NewGuid():N}{ext}";
                var destPath = Path.Combine(PhotoWidgetStorage.PhotosDirectory, destFileName);

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
                PhotoWidgetStorage.SavePhotoPaths(currentList);
                PhotoWidgetStorage.SetCurrentIndex(currentList.Count - 1);
                PhotoWidgetStorage.NotifyWidgetUpdate();
                UpdateStatusText();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudieron cargar las fotos: {ex.Message}", "OK");
        }
    }

    private void OnSelectPhotoForWidgetClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string path)
        {
            var list = PhotoWidgetStorage.GetPhotoPaths();
            var index = list.IndexOf(path);
            if (index >= 0)
            {
                PhotoWidgetStorage.SetCurrentIndex(index);
                PhotoWidgetStorage.NotifyWidgetUpdate();
                UpdateStatusText($"Mostrando foto #{index + 1} en el widget.");
            }
        }
    }

    private void OnRemovePhotoClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string path)
        {
            Photos.Remove(path);
            var list = PhotoWidgetStorage.GetPhotoPaths();
            list.Remove(path);
            PhotoWidgetStorage.SavePhotoPaths(list);

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ignorar si el archivo está en uso
            }

            PhotoWidgetStorage.NotifyWidgetUpdate();
            UpdateStatusText();
        }
    }

    private async void OnClearPhotosClicked(object? sender, EventArgs e)
    {
        if (Photos.Count == 0)
            return;

        bool confirm = await DisplayAlertAsync("Limpiar fotos", "¿Quieres quitar todas las fotos del widget?", "Sí", "Cancelar");
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
        PhotoWidgetStorage.SavePhotoPaths(new List<string>());
        PhotoWidgetStorage.SetCurrentIndex(0);
        PhotoWidgetStorage.NotifyWidgetUpdate();
        UpdateStatusText();
    }

    private void UpdateStatusText(string? customMessage = null)
    {
        if (!string.IsNullOrEmpty(customMessage))
        {
            StatusLabel.Text = customMessage;
            return;
        }

        StatusLabel.Text = Photos.Count == 0
            ? "Elige las fotos que quieres mostrar en tu pantalla principal."
            : $"{Photos.Count} foto(s) lista(s). Puedes cambiar de foto desde el widget con ◀ ▶ o tocando la imagen.";
    }
}
