/*----------------------------------------------------------------
 * Copyright (C) 2010 刘中原
 * 版权所有。 
 * 
 * 文件名： GSMModem.cs
 * 
 * 文件功能描述：   完成短信猫设备的打开关闭，短信的发送与接收以及
 *              其他相应功能
 *                  文件包含两个类GSMModem和PDUEncoding，PDUEncod-
 *              ing为私有类，完成PDU格式的编码与解码
 *              
 * 创建标识：   刘中原20101104
 * 
 * 修改标识：
 * 修改描述：
 * 
 * 修改标识：
 * 修改描述：
**----------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Threading;

namespace GSMMODEM
{
    /// <summary>
    /// “猫”设备类，完成短信发送 接收等
    /// </summary>
    public class GSMModem
    {
        /// <summary>
        /// 无参数构造函数 完成有关初始化工作
        /// </summary>
        public GSMModem()
        {
            sp = new SerialPort();

            sp.ReadTimeout = 3000;      //读超时时间 发送短信时间的需要
            sp.RtsEnable = true;        //必须为true 这样串口才能接收到数据

            sp.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comPort">串口号</param>
        /// <param name="baudRate">波特率</param>
        public GSMModem(string comPort, int baudRate)
        {
            sp = new SerialPort();

            sp.PortName = comPort;      //
            sp.BaudRate = baudRate;
            sp.ReadTimeout = 3000;      //读超时时间 发送短信时间的需要
            sp.RtsEnable = true;        //必须为true 这样串口才能接收到数据

            sp.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);
        }

        private SerialPort sp;          //私有字段 串口对象

        public int NewMsgIndex { get; set; }        //新消息序号

        /// <summary>
        /// 串口号 运行时只读 设备打开状态写入将引发异常
        /// 提供对串口端口号的访问
        /// </summary>
        public string ComPort
        {
            get
            {
                return sp.PortName;
            }
            set
            {
                try
                {
                    sp.PortName = value;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// 波特率 可读写
        /// 提供对串口波特率的访问
        /// </summary>
        public int BaudRate
        {
            get
            {
                return sp.BaudRate;
            }
            set
            {
                sp.BaudRate = value;
            }
        }

        /// <summary>
        /// 设备是否打开
        /// 对串口IsOpen属性访问
        /// </summary>
        public bool IsOpen
        {
            get
            {
                return sp.IsOpen;
            }
        }

        /// <summary>
        /// 创建事件收到信息的委托
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void OnRecievedHandler(object sender, EventArgs e);

        /// <summary>
        /// 收到短信息事件 OnRecieved 
        /// 收到短信将引发此事件
        /// </summary>
        public event OnRecievedHandler OnRecieved;

        /// <summary>
        /// 从串口收到数据 串口事件
        /// 程序未完成需要的可自己添加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string temp = sp.ReadLine();
            if (temp.Length > 8)
            {
                if (temp.Substring(0, 6) == "+CMTI:")
                {
                    NewMsgIndex = Convert.ToInt32(temp.Split(',')[1]);
                    OnRecieved(this, e);
                }
            }
        }

        /// <summary>
        /// 设备打开函数，无法时打开将引发异常
        /// </summary>
        public void Open()
        {
            try
            {
                sp.Open();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            //初始化设备
            if (sp.IsOpen)
            {
                sp.DataReceived -= sp_DataReceived;

                //更新添加连接识别
                sp.Write("AT\r");
                Thread.Sleep(50);
                string s = sp.ReadExisting().Trim();
                s = s.Substring(s.Length - 2, 2);           //有回显时 去后两位有效字符
                if (s != "OK")
                {
                    throw new Exception("硬件连接错误");    //硬件未连接、串口或是波特率设置错误
                }

                try
                {
                    SendAT("ATE0");
                    SendAT("AT+CMGF=0");
                    SendAT("AT+CNMI=2,1");
                }
                catch { }
            }
        }

        /// <summary>
        /// 设备关闭函数
        /// </summary>
        public void Close()
        {
            try
            {
                sp.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 发送AT指令 逐条发送AT指令 调用一次发送一条指令
        /// 能返回一个OK或ERROR算一条指令
        /// </summary>
        /// <param name="ATCom">AT指令</param>
        /// <returns>发送指令后返回的字符串</returns>
        public string SendAT(string ATCom)
        {
            string result = string.Empty;
            //忽略接收缓冲区内容，准备发送
            sp.DiscardInBuffer();

            //注销事件关联，为发送做准备
            sp.DataReceived -= sp_DataReceived;

            //发送AT指令
            try
            {
                sp.Write(ATCom + "\r");
            }
            catch (Exception ex)
            {
                sp.DataReceived += sp_DataReceived;
                throw ex;
            }

            //接收数据 循环读取数据 直至收到“OK”或“ERROR”
            try
            {
                string temp = string.Empty;
                while (temp.Trim() != "OK" && temp.Trim() != "ERROR")
                {
                    temp = sp.ReadLine();
                    result += temp;
                }
                return result;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                //事件重新绑定 正常监视串口数据
                sp.DataReceived += sp_DataReceived;
            }
        }

        /// <summary>
        /// 发送短信
        /// 发送失败将引发异常
        /// </summary>
        /// <param name="phone">手机号码</param>
        /// <param name="msg">短信内容</param>
        public void SendMsg(string phone, string msg)
        {
            PDUEncoding pe = new PDUEncoding();
            string temp = pe.PDUUSC2Encoder(phone, msg);

            int len = (temp.Length - Convert.ToInt32(temp.Substring(0, 2), 16) * 2 - 2) / 2;  //计算长度
            try
            {
                //注销事件关联，为发送做准备
                sp.DataReceived -= sp_DataReceived;

                sp.Write("AT+CMGS=" + len.ToString() + "\r");
                sp.ReadTo(">");
                sp.DiscardInBuffer();

                //事件重新绑定 正常监视串口数据
                sp.DataReceived += sp_DataReceived;

                temp = SendAT(temp + (char)(26));  //26 Ctrl+Z ascii码
            }
            catch (Exception)
            {
                throw new Exception("短信发送失败");
            }
            finally
            {
            }

            if (temp.Substring(temp.Length - 4, 3).Trim() == "OK")
            {
                return;
            }

            throw new Exception("短信发送失败");
        }

        //void ReadNewMsg(out string msgCenter, out string phone, out string msg, out string time)
        //{
        //    Char[] temp = null;

        //    sp.Read(temp, 0, 2);
        //}

        /// <summary>
        /// 按序号读取短信
        /// </summary>
        /// <param name="index">序号</param>
        /// <param name="msgCenter">短信中心</param>
        /// <param name="phone">发送方手机号码</param>
        /// <param name="msg">短信内容</param>
        /// <param name="time">时间字符串</param>
        public void ReadMsgByIndex(int index, out string msgCenter, out string phone, out string msg, out string time)
        {
            string temp = string.Empty;
            PDUEncoding pe = new PDUEncoding();
            try
            {
                temp = SendAT("AT+CMGR=" + index.ToString() + "\r");
            }
            catch (Exception ex)
            {
                throw ex;
            }

            if (temp.Trim() == "ERROR")
            {
                throw new Exception("没有此短信");
            }
            temp = temp.Split((char)(13))[2];       //取出PDU串(char)(13)为0x0a即\r 按\r分为多个字符串 第3个是PDU串

            pe.PDUDecoder(temp, out msgCenter, out phone, out msg, out time);
        }

        public void DeleteMsgByIndex(int index)
        {
            if (SendAT("AT+CMGD=" + index.ToString() + "\r").Trim() == "OK")
            {
                return;
            }

            throw new Exception("删除失败");
        }
    }

    /// <summary>
    /// PDU编解码类，完成PDU短信格式的编码与解码
    /// 目前只能发送接收USC2编码（中文编码）的
    /// 代码不是很安全，投入使用的话需要一定的改动 
    /// 短信右7bit编码格式，我没有做判断 可能会引发异常
    /// 私有类，只能命名空间内部使用 调试此类时须设为公有 完成后去掉public
    /// </summary>
    class PDUEncoding
    {
        private string serviceCenterAddress = "00";
        /// <summary>
        /// 消息服务中心(1-12个8位组)
        /// </summary>
        public string ServiceCenterAddress
        {
            get
            {
                int len = 2 * Convert.ToInt32(serviceCenterAddress.Substring(0, 2));
                string result = serviceCenterAddress.Substring(4, len - 2);

                result = ParityChange(result);
                result = result.TrimEnd('F', 'f');
                return result;
            }
            set                 //
            {
                if (value == null || value.Length == 0)      //号码为空
                {
                    serviceCenterAddress = "00";
                }
                else
                {
                    if (value[0] == '+')
                    {
                        value = value.TrimStart('+');
                    }
                    if (value.Substring(0, 2) != "86")
                    {
                        value = "86" + value;
                    }
                    value = "91" + ParityChange(value);
                    serviceCenterAddress = (value.Length / 2).ToString("X2") + value;
                }
                
            }
        }

        private string protocolDataUnitType = "11";
        /// <summary>
        /// 协议数据单元类型(1个8位组)
        /// </summary>
        public string ProtocolDataUnitType
        {
            set
            {

            }
            get
            {
                return "11";
            }
        }

        private string messageReference = "00";
        /// <summary>
        /// 所有成功的短信发送参考数目（0..255）
        /// (1个8位组)
        /// </summary>
        public string MessageReference
        {
            get
            {
                return "00";
            }
        }

        private string originatorAddress = "00";
        /// <summary>
        /// 发送方地址（手机号码）(2-12个8位组)
        /// </summary>
        public string OriginatorAddress
        {
            get
            {
                int len = Convert.ToInt32(originatorAddress.Substring(0, 2),16);    //十六进制字符串转为整形数据
                string result = string.Empty;
                if (len % 2 == 1)       //号码长度是奇数，长度加1 编码时加了F
                {
                    len++;
                }
                result = originatorAddress.Substring(4, len);
                result = ParityChange(result).TrimEnd('F', 'f');    //奇偶互换，并去掉结尾F

                return result;
            }
        }

        private string destinationAddress = "00";
        /// <summary>
        /// 接收方地址（手机号码）(2-12个8位组)
        /// </summary>
        public string DestinationAddress
        {
            set
            {
                if (value == null || value.Length == 0)      //号码为空
                {
                    destinationAddress = "00";
                }
                else
                {
                    if (value[0] == '+')
                    {
                        value = value.TrimStart('+');
                    } 
                    if (value.Substring(0, 2) == "86")
                    {
                        value = value.TrimStart('8', '6');
                    }
                    int len = value.Length;
                    value = ParityChange(value);

                    destinationAddress = len.ToString("X2") + "A1" + value;
                }
            }
        }

        private string protocolIdentifer = "00";
        /// <summary>
        /// 参数显示消息中心以何种方式处理消息内容
        /// （比如FAX,Voice）(1个8位组)
        /// </summary>
        public string ProtocolIdentifer
        {
            get
            {
                return protocolIdentifer;
            }
            set
            {

            }
        }

        private string dataCodingScheme = "08";     //暂时仅支持国内USC2编码
        /// <summary>
        /// 参数显示用户数据编码方案(1个8位组)
        /// </summary>
        public string DataCodingScheme
        {
            get
            {
                return dataCodingScheme;
            }
        }

        private string serviceCenterTimeStamp = "";
        /// <summary>
        /// 消息中心收到消息时的时间戳(7个8位组)
        /// </summary>
        public string ServiceCenterTimeStamp
        {
            get
            {
                string result = ParityChange(serviceCenterTimeStamp);
                result = "20" + result.Substring(0, 12);            //年加开始的“20”

                return result;
            }
        }

        private string validityPeriod = "C4";       //暂时固定有效期
        /// <summary>
        /// 短消息有效期(0,1,7个8位组)
        /// </summary>
        public string ValidityPeriod
        {
            get
            {
                return "C4";
            }
        }

        private string userDataLenghth = "";
        /// <summary>
        /// 用户数据长度(1个8位组)
        /// </summary>
        public string UserDataLenghth
        {
            get
            {
                return (userData.Length / 2).ToString("X2");
            }
        }

        private string userData = "";
        /// <summary>
        /// 用户数据(0-140个8位组)
        /// </summary>
        public string UserData
        {
            get
            {
                int len = Convert.ToInt32(userDataLenghth, 16) * 2;
                string result = string.Empty;

                if (dataCodingScheme == "08" || dataCodingScheme == "18")             //USC2编码
                {
                    //四个一组，每组译为一个USC2字符
                    for (int i = 0; i < len; i += 4)
                    {
                        string temp = userData.Substring(i, 4);

                        int byte1 = Convert.ToInt16(temp, 16);

                        result += ((char)byte1).ToString();
                    }
                }
                else
                {
                    result = PDU7bitDecoder(userData);
                }

                return result;
            }
            set
            {
                userData = string.Empty;
                Encoding encodingUTF = Encoding.BigEndianUnicode;

                byte[] Bytes = encodingUTF.GetBytes(value);

                for (int i = 0; i < Bytes.Length; i++)
                {
                    userData += BitConverter.ToString(Bytes, i, 1);
                }
                userDataLenghth = (userData.Length / 2).ToString("X2");
            }
        }


        /// <summary>
        /// 奇偶互换 (+F)
        /// </summary>
        /// <param name="str">要被转换的字符串</param>
        /// <returns>转换后的结果字符串</returns>
        private string ParityChange(string str)
        {
            string result = string.Empty;

            if (str.Length % 2 != 0)         //奇字符串 补F
            {
                str += "F";
            }
            for (int i = 0; i < str.Length; i += 2)
            {
                result += str[i + 1];
                result += str[i];
            }

            return result;
        }

        /// <summary>
        /// PDU编码器，完成PDU编码(USC2编码，最多70个字)
        /// </summary>
        /// <param name="phone">目的手机号码</param>
        /// <param name="Text">短信内容</param>
        /// <returns>编码后的PDU字符串</returns>
        public string PDUEncoder(string phone, string Text)
        {
            if (Text.Length > 70)
            {
                throw (new Exception("短信字数超过70"));
            }
            DestinationAddress = phone;
            UserData = Text;

            return serviceCenterAddress + protocolDataUnitType
                + messageReference + destinationAddress + protocolIdentifer
                + dataCodingScheme + validityPeriod + userDataLenghth + userData;
        }

        /// <summary>
        /// 完成手机或短信猫收到PDU格式短信的解码 暂时仅支持中文编码
        /// 未用DCS部分
        /// </summary>
        /// <param name="strPDU">短信PDU字符串</param>
        /// <param name="msgCenter">短消息服务中心 输出</param>
        /// <param name="phone">发送方手机号码 输出</param>
        /// <param name="msg">短信内容 输出</param>
        /// <param name="time">时间字符串 输出</param>
        public void PDUDecoder(string strPDU, out string msgCenter, out string phone, out string msg, out string time)
        {
            int lenSCA = Convert.ToInt32(strPDU.Substring(0, 2), 16) * 2 + 2;       //短消息中心占长度
            serviceCenterAddress = strPDU.Substring(0, lenSCA);

            int lenOA = Convert.ToInt32(strPDU.Substring(lenSCA + 2, 2),16);           //OA占用长度
            if (lenOA % 2 == 1)                                                     //奇数则加1 F位
            {
                lenOA++;
            }
            lenOA += 4;                 //加号码编码的头部长度
            originatorAddress = strPDU.Substring(lenSCA + 2, lenOA);

            dataCodingScheme = strPDU.Substring(lenSCA + lenOA + 4, 2);             //DCS赋值，区分解码7bit

            serviceCenterTimeStamp = strPDU.Substring(lenSCA + lenOA + 6, 14);

            userDataLenghth = strPDU.Substring(lenSCA + lenOA + 20, 2);
            int lenUD = Convert.ToInt32(userDataLenghth, 16) * 2;
            userData = strPDU.Substring(lenSCA + lenOA + 22);

            msgCenter = ServiceCenterAddress;
            phone = OriginatorAddress;
            msg = UserData;
            time = ServiceCenterTimeStamp;
        }

        /// <summary>
        /// PDU7bit的解码，供UserData的get访问器调用
        /// </summary>
        /// <param name="len">用户数据长度</param>
        /// <param name="userData">数据部分PDU字符串</param>
        /// <returns></returns>
        private string PDU7bitDecoder(string userData)
        {
            string result = string.Empty;
            byte[] b = new byte[100];
            string temp = string.Empty;

            for (int i = 0; i < userData.Length; i += 2)
            {
                b[i / 2] = (byte)Convert.ToByte((userData[i].ToString() + userData[i + 1].ToString()),16);
            }

            int j = 0;            //while计数
            int tmp = 1;            //temp中二进制字符字符个数
            while (j < userData.Length / 2 - 1)
            {
                string s = string.Empty;

                s = Convert.ToString(b[j], 2);

                while (s.Length < 8)            //s补满8位 byte转化来的 有的不足8位，直接解码将导致错误
                {
                    s = "0" + s;
                }

                result += (char)Convert.ToInt32(s.Substring(tmp) + temp, 2);        //加入一个字符 结果集 temp 上一位组剩余

                temp = s.Substring(0, tmp);             //前一位组多的部分

                if (tmp > 6)                            //多余的部分满7位，加入一个字符
                {
                    result += (char)Convert.ToInt32(temp, 2);
                    temp = string.Empty;
                    tmp = 0;
                }

                tmp++;
                j++;

                if (j == userData.Length / 2 - 1)           //最后一个字符
                {
                    result += (char)Convert.ToInt32(Convert.ToString(b[j], 2) + temp, 2);
                }
            }
            return result;
        }
    }
}
