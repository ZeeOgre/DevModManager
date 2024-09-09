using System;
using System.Windows.Forms;
using WixSharp;
using WixSharp.UI.WPF;


namespace ZO.DMM.WixSharp
{
    internal class Program
    {
        static void Main()
        {
            var project = new ManagedProject("MyProduct",
                              new Dir(@"%ProgramFiles%\My Company\My Product",
                                  new File("Program.cs")));

            project.GUID = new Guid("515c247c-1b32-4385-88a1-979af9d00e2d");

            // project.ManagedUI = ManagedUI.DefaultWpf; // all stock UI dialogs

            //custom set of UI WPF dialogs
            project.ManagedUI = new ManagedUI();

            project.ManagedUI.InstallDialogs.Add<ZO.DMM.WixSharp.WelcomeDialog>()
                                            .Add<ZO.DMM.WixSharp.LicenceDialog>()
                                            .Add<ZO.DMM.WixSharp.FeaturesDialog>()
                                            .Add<ZO.DMM.WixSharp.InstallDirDialog>()
                                            .Add<ZO.DMM.WixSharp.ConfirmInstallDirDialog>()
                                            .Add<ZO.DMM.WixSharp.ProgressDialog>()
                                            .Add<ZO.DMM.WixSharp.ExitDialog>();

            project.ManagedUI.ModifyDialogs.Add<ZO.DMM.WixSharp.MaintenanceTypeDialog>()
                                           .Add<ZO.DMM.WixSharp.FeaturesDialog>()
                                           .Add<ZO.DMM.WixSharp.ProgressDialog>()
                                           .Add<ZO.DMM.WixSharp.ExitDialog>();

            //project.SourceBaseDir = "<input dir path>";
            //project.OutDir = "<output dir path>";

            project.BuildMsi();
        }
    }
}