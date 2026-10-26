using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Views;
using Android.Widget;

namespace _6051.Platforms;

[BroadcastReceiver(Label = "Mis Fotos", Exported = true)]
[IntentFilter(new string[] {
    AppWidgetManager.ActionAppwidgetUpdate,
    ActionNextPhoto,
    ActionPrevPhoto
})]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/my_widget_provider")]
public class MyAppWidgetProvider : AppWidgetProvider
{
    public const string ActionNextPhoto = "com.companyname._6051.ACTION_NEXT_PHOTO";
    public const string ActionPrevPhoto = "com.companyname._6051.ACTION_PREV_PHOTO";

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null)
            return;

        // Si se actualiza por el temporizador automático del sistema, avanzamos a la siguiente foto
        var photos = PhotoWidgetStorage.GetPhotoPaths();
        if (photos.Count > 1)
        {
            PhotoWidgetStorage.MoveNextPhoto();
        }

        foreach (var widgetId in appWidgetIds)
        {
            UpdateWidget(context, appWidgetManager, widgetId);
        }
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);

        if (context == null || intent == null)
            return;

        if (intent.Action == ActionNextPhoto)
        {
            PhotoWidgetStorage.MoveNextPhoto();
            UpdateAllWidgets(context);
        }
        else if (intent.Action == ActionPrevPhoto)
        {
            PhotoWidgetStorage.MovePreviousPhoto();
            UpdateAllWidgets(context);
        }
    }

    public static void UpdateAllWidgets(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context);
        if (manager == null)
            return;

        var componentName = new ComponentName(context, Java.Lang.Class.FromType(typeof(MyAppWidgetProvider)));
        var widgetIds = manager.GetAppWidgetIds(componentName);
        if (widgetIds != null && widgetIds.Length > 0)
        {
            foreach (var id in widgetIds)
            {
                UpdateWidget(context, manager, id);
            }
        }
    }

    private static void UpdateWidget(Context context, AppWidgetManager appWidgetManager, int appWidgetId)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_photo);
        var photos = PhotoWidgetStorage.GetPhotoPaths();
        var currentPhoto = PhotoWidgetStorage.GetCurrentPhotoPath();
        var currentIndex = PhotoWidgetStorage.GetCurrentIndex();

        var pendingFlags = PendingIntentFlags.UpdateCurrent;
        if ( OperatingSystem.IsAndroidVersionAtLeast(23) )
        {
            pendingFlags |= PendingIntentFlags.Immutable;
        }

        // Intent para abrir la app al tocar la imagen o el texto vacío
        var openAppIntent = new Intent(context, typeof(MainActivity));
        openAppIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var openAppPendingIntent = PendingIntent.GetActivity(
            context,
            0,
            openAppIntent,
            pendingFlags);

        views.SetOnClickPendingIntent(Resource.Id.widget_empty_text, openAppPendingIntent);

        // Intent para botón Siguiente (o tocar la foto para cambiar a la siguiente)
        var nextIntent = new Intent(context, typeof(MyAppWidgetProvider));
        nextIntent.SetAction(ActionNextPhoto);
        var nextPendingIntent = PendingIntent.GetBroadcast(
            context,
            1,
            nextIntent,
            pendingFlags);

        // Intent para botón Anterior
        var prevIntent = new Intent(context, typeof(MyAppWidgetProvider));
        prevIntent.SetAction(ActionPrevPhoto);
        var prevPendingIntent = PendingIntent.GetBroadcast(
            context,
            2,
            prevIntent,
            pendingFlags);

        views.SetOnClickPendingIntent(Resource.Id.btn_next, nextPendingIntent);
        views.SetOnClickPendingIntent(Resource.Id.btn_prev, prevPendingIntent);

        if (!string.IsNullOrEmpty(currentPhoto) && File.Exists(currentPhoto))
        {
            using var bitmap = DecodeSampledBitmap(currentPhoto, 800, 800);
            if (bitmap != null)
            {
                views.SetImageViewBitmap(Resource.Id.widget_image, bitmap);
                views.SetViewVisibility(Resource.Id.widget_empty_text, ViewStates.Gone);
                views.SetViewVisibility(Resource.Id.widget_controls, ViewStates.Visible);
                views.SetTextViewText(Resource.Id.widget_counter, $"{currentIndex + 1} / {photos.Count}");

                // Al tocar la imagen también pasa a la siguiente foto (si hay varias) o abre la app
                views.SetOnClickPendingIntent(
                    Resource.Id.widget_image,
                    photos.Count > 1 ? nextPendingIntent : openAppPendingIntent);
            }
            else
            {
                ShowEmptyState(views, openAppPendingIntent);
            }
        }
        else
        {
            ShowEmptyState(views, openAppPendingIntent);
        }

        appWidgetManager.UpdateAppWidget(appWidgetId, views);
    }

    private static void ShowEmptyState(RemoteViews views, PendingIntent? openAppPendingIntent)
    {
        views.SetImageViewBitmap(Resource.Id.widget_image, null);
        views.SetViewVisibility(Resource.Id.widget_empty_text, ViewStates.Visible);
        views.SetViewVisibility(Resource.Id.widget_controls, ViewStates.Gone);
        views.SetOnClickPendingIntent(Resource.Id.widget_image, openAppPendingIntent);
    }

    private static Bitmap? DecodeSampledBitmap(string filePath, int reqWidth, int reqHeight)
    {
        try
        {
            var options = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(filePath, options);

            options.InSampleSize = CalculateInSampleSize(options, reqWidth, reqHeight);
            options.InJustDecodeBounds = false;

            var bitmap = BitmapFactory.DecodeFile(filePath, options);
            if (bitmap == null)
                return null;

            // Respetar la rotación EXIF de las fotos tomadas con la cámara
            var exif = new ExifInterface(filePath);
            int orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, (int)Android.Media.Orientation.Normal);
            int rotationDegrees = orientation switch
            {
                (int)Android.Media.Orientation.Rotate90 => 90,
                (int)Android.Media.Orientation.Rotate180 => 180,
                (int)Android.Media.Orientation.Rotate270 => 270,
                _ => 0
            };

            if (rotationDegrees != 0)
            {
                using var matrix = new Matrix();
                matrix.PostRotate(rotationDegrees);
                var rotated = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
                if (rotated != bitmap)
                {
                    bitmap.Recycle();
                }
                return rotated;
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static int CalculateInSampleSize(BitmapFactory.Options options, int reqWidth, int reqHeight)
    {
        int height = options.OutHeight;
        int width = options.OutWidth;
        int inSampleSize = 1;

        if (height > reqHeight || width > reqWidth)
        {
            int halfHeight = height / 2;
            int halfWidth = width / 2;

            while ((halfHeight / inSampleSize) >= reqHeight && (halfWidth / inSampleSize) >= reqWidth)
            {
                inSampleSize *= 2;
            }
        }

        return inSampleSize;
    }
}
