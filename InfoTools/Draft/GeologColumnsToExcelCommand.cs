using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Excel = Microsoft.Office.Interop.Excel;

//[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.GeologColumnsToExcelCommand))]

namespace Civil3DInfoTools.AuxiliaryCommands
{
    public class GeologColumnsToExcelCommand
    {

        /// <summary>
        /// ПРЕДПОЛАГАЕТСЯ, ЧТО ТАБЛИЦА НЕ СОДЕРЖИТ НИ ОДНОЙ ПУСТОЙ ЯЧЕЙКИ
        /// TODO?: Доработать для возможного наличия пустых ячеек. КАК?
        /// </summary>
        //[CommandMethod("S1NF0_GeologColumnsToExcel", CommandFlags.Modal)]
        public void GeologColumnsToExcel()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;


            try
            {
                //Указать название скважины
                PromptEntityOptions peo = new PromptEntityOptions("\nУкажите имя скважины (TEXT,MTEXT)");
                peo.SetRejectMessage("\nМожно выбрать только TEXT или MTEXT");
                peo.AddAllowedClass(typeof(MText), true);
                peo.AddAllowedClass(typeof(DBText), true);
                PromptEntityResult per1 = ed.GetEntity(peo);
                if (per1.Status == PromptStatus.OK)
                {
                    string boreholeName = null;
                    SortedDictionary<Point2d, SortedDictionary<Point2d, string>> columns
                        = new SortedDictionary<Point2d, SortedDictionary<Point2d, string>>(new HeaderCopmarer());
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Entity ent = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as Entity;
                        boreholeName = (ent as MText)?.Text;
                        if (boreholeName == null)
                        {
                            boreholeName = (ent as DBText)?.TextString;
                        }


                        //Выбор шапки таблицы (Мтексты)
                        TypedValue[] tv = new TypedValue[] { new TypedValue(0, "MTEXT") };
                        SelectionFilter flt = new SelectionFilter(tv);
                        PromptSelectionOptions pso = new PromptSelectionOptions();
                        pso.MessageForAdding = "\nВыберите заголовки шапки таблицы (только MTEXT)";

                        PromptSelectionResult acSSPrompt = adoc.Editor.GetSelection(pso, flt);
                        if (acSSPrompt.Status == PromptStatus.OK)
                        {
                            //Для каждого заголовка определить X его левой границы.
                            //Содержимое заголовка добавить как первый элемент в столбце

                            SelectionSet acSSet = acSSPrompt.Value;

                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                            foreach (SelectedObject acSSObj in acSSet)
                            {
                                if (acSSObj != null)
                                {
                                    MText mText = tr.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as MText;

                                    Extents3d? ext = mText.Bounds;
                                    if (ext != null)
                                    {
                                        Point2d basePt = new Point2d(ext.Value.MinPoint.X,// - точка по левой стороне посередине заголовка.
                                            (ext.Value.MaxPoint.Y + ext.Value.MinPoint.Y) / 2);//Y нужен для расчета положения добавляемого объекта во вложенном словаре

                                        SortedDictionary<Point2d, string> col = new SortedDictionary<Point2d, string>(new ColComparer(basePt));
                                        col[basePt] = mText.Text;
                                        columns[basePt] = col;

                                        //using (Xline xline = new Xline())
                                        //{
                                        //    xline.BasePoint = ext.Value.MinPoint;
                                        //    xline.SecondPoint = ext.Value.MinPoint - new Vector3d(0,-1,0);
                                        //    ms.AppendEntity(xline);
                                        //    tr.AddNewlyCreatedDBObject(xline, true);
                                        //}

                                    }

                                }
                            }





                            //TODO?: Отобразить лучами области столбцов

                            //Выбор содержимого таблицы
                            tv = new TypedValue[] { new TypedValue(0, "TEXT,MTEXT") };
                            flt = new SelectionFilter(tv);
                            pso = new PromptSelectionOptions();
                            pso.MessageForAdding = "\nВыберите содержимое таблицы (TEXT,MTEXT)";

                            acSSPrompt = adoc.Editor.GetSelection(pso, flt);
                            if (acSSPrompt.Status == PromptStatus.OK)
                            {
                                acSSet = acSSPrompt.Value;
                                //Распеределить по столбцам в зависимости от того в какую область Мтекста заголовка попадает точка вставки каждого текста

                                foreach (SelectedObject acSSObj in acSSet)
                                {
                                    if (acSSObj != null)
                                    {
                                        Entity text = tr.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as Entity;

                                        string contents = (text as MText)?.Text;
                                        if (contents == null)
                                        {
                                            contents = (text as DBText)?.TextString;
                                        }

                                        Extents3d? ext = text.Bounds;
                                        if (ext != null)
                                        {
                                            Point2d centerPt =
                                                new Point2d((ext.Value.MinPoint.X + ext.Value.MaxPoint.X) / 2, (ext.Value.MinPoint.Y + ext.Value.MaxPoint.Y) / 2);
                                            //Найти колонку, в которую добавляется текст и добавить его
                                            SortedDictionary<Point2d, string> col = columns.Where(c => c.Key.X < centerPt.X)?.LastOrDefault().Value;
                                            if (col != null)
                                                col[centerPt] = contents;
                                        }
                                    }
                                }


                            }
                            tr.Commit();
                        }


                        //Получить приложение Excel
                        Excel.Application instance = null;
                        try
                        {
                            instance = (Excel.Application)System.Runtime.InteropServices.Marshal.GetActiveObject("Excel.Application");
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            instance = new Excel.Application();
                        }
                        try
                        {
                            instance.ScreenUpdating = false;
                            //Создать новую книгу Excel и записать в нее таблицу
                            Excel._Workbook workbook = instance.Workbooks.Add(Type.Missing);
                            Excel.Worksheet wSheet = workbook.Sheets.Item[1];

                            int colNum = 2;
                            foreach (SortedDictionary<Point2d, string> col in columns.Values)
                            {
                                int rowNum = 1;
                                foreach (string val in col.Values)
                                {
                                    wSheet.Cells[rowNum, colNum] = val;
                                    if (colNum == 2)
                                    {
                                        wSheet.Cells[rowNum, 1] = boreholeName;
                                    }
                                    rowNum++;
                                }

                                colNum++;
                            }
                        }
                        finally
                        {
                            instance.ScreenUpdating = true;
                            instance.Visible = true;
                        }




                    }
                }


            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при получении таблицы Excel по геологической колонке");
            }

        }


        private class ColComparer : IComparer<Point2d>
        {
            private Point2d basePt;
            public ColComparer(Point2d basePt)
            {
                this.basePt = basePt;
            }

            public int Compare(Point2d pt1, Point2d pt2)
            {
                int compare = (basePt.Y - pt1.Y).CompareTo(basePt.Y - pt2.Y);
                if (compare != 0)
                    return compare;
                else
                    return (basePt.X - pt1.X).CompareTo(basePt.X - pt2.X);
            }
        }


        private class HeaderCopmarer : IComparer<Point2d>
        {
            public int Compare(Point2d pt1, Point2d pt2)
            {
                return pt1.X.CompareTo(pt2.X);
            }
        }
    }
}
