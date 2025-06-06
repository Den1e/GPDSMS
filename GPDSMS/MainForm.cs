﻿using IWshRuntimeLibrary;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Media;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using static System.Collections.Specialized.BitVector32;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;


namespace GPDSMS
{
    public partial class MainForm : Form
    {

        public MainForm()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenSerialPort();
        }


        private SerialPort serialPort = new SerialPort();

        private String receiveMessage = "";

        public Queue<String> receiveMessages = new Queue<String>();

        public Object messagesLock = new Object();

        private String CenterNo = "";

        private bool isInSending = false;

        private Thread doMessageQueueThread;

        private Hashtable messagePagesMap = new System.Collections.Hashtable();

        System.Media.SoundPlayer soundPlayer;

        public void SetSerialPort()
        {
            string[] serialProtArray = SerialPort.GetPortNames();

            serialPort.PortName = SerialPortName;

            //波特率
            serialPort.BaudRate = 9600;

            //奇偶校验
            serialPort.Parity = Parity.None;

            //数据位
            serialPort.DataBits = 8;

            //停止位
            serialPort.StopBits = StopBits.One;

            //串口接收数据事件
            serialPort.DataReceived += OnReceiveData;
        }


        public void OpenSerialPort()
        {
            try
            {
                serialPort.Open();
            }catch(Exception ex)
            {
                sendToast("", ex.Message, 1);
            }

            //sendToast("", "设备已连接", 1);

        }

        public void CloseSerialPort()
        {
            try
            {
                serialPort.Close();
            }
            catch { }
        }

        public void SendDataMethod(byte[] data)
        {
            bool isOpen = serialPort.IsOpen;

            if (!isOpen)
            {
                OpenSerialPort();
            }
 
            serialPort.Write(data, 0, data.Length);

            Thread.Sleep(100);
        }

        public void SendDataMethodNoLock(String data)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    OpenSerialPort();
                }
                // 发送字符串
                serialPort.Write(data + "\r");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送失败: {ex.Message}");
            }
            finally
            {
                Thread.Sleep(100);
            }
        }

        public String SendDataMethod(String data)
        {
            notifyIcon1.Icon = Properties.Resources.phone_sound;

            try
            {
                if (!serialPort.IsOpen)
                {
                    OpenSerialPort();
                }

                int timeout = 0;


                // 如果还有没被处理的行，则强制等待
                while (receiveMessages.Count > 0)
                {
                    timeout += 1;
                    // 超过5秒没处理，放弃
                    if (timeout >= 50)
                    {
                        break;
                        throw new Exception("队列有未处理的行，被抽取前无法执行");
                    }

                    Thread.Sleep(100);
                }

                // 如果执行成功，必定会读到OK

                StringBuilder sb = new StringBuilder();

                lock (messagesLock)
                {
                    // 发送字符串
                    serialPort.Write(data + "\r");

                    Thread.Sleep(100);

                    timeout = 0;

                    // 获取结果
                    while (true)
                    {
                        if (receiveMessages.Count > 0)
                        {
                            while (receiveMessages.Count > 0)
                            {
                                String line = receiveMessages.Dequeue();
                                sb.AppendLine(line);
                                Console.WriteLine(line);
                                if (line.Contains("\nOK") || line.Trim().Equals("OK"))
                                {
                                    return sb.ToString();
                                }
                            }
                        }
                        else
                        {
                            timeout += 1;
                            // 超过6秒没输出，放弃
                            if (timeout >= 60)
                            {
                                break;
                            }
                        }
                        Thread.Sleep(100);
                    }
                    
                }

                return sb.ToString();

            }
            catch(Exception ex)
            {
                Console.WriteLine($"发送失败: {ex.Message}");
            }
            finally
            {
                Thread.Sleep(100);
                notifyIcon1.Icon = Properties.Resources.phone;
            }

            return "";
        }

        private void doMessageQueue()
        {
            while (true)
            {
                if (receiveMessages.Count > 0)
                {
                    lock (messagesLock)
                    {
                        while (receiveMessages.Count > 0)
                        {
                            String value = receiveMessages.Dequeue();

                            //Console.WriteLine("==============================" + value);

                            if (!value.Equals(""))
                            {

                                if (value.Contains("CMTI"))
                                {
                                    // 收到短信
                                    String smsNo = value.Substring(value.LastIndexOf(",") + 1);

                                    SendDataMethodNoLock("AT+CMGF=0");
                                    SendDataMethodNoLock("AT+CMGR=" + smsNo);

                                    Console.WriteLine("发送：" + smsNo);
                                }
                                else if (value.Equals("RING"))
                                {
                                    //value = SendDataMethod("AT+CLCC");
                                    //String[] values = value.Split(',');
                                    //value = values[values.Length - 2].Replace("\"", "").Trim();

                                    //sendToast("来自 " + value + " 的电话", "正在呼叫", 3);
                                }
                                else if (value.StartsWith("+CMGR:"))
                                {
                                    // 检查value是否有短信结构
                                    if (value.Length > 16)
                                    {
                                        value = value.Substring(value.IndexOf("\r\n") + 2);
                                        value = value.Substring(0, value.IndexOf("\r\n"));

                                        String phoneNo = "";
                                        String content = "";
                                        String time = "";

                                        (phoneNo, time, content) = PDUTool.DecodeFullPdu(value);

                                        if (!content.Equals(""))
                                        {
                                            int batchNo = 0;
                                            int orderNo = 0;
                                            byte[] bytes = Encoding.Unicode.GetBytes(content);
                                            if (bytes[1] == 0x05 && bytes[0] == 0x00)
                                            {
                                                batchNo = bytes[2];
                                                int total = bytes[5];
                                                orderNo = bytes[4];

                                                // 组合分页
                                                String[] pages = null;
                                                if (messagePagesMap.ContainsKey(phoneNo + Convert.ToString(batchNo)))
                                                {
                                                    pages = (String[])messagePagesMap[phoneNo + Convert.ToString(batchNo)];
                                                    if(pages.Length < total)
                                                    {
                                                        pages = new string[total];
                                                        messagePagesMap[phoneNo + Convert.ToString(batchNo)] = pages;
                                                    }
                                                }
                                                else
                                                {
                                                    pages = new string[total];
                                                    messagePagesMap[phoneNo + Convert.ToString(batchNo)] = pages;
                                                }

                                                pages[orderNo - 1] = content.Substring(3);

                                                content = "";

                                                for(int i = 0; i < total; i++)
                                                {
                                                    if (pages[i] != null && !pages[i].Equals(""))
                                                    {
                                                        content += pages[i];
                                                    }
                                                    else
                                                    {
                                                        content = "";
                                                        break;
                                                    }
                                                }

                                                //Console.WriteLine(pages);

                                                if(!content.Equals(""))
                                                {
                                                    messagePagesMap.Remove(phoneNo + Convert.ToString(batchNo));

                                                    Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~收到分页文本：" + content);
                                                    SaveSMS(phoneNo, content);
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~收到文本：" + content);
                                                SaveSMS(phoneNo, content);
                                            }

                                        }
                                    }
                                }
                            }
                        }

                    }
                }

                Thread.Sleep(50);
            }
        }

        //public String SendDataMethod(String data, Boolean waitResult = true)
        //{
        //    isInSending = true;

        //    try
        //    {

        //        bool isOpen = serialPort.IsOpen;

        //        if (!isOpen)
        //        {
        //            OpenSerialPort();
        //        }

        //        //发送字符串
        //        serialPort.Write(data + "\r");

        //        // 等待
        //        if (waitResult)
        //        {

        //            String value = "null";
        //            int timeout = 0;

        //            while (true)
        //            {
        //                if (!receiveMessage.Equals(""))
        //                {
        //                    value = receiveMessage;
        //                    receiveMessage = "";
        //                    break;
        //                }
        //                timeout += 1;
        //                if (timeout >= 50)
        //                {
        //                    break;
        //                }
        //                Thread.Sleep(100);
        //            }

        //            return value;
        //        }
        //        else
        //        {
        //            return "";
        //        }
        //    }
        //    finally
        //    {
        //        isInSending = false;
        //    }
            
        //}

        private void OnReceiveData(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                byte[] result = new byte[serialPort.BytesToRead];
                serialPort.Read(result, 0, serialPort.BytesToRead);

                String str = $"{Encoding.UTF8.GetString(result)}";
                receiveMessages.Enqueue(str.Trim());

                RunIconAni();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void SaveSMS(String phoneNo, String message)
        {
            int id = 0;

            SQLiteConnection conn = null;
            try
            {
                conn = new SQLiteConnection("data source=" + Application.StartupPath + @"/storage.db");
                conn.Open();

                SQLiteCommand cmd = new SQLiteCommand();
                cmd.Connection = conn;

                cmd.CommandText = "INSERT INTO history(phone_no, create_date, message, type, batch_no, order_no, phone_to) VALUES(@phone_no, @create_date, @message, @type, @batch_no, @order_no, @phone_to)";
                cmd.Parameters.Add("phone_no", DbType.String).Value = phoneNo;
                cmd.Parameters.Add("phone_to", DbType.String).Value = "-";
                cmd.Parameters.Add("create_date", DbType.DateTime).Value = DateTime.Now;
                cmd.Parameters.Add("message", DbType.String).Value = message;
                cmd.Parameters.Add("type", DbType.String).Value = "sms";
                cmd.Parameters.Add("batch_no", DbType.Int16).Value = 0;
                cmd.Parameters.Add("order_no", DbType.Int16).Value = 0;
                cmd.ExecuteNonQuery();

                cmd.CommandText = "SELECT LAST_INSERT_ROWID();";
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    id = reader.GetInt16(0);
                }
                reader.Close();
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (conn != null) conn.Close();
            }

            sendToast("来自 " + phoneNo + " 的短信", message, 3, id + "");

            if (historyForm != null && historyForm.IsHandleCreated)
            {
                historyForm.RefreshList();
            }
        }

        //delegate void SetReceiveMessageCallback(String message);
        //private void SetReceiveMessage(String message)
        //{
        //    if (this.InvokeRequired)
        //    {
        //        while (!this.IsHandleCreated)
        //        {
        //            if (this.Disposing || this.IsDisposed)
        //                return;
        //        }
        //        SetReceiveMessageCallback d = new SetReceiveMessageCallback(SetReceiveMessage);
        //        this.Invoke(d, new object[] { message });
        //    }
        //    else
        //    {
        //        receiveMessage = message;

        //        //textBox2.Text += message;
        //    }

        //}

        private void button2_Click(object sender, EventArgs e)
        {
            CloseSerialPort();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            textBox2.Text = SendDataMethod(textBox1.Text);
        }

        private bool AddShortcut(bool recreated = false)
        {
            string pathToExe = Application.ExecutablePath;
            string commonStartMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string appStartMenuPath = Path.Combine(commonStartMenuPath, "Programs", Application.ProductName);

            if (recreated)
            {
                try
                {
                    Directory.Delete(appStartMenuPath, true);
                }catch (Exception ex)
                {
                    //MessageBox.Show("此操作请使用管理员权限运行。");
                    //Application.Exit();
                    //return;
                }

                MessageBox.Show("请重新以管理员权限运行一次。", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);

                CloseSerialPort();

                Application.Exit();
                return false;

            }

            if (!Directory.Exists(appStartMenuPath))
            {
                try
                {
                    Directory.CreateDirectory(appStartMenuPath);

                    string shortcutLocation = Path.Combine(appStartMenuPath, Application.ProductName + ".lnk");
                    WshShell shell = new WshShell();
                    IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutLocation);

                    shortcut.Description = Application.ProductName;
                    //shortcut.IconLocation = @"C:\Program Files (x86)\TestApp\TestApp.ico"; //uncomment to set the icon of the shortcut
                    shortcut.TargetPath = pathToExe;
                    shortcut.Save();

                    sendToast("操作成功", "您已经拥有通知权限。", 5);

                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("请至少使用管理员权限运行一次，以帮助应用程序获取通知权限。", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Application.Exit();
                    return false;
                }
            }
            return true;
        }

        protected void notificationOnActivated(ToastNotification notification, object obj)
        {
            if (!notification.Tag.Equals("-"))
            {
                //String phoneNo = notification.Tag.Substring(0, notification.Tag.IndexOf("|"));
                //String message = notification.Tag.Substring(notification.Tag.IndexOf("|") + 1);
                String id = notification.Tag;

                SQLiteConnection conn = null;
                try
                {
                    conn = new SQLiteConnection("data source=" + Application.StartupPath + @"/storage.db");
                    conn.Open();

                    SQLiteCommand cmd = new SQLiteCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "Select * from History Where id = " + id;
                    SQLiteDataReader reader = cmd.ExecuteReader();

                    String phoneNo = "";
                    String message = "";

                    while (reader.Read())
                    {

                        phoneNo = reader.GetString(1);
                        message = reader.GetString(2);

                        if (phoneNo.StartsWith("86"))
                        {
                            phoneNo = phoneNo.Substring(2);
                        }

                    }
                    reader.Close();

                    SendForm sendForm = new SendForm(this, phoneNo, message);
                    sendForm.Show();
                    sendForm.BringToFront();

                }
                catch (Exception ex)
                {
                }
                finally
                {
                    if (conn != null) conn.Close();
                }
            }
        }

        public void sendToast(String caption, String message, int seconds = 99999, String data = "-")
        {
            string xml = "<toast>" +
                "<visual>" +
                    "<binding template=\"ToastGeneric\">" +
                         "<text>" + caption + "</text>" +
                        "<text></text>" +
                       "<text>" + message + "</text>" +
                     "</binding>" +
                "</visual>" +
                "<audio silent='true'/>" +
            "</toast>";
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            ToastNotification notification = new ToastNotification(doc);
            notification.ExpirationTime = DateTimeOffset.Now.AddSeconds(seconds);
            notification.Activated += notificationOnActivated;
            notification.Tag = data;

            ToastNotifier nt = ToastNotificationManager.CreateToastNotifier(Application.ExecutablePath.Replace("/", "\\"));

            nt.Show(notification);

            soundPlayer.Play();
        }

        public void sendToastBackup(String caption, String message, int seconds = 99999, String data = "-")
        {
            notifyIcon1.ShowBalloonTip(seconds, caption, message, ToolTipIcon.Info);
        }

        String SerialPortName = "COM4";

        private void MainForm_Load(object sender, EventArgs e)
        {
            //String phoneNo = "";
            //String content = "";
            //String time = "";

            //(phoneNo, time, content) = PDU.DecodeFullPdu("06916851561406240BA13169899137F40000525022203245233FEDF27C1E3E97A1E17379DE0CC3373C38FAED2E3BDFA01568F876DBCB72BA8BFA9ED2E569F719250ED3C768E73BB5F1817A2078F85C9EEF00");

            //Console.WriteLine(content);

            //String content = PDU.DecodeGsm7Bit("9DE0CC3373C38FAED2E3BDFA01568F876DBCB72BA8BFA9ED2E569F719250ED3C768E73BB5F1817A2078F85C9EEF00");

            //byte[] bytes = Encoding.Unicode.GetBytes(content);

            //Console.WriteLine(content);

            //String msgCenter = "";
            //String phone = "";
            //String msg = "";
            //String time = "";

            //pdu2.PDUDecoder("003100300035FF1A79EF520667E58BE2000D000A0020003100300036FF1A79EF52066D888D398BB05F55000D000A0020003100300037FF1A624B673A4E0A7F516D4191CF000D000A0020003100300038FF1A79EF52064EA7751F8BB05F55000D000A0020003100350030FF1A5E38752867E58BE24E1A52A1000D000A002059829700", out msgCenter, out phone, out msg, out time);

            ////Console.WriteLine(pdu.GetContent("0891683190106605F0240D91683169899137F40008321031003381230862115F88597DFF1F"));

            //Console.WriteLine(phone);
            //Console.WriteLine(msg);

            //return;

            //System.Media.SystemSounds.Question.Play();

            soundPlayer = new SoundPlayer();
            soundPlayer.SoundLocation = "ringtone.wav";

            notifyIcon1.BalloonTipTitle = Application.ProductName;
            notifyIcon1.Text = Application.ProductName;
            this.Text = Application.ProductName;

            v10ToolStripMenuItem.Text = "v" + Application.ProductVersion;

            if (!AddShortcut())
            {
                Application.Exit();
                return;
            }

            try
            {
                SerialPortName = GetSerialPortName();
            }
            catch (Exception ex)
            {
                sendToast("", ex.Message, 3);
                Application.Exit();
                return;
            }

            SetSerialPort();
            OpenSerialPort();

            doMessageQueueThread = new Thread(new ThreadStart(this.doMessageQueue));
            doMessageQueueThread.Start();

            //String value = SendDataMethod("AT\r" + (char)0x1a);

            String value = SendDataMethod("AT");
            if (!value.Contains("OK"))
            {
                sendToast("", "AT连接失败", 3);
                Application.Exit();
                return;
            }

            value = SendDataMethod("AT+CMGF=0");
            if (!value.Contains("OK"))
            {
                sendToast("", "CMGF设置失败", 3);
                Application.Exit();
                return;
            }

            value = SendDataMethod("AT+CSCS=\"UCS2\"");

            if (!value.Contains("OK"))
            {
                sendToast("", "CSCS设置失败", 3);
                Application.Exit();
                return;
            }

            value = SendDataMethod("AT+CPMS=\"ME\",\"ME\",\"ME\"");

            if (!value.Contains("OK"))
            {
                sendToast("", "设置到模块存储失败", 3);
                Application.Exit();
                return;
            }

            // 获取短信中心号码
            value = SendDataMethod("AT+CSCA?");

            value = value.Substring(value.IndexOf("\"") + 1);
            value = value.Substring(0, value.IndexOf("\""));

            value = PDUTool.DecodeUnicode(value);

            CenterNo = value.Substring(1);

            sendToast("", "设备已入网", 1);


            RegistryKey R_local = Registry.LocalMachine;
            RegistryKey R_run = R_local.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

            if (R_run.GetValue(Application.ProductName) == null)
            {
                toolStripMenuItem2.Text = "设为开机自动运行";
            }
            else
            {
                toolStripMenuItem2.Text = "取消开机自动运行";
            }

            R_run.Close();
            R_local.Close();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                // 清空存储的历史短信
                SendDataMethodNoLock("AT+CMGD=1,4");
                CloseSerialPort();

                doMessageQueueThread.Abort();

                Application.Exit();
            }
            catch { }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            textBox1.Text += "\r";
        }

        private void button5_Click(object sender, EventArgs e)
        {
            textBox1.Text += (char)0x1a;
        }

        private bool needPDU(String message)
        {
            for (int i = 0; i < message.Length; i++)
            {
                if ((int)message[i] > 127)
                {
                    return true;
                }    
            }

            return false;
        }

        public void SendMSM(String phoneNo, String message)
        {

            if (needPDU(message))
            {

                String value = SendDataMethod("AT+CMGF=0");
                if (!value.Contains("OK"))
                {
                    sendToast("", "CMGF设置失败", 3);
                    return;
                }

                value = SendDataMethod("AT+CSCS=\"UCS2\"");
                if (!value.Contains("OK"))
                {
                    sendToast("", "CSCS设置失败", 3);
                    return;
                }

                (string encodedSMS, String len) = PDUTool.EncodeFullPdu(CenterNo, phoneNo, message);

                value = SendDataMethod(String.Format("AT+CMGS={0}", len));
                if (!value.Contains(">"))
                {
                    sendToast("", "发送失败", 3);
                    return;
                }

                value = SendDataMethod(String.Format("{0}" + (char)0x1a, encodedSMS));

                sendToast("", "短信已发送", 3);
            }else
            {
                try
                {
                    String value = SendDataMethod("AT+CMGF=1");
                    if (!value.Contains("OK"))
                    {
                        sendToast("", "CMGF设置失败", 3);
                        return;
                    }

                    value = SendDataMethod("AT+CSCS=\"GSM\"");
                    if (!value.Contains("OK"))
                    {
                        sendToast("", "CSCS设置失败", 3);
                        return;
                    }

                    value = SendDataMethod("AT+CMGS=\"" + phoneNo + "\"");
                    value = SendDataMethod(message + (char)0x1a);
                    if (!value.Contains((message)))
                    {
                        sendToast("", "短信发送失败", 3);
                        return;
                    }
                    else
                    {
                        sendToast("", "短信已发送", 3);
                    }
                }
                finally
                {
                    String value = SendDataMethod("AT+CMGF=0");
                    if (!value.Contains("OK"))
                    {
                        sendToast("", "CMGF设置失败", 3);
                    }
                }

            }

            SQLiteConnection conn = null;
            try
            {
                conn = new SQLiteConnection("data source=" + Application.StartupPath + @"/storage.db");
                conn.Open();

                SQLiteCommand cmd = new SQLiteCommand();
                cmd.Connection = conn;

                cmd.CommandText = "INSERT INTO history(phone_no, create_date, message, type, batch_no, order_no, phone_to) VALUES(@phone_no, @create_date, @message, @type, @batch_no, @order_no, @phone_to)";
                cmd.Parameters.Add("phone_no", DbType.String).Value = "-";
                cmd.Parameters.Add("phone_to", DbType.String).Value = phoneNo;
                cmd.Parameters.Add("create_date", DbType.DateTime).Value = DateTime.Now;
                cmd.Parameters.Add("message", DbType.String).Value = message;
                cmd.Parameters.Add("type", DbType.String).Value = "sms";
                cmd.Parameters.Add("batch_no", DbType.Int16).Value = 0;
                cmd.Parameters.Add("order_no", DbType.Int16).Value = 1;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (conn != null) conn.Close();
            }

            //SendDataMethod("AT+CMGS=\"" + textBox4.Text + "\"");
            //SendDataMethod(EncodeUCS2(textBox3.Text) + (char)0x1a);
        }

        private void button6_Click(object sender, EventArgs e)
        {



        }

        private void getMessage()
        {


        }

        private List<String> unreadSmsNos = new List<string>();

        private void timer1_Tick(object sender, EventArgs e)
        {

        }

        private void button7_Click(object sender, EventArgs e)
        {
            //Console.WriteLine(pdu.GetDataTime("069168515614066405A00110F0000832103151238123880500030804038FD456DE4E3B83DC5355FF0C8BF753D19001201C00310030003000310030201D81F3003100300030003100303002000D000A002030108BA9670D52A166F467096E295EA6FF014F7F75284E2D56FD8054901A004100500050FF0C8DB34E0D51FA62374EA48BDD8D39300167E54F59989D3001529E4E1A52A1FF0C514D6D4191CF770B"));
            ////Console.WriteLine(pdu.GetContent("0891683190106605F0240D91683169899137F400003210310070732309B1984C36B3D56837"));
            //Console.WriteLine(pdu.GetTelphone("069168515614066405A00110F0000832103151238123880500030804038FD456DE4E3B83DC5355FF0C8BF753D19001201C00310030003000310030201D81F3003100300030003100303002000D000A002030108BA9670D52A166F467096E295EA6FF014F7F75284E2D56FD8054901A004100500050FF0C8DB34E0D51FA62374EA48BDD8D39300167E54F59989D3001529E4E1A52A1FF0C514D6D4191CF770B"));
            //Console.WriteLine(pdu.GetContent("069168515614066405A00110F0000832103151238123880500030804038FD456DE4E3B83DC5355FF0C8BF753D19001201C00310030003000310030201D81F3003100300030003100303002000D000A002030108BA9670D52A166F467096E295EA6FF014F7F75284E2D56FD8054901A004100500050FF0C8DB34E0D51FA62374EA48BDD8D39300167E54F59989D3001529E4E1A52A1FF0C514D6D4191CF770B"));
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            SendForm sendForm = new SendForm(this, "", "");
            sendForm.Show();
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

        }

        private void 退出程序ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void notifyIcon1_MouseDown(object sender, MouseEventArgs e)
        {
        }

        public HistoryForm historyForm = null;

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { contextMenuStrip1.Show(); }
            if (e.Button == MouseButtons.Left) {
                //if (historyForm == null || !historyForm.IsHandleCreated)
                //{
                //    historyForm = new HistoryForm(this);
                //}

                //historyForm.RefreshList();
                //historyForm.Show();
            }
        }

        public enum HardwareEnum
        {
            // 硬件
            Win32_Processor, // CPU 处理器
            Win32_PhysicalMemory, // 物理内存条
            Win32_Keyboard, // 键盘
            Win32_PointingDevice, // 点输入设备，包括鼠标。
            Win32_FloppyDrive, // 软盘驱动器
            Win32_DiskDrive, // 硬盘驱动器
            Win32_CDROMDrive, // 光盘驱动器
            Win32_BaseBoard, // 主板
            Win32_BIOS, // BIOS 芯片
            Win32_ParallelPort, // 并口
            Win32_SerialPort, // 串口
            Win32_SerialPortConfiguration, // 串口配置
            Win32_SoundDevice, // 多媒体设置，一般指声卡。
            Win32_SystemSlot, // 主板插槽 (ISA & PCI & AGP)
            Win32_USBController, // USB 控制器
            Win32_NetworkAdapter, // 网络适配器
            Win32_NetworkAdapterConfiguration, // 网络适配器设置
            Win32_Printer, // 打印机
            Win32_PrinterConfiguration, // 打印机设置
            Win32_PrintJob, // 打印机任务
            Win32_TCPIPPrinterPort, // 打印机端口
            Win32_POTSModem, // MODEM
            Win32_POTSModemToSerialPort, // MODEM 端口
            Win32_DesktopMonitor, // 显示器
            Win32_DisplayConfiguration, // 显卡
            Win32_DisplayControllerConfiguration, // 显卡设置
            Win32_VideoController, // 显卡细节。
            Win32_VideoSettings, // 显卡支持的显示模式。
            // 操作系统
            Win32_TimeZone, // 时区
            Win32_SystemDriver, // 驱动程序
            Win32_DiskPartition, // 磁盘分区
            Win32_LogicalDisk, // 逻辑磁盘
            Win32_LogicalDiskToPartition, // 逻辑磁盘所在分区及始末位置。
            Win32_LogicalMemoryConfiguration, // 逻辑内存配置
            Win32_PageFile, // 系统页文件信息
            Win32_PageFileSetting, // 页文件设置
            Win32_BootConfiguration, // 系统启动配置
            Win32_ComputerSystem, // 计算机信息简要
            Win32_OperatingSystem, // 操作系统信息
            Win32_StartupCommand, // 系统自动启动程序
            Win32_Service, // 系统安装的服务
            Win32_Group, // 系统管理组
            Win32_GroupUser, // 系统组帐号
            Win32_UserAccount, // 用户帐号
            Win32_Process, // 系统进程
            Win32_Thread, // 系统线程
            Win32_Share, // 共享
            Win32_NetworkClient, // 已安装的网络客户端
            Win32_NetworkProtocol, // 已安装的网络协议
            Win32_PnPEntity,//all device 所有驱动
        }

        private string[] GetHarewareInfo(HardwareEnum hardType, string propKey)
        {
            List<string> strs = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from " + hardType))
                {
                    var hardInfos = searcher.Get();
                    foreach (var hardInfo in hardInfos)
                    {
                        if (hardInfo.Properties[propKey].Value != null)
                        {
                            String str = hardInfo.Properties[propKey].Value.ToString();
                            strs.Add(str);
                        }
                    }
                }
                return strs.ToArray();
            }
            catch
            {
                return null;
            }
            finally
            {
                strs = null;
            }
        }

        public String GetSerialPortName()
        {
            int comNum = -1;
            // 通过硬件名称进行判定

            string ComName = "Quectel USB AT Port";
            //string ComName = "Prolific USB-to-Serial Comm Port";
            //string[] strArr = GetHarewareInfo(HardwareEnum.Win32_SerialPort, "Name");
            string[] strArr = GetHarewareInfo(HardwareEnum.Win32_PnPEntity, "Name");
            foreach (string s in strArr)
            {
                if (s.Length >= ComName.Length && s.Contains(ComName))
                {
                    int start = s.IndexOf("(") + 3;
                    int end = s.IndexOf(")");
                    comNum = Convert.ToInt32(s.Substring(start + 1, end - start - 1));

                    //sendToast("", "发现硬件 " + s, 1);

                    break;
                }
            }


            return "COM" + comNum;
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            AddShortcut(true);
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            try
            {
                RegistryKey R_local = Registry.LocalMachine;
                RegistryKey R_run = R_local.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

                if (toolStripMenuItem2.Text.Equals("设为开机自动运行"))
                {

                        R_run.SetValue(Application.ProductName, Application.ExecutablePath);
                        toolStripMenuItem2.Text = "取消开机自动运行";

                }
                else
                {

                        R_run.DeleteValue(Application.ProductName, false);
                        toolStripMenuItem2.Text = "设为开机自动运行";

                }

                R_run.Close();
                R_local.Close();

            }
            catch (Exception)
            {
                MessageBox.Show("执行此操作请以管理员权限运行。", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //private void ReadSMS(String smsNo)
        //{

        //    String value = SendDataMethod("AT+CMGR=" + smsNo);

        //    if (value.Length < 16)
        //    {
        //        return;
        //    }

        //    value = value.Substring(value.IndexOf("\r\n") + 2);
        //    value = value.Substring(0, value.IndexOf("\r\n"));

        //    String center = "";
        //    String phoneNo = "";
        //    String message = "";
        //    String time = "";

        //    String phoneTo = "-";

        //    //pdu2.PDUDecoder(value, out center, out phoneNo, out message, out time);

        //    if (!message.Equals(""))
        //    {
        //        int batchNo = 0;
        //        int orderNo = 0;
        //        byte[] bytes = Encoding.Unicode.GetBytes(message);
        //        if (bytes[1] == 0x05 && bytes[0] == 0x00)
        //        {
        //            batchNo = bytes[2];
        //            int total = bytes[5];
        //            orderNo = bytes[4];

        //            message = "(" + orderNo + "/" + total + ")" + message.Substring(3);
        //        }


        //        //textBox2.Text += time + "\n";
        //        //textBox2.Text += phoneNo + "\n";
        //        //textBox2.Text += message + "\n";

        //        int id = -1;

        //        SQLiteConnection conn = null;
        //        try
        //        {
        //            conn = new SQLiteConnection("data source=" + Application.StartupPath + @"/storage.db");
        //            conn.Open();

        //            SQLiteCommand cmd = new SQLiteCommand();
        //            cmd.Connection = conn;

        //            cmd.CommandText = "INSERT INTO history(phone_no, create_date, message, type, batch_no, order_no, phone_to) VALUES(@phone_no, @create_date, @message, @type, @batch_no, @order_no, @phone_to)";
        //            cmd.Parameters.Add("phone_no", DbType.String).Value = phoneNo;
        //            cmd.Parameters.Add("phone_to", DbType.String).Value = phoneTo;
        //            cmd.Parameters.Add("create_date", DbType.DateTime).Value = DateTime.Now;
        //            cmd.Parameters.Add("message", DbType.String).Value = message;
        //            cmd.Parameters.Add("type", DbType.String).Value = "sms";
        //            cmd.Parameters.Add("batch_no", DbType.Int16).Value = batchNo;
        //            cmd.Parameters.Add("order_no", DbType.Int16).Value = orderNo;
        //            cmd.ExecuteNonQuery();

        //            cmd.CommandText = "SELECT LAST_INSERT_ROWID();";
        //            SQLiteDataReader reader = cmd.ExecuteReader();
        //            while (reader.Read())
        //            {
        //                id = reader.GetInt16(0);
        //            }
        //            reader.Close();
        //        }
        //        catch (Exception ex)
        //        {
        //        }
        //        finally
        //        {
        //            if (conn != null) conn.Close();
        //        }

        //        //sendToast("来自 " + phoneNo + " 的短信", message, 3, id + "");

        //        //if (historyForm != null && historyForm.IsHandleCreated)
        //        //{
        //        //    historyForm.RefreshList();
        //        //}
        //    }
        //}

        private void timer2_Tick(object sender, EventArgs e)
        {
 
        }

        private void button8_Click(object sender, EventArgs e)
        {
            byte[] bytes = Encoding.Unicode.GetBytes("Ԁ̢Є电影、玩游戏，点击 http://u.10010.cn/khddx ，马上拥有】");
            if (bytes[1] == 0x05 && bytes[0] == 0x00)
            {
                String a = "Ԁ̢Є电影、玩游戏，点击 http://u.10010.cn/khddx ，马上拥有】";
                a = a.Substring(2);
                Console.WriteLine(a);
            }
        }

        public void HistoryFormRefreshList()
        {
            if (historyForm != null && historyForm.IsHandleCreated)
            {
                historyForm.RefreshList();
                historyForm.Show();
            }
        }

        private void 新短信ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendForm sendForm = new SendForm(this, "", "");
            sendForm.Show();
        }

        private void 查看历史ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (historyForm == null || !historyForm.IsHandleCreated)
            {
                historyForm = new HistoryForm(this);
            }

            historyForm.RefreshList();
            historyForm.Show();
        }

        private void qQ56582083ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("不如和大力交个朋友", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            SendDataMethod("ATD13969819734;");
        }

        private int aniInc = 0;

        private System.Timers.Timer iconTimer;

        private void RunIconAni()
        {
            if (iconTimer == null || iconTimer.Enabled == false)
            {
                aniInc = 0;
                notifyIcon1.Icon = Properties.Resources.phone;

                if(iconTimer == null)
                {
                    iconTimer = new System.Timers.Timer(200);
                    iconTimer.Elapsed += OnTimedEvent;
                    iconTimer.AutoReset = true;
                }

                iconTimer.Enabled = true;
            }
            else
            {
                aniInc = aniInc % 2;
            }
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            if (aniInc % 2 == 0)
            {
                notifyIcon1.Icon = Properties.Resources.phone_sound;
            }
            else
            {
                notifyIcon1.Icon = Properties.Resources.phone;
            }
            aniInc++;
            if (aniInc >= 8)
            {
                aniInc = 0;
                iconTimer.Enabled = false;
                notifyIcon1.Icon = Properties.Resources.phone;
            }
        }
    }
}
