using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExcelDataReader;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

namespace Civil3DInfoTools.PipeNetworkCreating
{
    /// <summary>
    /// Предназначен для считывания данных из файлов Excel, описывающих колодцы и присоединенные трубы
    /// </summary>
    public class PipeStructureExcelReader
    {
        private const int SKIP_ROWS_COUNT = 5;
        private const int WELL_NUM_COL = 0;
        private const int NETWORK_TYPE_COL = 1;
        private const int SIZE1_COL = 2;
        private const int SIZE2_COL = 3;
        private const int WELL_MATERIAL_COL = 4;
        private const int WELL_TOP_LEVEL_COL = 5;
        private const int WELL_BOTTOM_LEVEL_COL = 6;
        private const int PIPE_JUNCTION_NUM_COL = 7;
        private const int PIPE_MATERIAL_COL = 8;
        private const int PIPE_SIZE_COL = 9;
        private const int PIPE_JUNCTION_LEVEL_COL = 10;

        public static readonly Regex SQUARE_LBL_REGEX = new Regex("^\\d{4}[_-]?\\d{2}[_-]?\\d{2}$");//(.xlsx)|(.xls)

        public Dictionary<int,//номер квадрата сетки
            Dictionary<string,//номер колодца (может быть не только цифрой)
                WellData>>//все данные о колодце и присоединениях к нему
            WellsData
        { get; private set; } = new Dictionary<int, Dictionary<string, WellData>>();

        public List<FileInfo> WellDataFiles { get; private set; } = null;


        public PipeStructureExcelReader(string path)
        {
            //path может быть либо папкой либо файлом
            //Получить директорию, в которой лежат все файлы с данными о колодцах
            string dir = null;
            FileAttributes attr = File.GetAttributes(path);
            if (attr.HasFlag(FileAttributes.Directory))
                dir = path;
            else
                dir = Path.GetDirectoryName(path);

            //Найти в этой директории все файлы Excel, у которых имя состоит из 8 цифр
            //в соответствии с номером квадрата (допускается. чтобы в имени были 2 дефиса или 2 подчеркивания)
            DirectoryInfo di = new DirectoryInfo(dir);
            FileInfo[] files = di.GetFiles("*.xls");
            
            WellDataFiles
                = files.Where(fi => SQUARE_LBL_REGEX.IsMatch(Path.GetFileNameWithoutExtension(fi.FullName)))
                .ToList();

        }

        public bool ReadDataFromExcel()
        {
            foreach (FileInfo fi in WellDataFiles)
            {
                FileStream fs = null;
                try
                {
                    fs = File.Open(fi.FullName, FileMode.Open, FileAccess.Read);
                }
                catch (System.IO.IOException ex)
                {
                    AccessException(ex);
                    return false;
                }
                if (fs != null)
                {


                    IExcelDataReader reader = null;
                    switch (fi.Extension)
                    {
                        case ".xls":
                            reader = ExcelReaderFactory.CreateBinaryReader(fs);
                            break;
                        case ".xlsx":
                            reader = ExcelReaderFactory.CreateOpenXmlReader(fs);
                            break;
                            //TODO?: можно добавить еще CSV
                    }

                    //Определить все видимые листы
                    HashSet<string> visibleSheets = new HashSet<string>();
                    for (var i = 0; i < reader.ResultsCount; i++)
                    {
                        // checking visible state
                        if (reader.VisibleState == "visible")
                        {
                            visibleSheets.Add(reader.Name);
                        }

                        reader.NextResult();
                    }


                    DataSet result = reader.AsDataSet();
                    string str = reader.VisibleState;


                    reader.Close();

                    int key = Convert.ToInt32(Path.GetFileNameWithoutExtension(fi.FullName).Replace("-", "").Replace("_", ""));

                    if (!WellsData.ContainsKey(key))
                    {
                        Dictionary<string, WellData> thisFileDict = new Dictionary<string, WellData>();

                        WellsData.Add(key, thisFileDict);
                        foreach (DataTable table in result.Tables)
                        {
                            //если таблица скрыта, то не трогать ее
                            if (!visibleSheets.Contains(table.TableName))
                            {
                                continue;
                            }

                            int skipped = 0;
                            WellData currentWellData = null;
                            foreach (DataRow row in table.Rows)
                            {
                                //Пропустить нужное количество строк с начала таблицы
                                if (skipped < SKIP_ROWS_COUNT)
                                {
                                    skipped++;
                                    continue;
                                }

                                string wellNum = row[WELL_NUM_COL].ToString();
                                if (!String.IsNullOrEmpty(wellNum))
                                {
                                    if (!thisFileDict.ContainsKey(wellNum))
                                    {
                                        string sizeStr = row[SIZE1_COL].ToString();
                                        double size1 = -1;
                                        double size2 = -1;
                                        double topLevel = -1;
                                        double bottomLevel = -1;

                                        double.TryParse(sizeStr, out size1);
                                        double.TryParse(row[SIZE2_COL].ToString(), out size2);

                                        double.TryParse(row[WELL_TOP_LEVEL_COL].ToString(), out topLevel);
                                        double.TryParse(row[WELL_BOTTOM_LEVEL_COL].ToString(), out bottomLevel);

                                        currentWellData = new WellData()
                                        {
                                            Num = wellNum,
                                            NetworkType = row[NETWORK_TYPE_COL].ToString(),
                                            SizeString = sizeStr,
                                            Size1 = size1,
                                            Size2 = size2,
                                            Material = row[WELL_MATERIAL_COL].ToString(),
                                            TopLevel = topLevel,
                                            BottomLevel = bottomLevel,
                                        };

                                        thisFileDict.Add(wellNum, currentWellData);

                                        ReadPipeJunctionData(row, currentWellData);
                                    }
                                }
                                else if (currentWellData != null)
                                {
                                    //добавление нового присоединения к данным колодца
                                    ReadPipeJunctionData(row, currentWellData);
                                }
                            }

                        }
                    }

                }

            }

            return true;

        }

        /// <summary>
        /// Считать из строки данные о присоединении трубы
        /// </summary>
        /// <param name="row"></param>
        /// <param name="currentWellData"></param>
        private void ReadPipeJunctionData(DataRow row, WellData currentWellData)
        {
            //номера присоединения может не быть у некоторых колодцев (напр у газовых коверов)
            //то есть все трубы пересекаются на одной отметке
            //ориентируемся на налицчие данных хотябы в одном столбце
            string num = row[PIPE_JUNCTION_NUM_COL].ToString();
            string material = row[PIPE_MATERIAL_COL].ToString();
            double size = -1;
            double level = -1;
            double.TryParse(row[PIPE_SIZE_COL].ToString(), out size);
            double.TryParse(row[PIPE_JUNCTION_LEVEL_COL].ToString(), out level);

            if (!String.IsNullOrEmpty(num) || !String.IsNullOrEmpty(material)
                || size >= 0 || level >= 0)
            {
                string key = !String.IsNullOrEmpty(num) ? num : "0";//в колодце может быть только одно присоединение без номера
                if (!currentWellData.PipeJunctions.ContainsKey(key))
                    currentWellData.PipeJunctions.Add(key, new PipeJunctionData()
                    { Num = num, Material = material, Size = size, JunctionLevel = level });
            }
        }

    }

    //Данные о колодце
    public class WellData
    {
        public string Num { get; set; }

        public string NetworkType { get; set; }


        public string SizeString { get; set; }//иногда в столбце габаритов написано "ковер"
        public double Size1 { get; set; }
        public double Size2 { get; set; }

        public string Material { get; set; }

        public double TopLevel { get; set; }

        public double BottomLevel { get; set; }


        public Dictionary<string, PipeJunctionData> PipeJunctions { get; set; }
            = new Dictionary<string, PipeJunctionData>();
    }


    public class PipeJunctionData
    {
        public string Num { get; set; }

        public string Material { get; set; }

        public double Size { get; set; }

        public double JunctionLevel { get; set; }
    }
}
