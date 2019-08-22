---
page_type: sample
description: "Industrial IoT OPC publisher diagnostics."
languages:
- csharp
products:
- azure
- azure-iot-hub
urlFragment: azure-iot-opc-publisher-dignostics
---

# iot-edge-opc-publisher-diagnostics

This .NET Core application allows to read diagnostic info from [OPC Publisher](https://github.com/Azure/iot-edge-opc-publisher) using IoTHub direct method calls.


## Features

The application is/can:
* Show diagnostic information with a recurring interval
* Show the startup log of OPC Publisher
* Show the tail of the log of OPC Publisher
* Trigger the OPC Publisher to exit


## Getting Started


### Prerequisites

The application requires .NET Core to be built and OPC Publisher deployed either standalone or as IoT Edge module at runtime.
The diagnostics functionality only works with OPC Publisher versions higher than 2.2.0.


### Installation

You need to compile the solution with Visual Studio and then run it.


### Quickstart

The application supports several command line options to control its functionality. 

Here the usage output:
        OPC Publisher diagnostic tool
        Log level is: info

        Usage: publisherdiag [<options>]

        If no options are given, diagnostic info is shown with an interval of 30 seconds.

        Options:
          -h, --help                 show this message and exit
              --ic, --iotHubConnectionString=VALUE
                                     IoTHub owner or service connectionstring
              --id, --iothubdevicename=VALUE
                                     IoTHub device name of the OPC Publisher
              --im, --iothubmodulename=VALUE
                                     IoT Edge module name of the OPC Publisher which
                                       runs in the IoT Edge device specified by id/
                                       iothubdevicename
              --di, --diagnosticsinterval=VALUE
                                     shows publisher diagnostic info at the specified
                                       interval in seconds
                                       Default: 0
              --sl, --showlastlog=VALUE
                                     shows last lines of OPC Publisher log output at
                                       the specified interval in seconds
                                       Default: 0
              --ss, --showstartuplog shows startup log of OPC Publisher log output
                                       Default: False
              --ea, --exitapplication=VALUE
                                     sends an exit command to OPC Publisher to exit in
                                       given number of seconds (ignores all other
                                       options)

If no options are specified, the diagnostic information is shown each 30 seconds. The options `exitapplication` and `showstartuplog` exit the application
after completed.
