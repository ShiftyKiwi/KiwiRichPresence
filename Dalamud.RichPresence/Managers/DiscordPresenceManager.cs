using System;
using System.Diagnostics;
using System.IO;
using Dalamud.Utility;
using DiscordRPC;
using DiscordRPC.Logging;

namespace Dalamud.RichPresence.Managers
{
    internal class DiscordPresenceManager : IDisposable
    {
        private const string DISCORD_CLIENT_ID = "478143453536976896";

        private FileInfo RpcBridgePath => new(
            Path.Combine(
                RichPresencePlugin.DalamudPluginInterface.AssemblyLocation.Directory?.FullName
                    ?? throw new InvalidOperationException("Unable to resolve plugin assembly directory."),
                "Resources",
                "binaries",
                "WineRPCBridge.exe"));

        private DiscordRpcClient RpcClient = null!;
        private Process? ownedBridgeProcess;
        private bool bridgeStartedByPlugin;

        internal DiscordPresenceManager()
        {
            this.CreateClient();
            this.ApplyRuntimeConfig(RichPresencePlugin.RichPresenceConfig);
        }

        private void CreateClient()
        {
            if (this.RpcClient is null || this.RpcClient.IsDisposed)
            {
                this.RpcClient = new DiscordRpcClient(DISCORD_CLIENT_ID)
                {
                    SkipIdenticalPresence = true,
                    Logger = new ConsoleLogger { Level = LogLevel.Warning },
                };
            }

            if (!this.RpcClient.IsInitialized)
            {
                this.RpcClient.Initialize();
            }
        }

        public void ApplyRuntimeConfig(RichPresence.Configuration.RichPresenceConfig config)
        {
            if (!Util.IsWine())
            {
                return;
            }

            if (config.RPCBridgeEnabled)
            {
                this.StartWineRpcBridge();
            }
            else
            {
                this.StopOwnedWineRpcBridge();
            }
        }

        public void StartWineRpcBridge()
        {
            try
            {
                if (this.ownedBridgeProcess is not null && !this.ownedBridgeProcess.HasExited)
                {
                    return;
                }

                var wineBridge = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(this.RpcBridgePath.Name));
                if (wineBridge.Length > 0)
                {
                    RichPresencePlugin.PluginLog.Information($"Found existing RPC bridge process, PID: {wineBridge[0].Id}, not starting a new one.");
                    return;
                }

                RichPresencePlugin.PluginLog.Information($"Starting RPC bridge process: {this.RpcBridgePath.FullName}");
                this.ownedBridgeProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = this.RpcBridgePath.FullName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                })!;
                this.bridgeStartedByPlugin = true;
                RichPresencePlugin.PluginLog.Information($"Started RPC bridge process, PID: {this.ownedBridgeProcess.Id}");
            }
            catch (Exception e)
            {
                RichPresencePlugin.PluginLog.Error(e, "Error starting Wine bridge process.");
            }
        }

        public void StopOwnedWineRpcBridge()
        {
            if (!this.bridgeStartedByPlugin || this.ownedBridgeProcess is null)
            {
                return;
            }

            try
            {
                if (!this.ownedBridgeProcess.HasExited)
                {
                    this.ownedBridgeProcess.Kill();
                    RichPresencePlugin.PluginLog.Information($"Killed RPC bridge process: {this.ownedBridgeProcess.Id}");
                }
            }
            catch (Exception ex)
            {
                RichPresencePlugin.PluginLog.Error(ex, "Error stopping Wine bridge process.");
            }
            finally
            {
                this.ownedBridgeProcess.Dispose();
                this.ownedBridgeProcess = null;
                this.bridgeStartedByPlugin = false;
            }
        }

        public void SetPresence(DiscordRPC.RichPresence newPresence)
        {
            this.CreateClient();
            this.RpcClient.SetPresence(newPresence);
        }

        public void ClearPresence()
        {
            this.CreateClient();
            this.RpcClient.ClearPresence();
        }

        public void UpdatePresenceDetails(string details)
        {
            this.CreateClient();
            this.RpcClient.UpdateDetails(details);
        }

        public void UpdatePresenceStartTime(DateTime newStartTime)
        {
            this.CreateClient();
            this.RpcClient.UpdateStartTime(newStartTime);
        }

        public void Dispose()
        {
            this.StopOwnedWineRpcBridge();
            this.RpcClient?.Dispose();
        }
    }
}
