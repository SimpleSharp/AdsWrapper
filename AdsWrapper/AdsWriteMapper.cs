using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using TwinCAT.Ads;

namespace AdsWrapper
{
    /// <summary>
    /// Provides functionality for mapping the properties of a .NET object to the corresponding
    /// fields of an ADS structure and writing the values to the ADS client.
    /// </summary>
    public class AdsWriteMapper : IDisposable
    {
        #region properties
        /// <summary> The object containing the data to be written to the ADS client. </summary>
        public INotifyPropertyChanged Data { get; private set; }
        /// <summary> The data object converted to a class suitable for marshalling </summary>
        public object DataAsMarshalledClass { get; private set; }
        /// <summary> The marshalled size of each field in the data structure </summary>
        public uint[] Sizes { get; private set; }
        /// <summary> The ADS variable handles assigned to the individual fields </summary>
        public uint[] VariableHandles { get; private set; }
        /// <summary> The field metadata of the marshalled data class </summary>
        public FieldInfo[] FieldInfos { get; private set; }
        /// <summary> The property metadata of the input data object </summary>
        public PropertyInfo[] PropertyInfos { get; private set; }
        /// <summary> A dictionary that maps property names to their corresponding indices </summary>
        public Dictionary<string, int> PropertyIndices { get; private set; }
        /// <summary> The name of the ADS structure containing the data to be written </summary>
        public string StructName { get; private set; }
        #endregion

        #region fields
        /// <summary> The ADS client used to manage variable handles </summary>
        private IAdsConnectAddress? adsClient;
        /// <summary> Indicates whether the object has been disposed </summary>
        private bool disposed;
        #endregion

        #region constructors
        /// <summary>
        /// Initializes a new instance of this class.
        /// </summary>
        /// <param name="data"> The object containing the data to be written to the ADS client </param>
        /// <param name="structName"> The name of the ADS structure containing the data to be written </param>
        public AdsWriteMapper(INotifyPropertyChanged data, string structName)
        {             
            StructName = structName;
            Data = data;                
            DataAsMarshalledClass = Marshalling.PropertyClassToMarshalledFieldClass(data);
            FieldInfos = DataAsMarshalledClass.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfos = data.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Sizes = GetMarshalSizesOfFieldClassElements(DataAsMarshalledClass);
            VariableHandles = new uint[FieldInfos.Length]; 
            PropertyIndices = PropertyInfos.Select((p,i)=> new {p.Name,i}).ToDictionary(x=>x.Name, x=>x.i);
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

        /// <summary>
        /// Attempts to retrieve the value of the specified property and its corresponding ADS variable handle.
        /// </summary>
        /// <param name="propertyName"> The name of the property whose value should be retrieved. </param>
        /// <param name="variableHandle"> Contains the ADS variable handle associated with the specified property </param>
        /// <param name="fieldValue"> Contains the value of the specified property converted to the corresponding marshalled field type </param>
        /// <returns> Return TRUE if the property was found and its value was successfully retrieved. </returns>
        public bool TryTakeOverPropertyValue(string propertyName, out uint? variableHandle, out object? fieldValue)
        {
            if (PropertyIndices.TryGetValue(propertyName, out int index) && PropertyInfos[index].GetValue(Data) is object value)
            {
                FieldInfos[index].SetValue(DataAsMarshalledClass, value);
                fieldValue = FieldInfos[index].GetValue(DataAsMarshalledClass);
                variableHandle = VariableHandles[index];
                return true;
            }
            variableHandle = null;
            fieldValue = null;
            return false;
        }

        /// <summary>
        /// Creates an ADS variable handle for each field in the marshalled data structure.
        /// </summary>
        /// <param name="adsClient"> The ADS client used to create the variable handles </param>
        public void GenerateVariableHandles(IAdsConnectAddress adsClient)
        {
            for (int i1 = 0; i1 < FieldInfos.Length; i1++)
            {
                VariableHandles[i1] = adsClient.CreateVariableHandle($"{StructName}.{FieldInfos[i1].Name}");
            }
            this.adsClient = adsClient;
        }
        #endregion

        #region private methods
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
            if (disposing && adsClient is not null && adsClient.IsConnected)
            {
                for (int i1 = 0; i1 < VariableHandles.Length; i1++)
                {
                    adsClient.DeleteVariableHandle(VariableHandles[i1]);
                }
            }
            disposed = true;
        }

        /// <summary> Determines the marshalled size of each field in the specified data structure. </summary>
        /// <param name="structure"> The data structure whose field sizes are to be determined </param>
        /// <returns> An array containing the marshalled size of each field. </returns>
        private static uint[] GetMarshalSizesOfFieldClassElements(object structure)
        {
            List<uint> sizes = [];
            FieldInfo[] fieldInfos = structure.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i1 = 0; i1 < fieldInfos.Length; i1++)
            {
                if (i1 == 0 && fieldInfos.Length == 1)
                {
                    sizes.Add((uint)Marshal.SizeOf(structure));
                }
                else if (i1 == 0 && fieldInfos.Length > 1)
                {
                    sizes.Add((uint)Marshal.OffsetOf(structure.GetType(), fieldInfos[i1 + 1].Name));
                }
                else if (i1 == fieldInfos.Length - 1)
                {
                    sizes.Add((uint)Marshal.SizeOf(structure) - (uint)Marshal.OffsetOf(structure.GetType(), fieldInfos[i1].Name));
                }
                else
                {
                    sizes.Add((uint)Marshal.OffsetOf(structure.GetType(), fieldInfos[i1 + 1].Name) - (uint)Marshal.OffsetOf(structure.GetType(), fieldInfos[i1].Name));
                }
            }
            return [.. sizes];
        }
        #endregion
    }
}