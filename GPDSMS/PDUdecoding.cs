using System;


using System.Text;
using System.Text.RegularExpressions;

namespace GPDSMS
{

    class PDU
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
                int len = Convert.ToInt32(originatorAddress.Substring(0, 2), 16);    //十六进制字符串转为整形数据
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
            try
            {


                int lenSCA = Convert.ToInt32(strPDU.Substring(0, 2), 16) * 2 + 2;       //短消息中心占长度
                serviceCenterAddress = strPDU.Substring(0, lenSCA);

                int lenOA = Convert.ToInt32(strPDU.Substring(lenSCA + 2, 2), 16);           //OA占用长度
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

            }catch(Exception ex)
            {
                msgCenter = "";
                phone = "";
                msg = "";
                time = "";
            }


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
                b[i / 2] = (byte)Convert.ToByte((userData[i].ToString() + userData[i + 1].ToString()), 16);
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

    public class PDUEncoding
    {
        public PDUEncoding()
        {

            // TODO: 在此处添加构造函数逻辑

        }

        public string DecodeUCS2(string src)
        {
            string decucs = src.Replace("\r", "");
            string pstr = "^[0-9a-fA-F]+$";
            if (!Regex.IsMatch(decucs, pstr))
            {
                return "";
            }

            StringBuilder builer = new StringBuilder();

            for (int i = 0; i < decucs.Length; i += 4)
            {
                int unicode_nu = Int32.Parse(decucs.Substring(i, 4), System.Globalization.NumberStyles.HexNumber);
                builer.Append(string.Format("{0}", (char)unicode_nu));
            }

            return builer.ToString();
        }


        /// <summary>
        /// 判断接受的短信是PDU格式还是TEXT格式
        /// </summary>

        public bool IsPDU(string SMS)
        {
            if (SMS.Substring(40, 2) != "08")
                return false;
            return true;
        }

        /// <summary>
        /// 函数功能：短信内容提取
        /// 函数名称：GetEverySMS(string SMS)
        /// 参 数：SMS 要进行提取的整个短信内容
        /// 返 回 值：将多个短信内容拆分
        /// </summary>

        public string[] GetEverySMS(string SMS)
        {
            char[] str = "\n".ToCharArray();
            string[] temp = SMS.Split(str);
            return temp;
        }

        /// <summary>
        /// 函数功能：提取短信的发送人电话号码
        /// 函数名称：GetTelphone(string SMS)
        /// 参 数：SMS 要进行转换的整个短信内容
        /// 返 回 值：电话号码
        /// </summary>

        public string GetTelphone(string SMS)
        {
            string tel = SMS.Substring(26, 12);
            string s = "";
            for (int i = 0; i < 9; i += 2)
            {
                s += tel[i + 1];
                s += tel[i];
            }
            s += tel[tel.Length - 1];
            return s;
        }

        /// <summary>
        /// 函数功能：提取短信的发送时间
        /// 函数名称：GetDataTime(string SMS)
        /// 参 数：SMS:要进行转换的整个短信内容
        /// 返 回 值：发送时间
        /// </summary>

        public string GetDataTime(string SMS)
        {
            string time = SMS.Substring(42, 12);
            string s = "";
            for (int i = 0; i < 11; i += 2)
            {
                s += time[i + 1];
                s += time[i];
            }
            string t = s.Substring(0, 2) + "-" + s.Substring(2, 2) + "-" + s.Substring(4, 2) + " " + s.Substring(6, 2) + ":" + s.Substring(8, 2) + ":" + s.Substring(10, 2);
            return t;
        }

        /// <summary>
        /// 函数功能：提取短信的内容(PDU)
        /// 函数名称：GetContent(string SMS)
        /// 参 数：SMS:要进行转换的整个短信内容
        /// 返 回 值：短信内容
        /// </summary>

        public string GetContent(string SMS)
        {
            string c = "";
            string len = SMS.Substring(56, 2);
            int length = System.Convert.ToInt16(len, 16);
            length *= 2;
            //string content = SMS.Substring(58, SMS.Length - 58);
            string content = SMS.Substring(58, length);
            for (int i = 0; i < length; i += 4)
            {
                string temp = content.Substring(i, 4);
                int by = System.Convert.ToInt16(temp, 16);
                char ascii = (char)by;
                c += ascii.ToString();
            }
            return c;
        }

        /// <summary>
        /// 函数功能：提取短信的TEXT内容(TEXT)
        /// 函数名称：GetTextContent(string SMS)
        /// 参 数：SMS:要进行转换的整个短信内容
        /// 返 回 值：短信内容
        /// </summary>

        public string GetTextContent(string SMS)
        {
            string str = "";
            string c = "";
            byte by;
            char ascii;
            int i;
            SMS = SMS.Replace("\r", "");
            SMS = SMS.Replace("\n", "");
            string content = SMS.Substring(58);
            for (i = content.Length - 2; i >= 0; i -= 2)
            {
                by = Convert.ToByte(content.Substring(i, 2), 16);
                str += Convert.ToString(by, 2).PadLeft(8, '0');
            }
            for (i = str.Length - 7; i >= 0; i -= 7)
            {
                by = Convert.ToByte(str.Substring(i, 7), 2);
                ascii = (char)by;
                c += ascii.ToString();
            }
            return c;
        }


        public string nLength;   //要发送内容的长度,由两部分组成,接收手机号加上要发送的内容
                                 /// <summary>
                                 /// 函数功能：短信内容编码
                                 /// 函数名称：smsPDUEncoded(string srvContent)
                                 /// 参    数：srvContent 要进行转换的短信内容,string类型
                                 /// 返 回 值：编码后的短信内容，string类型
                                 /// 程 序 员：sillnet@163.net
                                 /// 编制日期：2003-10-15
                                 /// 函数说明：
                                 ///          1，采用Big-Endian 字节顺序的 Unicode 格式编码，也就说把高低位的互换在这里完成了
                                 ///          2，将转换后的短信内容存进字节数组
                                 ///          3，去掉在进行Unicode格式编码中，两个字节中的"-",例如：00-21，变成0021
                                 ///          4，将整条短信内容的长度除2，保留两位16进制数
                                 /// </summary>
        public string smsPDUEncoded(string srvContent)
        {
            Encoding encodingUTF = System.Text.Encoding.BigEndianUnicode;
            string s = null;
            byte[] encodedBytes = encodingUTF.GetBytes(srvContent);
            for (int i = 0; i < encodedBytes.Length; i++)
            {
                s += BitConverter.ToString(encodedBytes, i, 1);
            }
            s = String.Format("{0:X2}{1}", s.Length / 2, s);

            return s;
        }

        /// <summary>
        /// 函数功能：短信中心号编码
        /// 函数名称：smsDecodedCenterNumber(string srvCenterNumber)
        /// 参    数：srvCenterNumber 要进行转换的短信中心号,string类型
        /// 返 回 值：编码后的短信中心号，string类型
        /// 程 序 员：sillnet@163.net
        /// 编制日期：2003-10-15
        /// 函数说明：
        ///          1，将奇数位和偶数位交换。
        ///          2，短信中心号奇偶数交换后，看看长度是否为偶数，如果不是，最后添加F
        ///          3，加上短信中心号类型，91为国际化
        ///          4，计算编码后的短信中心号长度，并格化成二位的十六进制
        /// </summary>
        public string smsEncodedCenterNumber(string srvCenterNumber)
        {
            string s = null;
            int nLength = srvCenterNumber.Length;
            for (int i = 1; i < nLength; i += 2)                       //奇偶互换
            {
                s += srvCenterNumber[i];
                s += srvCenterNumber[i - 1];
            }
            if (!(nLength % 2 == 0))                           //是否为偶数，不是就加上F，并对最后一位与加上的F位互换
            {
                s += 'F';
                s += srvCenterNumber[nLength - 1];
            }
            s = String.Format("91{0}", s);                    //加上91,代表短信中心类型为国际化
            s = String.Format("{0:X2}{1}", s.Length / 2, s);   //编码后短信中心号长度，并格式化成二位十六制
            return s;
        }

        /// <summary>
        /// 函数功能：接收短信手机号编码
        /// 函数名称：smsDecodedNumber(string srvNumber)
        /// 参    数：srvCenterNumber 要进行转换的短信中心号,string类型
        /// 返 回 值：编码后的接收短信手机号，string类型
        /// 程 序 员：sillnet@163.net
        /// 编制日期：2003-10-15
        /// 函数说明：
        ///          1，检查当前接收手机号是否按标准格式书写，不是，就补上“86”
        ///          1，将奇数位和偶数位交换。
        ///          2，短信中心号奇偶数交换后，看看长度是否为偶数，如果不是，最后添加F
        /// </summary>
        public string smsEncodedNumber(string srvNumber)
        {
            string s = null;
            if (!(srvNumber.Substring(0, 2) == "86"))
            {
                srvNumber = String.Format("86{0}", srvNumber);     //检查当前接收手机号是否按标准格式书写，不是，就补上“86”
            }
            int nLength = srvNumber.Length;
            for (int i = 1; i < nLength; i += 2)                 //将奇数位和偶数位交换
            {
                s += srvNumber[i];
                s += srvNumber[i - 1];
            }
            if (!(nLength % 2 == 0))                              //是否为偶数，不是就加上F，并对最后一位与加上的F位互换
            {
                s += 'F';
                s += srvNumber[nLength - 1];
            }
            return s;
        }

        /// <summary>
        /// 函数功能：整个短信的编码
        /// 函数名称：smsDecodedsms(string strCenterNumber, string strNumber, string strSMScontent)
        /// 参    数：strCenterNumber 要进行转换的短信中心号,string类型
        ///           strNumber       接收手机号码，string类型
        ///           strSMScontent   短信内容
        /// 返 回 值：完整的短信编码，可以在AT指令中执行，string类型
        /// 程 序 员：sillnet@163.net
        /// 编制日期：2003-10-15
        /// 函数说明：
        ///           11000D91和000800   在国内，根据PDU编码原则，我们写死在此，详细解释请看我的文章     
        /// </summary>
        public string smsEncodedsms(string strCenterNumber, string strNumber, string strSMScontent)
        {
            string s = String.Format("{0}11000D91{1}000800{2}", smsEncodedCenterNumber(strCenterNumber), smsEncodedNumber(strNumber), smsPDUEncoded(strSMScontent));
            nLength = String.Format("{0:D2}", (s.Length - smsEncodedCenterNumber(strCenterNumber).Length) / 2);   //获取短信内容加上手机号码长度
            return s;
        }
    }
}
