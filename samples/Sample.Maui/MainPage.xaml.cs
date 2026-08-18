using IntelligenceKit.Core.Enums;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Core.Services;
using IntelligenceKit.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Maui
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private static IServiceProvider Services =>
            IPlatformApplication.Current!.Services;

        private async void OnTrackEventClicked(object? sender, EventArgs e)
        {
            var kit = Services.GetRequiredService<IIntelligenceKit>();

            var intelligenceEvent = new IntelligenceEvent
            {
                EventType = EventType.Log,
                Data =
                {
                    ["Message"] = "Evento de prueba desde el sample",
                    ["ClickedAt"] = DateTime.Now.ToString("O")
                }
            };

            await kit.TrackAsync(intelligenceEvent);
            await ShowQueueAsync("Evento normal encolado y enviado.");
        }

        private async void OnTrackExceptionClicked(object? sender, EventArgs e)
        {
            var kit = Services.GetRequiredService<IIntelligenceKit>();

            try
            {
                throw new InvalidOperationException("Excepción controlada de prueba");
            }
            catch (Exception ex)
            {
                await kit.TrackExceptionAsync(ex);
            }

            await ShowQueueAsync("Excepción controlada reportada.");
        }

        private async void OnShowQueueClicked(object? sender, EventArgs e)
        {
            await ShowQueueAsync("Cola consultada.");
        }

        private async void OnFlushClicked(object? sender, EventArgs e)
        {
            var uploader = Services.GetRequiredService<IEventUploader>();
            await uploader.FlushAsync();
            await ShowQueueAsync("Flush ejecutado.");
        }

        private async void OnGoSecondPageClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("SecondPage");
        }

        private void OnSetUserClicked(object? sender, EventArgs e)
        {
            var kit = Services.GetRequiredService<IIntelligenceKit>();
            kit.SetUser($"anonymous-{Random.Shared.Next(10000, 99999)}");
            kit.SetTag("tier", "premium");
            StatusLabel.Text = "Usuario y tag asociados a los próximos eventos.";
        }

        private int _breadcrumbCount;

        private void OnAddBreadcrumbClicked(object? sender, EventArgs e)
        {
            var kit = Services.GetRequiredService<IIntelligenceKit>();
            _breadcrumbCount++;
            kit.AddBreadcrumb(
                $"Tap on demo action #{_breadcrumbCount}",
                BreadcrumbCategories.User,
                data: new Dictionary<string, string> { ["count"] = _breadcrumbCount.ToString() });
            StatusLabel.Text = $"Breadcrumb #{_breadcrumbCount} agregado (viaja con el próximo evento).";
        }

        private async void OnLogClicked(object? sender, EventArgs e)
        {
            var kit = Services.GetRequiredService<IIntelligenceKit>();
            await kit.TrackLogAsync(SeverityLevel.Warning, "Token de pago vacío",
                new Dictionary<string, string> { ["service"] = "PaymentService" });
            await ShowQueueAsync("Log (warning) registrado.");
        }

        private void OnCrashClicked(object? sender, EventArgs e)
        {
            // Unhandled on purpose: exercises the crash reporter. The event is
            // persisted to SQLite before the process dies and uploaded on the
            // next launch.
            throw new Exception("Crash generado desde botón de prueba");
        }

        private async Task ShowQueueAsync(string action)
        {
            var store = Services.GetRequiredService<IEventStore>();
            var pending = await store.CountAsync();
            StatusLabel.Text = $"{action} Pendientes en cola: {pending}.";
        }
    }
}
