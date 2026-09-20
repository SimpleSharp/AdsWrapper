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
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐  │
│  │ AdsReadMapper│    │AdsWriteMapper│    │DedicatedThread │  │
│  │ (Read Data) │    │ (Write Data) │    │   Runner       │  │
│  └─────────────┘    └──────────────┘    └─────────────────┘  │
│         │                    │                    │         │
│         ▼                    ▼                    ▼         │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐  │
│  │Marshalling  │    │ Marshalling  │    │   STA Thread    │  │
│  │ (Properties│    │ (Properties  │    │  (Sequential)   │  │
│  │  → Fields)  │    │  → Fields)   │    │                 │  │
│  └─────────────┘    └──────────────┘    └─────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
                  ┌───────────────┐
                  │ TwinCAT ADS   │
                  │   (PLC)       │
                  └───────────────┘

Currently supported Data Types

    The `Marshalling.PropertyClassToMarshalledFieldClass` method automatically converts your .NET properties to ADS-compatible field structures at runtime using `System.Reflection.Emit`.

    | .NET Type | ADS/UnmanagedType | Notes |
    |-----------|-------------------|-------|
    | `bool` | `UNINT (U1)` | 1 byte |
    | `byte` | `UNSINT (U1)` | 1 byte |
    | `short` | `INT (I2)` | 2 bytes, signed |
    | `int` | `DINT (I4)` | 4 bytes, signed |
    | `long` | `LINT (I8)` | 8 bytes, signed |
    | `ushort` | `USINT (U2)` | 2 bytes, unsigned |
    | `uint` | `UDINT (U4)` | 4 bytes, unsigned |
    | `ulong` | `ULINT (U8)` | 8 bytes, unsigned |
    | `float` | `REAL (R4)` | 4 bytes, IEEE 754 |
    | `double` | `LREAL (R8)` | 8 bytes, IEEE 754 |
    | `string[80]` | `STRING[81]` | 80 chars + null terminator |
    | `ObservableCollection<T>[]` | `ARRAY` | Dynamic arrays with type tracking |
    | `T[]` | `ARRAY` | Fixed-size arrays |