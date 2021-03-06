﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;
using RobotDotNet.FRC_Extension.SettingsPages;

namespace RobotDotNet.FRC_Extension.RoboRIOCode
{
    public enum ConnectionType
    {
        USB,
        MDNS,
        IP,
        None
    }

    public enum ConnectionUser
    {
        Admin,
        LvUser
    }

    public class ConnectionReturn
    {
        public ConnectionType ConnectionType { get; private set; }
        public string ConnectionIp { get; private set; }
        public bool Success { get; private set; }

        public ConnectionReturn(ConnectionType type, string ip, bool success)
        {
            ConnectionType = type;
            ConnectionIp = ip;
            Success = success;
        }
    }


    public static class RoboRIOConnection
    {


        public const string RoboRioMdnsFormatString = "roborio-{0}-FRC.local";
        public const string RoboRioUSBIp = "172.22.11.2";
        public const string RoboRioIpFormatString = "10.{0}.{1}.2";

        private static ConnectionInfo s_adminConnectionInfo;
        private static ConnectionInfo s_lvUserConnectionInfo;

        public static ConnectionInfo AdminConnectionInfo => s_adminConnectionInfo;
        public static ConnectionInfo LvUserConnectionInfo => s_lvUserConnectionInfo;

        public static async Task<ConnectionReturn> CheckConnection(string teamNumberS)
        {
            return await CheckConnection(teamNumberS, TimeSpan.FromSeconds(2));
        }

        public static async Task<ConnectionReturn> CheckConnection(string teamNumberS, TimeSpan timeout)
        {
            int teamNumber;
            int.TryParse(teamNumberS, out teamNumber);


            if (teamNumber < 0)
            {
                teamNumber = 0;
            }
            string roboRioMDNS = string.Format(RoboRioMdnsFormatString, teamNumber);
            string roboRIOIP = string.Format(RoboRioIpFormatString, teamNumber / 100, teamNumber % 100);

            if (await GetWorkingConnectionInfo(roboRioMDNS, timeout))
            {
                return new ConnectionReturn(ConnectionType.MDNS, roboRioMDNS, true);
            }
            else if (await GetWorkingConnectionInfo(RoboRioUSBIp, timeout))
            {
                return new ConnectionReturn(ConnectionType.USB, RoboRioUSBIp, true);
            }
            else if (await GetWorkingConnectionInfo(roboRIOIP, timeout))
            {
                return new ConnectionReturn(ConnectionType.IP, roboRIOIP, true);
            }
            s_lvUserConnectionInfo = null;
            s_adminConnectionInfo = null;
            return null;
        }

        private static async Task<bool> GetWorkingConnectionInfo(string ip, TimeSpan timeout)
        {

            //User auth method
            KeyboardInteractiveAuthenticationMethod authMethod = new KeyboardInteractiveAuthenticationMethod("lvuser");
            PasswordAuthenticationMethod pauth = new PasswordAuthenticationMethod("lvuser", "");

            authMethod.AuthenticationPrompt += (sender, e) =>
            {
                foreach (
                    AuthenticationPrompt p in
                        e.Prompts.Where(
                            p => p.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1))
                {
                    p.Response = "";
                }
            };

            //Admin Auth Method
            KeyboardInteractiveAuthenticationMethod authMethodAdmin = new KeyboardInteractiveAuthenticationMethod("admin");
            PasswordAuthenticationMethod pauthAdmin = new PasswordAuthenticationMethod("admin", "");

            authMethodAdmin.AuthenticationPrompt += (sender, e) =>
            {
                foreach (
                    AuthenticationPrompt p in
                        e.Prompts.Where(
                            p => p.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1))
                {
                    p.Response = "";
                }
            };

            s_lvUserConnectionInfo = new ConnectionInfo(ip, "lvuser", pauth, authMethod) { Timeout = timeout };


            s_adminConnectionInfo = new ConnectionInfo(ip, "admin", pauthAdmin, authMethodAdmin) { Timeout = timeout };
            using (SshClient zeroConfClient = new SshClient(s_lvUserConnectionInfo))
            {
                try
                {
                    await Task.Run(() => zeroConfClient.Connect());
                    return true;
                }
                catch (SocketException)
                {
                    return false;
                }
                catch (SshOperationTimeoutException)
                {
                    return false;
                }
            }
        }

        public static async Task<Dictionary<string, SshCommand>> RunCommands(string[] commands, ConnectionUser user)
        {
            ConnectionInfo connectionInfo;
            switch (user)
            {
                case ConnectionUser.Admin:
                    connectionInfo = s_adminConnectionInfo;
                    break;
                case ConnectionUser.LvUser:
                    connectionInfo = s_lvUserConnectionInfo;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(user), user, null);
            }

            Dictionary<string, SshCommand> retCommands = new Dictionary<string, SshCommand>();
            using (SshClient ssh = new SshClient(connectionInfo))
            {
                try
                {
                    await Task.Run(() => ssh.Connect());
                }
                catch (SshOperationTimeoutException)
                {
                    return null;
                }
                var settings = SettingsProvider.ExtensionSettingsPage;
                bool verbose = settings.Verbose || settings.DebugMode;
                foreach (string s in commands)
                {
                    if (verbose)
                    {
                        OutputWriter.Instance.WriteLine($"Running command: {s}");
                    }
                    await Task.Run(() =>
                    {
                        var x = ssh.RunCommand(s);

                        retCommands.Add(s, x);
                    });
                }
            }
            return retCommands;
        }

        public static async Task<SshCommand> RunCommand(string command, ConnectionUser user)
        {
            ConnectionInfo connectionInfo;
            switch (user)
            {
                case ConnectionUser.Admin:
                    connectionInfo = s_adminConnectionInfo;
                    break;
                case ConnectionUser.LvUser:
                    connectionInfo = s_lvUserConnectionInfo;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(user), user, null);
            }

            using (SshClient ssh = new SshClient(connectionInfo))
            {
                try
                {
                    await Task.Run(() => ssh.Connect());
                }
                catch (SshOperationTimeoutException)
                {
                    return null;
                }
                var settings = SettingsProvider.ExtensionSettingsPage;
                bool verbose = settings.Verbose || settings.DebugMode;
                if (verbose)
                {
                    OutputWriter.Instance.WriteLine($"Running command: {command}");
                }
                return await Task.Run(() => ssh.RunCommand(command));
            }
        }

        public static async Task<bool> ReceiveFile(string remoteFile, Stream receiveStream, ConnectionUser user)
        {
            ConnectionInfo connectionInfo;
            switch (user)
            {
                case ConnectionUser.Admin:
                    connectionInfo = s_adminConnectionInfo;
                    break;
                case ConnectionUser.LvUser:
                    connectionInfo = s_lvUserConnectionInfo;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(user), user, null);
            }

            if (receiveStream == null || !receiveStream.CanWrite)
            {
                return false;
            }

            if (connectionInfo == null) return false;
            using (ScpClient scp = new ScpClient(connectionInfo))
            {
                try
                {
                    await Task.Run(() => scp.Connect());
                }
                catch (SshOperationTimeoutException)
                {
                    return false;
                }
                var settings = SettingsProvider.ExtensionSettingsPage;
                bool verbose = settings.Verbose || settings.DebugMode;
                if (verbose)
                {
                    OutputWriter.Instance.WriteLine($"Receiving File: {remoteFile}");
                }
                try
                {
                    await Task.Run(() => scp.Download(remoteFile, receiveStream));
                }
                catch (SshException)
                {
                    return false;
                }
            }
            return true;
        }

        public static async Task<bool> DeployFile(Stream file, string deployLocation, ConnectionUser user)
        {
            ConnectionInfo connectionInfo;
            switch (user)
            {
                case ConnectionUser.Admin:
                    connectionInfo = s_adminConnectionInfo;
                    break;
                case ConnectionUser.LvUser:
                    connectionInfo = s_lvUserConnectionInfo;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(user), user, null);
            }

            if (connectionInfo == null) return false;
            using (ScpClient scp = new ScpClient(connectionInfo))
            {
                try
                {
                    await Task.Run(() => scp.Connect());
                }
                catch (SshOperationTimeoutException)
                {
                    return false;
                }
                var settings = SettingsProvider.ExtensionSettingsPage;
                bool verbose = settings.Verbose || settings.DebugMode;
                if (verbose)
                {
                    OutputWriter.Instance.WriteLine($"Deploying File: {deployLocation}");
                }
                await Task.Run(() => scp.Upload(file, deployLocation));
            }
            return true;
        }

        public static async Task<bool> DeployFiles(IEnumerable<string> files, string deployLocation, ConnectionUser user)
        {
            ConnectionInfo connectionInfo;
            switch (user)
            {
                case ConnectionUser.Admin:
                    connectionInfo = s_adminConnectionInfo;
                    break;
                case ConnectionUser.LvUser:
                    connectionInfo = s_lvUserConnectionInfo;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(user), user, null);
            }

            if (connectionInfo == null) return false;
            using (ScpClient scp = new ScpClient(connectionInfo))
            {
                try
                {
                    await Task.Run(() => scp.Connect());
                }
                catch (SshOperationTimeoutException)
                {
                    return false;
                }
                var settings = SettingsProvider.ExtensionSettingsPage;
                bool verbose = settings.Verbose || settings.DebugMode;
                foreach (FileInfo fileInfo in from string s in files where File.Exists(s) select new FileInfo(s))
                {
                    if (verbose)
                    {
                        OutputWriter.Instance.WriteLine($"Deploying File: {fileInfo.Name}");
                    }
                    await Task.Run(() => scp.Upload(fileInfo, deployLocation));
                }
            }
            return true;
        }
    }
}
