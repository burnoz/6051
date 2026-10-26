using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace _6051
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public static int LastTappedWidgetId { get; set; } = AppWidgetManager.InvalidAppwidgetId;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            HandleWidgetIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            Intent = intent;
            HandleWidgetIntent(intent);
        }

        private static void HandleWidgetIntent(Intent? intent)
        {
            if (intent == null)
                return;

            int widgetId = intent.GetIntExtra(
                AppWidgetManager.ExtraAppwidgetId,
                AppWidgetManager.InvalidAppwidgetId);

            if (widgetId != AppWidgetManager.InvalidAppwidgetId)
            {
                LastTappedWidgetId = widgetId;
                PhotoWidgetStorage.RaiseWidgetSelectedFromIntent(widgetId);
            }
        }
    }
}
