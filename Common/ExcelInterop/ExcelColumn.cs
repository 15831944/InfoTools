using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.ExcelInterop
{
    public class CellValue
    {
        public string DisplayString { get; private set; }
        public object Value2 { get; private set; }
        public int RowNum { get; private set; }
        public int ColumnNum { get; private set; }

        public CellValue(string displayString, object value2, int rowNum, int columnNum)
        {
            DisplayString = displayString;
            Value2 = value2;
            RowNum = rowNum;
            ColumnNum = columnNum;
        }
    }
}
