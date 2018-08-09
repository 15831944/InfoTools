using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
                            Convert.ToDouble(xyzLineTrim[0].Replace(" ", "").Replace(",",".")),
                            Convert.ToDouble(xyzLineTrim[1].Replace(" ", "").Replace(",",".")),
                            Convert.ToDouble(xyzLineTrim[2].Replace(" ", "").Replace(",","."))
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
    }
}
