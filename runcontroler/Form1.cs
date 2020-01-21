// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Form1.cs" company="231216">
//   
// </copyright>
// <summary>
//   Defines the Form1 type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace runcontroler
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Linq;
    using System.Runtime.Remoting.Messaging;
    using System.Threading;
    using System.Windows.Forms;

    using CCWin;
    using CCWin.SkinClass;

    /// <summary>TODO The form 1.</summary>
    public partial class Form1 : Skin_Color
    {
        public Form1(string[] p)
        {
            InitializeComponent();
            _mtid = p[0].ToInt32();
            _gnpy = p[1];
            _packet = _gnpy;

        }

        private void Form1_Shown(object sender, EventArgs e)
        {


            Environment.SetEnvironmentVariable("maindir", $@"D:\developer\{_gnpy}\data\syslog");
            Directory.SetCurrentDirectory(@"D:\developer");

            _ip = get64segmentIP.LocalIp;

            Log.Info($@"任务编号：{_mtid} 渠道：{_gnpy} 当前ip：{_ip}");
            if (_ip == "192.168.64.105")
            {
                delDev();

                var setMysql = new Thread(SetModule) { IsBackground = true };
                setMysql.Start();
            }

            var run = new Thread(Main) { IsBackground = true };
            run.Start();
        }

        private static readonly Dictionary<string, string[]> DevDict = new Dictionary<string, string[]>();

        private static readonly Dictionary<string, string[]> TempDevDict = new Dictionary<string, string[]>(); //比较用，准确更新当前设备

        private readonly string _gnpy;

        private readonly string _packet;

        private string _ip = string.Empty;

        private readonly int _mtid;

        private bool _isWatch = true;
   

        private delegate void SetDgvSource(object[] dt);

        private void SetDgvSourceFunction(object[] dt)
        {
            if (skinDataGridView1.InvokeRequired)
            {
                SetDgvSource delegateSetSource = SetDgvSourceFunction;
                skinDataGridView1.Invoke(delegateSetSource, new Object[] { dt });
            }
            else
            {
                skinDataGridView1.Rows.Add(dt);
            }
        }


        /// <summary>
        /// 调用python委托
        /// </summary>
        /// <param name="devicesid"></param>
        /// <param name="scriptName"></param>
        /// <param name="id"></param>
        private delegate void AsyncMethodCaller(string devicesid, string scriptName, string scriptCn, out string id, out string script);


        /// <summary>
        /// 调用python启动
        /// </summary>
        /// <param name="devicesid"></param>
        /// <param name="scriptName"></param>
        /// <param name="scriptCn"></param>
        /// <param name="id"></param>
        /// <param name="script"></param>
        private void StartPython(string devicesid, string scriptName, string scriptCn, out string id, out string script)
        {
            var runcmd = new RunCmd();
            id = devicesid;
            script = scriptName;
            runcmd.Exe($@"python D:\developer\{_gnpy}\main\controler.py {devicesid} {scriptName} {scriptCn}");

        }


        /// <summary>
        /// 设置可执行设备列表
        /// </summary>
        private void SetDevList()
        {

            TempDevDict.Clear();
            var tables = DevicesMysql();

            foreach (var devId in AdbHelp.GetDevList())
            {
                try
                {
                    if (AdbHelp.GetBattleLev(devId) > 40)
                    {
                        TempDevDict.Add(devId, ",".Split(','));

                        if (DevDict.ContainsKey(devId) == false)
                        {
                            DevDict.Add(devId, ",".Split(','));
                            Log.Info($@"检测到设备连接 {devId}");
                        }

                        if (tables.Select($"ID = '{devId}'").Length == 0)
                        {
                            InsertDevice(devId);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"setDevList:{e}");
                }
            }

        }


        #region 运行controler

        private void Main()
        {

            while (_isWatch)
            {
                SetDevList();

                try
                {
                    for (var count = 0; count < DevDict.Count; count++)
                    {
                        var element = DevDict.ElementAt(count);
                        var key = element.Key;

                        if (TempDevDict.ContainsKey(key) == false)
                        {
                            if (element.Value != ",".Split(','))
                            {
                                //当前有脚本在执行
                                UpdateOffline(element.Value[0]);
                                //生成报告
                                DirFile.CreateFile($@"D:\developer\{_gnpy}\data\result\excel\{_ip}@{key}{element.Value[1]}测试总报告.xlsx");
                                Log.Info($@"检测到设备断开连接 {key} {element.Value[0]}已主动生成报告");
                            }
                            else
                            {
                                Log.Info($@"检测到设备断开连接 {key} 当前没有正在执行脚本");
                            }

                            DevDict.Remove(key);
                            deleteDevice(key);

                        }

                        else
                        {
                            var scriptName = SelectScriptName(key);
                            var selectScriptCn = scriptName[0];
                            var selectScriptEn = scriptName[1];

                            if (selectScriptEn == null)
                            {
                                // 数据库中不存在未执行脚本
                                continue;
                            }

                            if (element.Value[0] == string.Empty)
                            {
                                // 当前设备空闲    

                                object[] values = { "python", "controle.py", key, selectScriptEn, selectScriptCn, string.Empty};
                                SetDgvSourceFunction(values);

                                Updatemobile(key, selectScriptEn);
                                Updateflog(1, selectScriptEn);

                                DevDict[key] = $"{selectScriptEn},{selectScriptCn}".Split(',');
                                var caller = new AsyncMethodCaller(StartPython);
                                caller.BeginInvoke(key, selectScriptEn, selectScriptCn, out _, out _, CallbackMethod, null);
                                Log.Info($@"python controle.py {key} {selectScriptEn} {selectScriptCn}");
                            }
                        }
                    }

                    if (RunEnd())
                    {
                        _isWatch = false;
                        break;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString(), @"main");
                    Log.Error("main:" + e);
                }
            }

            Log.Info(@"循环结束，关闭程序");
            Application.Exit();
        }

        /// <summary>
        /// 异步回调函数
        /// </summary>
        /// <param name="ar"></param>
        private void CallbackMethod(IAsyncResult ar)
        {
            try
            {
                string id;
                string script;

                var result = (AsyncResult)ar;
                var caller = (AsyncMethodCaller)result.AsyncDelegate;
                caller.EndInvoke(out id, out script, ar);
                Console.WriteLine(id);
                DevDict[id] = ",".Split(',');
                Updateflog(3, script);
                Updateflog(3, script);
                // 设置设备状态为0
                setDevIdle(id, 0);
                Log.Info($@"成功回调 {id} {script}");
            }
            catch (Exception e)
            {
                Log.Error("CallbackMethod:" + e);
            }

        }


        #endregion


        /// <summary>返回脚本名称, mobile is null</summary>
        /// <param name="dev">The dev.</param>
        /// <returns>string[cn,en]</returns>
        private string[] SelectScriptName(string dev = "")
        {
            var nameStrings = new string[2];
            try
            {
                DataTable result;

                if (dev != string.Empty)
                {
                    result = SqlHelper.ExecuteDataSet(
                        CommandType.Text,
                        $"select top 1 CHscript,ENscript from MobileScriptModule where GNPY='{_gnpy}' and flog={_mtid} and PacketChannel='{_packet}' and mobile = '{dev}' order by SID",
                        new List<SqlParameter>().ToArray()).Tables[0];
                }
                else
                {
                    result = SqlHelper.ExecuteDataSet(
                        CommandType.Text,
                        $"select top 1 CHscript,ENscript from MobileScriptModule where GNPY='{_gnpy}' and flog={_mtid} and PacketChannel='{_packet}' and mobile is null order by SID",
                        new List<SqlParameter>().ToArray()).Tables[0];
                }

                nameStrings[0] = result.Rows[0][0].ToString();
                nameStrings[1] = result.Rows[0][1].ToString();
                return nameStrings;
            }
            catch (Exception)
            {
                return nameStrings;
            }

        }


        #region 修改脚本状态标志

        private void Updateflog(int flog, string scriptEn)
        {
            try
            {
                SqlHelper.ExecteNonQuery(
                    CommandType.Text,
                    $"update MobileScriptModule set flog= {flog} where ENscript='{scriptEn}' and PacketChannel='{_packet}'",
                    new List<SqlParameter>().ToArray());
            }
            catch (Exception e)
            {
                Log.Error("updateflog:" + e);
            }

        }

        /// <summary>修改离线设备mysql状态</summary>
        /// <param name="scriptEn">The script EN.</param>
        private void UpdateOffline(string scriptEn)
        {
            SqlHelper.ExecteNonQuery(
                CommandType.Text,
                $"update MobileScriptModule set flog= 3,uploadflog=2 where ENscript='{scriptEn}' and PacketChannel='{_packet}'",
                new List<SqlParameter>().ToArray());
        }

        #endregion

        #region 更新正在跑的脚本的ip和设备id

        private void Updatemobile(string devId, string scriptEn)
        {

            SqlHelper.ExecteNonQuery(
                CommandType.Text,
                $"update MobileScriptModule set mobile='{devId}' ,ip = '{_ip}' where ENscript='{scriptEn}' and PacketChannel='{_packet}'",
                new List<SqlParameter>().ToArray());
        }

        #endregion

        #region 执行完成判断

        private bool RunEnd()
        {
            var allRest = SqlHelper.ExecuteDataSet(
                CommandType.Text,
                $"select count(*) from MobileScriptModule where flog = {_mtid} and PacketChannel = '{_packet}'",
                new List<SqlParameter>().ToArray()).Tables[0].Rows[0][0].ToString();

            var myRest = SqlHelper.ExecuteDataSet(
                CommandType.Text,
                $"select count(*) from MobileScriptModule where flog = 1 and ip = '{_ip}' and PacketChannel = '{_packet}'",
                new List<SqlParameter>().ToArray()).Tables[0].Rows[0][0].ToString();

            return allRest == "0" && myRest == "0";
        }

        #endregion

        /// <summary>
        /// 获取一台空闲设备
        /// </summary>
        /// <returns></returns>
        private string getDeviceIdle()
        {
            try
            {
                return SqlHelper.ExecuteDataSet(
                    CommandType.Text,
                    "select top 1 ID from DeviceIdle where idle = 0 order by ID",
                    new List<SqlParameter>().ToArray()).Tables[0].Rows[0][0].ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }


        /// <summary>
        /// mysql 获取所有设备
        /// </summary>
        /// <returns></returns>
        private DataTable DevicesMysql()
        {
            return SqlHelper.ExecuteDataSet(
                CommandType.Text,
                "select ID from DeviceIdle",
                new List<SqlParameter>().ToArray()).Tables[0];
        }

        /// <summary>
        /// mysql插入一台设备
        /// </summary>
        /// <param name="id"></param>
        private void InsertDevice(string id)
        {
            SqlHelper.ExecuteDataSet(
                CommandType.Text,
                $"insert into DeviceIdle (id,idle) VALUES ('{id}',0)",
                new List<SqlParameter>().ToArray());
        }

        /// <summary>
        /// mysql移除一台设备
        /// </summary>
        /// <param name="id"></param>
        private void deleteDevice(string id)
        {
            SqlHelper.ExecuteDataSet(
                CommandType.Text,
                $"DELETE FROM DeviceIdle where id = '{id}'",
                new List<SqlParameter>().ToArray());
        }

        /// <summary>
        /// 清除设备信息
        /// </summary>
        private void delDev()
        {
            SqlHelper.ExecuteDataSet(
                CommandType.Text,
                "DELETE FROM DeviceIdle",
                new List<SqlParameter>().ToArray());
        }

        /// <summary>设置设备状态</summary>
        /// <param name="dev"></param>
        /// <param name="idle">The idle.</param>
        private void setDevIdle(string dev, int idle)
        {
            SqlHelper.ExecuteDataSet(
                CommandType.Text,
                $"update DeviceIdle set idle = {idle} where ID = '{dev}'",
                new List<SqlParameter>().ToArray());
        }


        /// <summary>
        /// 循环空闲设备插入模块表后改变设备状态
        /// </summary>
        private void SetModule()
        {
            while (_isWatch)
            {
                try
                {
                    var devId = getDeviceIdle();
                    var scriptName = SelectScriptName()[1];

                    if (devId != string.Empty && scriptName != null)
                    {
                        SqlHelper.ExecuteDataSet(
                            CommandType.Text,
                            $"UPDATE MobileScriptModule SET mobile = '{devId}' where GNPY='{_gnpy}' and flog= {_mtid} and PacketChannel='{_packet}' and ENscript = '{scriptName}'",
                            new List<SqlParameter>().ToArray());

                        setDevIdle(devId, 1);
                        Log.Info($@" {devId} 设备分配脚本 {scriptName}");
                    }
                }
                catch (Exception e)
                {
                    Log.Error("setModule:" + e);
                }
            }
        }
    }
}

