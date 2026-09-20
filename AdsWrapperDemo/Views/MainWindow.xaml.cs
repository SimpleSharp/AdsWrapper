using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using TwinCAT.Ads;
using AdsWrapper;
using AdsWrapperDemo.Models;

namespace AdsWrapperDemo.Views
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region properties
        /// <summary> Contains data read from the PLC for data binding </summary>
        public ObservableCollection<string> ReadPlcData
        {
            get { return readPlcData; }
            set
            {
                readPlcData = value;
                OnPropertyChanged(nameof(ReadPlcData));
            }
        }
        private ObservableCollection<string> readPlcData = [];
        /// <summary> Contains data written to the PLC for data binding </summary>
        public ObservableCollection<string> WrittenPlcData
        {
            get { return writtenPlcData; }
            set
            {
                writtenPlcData = value;
                OnPropertyChanged(nameof(WrittenPlcData));
            }
        }
        private ObservableCollection<string> writtenPlcData = [];
        #endregion

        #region events
        /// <summary> Occurs when a property value changes </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion

        #region private fields
        /// <summary>  ADS client used for communication </summary>
        private AdsSyncClient? adsSyncClient;
        /// <summary> Data to be sent to the client </summary>
        private readonly DataToClient dataToPlc = new();
        /// <summary> Data to be read from the client </summary>
        private readonly DataFromClient dataFromPlc = new();
        /// <summary> Name of the client structure containing data to be written </summary>
        private readonly string structNameDataFromClient = "GVL.Data_To_Write";
        /// <summary> Name of the PLC structure containing data to be read </summary>
        private readonly string structNameDataToClient = "GVL.Data_To_Read";
        /// <summary> Timer used to toggle the life bit periodically </summary>
        private readonly DispatcherTimer dispatcherTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        /// <summary> Indicates whether the object has been disposed </summary>
        private bool disposed;
        #endregion

        #region constructors
        /// <summary>
        /// Constructor of the MainWindow class. Initializes the window, sets up data context, and starts a timer to toggle the Lifebit property in the dataToPlc object every second. 
        /// Also sets up property change notifications for the FirstCounter and SecondCounter properties of the dataFromPlc object to update the corresponding properties in the dataToPlc object.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            dispatcherTimer.Tick += SetLifeBit;
            dispatcherTimer.Start();
            ExecuteOnPropertyChanged(nameof(dataFromPlc.FirstCounter), dataFromPlc, SyncFirstCounter);
            ExecuteOnPropertyChanged(nameof(dataFromPlc.SecondCounter), dataFromPlc, SyncSecondCounter);
            LoadFromPlcProperties(dataFromPlc);
            LoadToPlcProperties(dataToPlc);
        }
        #endregion

        #region public methods
        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region private methods
        /// <summary>
        /// Synchronizes the FirstCounter property of the dataToPlc object with the FirstCounter property of the dataFromPlc object whenever the latter changes.
        /// This method is called as an event handler for property change notifications.
        /// </summary>
        private void SyncFirstCounter(object? sender, EventArgs e)
        {
            dataToPlc.FirstCounter = dataFromPlc.FirstCounter;
        }

        /// <summary>
        /// Synchronizes the SecondCounter property of the dataToPlc object with the SecondCounter property of the dataFromPlc object whenever the latter changes.
        /// This method is called as an event handler for property change notifications.
        /// </summary>
        private void SyncSecondCounter(object? sender, EventArgs e)
        {
            dataToPlc.SecondCounter = dataFromPlc.SecondCounter;
        }

        /// <summary>
        /// Updates the TextBoxAdsStateSoftware control with the current software state of the adsSyncClient whenever the state changes.
        /// This method is called as an event handler for property change notifications.
        /// </summary>
        private void ShowAdsSoftwareState(object? sender, EventArgs e)
        {
            if (adsSyncClient is not null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    TextBoxSoftwareState.Text = adsSyncClient.StateSoftware.ToString();
                });
            }
        }

        /// <summary>
        /// Updates the TextBoxAdsStateHardware control with the current hardware state of the adsSyncClient whenever the state changes.
        /// This method is called as an event handler for property change notifications.
        /// </summary>
        private void ShowAdsHardwareState(object? sender, EventArgs e)
        {
            if (adsSyncClient is not null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    TextBoxHardwareState.Text = adsSyncClient.StateHardware.ToString();
                });
            }
        }

        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> to release managed resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                dispatcherTimer.Stop();
                dispatcherTimer.Tick -= SetLifeBit;
                if (adsSyncClient is not null)
                {
                    if (adsSyncClient.StateSoftware == AdsState.Run)
                    {
                        adsSyncClient.StopSyncAsync().GetAwaiter().GetResult();
                    }
                    adsSyncClient.Dispose();
                    ExecuteOnPropertyChanged(nameof(adsSyncClient.StateSoftware), adsSyncClient, ShowAdsSoftwareState);
                    ExecuteOnPropertyChanged(nameof(adsSyncClient.StateHardware), adsSyncClient, ShowAdsHardwareState);
                }
            }
            disposed = true;
        }

        /// <summary>
        /// Toggles the Lifebit property of the dataToPlc object every time the dispatcher timer ticks (every second).
        /// </summary>
        private void SetLifeBit(object? sender, EventArgs? e)
        {
            dataToPlc.Lifebit = !dataToPlc.Lifebit;
        }

        /// <summary>
        /// Raises the PropertyChanged event for the specified property name. This method is called whenever a property value changes to notify any subscribers that the property has changed.
        /// </summary>
        /// <param name="propertyName"> The name of the property that has changed </param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Parses the ADS address from the TextBoxAds control, creates a new instance of AdsSyncClient with the specified parameters, and starts the synchronization process.
        /// If an exception occurs during this process, it displays an error message to the user.
        /// </summary>
        private async void ToggleButtonActivateChecked(object sender, RoutedEventArgs e)
        {
            AmsNetId amsNetId;
            try
            {
                _ = AmsNetId.Parse(TextBoxAds.Text);
            }
            catch (FormatException)
            {
                TextBoxAds.Text = "Enter valid ADS!";
                e.Handled = true;
                return;
            }
            amsNetId = AmsNetId.Parse(TextBoxAds.Text);
            adsSyncClient = new AdsSyncClient(new AdsClient(), amsNetId, dataToPlc, dataFromPlc, structNameDataToClient, structNameDataFromClient);
            ExecuteOnPropertyChanged(nameof(adsSyncClient.StateSoftware), adsSyncClient, ShowAdsSoftwareState);
            ExecuteOnPropertyChanged(nameof(adsSyncClient.StateHardware), adsSyncClient, ShowAdsHardwareState);
            Task task = adsSyncClient.ActivateSync();
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "AdsWrapperDemo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// When the toggle button is unchecked, it calls the DisconnectAsync method to disconnect from the ADS client. If an exception occurs during this process, it displays an error message to the user.
        /// </summary>
        private async void ToggleButtonActivateUnchecked(object sender, EventArgs e)
        {
            try
            {
                if (adsSyncClient is not null && adsSyncClient.StateSoftware == AdsState.Run)
                {
                    await adsSyncClient.StopSyncAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "AdsWrapperDemo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads the properties of the specified dataFromPlc object into the ReadPlcData collection for display in the UI. It also sets up a property change notification handler to update the
        /// ReadPlcData collection whenever a property value changes in the dataFromPlc object.
        /// </summary>
        /// <param name="dataFromPlc">  The object containing the properties to load </param>
        private void LoadFromPlcProperties(object dataFromPlc)
        {
            ReadPlcData.Clear();

            var properties = dataFromPlc.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            void Update(string? propertyName = null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    var selected = string.IsNullOrEmpty(propertyName)
                        ? properties
                        : properties.Where(p => p.Name == propertyName);
                    foreach (var property in selected)
                    {
                        var text = $"{property.Name}: {property.GetValue(dataFromPlc)}";
                        var index = ReadPlcData.Select((value, i) => new { value, i }).FirstOrDefault(x => x.value.StartsWith(property.Name + ": "))?.i;
                        if (index.HasValue)
                        {
                            ReadPlcData[index.Value] = text;
                        }
                        else
                        {
                            ReadPlcData.Add(text);
                        }
                    }
                });
            }
            Update();
            if (dataFromPlc is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged += (_, args) => Update(args.PropertyName);
            }
        }

        /// <summary>
        /// Loads the properties of the specified dataToPlc object into the WrittenPlcData collection for display in the UI. It also sets up a property change notification handler to update the
        /// </summary>
        /// <param name="dataToPlc"> The object containing the properties to load </param>
        private void LoadToPlcProperties(object dataToPlc)
        {
            WrittenPlcData.Clear();
            var properties = dataToPlc.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            void Update(string? propertyName = null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    var selected = string.IsNullOrEmpty(propertyName)
                        ? properties
                        : properties.Where(p => p.Name == propertyName);
                    foreach (var property in selected)
                    {
                        var text = $"{property.Name}: {property.GetValue(dataToPlc)}";

                        var index = WrittenPlcData.Select((value, i) => new { value, i }).FirstOrDefault(x => x.value.StartsWith(property.Name + ": "))?.i;
                        if (index.HasValue)
                        {
                            WrittenPlcData[index.Value] = text;
                        }
                        else
                        {
                            WrittenPlcData.Add(text);
                        }
                    }
                });
            }
            Update();
            if (dataToPlc is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged += (_, args) => Update(args.PropertyName);
            }
        }

        /// <summary> 
        /// Registers a handler that is invoked when the value of the specified property on the object instance changes.
        /// </summary>
        /// <param name="nameofProperty"> The name of the property for which the change handler should be registered </param>
        /// <param name="objectInstance"> The object instance whose property should be monitored </param>
        /// <param name="handler"> The event handler to invoke when the property value changes </param>
        private static void ExecuteOnPropertyChanged(string nameofProperty, object objectInstance, EventHandler handler)
        {
            PropertyDescriptorCollection? properties = TypeDescriptor.GetProperties(objectInstance);
            PropertyDescriptor? property = properties?.Find(nameofProperty, false);
            property?.AddValueChanged(objectInstance, handler);
        }

        /// <summary> Stops the specified handler from being invoked when the value of a property changes. </summary>
        /// <param name="nameofProperty"> The name of the property, specified by using nameof(x) </param>
        /// <param name="objectInstance"> The object instance that contains the property </param>
        /// <param name="handler"> The event handler to unregister from property change notifications </param>
        private static void NoLongerExecuteOnPropertyChanged(string nameofProperty, object objectInstance, EventHandler handler)
        {
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(objectInstance);
            PropertyDescriptor property = properties.Find(nameofProperty, false)!;
            property.RemoveValueChanged(objectInstance, handler);
        }
        #endregion
    }
}