// --------------------------------------------------------------------------------------------------------------------
// <copyright file="adbHelp.cs" company="231216">
//   
// </copyright>
// <summary>
//   Defines the adbHelp type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------


using System.Windows.Forms;

namespace runcontroler
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using CCWin.SkinClass;

    using SharpAdbClient;

    public static class AdbHelp
    {
        private static string Run(string command, string dev = null)
        {
            if (dev != null)
            {
                command = $"-s {dev} {command}";
            }
            var startInfo =
                new ProcessStartInfo("adb.exe", command)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };

            var proce = Process.Start(startInfo);
            if (proce != null)
            {
                var reader = proce.StandardOutput;
                var output = reader.ReadToEnd();
                proce.WaitForExit();
                return output;
            }
            return null;
        }

        #region 运行"adb devices"命令（无需设备id）

        public static string RunAdbDevices()
        {
            return Run("devices -l");
        }

        #endregion

        #region 获取设备列表

        public static string[] GetDevList()
        {
            try
            {
                var devices = AdbClient.Instance.GetDevices();
                var devlist = new string[devices.Count];
                var i = 0;
                foreach (var device in devices)
                {
                    if (device.State.ToString() == "Online")
                    {
                        devlist[i] = device.Serial;
                        i = i + 1;
                    }
                }
                return devlist.Where(s => !string.IsNullOrEmpty(s)).ToArray();
                //return new string[0];
            }

            catch (Exception e)
            {
                return new string[0];
            }

        }

        #endregion

        #region 获取设备电池百分比

        public static int GetBattleLev(string dev)
        {
            
            var tempstr = Run("shell dumpsys battery", dev);
            var tempindexfo = tempstr.IndexOf("level:", StringComparison.Ordinal);

            return tempindexfo > 0 ? tempstr.Substring(tempindexfo + 7, 3).ToInt32() : 100;
        }

       #endregion
    }
}
