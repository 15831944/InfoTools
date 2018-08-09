using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;

namespace Common.ExcelInterop
{
    public static class Utils
    {
        /// <summary>
        /// Получмить значения ячеек одной из строк листа
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="rowNum"></param>
        /// <returns></returns>
        public static SortedDictionary<int, CellValue> GetRowValues(Excel._Worksheet sheet, int rowNum)
        {
            SortedDictionary<int, CellValue> excelColumns = new SortedDictionary<int, CellValue>();
            Excel.Range rows = sheet.Rows;
            Excel.Range row = (Excel.Range)rows[rowNum];

            int N = sheet.UsedRange.Columns.Count;

            object[,] vs = row.Value2;
            for (int i = sheet.UsedRange.Column; i <= N; i++)
            {
                string dn = null;
                object value = null;
                try
                {
                    value = vs[1, i];
                    dn = Convert.ToString(value);
                }
                catch { }
                if (value != null && !String.IsNullOrEmpty(dn))
                {
                    excelColumns.Add(i, new CellValue(dn, value, rowNum, i));
                }
            }
            return excelColumns;
        }
    }
}
