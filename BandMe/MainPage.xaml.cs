using Microsoft.Azure.Devices.Client;
using Microsoft.Band;
using Microsoft.Band.Sensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Документацию по шаблону элемента "Пустая страница" см. по адресу http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BandMe
{

    public class HRReading
    {
        public int HR { get; set; }
        public string Reading { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Id { get; set; }
    }

    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        IBandClient Band = null;
        DispatcherTimer dt = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
        int HR = 80;
        Random Rnd = new Random();

        DeviceClient iothub;

        public MainPage()
        {
            this.InitializeComponent();
        }
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            iothub = DeviceClient.CreateFromConnectionString("HostName=HRHub.azure-devices.net;DeviceId=shwarsphone;SharedAccessKey=EyWYH2YyV2wveHorZziuIEGBvfEN5fcFqmLCbe51X6o=");
            await iothub.OpenAsync();

            IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
            if (pairedBands.Length > 0)
            {
                try
                {
                    Band = await BandClientManager.Instance.ConnectAsync(pairedBands[0]);
                }
                catch { Band = null; }
            }
            if (Band==null)
            {
                dt.Tick += (s, ea) =>
                {
                    HeartRateObtained(null, null);
                };
                dt.Start();
                return;
            }

            if (Band.SensorManager.HeartRate.GetCurrentUserConsent() != UserConsent.Granted)
            {  // user hasn’t consented, request consent  
                await Band.SensorManager.HeartRate.RequestUserConsentAsync();
            }
            Band.SensorManager.HeartRate.ReadingChanged += HeartRateObtained;
            // Band.SensorManager.HeartRate.ReportingInterval = TimeSpan.FromSeconds(10);
            await Band.SensorManager.HeartRate.StartReadingsAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (Band != null) Band.SensorManager.HeartRate.StopReadingsAsync();
        }

        public async Task Send(HRReading HR)
        {
            var s = Newtonsoft.Json.JsonConvert.SerializeObject(HR);
            var b = Encoding.UTF8.GetBytes(s);
            await iothub.SendEventAsync(new Message(b));
        }

        private async void HeartRateObtained(object sender, Microsoft.Band.Sensors.BandSensorReadingEventArgs<Microsoft.Band.Sensors.IBandHeartRateReading> e)
        {
            string desc;
            if (Band == null || sender == null)
            {
                desc = "Sim";
                if (HR < 70) HR += 1;
                else if (HR > 100) HR -= 1;
                else HR += Rnd.Next(-1, 2);
            }
            else
            {
                desc = (e.SensorReading.Quality == HeartRateQuality.Locked) ? "Locked" : "Acquiring";
                HR = e.SensorReading.HeartRate;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,()=>
                 txt.Text = $"{HR} ({desc})");
            await Send(new HRReading() { HR = HR, Id = "shwarsphone", Reading = desc, TimeStamp = DateTime.Now });
        }
    }
}
