using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Views;
using Android.Widget;
using APath = Android.Graphics.Path;
using ARectF = Android.Graphics.RectF;
using AMatrix = Android.Graphics.Matrix;
using APaint = Android.Graphics.Paint;

namespace _6051.Platforms;

[BroadcastReceiver(Label = "Mis Fotos", Exported = true)]
[IntentFilter(new string[] {
    AppWidgetManager.ActionAppwidgetUpdate,
    ActionNextPhoto
})]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/my_widget_provider")]
public class MyAppWidgetProvider : AppWidgetProvider
{
    public const string ActionNextPhoto = "com.companyname._6051.ACTION_NEXT_PHOTO";

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null)
            return;

        foreach (var widgetId in appWidgetIds)
        {
            var photos = PhotoWidgetStorage.GetPhotoPaths(widgetId);
            if (photos.Count > 1)
            {
                PhotoWidgetStorage.MoveNextPhoto(widgetId);
            }

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
            int widgetId = intent.GetIntExtra(
                AppWidgetManager.ExtraAppwidgetId,
                AppWidgetManager.InvalidAppwidgetId);

            if (widgetId != AppWidgetManager.InvalidAppwidgetId)
            {
                PhotoWidgetStorage.MoveNextPhoto(widgetId);
                UpdateSingleWidget(context, widgetId);
            }
        }
    }

    public override void OnDeleted(Context? context, int[]? appWidgetIds)
    {
        base.OnDeleted(context, appWidgetIds);

        if (appWidgetIds == null)
            return;

        foreach (var widgetId in appWidgetIds)
        {
            PhotoWidgetStorage.DeleteWidgetData(widgetId);
        }
    }

    public static void UpdateSingleWidget(Context context, int widgetId)
    {
        var manager = AppWidgetManager.GetInstance(context);
        if (manager == null)
            return;

        UpdateWidget(context, manager, widgetId);
    }

    private static void UpdateWidget(Context context, AppWidgetManager appWidgetManager, int appWidgetId)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_photo);
        var photos = PhotoWidgetStorage.GetPhotoPaths(appWidgetId);
        var currentPhoto = PhotoWidgetStorage.GetCurrentPhotoPath(appWidgetId);
        var shape = PhotoWidgetStorage.GetWidgetShape(appWidgetId);

        var pendingFlags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            pendingFlags |= PendingIntentFlags.Immutable;
        }

        // Intent para abrir la app directamente en la configuración de ESTE widget
        var openAppIntent = new Intent(context, typeof(MainActivity));
        openAppIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        openAppIntent.PutExtra(AppWidgetManager.ExtraAppwidgetId, appWidgetId);
        var openAppPendingIntent = PendingIntent.GetActivity(
            context,
            appWidgetId * 10 + 1,
            openAppIntent,
            pendingFlags);

        views.SetOnClickPendingIntent(Resource.Id.widget_empty_text, openAppPendingIntent);

        // Intent para cambiar a la siguiente foto al tocar la imagen
        var nextIntent = new Intent(context, typeof(MyAppWidgetProvider));
        nextIntent.SetAction(ActionNextPhoto);
        nextIntent.PutExtra(AppWidgetManager.ExtraAppwidgetId, appWidgetId);
        var nextPendingIntent = PendingIntent.GetBroadcast(
            context,
            appWidgetId * 10 + 2,
            nextIntent,
            pendingFlags);

        if (!string.IsNullOrEmpty(currentPhoto) && File.Exists(currentPhoto))
        {
            using var rawBitmap = DecodeSampledBitmap(currentPhoto, 800, 800);
            if (rawBitmap != null)
            {
                views.SetViewVisibility(Resource.Id.widget_empty_text, ViewStates.Gone);
                var clickIntent = photos.Count > 1 ? nextPendingIntent : openAppPendingIntent;

                if (shape == WidgetShape.FullRectangle)
                {
                    // Opción original: ocupa todo el espacio del widget (centerCrop)
                    views.SetViewVisibility(Resource.Id.widget_image_shaped, ViewStates.Gone);
                    views.SetImageViewBitmap(Resource.Id.widget_image_shaped, null);

                    views.SetViewVisibility(Resource.Id.widget_image_full, ViewStates.Visible);
                    views.SetImageViewBitmap(Resource.Id.widget_image_full, rawBitmap);
                    views.SetOnClickPendingIntent(Resource.Id.widget_image_full, clickIntent);
                }
                else
                {
                    // Formas recortadas con fondo transparente
                    using var shapedBitmap = ApplyShapeMask(rawBitmap, shape, 700);
                    views.SetViewVisibility(Resource.Id.widget_image_full, ViewStates.Gone);
                    views.SetImageViewBitmap(Resource.Id.widget_image_full, null);

                    views.SetViewVisibility(Resource.Id.widget_image_shaped, ViewStates.Visible);
                    views.SetImageViewBitmap(Resource.Id.widget_image_shaped, shapedBitmap);
                    views.SetOnClickPendingIntent(Resource.Id.widget_image_shaped, clickIntent);
                }
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
        views.SetImageViewBitmap(Resource.Id.widget_image_full, null);
        views.SetImageViewBitmap(Resource.Id.widget_image_shaped, null);
        views.SetViewVisibility(Resource.Id.widget_image_full, ViewStates.Visible);
        views.SetViewVisibility(Resource.Id.widget_image_shaped, ViewStates.Gone);
        views.SetViewVisibility(Resource.Id.widget_empty_text, ViewStates.Visible);
        views.SetOnClickPendingIntent(Resource.Id.widget_image_full, openAppPendingIntent);
    }

    private static Bitmap ApplyShapeMask(Bitmap source, WidgetShape shape, int targetSize)
    {
        var output = Bitmap.CreateBitmap(targetSize, targetSize, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(output);

        using var paint = new APaint(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);
        using var shader = new BitmapShader(source, Shader.TileMode.Clamp!, Shader.TileMode.Clamp!);

        // Ajuste Center-Crop del bitmap al cuadrado targetSize x targetSize
        float scale = Math.Max((float)targetSize / source.Width, (float)targetSize / source.Height);
        float dx = (targetSize - source.Width * scale) * 0.5f;
        float dy = (targetSize - source.Height * scale) * 0.5f;

        using var shaderMatrix = new AMatrix();
        shaderMatrix.SetScale(scale, scale);
        shaderMatrix.PostTranslate(dx, dy);
        shader.SetLocalMatrix(shaderMatrix);
        paint.SetShader(shader);

        // Redondear suavemente las puntas y valles de la estrella y el hexágono
        if (shape == WidgetShape.Star)
        {
            paint.SetPathEffect(new CornerPathEffect(targetSize * 0.085f));
        }
        else if (shape == WidgetShape.Hexagon)
        {
            paint.SetPathEffect(new CornerPathEffect(targetSize * 0.07f));
        }

        using var path = CreateShapePath(shape, targetSize);
        canvas.DrawPath(path, paint);

        return output;
    }

    private static APath CreateShapePath(WidgetShape shape, int size)
    {
        var path = new APath();
        float s = size;
        float cx = s / 2f;
        float cy = s / 2f;
        float pad = s * 0.03f;

        switch (shape)
        {
            case WidgetShape.Circle:
                path.AddCircle(cx, cy, (s / 2f) - pad, APath.Direction.Cw!);
                break;

            case WidgetShape.Clover:
                // Marco de 4 esquinas abombadas con hendidura suave arriba/abajo/izq/der (arriba a la izq en la imagen)
                int cloverSteps = 260;
                float cloverBaseR = s * 0.435f;
                float cloverAmp = s * 0.055f;
                for (int i = 0; i <= cloverSteps; i++)
                {
                    double theta = (2.0 * Math.PI * i) / cloverSteps;
                    // -Cos(4 * theta) produce valles en 0, 90, 180, 270 grados y esquinas redondeadas en 45, 135, 225, 315
                    float r = cloverBaseR - cloverAmp * (float)Math.Cos(4.0 * theta);
                    float x = cx + r * (float)Math.Cos(theta);
                    float y = cy + r * (float)Math.Sin(theta);
                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);
                }
                path.Close();
                break;

            case WidgetShape.Scallop:
                // Marco circular con muchas ondas pequeñas tipo sello/galleta (arriba a la der en la imagen, 16 ondas)
                int scallopLobes = 16;
                int scallopSteps = 320;
                float scallopBaseR = s * 0.45f;
                float scallopAmp = s * 0.02f;
                for (int i = 0; i <= scallopSteps; i++)
                {
                    double theta = -Math.PI / 2.0 + (2.0 * Math.PI * i) / scallopSteps;
                    float r = scallopBaseR + scallopAmp * (float)Math.Cos(scallopLobes * (theta + Math.PI / 2.0));
                    float x = cx + r * (float)Math.Cos(theta);
                    float y = cy + r * (float)Math.Sin(theta);
                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);
                }
                path.Close();
                break;

            case WidgetShape.Pill:
                // Cápsula / Óvalo horizontal con extremos semicirculares (tercera fila de la imagen)
                float pillMarginX = pad;
                float pillMarginY = s * 0.16f;
                var pillRect = new ARectF(pillMarginX, pillMarginY, s - pillMarginX, s - pillMarginY);
                float pillRadius = pillRect.Height() / 2f;
                path.AddRoundRect(pillRect, pillRadius, pillRadius, APath.Direction.Cw!);
                break;

            case WidgetShape.Heart:
                // Corazón estilizado con dos semicírculos superiores bien definidos
                path.MoveTo(cx, s * 0.92f);

                path.CubicTo(
                    s * 0.14f, s * 0.68f,
                    s * 0.03f, s * 0.46f,
                    s * 0.05f, s * 0.29f);

                path.CubicTo(
                    s * 0.07f, s * 0.07f,
                    s * 0.39f, s * 0.05f,
                    cx, s * 0.25f);

                path.CubicTo(
                    s * 0.61f, s * 0.05f,
                    s * 0.93f, s * 0.07f,
                    s * 0.95f, s * 0.29f);

                path.CubicTo(
                    s * 0.97f, s * 0.46f,
                    s * 0.86f, s * 0.68f,
                    cx, s * 0.92f);

                path.Close();
                break;

            case WidgetShape.Star:
                int points = 5;
                float outerRadius = (s / 2f) - pad;
                float innerRadius = outerRadius * 0.50f;
                double angle = -Math.PI / 2.0; // Punta superior hacia arriba
                double step = Math.PI / points;

                for (int i = 0; i < points * 2; i++)
                {
                    float r = (i % 2 == 0) ? outerRadius : innerRadius;
                    float x = cx + (float)(r * Math.Cos(angle));
                    float y = cy + (float)(r * Math.Sin(angle));
                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);
                    angle += step;
                }
                path.Close();
                break;

            case WidgetShape.Hexagon:
                float hexRadius = (s / 2f) - pad;
                for (int i = 0; i < 6; i++)
                {
                    double a = -Math.PI / 2.0 + i * (Math.PI / 3.0);
                    float x = cx + (float)(hexRadius * Math.Cos(a));
                    float y = cy + (float)(hexRadius * Math.Sin(a));
                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);
                }
                path.Close();
                break;

            case WidgetShape.Flower:
                // Silueta idéntica al reloj "Scallop" de Material You (10 ondas suaves)
                int lobes = 10;
                int steps = 300;
                float baseR = s * 0.445f;
                float amp = s * 0.025f;
                double phase = Math.PI;
                for (int i = 0; i <= steps; i++)
                {
                    double theta = -Math.PI / 2.0 + (2.0 * Math.PI * i) / steps;
                    float r = baseR + amp * (float)Math.Cos(lobes * (theta + Math.PI / 2.0) + phase);
                    float x = cx + r * (float)Math.Cos(theta);
                    float y = cy + r * (float)Math.Sin(theta);
                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);
                }
                path.Close();
                break;

            case WidgetShape.RoundedRect:
            default:
                float cornerRadius = s * 0.18f;
                var rect = new ARectF(pad, pad, s - pad, s - pad);
                path.AddRoundRect(rect, cornerRadius, cornerRadius, APath.Direction.Cw!);
                break;
        }

        return path;
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
                using var matrix = new AMatrix();
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
