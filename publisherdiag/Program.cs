
using Mono.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PubisherDiag
{
    using OpcPublisher;
    using System.Net;
    using System.Reflection;
    using static System.Console;

    public class Program
    {
        /// <summary>
        /// Logging object.
        /// </summary>
        public static Serilog.Core.Logger Logger = null;

        /// <summary>
        /// Interval in sec to show the diagnostic info.
        /// </summary>
        public static uint DiagnosticInterval { get; set; } = 0;

        /// <summary>
        /// Interval to show the end OPC Publisher log output.
        /// </summary>
        public static int ShowLastLogInterval { get; set; } = 0;

        /// <summary>
        /// Flag to enable showing OPC Publisher startup log output.
        /// </summary>
        public static bool ShowStartupLogSwitch { get; set; } = false;

        /// <summary>
        /// Sends an exit command to OPC Publisher.
        /// </summary>
        public static int SecondsTillExit { get; set; } = -1;

        /// <summary>
        /// Synchronous main method of the app.
        /// </summary>
        public static void Main(string[] args)
        {
            int exitCode = 0;
            try
            {
                MainAsync(args).Wait();
            }
            catch (Exception e)
            {
                if (!(e is TaskCanceledException))
                {
                    exitCode = 1;
                }
            }
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Asynchronous part of the main method of the app.
        /// </summary>
        public async static Task MainAsync(string[] args)
        {
            Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .MinimumLevel.Debug()
                .CreateLogger();

            Logger.Information($"OPC Publisher diagnostic tool");

            // command line options
            bool showHelp = false;
            string iotHubConnectionString = string.Empty;
            string iotHubPublisherDeviceName = string.Empty;
            string iotHubPublisherModuleName = string.Empty;

            OptionSet options = new OptionSet {
                { "h|help", "show this message and exit", h => showHelp = h != null },

                { "ic|iotHubConnectionString=", "IoTHub owner or service connectionstring", (string s) => iotHubConnectionString = s },
                { "id|iothubdevicename=", "IoTHub device name of the OPC Publisher", (string s) => iotHubPublisherDeviceName = s },
                { "im|iothubmodulename=", "IoT Edge module name of the OPC Publisher which runs in the IoT Edge device specified by id/iothubdevicename", (string s) => iotHubPublisherModuleName = s },

                { "di|diagnosticsinterval=", $"shows publisher diagnostic info at the specified interval in seconds\nDefault: {DiagnosticInterval}", (uint u) => DiagnosticInterval = u },
                { "sl|showlastlog=", $"shows last lines of OPC Publisher log output at the specified interval in seconds\nDefault: {ShowLastLogInterval}", (int i) => ShowLastLogInterval = i },
                { "ss|showstartuplog", $"shows startup log of OPC Publisher log output\nDefault: {ShowStartupLogSwitch}", b => ShowStartupLogSwitch = b != null },
                { "ea|exitapplication=", $"sends an exit command to OPC Publisher to exit in given number of seconds (ignores all other options)", (int i) => SecondsTillExit = i },
            };

            IList<string> extraArgs = null;
            try
            {
                extraArgs = options.Parse(args);
            }
            catch (OptionException e)
            {
                // initialize logging
                InitLogging();

                // show message
                Logger.Fatal(e, "Error in command line options");

                // show usage
                Usage(options, args);
                return;
            }

            // initialize logging
            InitLogging();

            // show usage if requested
            if (showHelp)
            {
                Usage(options, null);
                return;
            }

            // no extra options
            if (extraArgs.Count > 0)
            {
                for (int i = 1; i < extraArgs.Count; i++)
                {
                    Logger.Error("Error: Unknown option: {0}", extraArgs[i]);
                }
                Usage(options, args);
                return;
            }

            // sanity check parameters
            if (string.IsNullOrEmpty(iotHubConnectionString) || string.IsNullOrEmpty(iotHubPublisherDeviceName))
            {
                Logger.Fatal("For IoTHub communication an IoTHub connection string and the publisher devicename (and modulename) must be specified.");
                return;
            }
            Logger.Information($"IoTHub connectionstring: {iotHubConnectionString}");
            if (string.IsNullOrEmpty(iotHubPublisherModuleName))
            {
                Logger.Information($"OPC Publisher not running in IoT Edge.");
                Logger.Information($"IoTHub OPC Publisher device name: {iotHubPublisherDeviceName}");
            }
            else
            {
                Logger.Information($"OPC Publisher running as IoT Edge module.");
                Logger.Information($"IoT Edge device name: {iotHubPublisherDeviceName}");
                Logger.Information($"OPC Publisher module name: {iotHubPublisherModuleName}");
            }
            Logger.Information("");
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            // instantiate OPC Publisher interface
            _publisher = new Publisher(iotHubConnectionString, iotHubPublisherDeviceName, iotHubPublisherModuleName, ct);

            // validate OPC Publisher version
            if (!await ValidatePublisherVersionAsync(ct))
            {
                Environment.Exit(1);
            }

            // if there is no option given, then we default to diagnostic output with an interval of 30 sec
            if (!ShowStartupLogSwitch && ShowLastLogInterval == 0 && DiagnosticInterval == 0 && SecondsTillExit == -1)
            {
                DiagnosticInterval = 30;
                Logger.Information($"Diagnostic interval set to {DiagnosticInterval} seconds");
            }

            // if exit OPC Publisher was requested, send the command and we are done
            if (SecondsTillExit >= 0)
            {
                bool result = _publisher.ExitApplicationAsync(SecondsTillExit).Result;
                Environment.Exit(result ? 0 : 1);
            }

            // show startup log if requested and we are done
            if (ShowStartupLogSwitch)
            {
                await ShowStartupLogAsync(ct);
                Environment.Exit(0);
            }

            // allow canceling the application
            var quitEvent = new ManualResetEvent(false);
            try
            {
                Console.CancelKeyPress += (sender, eArgs) =>
                {
                    quitEvent.Set();
                    eArgs.Cancel = true;
                    cts.Cancel();
                };
            }
            catch
            {
            }

            // stop on user request
            Logger.Information("");
            Logger.Information("Press CTRL-C to quit.");

            // process show log option
            if (ShowLastLogInterval > 0)
            {
                Logger.Information("");
                Logger.Information($"Show log interval set to {ShowLastLogInterval} seconds");
                await Task.Run(() => ShowLogAsync(ct));
            }

            if (DiagnosticInterval > 0)
            {
                Logger.Information("");
                Logger.Information($"Diagnostic interval is {DiagnosticInterval} seconds");
                await Task.Run(() => ShowDiagnosticInfoAsync(ct));
            }

            quitEvent.WaitOne(Timeout.Infinite);

            Logger.Information("");
            Logger.Information("");
            cts.Cancel();
            Logger.Information($"Done. Exiting....");
            return;
        }

        /// <summary>
        /// Validates if the publisher is there and supports the method calls we need.
        /// </summary>
        /// <returns></returns>
        private static async Task<bool> ValidatePublisherVersionAsync(CancellationToken ct)
        {
            // fetch the information
            GetInfoMethodResponseModel info = await _publisher.GetInfoAsync(ct);
            if (info == null)
            {
                return false;
            }

            Logger.Information($"OPC Publisher V{info.VersionMajor}.{info.VersionMinor}.{info.VersionPatch} was detected.");
            return true;
        }

        /// <summary>
        /// Task to fetch and display diagnostic info.
        /// </summary>
        /// <returns></returns>
        private static async Task ShowDiagnosticInfoAsync(CancellationToken ct)
        {
            do
            {
                // fetch the diagnostic data
                DiagnosticInfoMethodResponseModel diagnosticInfo = await _publisher.GetDiagnosticInfoAsync(ct);

                // display the diagnostic data
                if (diagnosticInfo != null)
                {
                    Logger.Information("==========================================================================");
                    Logger.Information($"OpcPublisher started @ {diagnosticInfo.PublisherStartTime})");
                    Logger.Information("---------------------------------");
                    Logger.Information($"OPC sessions: {diagnosticInfo.NumberOfOpcSessions}");
                    Logger.Information($"connected OPC sessions: {diagnosticInfo.NumberOfConnectedOpcSessions}");
                    Logger.Information($"connected OPC subscriptions: {diagnosticInfo.NumberOfConnectedOpcSubscriptions}");
                    Logger.Information($"OPC monitored items: {diagnosticInfo.NumberOfMonitoredItems}");
                    Logger.Information("---------------------------------");
                    Logger.Information($"monitored items queue bounded capacity: {diagnosticInfo.MonitoredItemsQueueCapacity}");
                    Logger.Information($"monitored items queue current items: {diagnosticInfo.MonitoredItemsQueueCount}");
                    Logger.Information($"monitored item notifications enqueued: {diagnosticInfo.EnqueueCount}");
                    Logger.Information($"monitored item notifications enqueue failure: {diagnosticInfo.EnqueueFailureCount}");
                    Logger.Information("---------------------------------");
                    Logger.Information($"messages sent to IoTHub: {diagnosticInfo.SentMessages}");
                    Logger.Information($"last successful msg sent @: {diagnosticInfo.SentLastTime}");
                    Logger.Information($"bytes sent to IoTHub: {diagnosticInfo.SentBytes}");
                    Logger.Information($"avg msg size: {diagnosticInfo.SentBytes / (diagnosticInfo.SentMessages == 0 ? 1 : diagnosticInfo.SentMessages)}");
                    Logger.Information($"msg send failures: {diagnosticInfo.FailedMessages}");
                    Logger.Information($"messages too large to sent to IoTHub: {diagnosticInfo.TooLargeCount}");
                    Logger.Information($"times we missed send interval: {diagnosticInfo.MissedSendIntervalCount}");
                    Logger.Information($"number of events: {diagnosticInfo.NumberOfEvents}");
                    Logger.Information("---------------------------------");
                    Logger.Information($"current working set in MB: {diagnosticInfo.WorkingSetMB}");
                    Logger.Information($"--si setting: {diagnosticInfo.DefaultSendIntervalSeconds}");
                    Logger.Information($"--ms setting: {diagnosticInfo.HubMessageSize}");
                    Logger.Information($"--ih setting: {diagnosticInfo.HubProtocol}");
                    Logger.Information("==========================================================================");
                    Logger.Information("");
                }

                // wait for next interval
                await Task.Delay((int)DiagnosticInterval * 1000, ct);

            } while (!ct.IsCancellationRequested);
        }

        /// <summary>
        /// Task to fetch and display startup log.
        /// </summary>
        /// <returns></returns>
        private static async Task ShowStartupLogAsync(CancellationToken ct)
        {
            // fetch the log
            DiagnosticLogMethodResponseModel diagnosticLog = await _publisher.GetDiagnosticLogAsync(ct);

            // process log request
            Logger.Information("");
            Logger.Information($"Messages fetched from startup log buffer: {diagnosticLog.StartupLogMessageCount}");
            Logger.Information($"Startup log from OPC Publisher ===========================================");
            if (diagnosticLog.StartupLog != null)
            {
                foreach (var line in diagnosticLog.StartupLog)
                {
                    WriteLine($">>>> {line}");
                }
            }
            else
            {
                Logger.Warning($"There was no startup log recoreded yet.");
            }
            Logger.Information($"End of OPC Publisher startup log =========================================");
            Logger.Information("");
        }

        /// <summary>
        /// Task to fetch and display diagnostic info.
        /// </summary>
        /// <returns></returns>
        private static async Task ShowLogAsync(CancellationToken ct)
        {
            bool first = true;
            do
            {
                // fetch the log
                DiagnosticLogMethodResponseModel diagnosticLog = await _publisher.GetDiagnosticLogAsync(ct);

                // process log request
                if (first)
                {
                    Logger.Information("");
                    Logger.Information($"Messages missed since last time: {diagnosticLog.MissedMessageCount}");
                    Logger.Information($"Max message capacity of log buffer: {diagnosticLog.LogMessageCount}");
                    first = false;
                }

                if (diagnosticLog.Log != null)
                {
                    Logger.Information("");
                    Logger.Information($"Messages fetched from log buffer: {diagnosticLog.Log.Length}");
                    Logger.Information($"Log from OPC Publisher ===================================================");
                    foreach (var line in diagnosticLog.Log)
                    {
                        WriteLine($">>>> {line}");
                    }
                    Logger.Information($"End of OPC Publisher log =================================================");
                }
                else
                {
                    Logger.Information($"There are no new messages in the log.");
                }
                Logger.Information("");

                // wait for next interval
                await Task.Delay((int)ShowLastLogInterval * 1000, ct);

            } while (!ct.IsCancellationRequested);
        }

        /// <summary>
        /// Initialize logging.
        /// </summary>
        private static void InitLogging()
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();

            // set the log level
            switch (_logLevel)
            {
                case "fatal":
                    loggerConfiguration.MinimumLevel.Fatal();
                    break;
                case "error":
                    loggerConfiguration.MinimumLevel.Error();
                    break;
                case "warn":
                    loggerConfiguration.MinimumLevel.Warning();
                    break;
                case "info":
                    loggerConfiguration.MinimumLevel.Information();
                    break;
                case "debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    break;
                case "verbose":
                    loggerConfiguration.MinimumLevel.Verbose();
                    break;
            }

            // set logging sinks
            loggerConfiguration.WriteTo.Console();

            Logger.Information($"Log level is: {_logLevel}");
            return;
        }

        /// <summary>
        /// Usage message.
        /// </summary>
        private static void Usage(Mono.Options.OptionSet options, string[] args)
        {
            Logger.Information("");

            // show the args
            if (args != null)
            {
                string commandLine = string.Empty;
                foreach (var arg in args)
                {
                    commandLine = commandLine + " " + arg;
                }
                Logger.Information($"Command line: {commandLine}");
            }

            // show usage
            Logger.Information("");
            Logger.Information("");
            Logger.Information($"Usage: iot-edge-opc-publisher-diagnostics [<options>]");
            Logger.Information("");
            Logger.Information("If no options are given, diagnostic info is shown with an interval of 30 seconds.");
            Logger.Information("");

            // output the options
            Logger.Information("Options:");
            StringBuilder stringBuilder = new StringBuilder();
            StringWriter stringWriter = new StringWriter(stringBuilder);
            options.WriteOptionDescriptions(stringWriter);
            string[] helpLines = stringBuilder.ToString().Split("\r\n");
            foreach (var line in helpLines)
            {
                Logger.Information(line);
            }
        }

        private static string _logLevel = "info";
        private static Publisher _publisher = null;
    }
}
