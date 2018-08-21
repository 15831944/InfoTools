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

            try
            {
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
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(rows);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(row);
            }
            return excelColumns;
        }


        public static Dictionary<string, int> GetColumnValues(Excel._Worksheet sheet, int colNum)
        {
            Dictionary<string, int> colValues = new Dictionary<string, int>();

            Excel.Range columns = sheet.Columns;
            Excel.Range column = (Excel.Range)columns[colNum];

            try
            {
                int N = sheet.UsedRange.Rows.Count;
                object[,] vs = column.Value2;
                for (int i = sheet.UsedRange.Row; i <= N; i++)
                {
                    string str = null;
                    object value = null;
                    try
                    {
                        value = vs[i, 1];
                        str = Convert.ToString(value);
                    }
                    catch { }
                    if (value != null && !String.IsNullOrEmpty(str))
                    {
                        colValues.Add(str, i);
                    }
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(columns);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(column);
            }

            return colValues;
        }
    }
}
