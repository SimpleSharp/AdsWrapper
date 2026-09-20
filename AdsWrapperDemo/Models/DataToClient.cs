using CommunityToolkit.Mvvm.ComponentModel;

namespace AdsWrapperDemo.Models
{
    /// <summary>
    /// Represents the data structure containing values written to the ADS client. The class inherits 
    /// from <see cref="ObservableObject"/> to support property change notifications required by the 
    /// ADS synchronization mechanism. Properties must be used to represent the ADS data; fields are 
    /// not supported and cause a <see cref="FormatException"/> when the AdsSyncClient is initialized.
    /// </summary>
    public partial class DataToClient : ObservableObject
    {
        /// <summary> Lifebit </summary>
        [ObservableProperty] private bool lifebit;
        /// <summary> first counter </summary>
        [ObservableProperty] private ulong firstCounter = 100402;
        /// <summary> second counter </summary>
        [ObservableProperty] private ulong secondCounter = 89273;
    }
}
