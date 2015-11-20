﻿using System;

namespace RobotDotNet.FRC_Extension.Buttons
{
    public class SettingsButton : ButtonBase
    {
        public SettingsButton(Frc_ExtensionPackage package) : base(package, false, GuidList.guidFRC_ExtensionCmdSet, (int)PkgCmdIDList.cmdidSettings)
        {
        }

        public override void ButtonCallback(object sender, EventArgs e)
        {
            m_package.ShowOptionPage(typeof(SettingsPageGrid));
        }
    }
}