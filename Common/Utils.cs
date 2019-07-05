using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Common
{
    public static class Utils
    {
        /// <summary>
        /// Чтение координат из файла CSV
        /// Выбрасывает IOException если файл CSV занят
        /// </summary>
        /// <param name="csvFileName"></param>
        /// <returns></returns>
        public static List<double[]> ReadCoordinates(string csvFileName)
        {
            string[] csvLines = null;


            csvLines = File.ReadAllLines(csvFileName, Encoding.UTF8);


            List<double[]> ptList = new List<double[]>();
            foreach (string csvLine in csvLines)
            {
                string[] xyzLine = csvLine.Split(';');

                List<string> xyzLineTrim = new List<string>();
                Regex regex = new Regex("^.*\\w+.*$");//Содержит хотябы один любой символ кроме пропусков
                foreach (string str in xyzLine)
                {
                    if (regex.IsMatch(str))
                    {
                        xyzLineTrim.Add(str);
                    }
                }

                if (xyzLineTrim.Count == 0)//Строка содержит только пропуски. Переход к следующей строке
                    continue;

                if (xyzLineTrim.Count == 3)
                {
                    double[] globalPoint = new double[]
                    {
                            Convert.ToDouble(xyzLineTrim[0].Replace(" ", "").Replace(",","."), System.Globalization.CultureInfo.InvariantCulture),
                            Convert.ToDouble(xyzLineTrim[1].Replace(" ", "").Replace(",","."), System.Globalization.CultureInfo.InvariantCulture),
                            Convert.ToDouble(xyzLineTrim[2].Replace(" ", "").Replace(",","."), System.Globalization.CultureInfo.InvariantCulture)
                    };

                    ptList.Add(globalPoint);
                }
                else
                {
                    throw new FormatException("Неверный формат данных");
                }

            }
            return ptList;
        }

        public static string GetNonExistentFileName(string dir, string baseFileName, string ext)
        {
            string targetFullPath = null;

            targetFullPath = Path.Combine(dir, baseFileName + "." + ext);
            if (File.Exists(targetFullPath))
            {
                int n = 0;
                do
                {
                    targetFullPath = Path.Combine(dir, baseFileName + n + "." + ext);
                    n++;
                } while (File.Exists(targetFullPath));
            }

            return targetFullPath;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/146134/how-to-remove-illegal-characters-from-path-and-filenames
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public static string GetSavePathName(string pathname)
        {
            return string.Join("_", pathname.Split(Path.GetInvalidPathChars()));
        }



        /// <summary>
        /// Для использования 2-связного списка как циклического списка
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        public static LinkedListNode<T> NextVertCycled<T>(LinkedListNode<T> current)
        {
            LinkedListNode<T> next = current.Next;
            if (next != null)
            {
                return next;
            }
            else
            {
                return current.List.First;
            }
        }
        /// <summary>
        /// Для использования 2-связного списка как циклического списка
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        public static LinkedListNode<T> PreviousVertCycled<T>(LinkedListNode<T> current)
        {
            LinkedListNode<T> next = current.Previous;
            if (next != null)
            {
                return next;
            }
            else
            {
                return current.List.Last;
            }
        }


        /// <summary>
        /// При парсинге XML могут возникать ошибки типа "hexadecimal value 0x1F, is an invalid character. Line 1, position 1"
        /// В строке этот символ представлен в виде html кода типа "&#x1F;" (x1F - это шестнадцатиричный номер символа Unicode)
        /// Из строки удаляются все невалидные включения согласно https://www.w3.org/TR/xml/#charsets
        /// </summary>
        /// <returns></returns>
        public static string RemoveInvalidXmlSubstrs(string xmlStr)
        {
            string pattern = "&#[^&#]((\\d+)|(x\\S+))[^;];";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            if (regex.IsMatch(xmlStr))
            {
                xmlStr = regex.Replace(xmlStr, new MatchEvaluator(m =>
                {
                    string s = m.Value;
                    string unicodeNumStr = s.Substring(2, s.Length - 3);

                    int unicodeNum = unicodeNumStr.StartsWith("x") ?
                    Convert.ToInt32(unicodeNumStr.Substring(1), 16)
                    : Convert.ToInt32(unicodeNumStr);

                    if ((unicodeNum == 0x9 || unicodeNum == 0xA || unicodeNum == 0xD) ||
                    ((unicodeNum >= 0x20) && (unicodeNum <= 0xD7FF)) ||
                    ((unicodeNum >= 0xE000) && (unicodeNum <= 0xFFFD)) ||
                    ((unicodeNum >= 0x10000) && (unicodeNum <= 0x10FFFF)))
                    {
                        return s;
                    }
                    else
                    {
                        return String.Empty;
                    }
                })
                );
            }
            return xmlStr;
        }




        /// <summary>
        /// Удаляет из строки символы, которые не подходят для XML
        /// https://forums.asp.net/t/1483793.aspx?Need+a+method+that+removes+illegal+XML+characters+from+a+String
        /// </summary>
        /// <param name="textIn"></param>
        /// <returns></returns>
        public static String RemoveNonValidXMLCharacters(string textIn)
        {
            StringBuilder textOut = new StringBuilder(); // Used to hold the output.
            char current; // Used to reference the current character.


            if (textIn == null || textIn == string.Empty) return string.Empty; // vacancy test.
            for (int i = 0; i < textIn.Length; i++)
            {
                current = textIn[i];


                if ((current == 0x9 || current == 0xA || current == 0xD) ||
                    ((current >= 0x20) && (current <= 0xD7FF)) ||
                    ((current >= 0xE000) && (current <= 0xFFFD)) ||
                    ((current >= 0x10000) && (current <= 0x10FFFF)))
                {
                    textOut.Append(current);
                }
            }
            return textOut.ToString();
        }

    }
}
