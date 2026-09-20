using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace AdsWrapper
{
    /// <summary>
    /// Klasse, die Methoden für das Mashalling bereitstellt
    /// </summary>
    public static class Marshalling
    {
        #region public methods
        /// <summary>
        /// Creates a dynamic class with corresponding marshalled fields from an object's public properties.
        /// </summary>
        /// <param name="originalClass"> The original class with properties. </param>
        public static object PropertyClassToMarshalledFieldClass(object originalClass)
        {
            //Use a specialized conversion method for observable collections
            if (IsTypeOfObservableCollection(originalClass))
            {
                return ObservableCollectionAsMarshalledFieldClass((IList)originalClass);
            }
            //Create the type builder used to generate the runtime class
            TypeBuilder typeBuilder = CreateTypeBuilder();
            //Retrieve and order the source properties before creating the corresponding fields
            PropertyInfo[] propertyInfos = originalClass.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            propertyInfos = [.. propertyInfos.OrderBy(p => p.MetadataToken)];
            int fieldOffset = new();
            for (int i1 = propertyInfos.GetLowerBound(0); i1 <= propertyInfos.GetUpperBound(0); i1++)
            {
                object propertyValue = propertyInfos[i1].GetValue(originalClass)!;
                //Create a public field with the same name and the corresponding field type
                FieldBuilder fieldBuilder = typeBuilder.DefineField(propertyInfos[i1].Name, GetTypeByObject(propertyValue), FieldAttributes.Public);
                //Determine the MarshalAsAttribute constructor arguments required for the field type
                FieldInfo[] namedFields = GetArgumentsByObject(propertyValue);
                //Determine the values of the MarshalAsAttribute arguments required for the field type
                object[] fieldValues = GetArgumentValuesByObject(propertyValue);
                //Create the MarshalAsAttribute for the generated field
                CustomAttributeBuilder customBuilder = new(
                    typeof(MarshalAsAttribute).GetConstructor([typeof(UnmanagedType)])!,
                    [TypeToUnmanagedType(fieldBuilder.FieldType)],
                    namedFields,
                    fieldValues);
                //Marshal-Attribut setzen                           
                fieldBuilder.SetCustomAttribute(customBuilder);
                fieldBuilder.SetOffset(fieldOffset);
                fieldOffset++;
            }
            //Apply the marshalling attribute and set the field offset
            Type classType = typeBuilder.CreateType()!;
            return Activator.CreateInstance(classType)!;
        }
        #endregion

        #region private & protected methods
        /// <summary>
        /// Creates a new class based on an ObservableCollection.
        /// </summary>
        /// <returns> Returns the new class. </returns>
        private static object ObservableCollectionAsMarshalledFieldClass(IList observableCollection)
        {
            //Create the type builder used to generate the runtime class
            TypeBuilder typeBuilder = CreateTypeBuilder();
            //Use the first collection element to determine the properties and corresponding fields of the generated class
            PropertyInfo[] propertyInfos = observableCollection[0]!.GetType().GetProperties();
            int fieldOffset = new();
            for (int i1 = propertyInfos.GetLowerBound(0); i1 <= propertyInfos.GetUpperBound(0); i1++)
            {
                object propertyValue = propertyInfos[i1].GetValue(observableCollection[0])!;
                //Create a public field with the same name and the corresponding field type
                FieldBuilder fieldBuilder = typeBuilder.DefineField(propertyInfos[i1].Name, GetTypeByObject(propertyValue), FieldAttributes.Public);
                //Determine the MarshalAsAttribute constructor arguments required for the field type
                FieldInfo[] namedFields = GetArgumentsByObject(propertyValue);
                //Determine the values of the MarshalAsAttribute arguments required for the field type
                object[] fieldValues = GetArgumentValuesByObject(propertyValue);
                //Create the MarshalAsAttribute for the generated field
                CustomAttributeBuilder customBuilder = new(
                    typeof(MarshalAsAttribute).GetConstructor([typeof(UnmanagedType)])!,
                    [TypeToUnmanagedType(fieldBuilder.FieldType)],
                    namedFields,
                    fieldValues);
                //Apply the marshalling attribute and set the field offset                           
                fieldBuilder.SetCustomAttribute(customBuilder);
                fieldBuilder.SetOffset(fieldOffset);
                fieldOffset++;
            }
            //Create the generated class type
            Type classType = typeBuilder.CreateType()!;
            //Create an array with one generated object for each collection element
            IList array = Array.CreateInstance(classType, observableCollection.Count);
            for (int i1 = 0; i1 < array.Count; i1++)
            {
                array[i1] = Activator.CreateInstance(classType);
            }
            return array;
        }

        /// <summary>
        /// Creates a new typebuilder.
        /// </summary>
        /// <returns> Returns the typebuilder. </returns>
        private static TypeBuilder CreateTypeBuilder()
        {
            var assemblyName = new AssemblyName("MarshalAssembly");
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name!);
            TypeBuilder typeBuilder = moduleBuilder.DefineType("DynamicClass",
                                                               TypeAttributes.Public | TypeAttributes.SequentialLayout,
                                                               typeof(object),
                                                               PackingSize.Size1);
            return typeBuilder;
        }

        /// <summary>
        /// Gets the type of an object.
        /// </summary>
        /// <param name="value"> the object </param>
        /// <returns> Return the type. </returns>
        private static Type GetTypeByObject(object value)
        {
            if (IsTypeOfObservableCollection(value))
            {
                ICollection collection = (ICollection)value;
                Array array = Array.CreateInstance(collection.GetType().GetGenericArguments().Single(), collection.Count);
                return array.GetType();
            }
            else
            {
                return value.GetType();
            }
        }

        /// <summary>
        /// Returns the MarshalAsAttribute fields required for the specified value type.
        /// </summary>
        /// <param name="value"> Der Wert, von dem die Argumente ermittelt werden sollen </param>
        /// <returns> Gibt die Argumente zurück. </returns>
        private static FieldInfo[] GetArgumentsByObject(object value)
        {
            FieldInfo[] namedFields;
            if (value.GetType().IsArray || IsTypeOfObservableCollection(value))
            {
                namedFields = [typeof(MarshalAsAttribute).GetField("SizeConst")!, typeof(MarshalAsAttribute).GetField("ArraySubType")!];
            }
            else
            {
                namedFields = [typeof(MarshalAsAttribute).GetField("SizeConst")!];
            }
            return namedFields;
        }

        /// <summary>
        /// Determines the values required for the marshalling arguments based on the type and value of the specified object
        /// </summary>
        /// <param name="value"> The value used to determine the required marshalling arguments </param>
        /// /// <returns> An array containing the values required to configure the field's marshalling attributes. </returns>
        private static object[] GetArgumentValuesByObject(object value)
        {
            if (value is string)
            {
                //Bei einem String wird ein Objekt mit der Zahl 81 übergeben für 80 Zeichen + 1 für das Byte, dass die Länge des Strings angibt
                return [81];
            }
            else if (IsTypeOfObservableCollection(value))
            {
                //Bei einer ObservableCollection wird die Länge des Arrays und der Typ übergeben
                ICollection collection = (ICollection)value;
                Array array = Array.CreateInstance(collection.GetType().GetGenericArguments().Single(), collection.Count);
                return [array.Length, TypeToUnmanagedType(array.GetValue(0)!.GetType())];
            }
            else if (value.GetType().IsArray)
            {
                //Bei einem Array wird ebenso die Länge des Arrays und der Typ übergeben
                return [((Array)value).Length, TypeToUnmanagedType(((Array)value).GetValue(0)!.GetType())];
            }
            else
            {
                //Alternativ wird eine 0 übergeben
                return [0];
            }
        }

        /// <summary>
        /// Determines whether the specified object is an ObservableCollection .
        /// </summary>
        /// <param name="o"> The object to check </param>
        /// <returns> Gibt TRUE zurück, wenn es sich um eine ObservableCollection handelt </returns>
        private static bool IsTypeOfObservableCollection(object o)
        {
            return o.GetType() is Type type && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ObservableCollection<>);
        }

        /// <summary>
        /// Determines the corresponding unmanaged marshalling type for the specified managed type.
        /// </summary>
        /// <param name="type"> The managed type for which the unmanaged marshalling type is determined </param>
        /// <returns> The <see cref="UnmanagedType"/> corresponding to the specified managed type. </returns>
        /// <exception cref="NotSupportedException"> Thrown when the specified managed type is not supported for marshalling. </exception>
        private static UnmanagedType TypeToUnmanagedType(Type type)
        {
            if (type.IsArray || type.GetInterface(nameof(ICollection)) is not null)
            {
                return UnmanagedType.ByValArray;
            }
            else if (type == typeof(bool) || type == typeof(byte))
            {
                return UnmanagedType.U1;
            }
            else if (type == typeof(short))
            {
                return UnmanagedType.I2;
            }
            else if (type == typeof(int))
            {
                return UnmanagedType.I4;
            }
            else if (type == typeof(long))
            {
                return UnmanagedType.I8;
            }
            else if (type == typeof(ushort))
            {
                return UnmanagedType.U2;
            }
            else if (type == typeof(uint))
            {
                return UnmanagedType.U4;
            }
            else if (type == typeof(ulong))
            {
                return UnmanagedType.U8;
            }
            else if (type == typeof(float))
            {
                return UnmanagedType.R4;
            }
            else if (type == typeof(double))
            {
                return UnmanagedType.R8;
            }
            else if (type == typeof(string))
            {
                return UnmanagedType.ByValTStr;
            }
            else
            {
                throw new NotSupportedException($"The managed type '{type.FullName}' is not supported for marshalling.");
            }
        }
        #endregion
    }
}
