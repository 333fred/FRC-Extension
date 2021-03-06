﻿using System.Collections.Generic;
using System.Threading.Tasks;
using RobotDotNet.FRC_Extension.RoboRIOCode;

namespace RobotDotNet.FRC_Extension.MonoCode
{
    public class MonoDeploy
    {
        private readonly DeployManager m_deployManager;
        private readonly MonoFile m_monoFile;
        private readonly string m_teamNumber;

        public MonoDeploy(string teamNumber, DeployManager deployManager, MonoFile monoFile)
        {
            m_deployManager = deployManager;
            m_teamNumber = teamNumber;
            m_monoFile = monoFile;
        }

        internal async Task DeployMono()
        {
            var writer = OutputWriter.Instance;
            writer.Clear();

            //Connect to RoboRIO
            writer.WriteLine("Attempting to Connect to RoboRIO");

            Task<bool> rioConnectionTask = m_deployManager.StartConnectionTask(m_teamNumber);
            Task delayTask = Task.Delay(10000);


            bool success = await m_monoFile.UnzipMonoFile();

            if (!success) return;

            //Successfully extracted files.

            writer.WriteLine("Waiting for Connection to Finish");
            if (await Task.WhenAny(rioConnectionTask, delayTask) == rioConnectionTask)
            {
                //Completed
                if (rioConnectionTask.Result)
                {
                    writer.WriteLine("Successfully Connected to RoboRIO");

                    List<string> deployFiles = m_monoFile.GetUnzippedFileList();

                    writer.WriteLine("Creating Opkg Directory");

                    await RoboRIOConnection.RunCommand($"mkdir -p {DeployProperties.RoboRioOpgkLocation}", ConnectionUser.Admin);

                    writer.WriteLine("Deploying Mono Files");

                    success = await RoboRIOConnection.DeployFiles(deployFiles, DeployProperties.RoboRioOpgkLocation, ConnectionUser.Admin);

                    if (!success)
                    {
                        return;
                    }

                    writer.WriteLine("Installing Mono");

                    var monoRet = await RoboRIOConnection.RunCommand(DeployProperties.OpkgInstallCommand, ConnectionUser.Admin);

                    //Check for success.
                    bool monoSuccess = await m_deployManager.CheckMonoInstall();

                    if (monoSuccess)
                    {
                        writer.WriteLine("Mono Installed Successfully");
                    }
                    else
                    {
                        writer.WriteLine("Mono not installed successfully. Please try again.");
                    }

                    writer.WriteLine("Cleaning up installation");
                    // Set allow realtime on Mono instance
                    await
                        RoboRIOConnection.RunCommand("setcap cap_sys_nice=pe /usr/bin/mono-sgen", ConnectionUser.Admin);

                    //Removing ipk files from the RoboRIO
                    await RoboRIOConnection.RunCommand($"rm -rf {DeployProperties.RoboRioOpgkLocation}", ConnectionUser.Admin);

                    writer.WriteLine("Done. You may now deploy code to your robot.");
                }
                else
                {
                    //Did not successfully connect
                    writer.WriteLine("Failed to Connect to RoboRIO. Exiting.");
                }
            }
            else
            {
                //Timedout
                writer.WriteLine("RoboRIO connection timedout. Exiting.");
            }
        }
    }
}
