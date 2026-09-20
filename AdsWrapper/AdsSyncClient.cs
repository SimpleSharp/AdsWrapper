using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Reflection;
using TwinCAT.Ads;
namespace AdsWrapper
{
    /// <summary>
    /// Provides synchronized data exchange between .NET objects and an ADS client.
    /// </summary>
    /// <remarks>
    /// This class monitors property changes on the object containing the data to be written to the ADS client and writes updated values to the corresponding ADS
    /// variables. It also reads values from the ADS client when notifications are received and updates the corresponding properties on the object containing the read data.
    /// The class manages the ADS connection, variable handles, notification handles, synchronization, and the dedicated thread used for thread-safe ADS operations.
    /// Call <see cref="ActivateSync"/> to start the data exchange and <see cref="StopSyncAsync"/> to stop it. The instance must be disposed when it is no longer needed.
    /// </remarks>
    public partial class AdsSyncClient : ObservableObject, IDisposable
    {
        #region properties
        /// <summary> Status of the ADS client's hardware </summary>
        [ObservableProperty] private AdsState stateHardware = AdsState.Invalid;
        /// <summary> Status of the ADS client's software </summary>
        [ObservableProperty] private AdsState stateSoftware = AdsState.Invalid;
        #endregion

        #region fields
        /// <summary> The ADS client </summary>
        private readonly IAdsConnectAddress adsClient;
        /// <summary> The AMS address of the ADS client </summary>
        private readonly AmsNetId amsNetId;
        /// <summary> The port of the ADS client's program.</summary>
        private readonly AmsPort amsPort = AmsPort.PlcRuntime_851;
        /// <summary> Thread for thread-safe execution of ADS tasks </summary>
        private readonly DedicatedThreadRunner thread = new();
        /// <summary> Contains the data to be sent and all related information </summary>
        private readonly AdsWriteMapper adsDataMapperWrite;
        /// <summary> Contains the data to be read and all related information </summary>
        private readonly AdsReadMapper adsDataMapperRead;
        /// <summary> Cancellation token source for terminating the communication </summary>
        private CancellationTokenSource tokenSource = new();
        /// <summary> Task responsible for data exchange </summary>
        private Task? taskExchange;
        /// <summary> Indicates whether the object has been disposed </summary>
        private bool disposed;
        #endregion

        #region  constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="adsClient"> The ADS client </param>
        /// <param name="amsNetId"> The AMS Net ID of the ADS client </param>
        /// <param name="dataToAdsClient"> The data to be sent to the ADS client </param>
        /// <param name="dataFromAdsClient"> The data to be read from the ADS client </param>
        /// <param name="structNameDataToClient"> The variable name in the ADS client to which the data is written </param>
        /// <param name="structNameDataFromClient"> The variable name in the ADS client from which the data is read </param>
        /// <exception cref="FormatException"> Thrown when the specified data format is invalid </exception>
        public AdsSyncClient(IAdsConnectAddress adsClient,
                             AmsNetId amsNetId,
                             INotifyPropertyChanged dataToAdsClient,
                             INotifyPropertyChanged dataFromAdsClient,
                             string structNameDataToClient,
                             string structNameDataFromClient) :
            this(adsClient,
                 amsNetId,
                 AmsPort.PlcRuntime_851,
                 dataToAdsClient,
                 dataFromAdsClient,
                 structNameDataToClient,
                 structNameDataFromClient) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="adsClient"> The ADS client </param>
        /// <param name="amsNetId"> The AMS Net ID of the ADS client </param>
        /// <param name="dataToAdsClient"> The data to be sent to the ADS client </param>
        /// <param name="dataFromAdsClient"> The data to be read from the ADS client </param>
        /// <param name="structNameDataToClient"> The variable name in the ADS client to which the data is written </param>
        /// <param name="structNameDataFromClient"> The variable name in the ADS client from which the data is read </param>
        /// <exception cref="FormatException"> Thrown when the specified data format is invalid </exception>
        public AdsSyncClient(AdsClient adsClient,
                             AmsNetId amsNetId,
                             INotifyPropertyChanged dataToAdsClient,
                             INotifyPropertyChanged dataFromAdsClient,
                             string structNameDataToClient,
                             string structNameDataFromClient) :
            this(adsClient,
                 amsNetId,
                 AmsPort.PlcRuntime_851,
                 dataToAdsClient,
                 dataFromAdsClient,
                 structNameDataToClient,
                 structNameDataFromClient) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="adsClient"> The ADS client </param>
        /// <param name="amsNetId"> The AMS Net ID of the ADS client </param>
        /// <param name="amsPort"> The port of the ADS client's program </param>
        /// <param name="dataToAdsClient"> The data to be sent to the ADS client </param>
        /// <param name="dataFromAdsClient"> The data to be read from the ADS client </param>
        /// <param name="structNameDataToClient"> The variable name in the ADS client to which the data is written </param>
        /// <param name="structNameDataFromClient"> The variable name in the ADS client from which the data is read </param>
        /// <exception cref="FormatException"> Thrown when the specified data format is invalid </exception>
        public AdsSyncClient(AdsClient adsClient,
                             AmsNetId amsNetId,
                             AmsPort amsPort,
                             INotifyPropertyChanged dataToAdsClient,
                             INotifyPropertyChanged dataFromAdsClient,
                             string structNameDataToClient,
                             string structNameDataFromClient) :
            this((IAdsConnectAddress)adsClient,
                 amsNetId,
                 amsPort,
                 dataToAdsClient,
                 dataFromAdsClient,
                 structNameDataToClient,
                 structNameDataFromClient) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="adsClient"> The ADS client </param>
        /// <param name="amsNetId"> The AMS Net ID of the ADS client </param>
        /// <param name="amsPort"> The port of the ADS client's program </param>
        /// <param name="dataToAdsClient"> The data to be sent to the ADS client </param>
        /// <param name="dataFromAdsClient"> The data to be read from the ADS client </param>
        /// <param name="structNameDataToClient"> The variable name in the ADS client to which the data is written </param>
        /// <param name="structNameDataFromClient"> The variable name in the ADS client from which the data is read </param>
        /// <exception cref="FormatException"> Thrown when the specified data format is invalid </exception>
        public AdsSyncClient(IAdsConnectAddress adsClient,
                             AmsNetId amsNetId,
                             AmsPort amsPort,
                             INotifyPropertyChanged dataToAdsClient,
                             INotifyPropertyChanged dataFromAdsClient,
                             string structNameDataToClient,
                             string structNameDataFromClient)
        {
            if (ObjectContainsFields(dataToAdsClient) || ObjectContainsFields(dataFromAdsClient))
            {
                throw new FormatException("Objects contain fields. Please use properties instead of fields.");
            }
            this.adsClient = adsClient;
            this.amsNetId = amsNetId;
            this.amsPort = amsPort;
            adsDataMapperRead = new(dataFromAdsClient, structNameDataFromClient);
            adsDataMapperWrite = new(dataToAdsClient, structNameDataToClient);     
            foreach (PropertyInfo propertyInfo in dataToAdsClient.GetType().GetProperties())
            {
                ExecuteOnPropertyChanged(propertyInfo.Name, dataToAdsClient, Write);
            }
        }
        #endregion

        #region  public methods and tasks
        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Activates the data exchange with the ads client
        /// </summary>
        /// <returns> Returns the task for exception handling. </returns>
        public Task ActivateSync()
        {
            taskExchange = ManageConnectionAsync(tokenSource.Token);
            return taskExchange;
        }

        /// <summary>
        /// Stops the data exchange with the ads client
        /// </summary>
        /// <returns> Returns the task for exception handling. </returns>
        public async Task StopSyncAsync()
        {
            if (adsClient is IAdsDisposableConnection adsDisposable)
            {
                if (!adsDisposable.IsDisposed && adsClient.IsConnected && adsDataMapperRead.NotificationHandles.Length != 0)
                {
                    adsClient.AdsNotification -= Read;
                }
                adsDataMapperRead.Dispose();
                adsDataMapperWrite.Dispose();
                adsDisposable.Dispose();
            }
            tokenSource.Cancel();
            if (taskExchange is not null)
            {
                await taskExchange!;
            }
            tokenSource.Dispose();
            tokenSource = new();
            StateSoftware = AdsState.Invalid;
            StateHardware = AdsState.Invalid;
        }
        #endregion

        #region private methods and tasks
        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        /// <param name="disposing"> Disposes this istance if TRUE </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            if (disposing && adsClient.IsConnected)
            {
                foreach (PropertyInfo propertyInfo in adsDataMapperWrite.PropertyInfos)
                {
                    NoLongerExecuteOnPropertyChanged(propertyInfo.Name, adsDataMapperWrite.Data, Write);
                }
                if (adsClient is IAdsDisposableConnection adsDisposable)
                {
                    if (!adsDisposable.IsDisposed && adsClient.IsConnected && adsDataMapperRead.NotificationHandles.Length != 0)
                    {
                        adsClient.AdsNotification -= Read;
                    }
                    adsDisposable.Dispose();
                }
                adsDataMapperRead.Dispose();
                adsDataMapperWrite.Dispose();
                tokenSource.Cancel();
                taskExchange?.GetAwaiter().GetResult();
                tokenSource.Dispose();
                thread.Dispose();
            }
            disposed = true;
        }

        /// <summary>
        /// Maintains the connection to the ADS client and initializes the required variable and notification handles after a successful connection.
        /// </summary>
        /// <param name="token"> The token used to cancel the exchange loop. </param>
        private async Task ManageConnectionAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    //Tries to connect to the ads client
                    if (!adsClient.IsConnected && HardwareOfAdsClientIsRunning() && SoftwareOfAdsClientIsRunning())
                    {
                        await thread.InvokeAsync(() => adsClient.Connect(amsNetId, (int)amsPort));
                        if (adsClient.IsConnected)
                        {
                            await thread.InvokeAsync(() =>
                            {
                                adsDataMapperRead.GenerateNotificationsAndVariableHandles(adsClient);
                                adsDataMapperWrite.GenerateVariableHandles(adsClient);
                                InitializeStructureSync();
                            });
                            if (adsDataMapperRead.NotificationHandles.Length > 0)
                            {
                                adsClient.AdsNotification += Read;
                            }
                        }
                    }
                    //Determines the ads client state every second if connected
                    else if (adsClient.IsConnected)
                    {
                        HardwareOfAdsClientIsRunning();
                        SoftwareOfAdsClientIsRunning();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                await thread.InvokeAsync(adsClient.Close);
            }
        }

        /// <summary>
        /// Executes a complete data exchange (read and write) to synchronize the data between the clients
        /// </summary>
        private void InitializeStructureSync()
        {
            for (int i1 = 0; i1 < adsDataMapperWrite.PropertyInfos.Length; i1++)
            {
                string propertyName = adsDataMapperWrite.PropertyInfos[i1].Name;
                if (adsDataMapperWrite.TryTakeOverPropertyValue(propertyName, out uint? varHandle, out object? value))
                {
                    adsClient.WriteAny((uint)varHandle!, value!);
                }
            }
            for (int i1 = 0; i1 < adsDataMapperRead.PropertyInfos.Length; i1++)
            {
                object value = adsClient.ReadAny(adsDataMapperRead.VariableHandles[i1], adsDataMapperRead.Types[i1]);
                adsDataMapperRead.PropertyInfos[i1].SetValue(adsDataMapperRead.Data, value);
            }
        }

        /// <summary>
        /// Gets the current software state of the ADS client
        /// </summary>
        /// <returns> Return TRUE if the software is in RUN mode. </returns>
        private bool SoftwareOfAdsClientIsRunning()
        {
            using AdsClient adsClientSoftware = new() { Timeout = 500 };
            adsClientSoftware.Connect(amsNetId, amsPort);
            try
            {
                StateSoftware = adsClientSoftware.ReadState().AdsState;
            }
            catch
            {
                StateSoftware = AdsState.Invalid;
            }
            return StateSoftware == AdsState.Run;
        }

        /// <summary>
        /// Gets the current hardware state of the ADS client
        /// </summary>
        /// <returns> Return TRUE if the hardware is in RUN mode. </returns>
        private bool HardwareOfAdsClientIsRunning()
        {
            using AdsClient adsClientHardware = new() { Timeout = 500 };
            adsClientHardware.Connect(amsNetId, (int)AmsPort.SystemService);
            try
            {
                StateHardware = adsClientHardware.ReadState().AdsState;
            }
            catch
            {
                StateHardware = AdsState.Invalid;
            }
            return StateHardware == AdsState.Run;
        }

        /// <summary>
        /// Checks if the instance of an object contains fields
        /// </summary>
        /// <param name="o"> The object that has to be checked </param>
        /// <returns> Returns TRUE if the object contains fields. </returns>
        private static bool ObjectContainsFields(object o) => o.GetType().GetFields().Length > 0;

        /// <summary>
        /// Writes new data into the ADS client if a value of a property has changed
        /// </summary>
        private void Write(object? sender, EventArgs? e)
        {
            if (e is PropertyChangedEventArgs pe && !string.IsNullOrEmpty(pe.PropertyName) && adsClient.IsConnected)
            {
                thread.BeginInvoke(() =>
                {
                    bool written = adsDataMapperWrite.TryTakeOverPropertyValue(pe.PropertyName, out uint? varHandle, out object? value);
                    if (written && varHandle is uint && value is not null)
                    {
                        adsClient.WriteAny((uint)varHandle!, value!);
                    }
                });
            }
        }

        /// <summary>
        /// Reads data from the ADS client if a value has changed
        /// </summary>
        private void Read(object? sender, AdsNotificationEventArgs? e)
        {
            if (e is not null && adsDataMapperRead.NotificationHandles.Contains(e.Handle))
            {
                thread.BeginInvoke(() =>
                {
                    int index = Array.IndexOf(adsDataMapperRead.NotificationHandles, e.Handle);
                    object value = adsClient.ReadAny(adsDataMapperRead.VariableHandles[index], adsDataMapperRead.Types[index]);
                    adsDataMapperRead.PropertyInfos[index].SetValue(adsDataMapperRead.Data, value);
                }); 
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
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(objectInstance);
            PropertyDescriptor property = properties.Find(nameofProperty, false)!;
            property.AddValueChanged(objectInstance, handler);
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