// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RunCmd.cs" company="231216">
//   
// </copyright>
// <summary>
//   Defines the RunCmd type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace runcontroler
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.ServiceModel.Channels;

    public class RunCmd
    {
        private readonly Process proc = null;
        

        public RunCmd()
        {
            proc = new Process();
        }

        public void Exe(string cmd)
        {
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.FileName = "cmd.exe";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;

            //proc.OutputDataReceived += sortProcess_OutputDataReceived;
            proc.ErrorDataReceived += sortProcess_ErrortDataReceived;

            proc.Start();
            var cmdWriter = proc.StandardInput;
            
            if (!string.IsNullOrEmpty(cmd))
            {
                cmdWriter.WriteLine(cmd + "&exit");
            }

            cmdWriter.Close();
            //proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();
            proc.Close();
        }
    


        private void sortProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Log.Error("controler" + e.Data);
            }
        }

        private void sortProcess_ErrortDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Log.Error("Exe:" + e.Data);
            }
        }
    }
}
