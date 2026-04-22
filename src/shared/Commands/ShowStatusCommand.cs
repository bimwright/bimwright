using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Bimwright.Rvt.Plugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowStatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (App.Instance == null) return Result.Failed;

            var transport = App.Instance.Transport;
            var running = App.Instance.IsTransportRunning && transport != null;
            var connectionInfo = running ? (transport.ConnectionInfo ?? string.Empty) : string.Empty;

            var connectionKind = running ? "Unknown" : "Not running";
            var clipboardValue = string.Empty;
            var connectionValue = "N/A";

            if (!string.IsNullOrWhiteSpace(connectionInfo))
            {
                if (connectionInfo.StartsWith("TCP:", System.StringComparison.OrdinalIgnoreCase))
                {
                    connectionKind = "TCP";
                    clipboardValue = connectionInfo.Substring(4);
                    connectionValue = clipboardValue;
                }
                else if (connectionInfo.StartsWith("Pipe:", System.StringComparison.OrdinalIgnoreCase))
                {
                    connectionKind = "Named Pipe";
                    clipboardValue = connectionInfo.Substring(5);
                    connectionValue = clipboardValue;
                }
                else
                {
                    connectionValue = connectionInfo;
                }
            }

            var copiedLabel = connectionKind == "TCP" ? "Port" : "Pipe name";
            var copySucceeded = false;
            if (!string.IsNullOrEmpty(clipboardValue))
            {
                try
                {
                    System.Windows.Clipboard.SetText(clipboardValue);
                    copySucceeded = true;
                }
                catch
                {
                    copySucceeded = false;
                }
            }

            var client = !running
                ? "Not running"
                : transport.IsClientConnected ? "Client connected" : "Waiting for client";
            var lastCmd = running
                ? transport.LastCommandTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unavailable"
                : "Unavailable";
            var copiedMessage = string.IsNullOrEmpty(clipboardValue)
                ? "Nothing was copied to clipboard."
                : copySucceeded
                    ? $"{copiedLabel} copied to clipboard: {clipboardValue}"
                    : $"Unable to copy {copiedLabel.ToLowerInvariant()} to clipboard. Value: {clipboardValue}";

            var td = new TaskDialog("BIMwright Status")
            {
                CommonButtons = TaskDialogCommonButtons.Ok,
                MainInstruction = $"MCP is {(running ? "running" : "not running")}",
                MainContent =
                    $"Connection kind: {connectionKind}\n" +
                    $"Connection value: {connectionValue}\n" +
                    $"Client: {client}\n" +
                    $"Last command time: {lastCmd}\n\n" +
                    copiedMessage,
                MainIcon = running ? TaskDialogIcon.TaskDialogIconInformation : TaskDialogIcon.TaskDialogIconWarning
            };
            td.Show();

            return Result.Succeeded;
        }
    }
}
