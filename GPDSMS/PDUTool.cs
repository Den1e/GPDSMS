using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using System.Windows.Forms;

namespace GPDSMS
{
    class PDUTool
    {
        public static (String, String, String) DecodeFullPdu(string pdu)
        {
            if (pdu.Length > 20 && !pdu.StartsWith("0"))
            {
                return ("", "", DecodeUnicode(pdu)); 
            }

            // 否则按完整PDU解析
            try
            {
                int index = 0;

                // 1. 短信中心号码 (SCA)
                int scaLength = Convert.ToInt32(pdu.Substring(index, 2), 16);
                if (scaLength > 0)
                {
                    index += scaLength * 2 + 2;
                }
                else
                {
                    index += 2;
                }

                // 2. PDU类型 (PDU-Type)
                //Console.WriteLine(pdu.Substring(index, 2));
                index += 2;

                // 3. 发送方号码 (OA)
                int phoneNoLength = Convert.ToInt32('0' + pdu.Substring(index + 1, 1), 16);
                index += 2;
                string phoneNoType = pdu.Substring(index, 2);
                index += 2;
                string phoneNo = DecodePhoneNo(pdu.Substring(index, (phoneNoLength + 1) / 2 * 2)); // 对齐到偶数长度
                index += (phoneNoLength + 1) / 2 * 2;

                // 4. 协议标识 (PID)
                index += 2;

                // 5. 数据编码方案 (DCS)
                string dcs = pdu.Substring(index, 2);
                bool isUnicode = dcs == "08";
                index += 2;

                // 6. 时间戳 (SCTS)
                string time = DecodeTime(pdu.Substring(index, 14));
                index += 14;

                // 7. 用户数据长度 (UDL)
                int udLength = Convert.ToInt32(pdu.Substring(index, 2), 16);
                index += 2;

                // 8. 用户数据 (UD)
                string message = isUnicode
                    ? DecodeUnicode(pdu.Substring(index))
                    : DecodeGsm7Bit(pdu.Substring(index), udLength);

                return (phoneNo, time, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解码失败: {ex.Message}");
                return ("", "", "");
            }
 
        }

        private static string DecodePhoneNo(string hex)
        {
            StringBuilder number = new StringBuilder();
            for (int i = 0; i < hex.Length; i += 2)
            {
                string pair = hex.Substring(i, 2);
                if (pair == "F") break;
                number.Append(pair[1]);
                if (pair[0] != 'F') number.Append(pair[0]);
            }
            return number.Length > 0 && number[0] == '0' ? number.ToString(1, number.Length - 1) : number.ToString();
        }

        private static string DecodeTime(string hex)
        {
            try
            {
                // 每两位反转并组合（BCD编码）
                string year = $"{hex[1]}{hex[0]}";
                string month = $"{hex[3]}{hex[2]}";
                string day = $"{hex[5]}{hex[4]}";
                string hour = $"{hex[7]}{hex[6]}";
                string minute = $"{hex[9]}{hex[8]}";
                string second = $"{hex[11]}{hex[10]}";
                string timezone = $"{hex[13]}{hex[12]}";

                return $"20{year}-{month}-{day} {hour}:{minute}:{second}";
            }
            catch
            {
                return "时间解码失败";
            }
        }

        public static string DecodeUnicode(string hex)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hex.Length; i += 4)
                {
                    string hexChar = hex.Substring(i, 4);
                    sb.Append((char)Convert.ToUInt16(hexChar, 16));
                }
                return sb.ToString();
            }
            catch
            {
                return hex;
            }
        }

        // GSM 7-bit 默认字符集（不含扩展表）
        private static readonly char[] DefaultAlphabet =
        {
            '@', '£', '$', '¥', 'è', 'é', 'ù', 'ì', 'ò', 'Ç', '\n', 'Ø', 'ø', '\r', 'Å', 'å',
            'Δ', '_', 'Φ', 'Γ', 'Λ', 'Ω', 'Π', 'Ψ', 'Σ', 'Θ', 'Ξ', ' ', 'Æ', 'æ', 'ß', 'É',
            ' ', '!', '"', '#', '¤', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '<', '=', '>', '?',
            '¡', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
            'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'Ä', 'Ö', 'Ñ', 'Ü', '§',
            '¿', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o',
            'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'ä', 'ö', 'ñ', 'ü', 'à'
        };

        // GSM 7-bit 扩展字符集（0x1B 开头的转义字符）
        private static readonly Dictionary<byte, char> ExtensionTable = new Dictionary<byte, char>
        {
            { 0x0A, '\f' }, { 0x14, '^' }, { 0x28, '{' }, { 0x29, '}' }, { 0x2F, '\\' },
            { 0x3C, '[' }, { 0x3D, '~' }, { 0x3E, ']' }, { 0x40, '|' }, { 0x65, '€' }
        };

        public static string DecodeGsm7Bit(string hex, int udLength)
        {
            byte[] bytes = HexToBytes(hex);
            List<byte> septets = new List<byte>();

            int bitOffset = 0;
            byte remainingBits = 0;
            int decodedSeptetCount = 0;  // 新增：实际解码的7-bit字符计数器

            for (int i = 0; i < bytes.Length && decodedSeptetCount < udLength; i++) // 增加长度限制
            {
                byte currentByte = bytes[i];

                // 提取7-bit字符
                byte septet = (byte)((currentByte << bitOffset) | remainingBits);
                septet &= 0x7F;
                septets.Add(septet);
                decodedSeptetCount++;  // 计数增加

                // 如果已经达到所需长度，立即停止处理
                if (decodedSeptetCount >= udLength) break;

                remainingBits = (byte)(currentByte >> (7 - bitOffset));
                bitOffset++;

                if (bitOffset == 7)
                {
                    if (decodedSeptetCount < udLength)  // 只在需要时添加剩余位
                    {
                        septets.Add(remainingBits);
                        decodedSeptetCount++;
                    }
                    bitOffset = 0;
                    remainingBits = 0;
                }
            }

            // 解码字符
            StringBuilder message = new StringBuilder();
            bool isEscaped = false;

            for (int i = 0; i < septets.Count && message.Length < udLength; i++) // 增加长度检查
            {
                byte septet = septets[i];

                if (septet == 0x1B && !isEscaped)
                {
                    isEscaped = true;
                    continue;
                }

                char c;
                if (isEscaped)
                {
                    ExtensionTable.TryGetValue(septet, out c);
                    isEscaped = false;
                }
                else
                {
                    c = (septet < DefaultAlphabet.Length) ? DefaultAlphabet[septet] : ' ';
                }

                message.Append(c);
            }

            return message.ToString();
        }

        /// <summary>
        /// 将十六进制字符串转换为字节数组
        /// </summary>
        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        public static string DecodeGsm7BitOld(string userData)
        {
            string result = string.Empty;
            byte[] b = new byte[100];
            string temp = string.Empty;

            for (int i = 0; i < userData.Length; i += 2)
            {
                b[i / 2] = (byte)Convert.ToByte((userData[i].ToString() + userData[i + 1].ToString()), 16);
            }

            int j = 0;            
            int tmp = 1;            
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

        public static string EncodeUnicode(string srvContent)
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


        public static string EncodeCenterNo(string srvCenterNo)
        {
            string s = null;
            int nLength = srvCenterNo.Length;
            for (int i = 1; i < nLength; i += 2)                       //奇偶互换
            {
                s += srvCenterNo[i];
                s += srvCenterNo[i - 1];
            }
            if (!(nLength % 2 == 0))                           //是否为偶数，不是就加上F，并对最后一位与加上的F位互换
            {
                s += 'F';
                s += srvCenterNo[nLength - 1];
            }
            s = String.Format("91{0}", s);                    //加上91,代表短信中心类型为国际化
            s = String.Format("{0:X2}{1}", s.Length / 2, s);   //编码后短信中心号长度，并格式化成二位十六制
            return s;
        }

        public static string EncodePhoneNo(string srvPhoneNo)
        {
            string s = null;
            if (!(srvPhoneNo.Substring(0, 2) == "86"))
            {
                srvPhoneNo = String.Format("86{0}", srvPhoneNo);     //检查当前接收手机号是否按标准格式书写，不是，就补上“86”
            }
            int nLength = srvPhoneNo.Length;
            for (int i = 1; i < nLength; i += 2)                 //将奇数位和偶数位交换
            {
                s += srvPhoneNo[i];
                s += srvPhoneNo[i - 1];
            }
            if (!(nLength % 2 == 0))                              //是否为偶数，不是就加上F，并对最后一位与加上的F位互换
            {
                s += 'F';
                s += srvPhoneNo[nLength - 1];
            }
            return s;
        }


        public static (string, String) EncodeFullPdu(string strCenterNumber, string strNumber, string strSMScontent)
        {
            String value = String.Format("{0}11000D91{1}000800{2}", EncodeCenterNo(strCenterNumber), EncodePhoneNo(strNumber), EncodeUnicode(strSMScontent));
            String len = String.Format("{0:D2}", (value.Length - EncodeCenterNo(strCenterNumber).Length) / 2);
            return (value, len);
        }
    }
}
