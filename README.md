# AdsWrapper

AdsWrapper is a C# library that simplifies communication with Beckhoff TwinCAT PLCs via ADS. It eliminates boilerplate code by providing automatic data synchronization between .NET objects and ADS structures.

Key capabilities:

    * Automatic read/write on property changes - No manual polling or event subscription
    * Auto-reconnection with hardware/software state monitoring
    * Thread-safe ADS operations via dedicated background thread
    * MVVM compatible using CommunityToolkit.Mvvm
    * Runtime marshalling of .NET objects to ADS-compatible structures

Features

    🔄 Auto Synchronization: Properties sync automatically when values change
    🔌 Connection Management: Auto-connect/reconnect with state monitoring
    🧵 Thread Safety: Dedicated STA thread for sequential ADS operations
    📦 Marshalling: Dynamic structure-to-field conversion for ADS compatibility
    💡 MVVM Ready: Works with INotifyPropertyChanged / ObservableObject
    🛡️ Disposable: Proper resource cleanup with IDisposable pattern

Requirements

    .NET 8.0 or higher
    Beckhoff TwinCAT 3 Runtime (tested with version 4024.78)
    Beckhoff.TwinCAT.Ads NuGet package
    CommunityToolkit.Mvvm NuGet package

From Source

    git clone https://github.com/SimpleSharp/AdsWrapper.git
    cd AdsWrapper
    dotnet build

Architecture Overview

    ┌─────────────────────────────────────────────────────────────┐
    │                     AdsSyncClient                           │
    ├─────────────────────────────────────────────────────────────┤
    │  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐ │
    │  │AdsReadMapper│    │AdsWriteMapper│    │DedicatedThread  │ │
    │  │ (Read Data) │    │ (Write Data) │    │   Runner        │ │
    │  └─────────────┘    └──────────────┘    └─────────────────┘ │
    │         │                    │                    │         │
    │         ▼                    ▼                    ▼         │
    │  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐ │
    │  │Marshalling  │    │ Marshalling  │    │   STA Thread    │ │
    │  │ (Properties │    │ (Properties  │    │  (Sequential)   │ │
    │  │  → Fields)  │    │  → Fields)   │    │                 │ │
    │  └─────────────┘    └──────────────┘    └─────────────────┘ │
    └─────────────────────────────────────────────────────────────┘
                               │
                               ▼
                      ┌───────────────┐
                      │ TwinCAT ADS   │
                      │   (PLC)       │
                      └───────────────┘

Currently supported Data Types

    The `Marshalling.PropertyClassToMarshalledFieldClass` method automatically converts your .NET properties to ADS-compatible field structures at runtime using `System.Reflection.Emit`.

    | .NET Type                 | ADS type      | Notes                             |
    |---------------------------|---------------|-----------------------------------|
    | `bool`                    | `UNINT (U1)`  | 1 byte                            |
    | `byte`                    | `UNSINT (U1)` | 1 byte                            |
    | `short`                   | `INT (I2)`    | 2 bytes, signed                   |
    | `int`                     | `DINT (I4)`   | 4 bytes, signed                   |
    | `long`                    | `LINT (I8)`   | 8 bytes, signed                   |
    | `ushort`                  | `USINT (U2)`  | 2 bytes, unsigned                 |
    | `uint`                    | `UDINT (U4)`  | 4 bytes, unsigned                 |
    | `ulong`                   | `ULINT (U8)`  | 8 bytes, unsigned                 |
    | `float`                   | `REAL (R4)`   | 4 bytes, IEEE 754                 |
    | `double`                  | `LREAL (R8)`  | 8 bytes, IEEE 754                 |
    | `string[80]`              | `STRING[81]`  | 80 chars + null terminator        |
    | `ObservableCollection`    | `ARRAY`       | Dynamic arrays with type tracking |
    | `T[]`                     | `ARRAY`       | Fixed-size arrays                 |
    |---------------------------|---------------|-----------------------------------|

Quick Start Step 1: Create two structures in your PLC project containing the data to be read and written (you can choose any name you like)
    
    TYPE _DATA_FROM_PLC_TO_HMI :
    STRUCT
	    ExampleVariable		: BOOL;
    END_STRUCT
    END_TYPE

    TYPE _DATA_FROM_HMI_TO_PLC :
    STRUCT
	    ExampleVariable		: BOOL;
    END_STRUCT
    END_TYPE

Quick Start Step 2: Create the structures as variables (you can choose any name you like)

    VAR_GLOBAL
	    DATA_FROM_HMI_TO_PLC	: _DATA_FROM_HMI_TO_PLC;
	    DATA_FROM_PLC_TO_HMI	: _DATA_FROM_PLC_TO_HMI;
    END_VAR

Quick Start Step 3: In Visual Studio, install NuGet Packages

    dotnet add package Beckhoff.TwinCAT.Ads
    dotnet add package CommunityToolkit.Mvvm

Quick Start Step 4: Add this library to your project

    git clone https://github.com/SimpleSharp/AdsWrapper.git
    cd AdsWrapper
    dotnet build -c Release
    Rightclick on your solution -> Add -> Existing Project -> Browse -> AdsWrapper\AdsWrapper.csproj


Quick Start Step 5: Create two classes that are identical in content to the structures in the PLC (you can choose any name for the classes and properties)
                    Using ObservableProperty or INotif

    public partial class DataFromPlc : ObservableObject
    {
        [ObservableProperty] private bool lifebit;

    }
    public partial class DataToPlc : ObservableObject
    {
        [ObservableProperty] private bool lifebit;

    }

Quick Start Step 6: Create the ads the wrapper the data objects and define the names of the structure variables in the PLC

       
    private readonly DataToClient dataToPlc = new();
    private readonly DataFromClient dataFromPlc = new();
    private readonly string structNameDataFromClient = "GVL.DATA_FROM_PLC_TO_HMI";
    private readonly string structNameDataToClient = "GVL.DATA_FROM_HMI_TO_PLC";

Quick Start Step 7: Create the AdsSyncClient and start it

        adsSyncClient = new AdsSyncClient(new AdsClient(), AmsNetId.Parse("127.0.0.1.1.1"), dataToPlc, dataFromPlc, structNameDataToClient, structNameDataFromClient);
        Task task = adsSyncClient.ActivateSync();
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            //Insert exception handling
        }

If “adsSyncClient.StateSoftware” = “Run,” data exchange is active. All data is now automatically read and written.
