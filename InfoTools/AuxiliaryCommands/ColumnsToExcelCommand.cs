using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Excel = Microsoft.Office.Interop.Excel;

[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.ColumnsToExcelCommand))]


namespace Civil3DInfoTools.AuxiliaryCommands
{
    public class ColumnsToExcelCommand
    {
        [CommandMethod("S1NF0_ColumnsToExcel", CommandFlags.Modal)]
        public void ColumnsToExcel()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            try
            {
                //Выбор текстов и линий 
                TypedValue[] tv = new TypedValue[] { new TypedValue(0, "LWPOLYLINE,LINE,TEXT,MTEXT") };
                SelectionFilter flt = new SelectionFilter(tv);
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\nВыберите содержимое таблицы (LWPOLYLINE,LINE,TEXT,MTEXT). Нужно выбрать все тексты, а так же все горизонтальные линии, разделяющие строки таблицы";

                PromptSelectionResult acSSPrompt = adoc.Editor.GetSelection(pso, flt);
                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    //SortedSet<LineSegment2d> horizontalLines = new SortedSet<LineSegment2d>(new HorizontalComparer());
                    List<LineSegment2d> horizontalLines = new List<LineSegment2d>();
                    SortedSet<SLEvent> eventQueue = new SortedSet<SLEvent>();
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        //Среди линий отобрать все горизонтальные линии в отдельную коллекцию
                        //Для всех текстов определить прямоугольник расположения
                        //Построить очередь событий начала и конца текстов, рассматривая их как горизонтальные отрезки
                        SelectionSet acSSet = acSSPrompt.Value;
                        foreach (ObjectId id in acSSet.GetObjectIds())
                        {
                            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);

                            if (ent is MText || ent is DBText)
                            {
                                string contents = GetTextContents(ent);
                                if (!String.IsNullOrEmpty(contents))
                                {
                                    Extents3d? ext = ent.Bounds;
                                    if (ext != null)
                                    {
                                        new TextInfo(contents, new Point2d(ext.Value.MaxPoint.X, ext.Value.MaxPoint.Y),
                                            new Point2d(ext.Value.MinPoint.X, ext.Value.MinPoint.Y), eventQueue);
                                    }
                                }

                            }
                            else if (ent is Polyline)
                            {
                                Polyline polyline = ent as Polyline;
                                if (!polyline.HasBulges && !polyline.Closed)//не рассматривать замкнутые и полилинии с дугами
                                {
                                    for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
                                    {
                                        Point2d segPt1 = polyline.GetPoint2dAt(i);
                                        Point2d segPt2 = polyline.GetPoint2dAt(i + 1);
                                        SelectHorizontalLines(segPt1, segPt2, horizontalLines);
                                    }
                                }

                            }
                            else if (ent is Line)
                            {
                                Line line = ent as Line;
                                Point2d segPt1 = new Point2d(line.StartPoint.X, line.StartPoint.Y);
                                Point2d segPt2 = new Point2d(line.EndPoint.X, line.EndPoint.Y);
                                SelectHorizontalLines(segPt1, segPt2, horizontalLines);
                            }

                        }

                        //TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST
                        //BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                        //BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        //foreach (LineSegment2d hor in horizontalLines)
                        //{
                        //    using (Line line = new Line(
                        //        new Point3d(hor.StartPoint.X, hor.StartPoint.Y, 0),
                        //        new Point3d(hor.EndPoint.X, hor.EndPoint.Y, 0)))
                        //    {
                        //        line.ColorIndex = 1;

                        //        ms.AppendEntity(line);
                        //        tr.AddNewlyCreatedDBObject(line, true);
                        //    }
                        //}
                        //TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST//TEST
                        tr.Commit();
                    }

                    //Используя логику сканирующей линии распределить все тексты по столбцам, отсортировав сверху вниз
                    SweepLine sweepLine = new SweepLine(eventQueue);

                    List<SortedSet<TextInfo>> columns = sweepLine.Columns;



                    //Для каждого столбца определить границы ячеек по следующим правилам:
                    // - Если прямоугольники расположения текстов перекрываются, то считать что тексты находятся в разных ячейках
                    // - Иначе проверить есть ли между центральными точками прямоугольников горизонтальные линии. Если нет, то тексты находятся в одной ячейке

                    //for (int i = 0; i < columns.Count; i++)
                    //{
                    //    SortedSet<TextInfo> col = columns.ElementAt(i);

                    List<List<string>> columnsToExcel = new List<List<string>>();

                    foreach (SortedSet<TextInfo> col in columns)
                    {
                        List<string> colToExcel = new List<string>();
                        columnsToExcel.Add(colToExcel);
                        TextInfo prevTi = null;
                        foreach (TextInfo ti in col)
                        {
                            bool concat = false;
                            if (prevTi != null)
                            {
                                if (!Utils.BoxesAreSuperimposed(prevTi.MaxPt, prevTi.MinPt, ti.MaxPt, ti.MinPt)
                                    && !AreDivided(prevTi, ti, horizontalLines))//Проверить есть ли между текстами горизонтальная линия
                                {
                                    concat = true;
                                }
                            }

                            if (concat)
                            {
                                colToExcel[colToExcel.Count - 1] = colToExcel[colToExcel.Count - 1] + " " + ti.Contents;
                            }
                            else
                            {
                                colToExcel.Add(ti.Contents);
                            }


                            prevTi = ti;
                            //colToExcel[colToExcel.Count] = colToExcel[colToExcel.Count] + " " + ti.Contents;

                            //colToExcel.Add(ti.Contents);
                        }
                    }


                    //Excel
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

                        int colNum = 1;
                        foreach (List<string> col in columnsToExcel)
                        {
                            int rowNum = 1;
                            foreach (string val in col)
                            {
                                wSheet.Cells[rowNum, colNum] = val;
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
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при переносе столбцов текста из AutoCAD в Excel");
            }


        }

        private static string GetTextContents(Entity ent)
        {
            string contents = (ent as MText)?.Text;
            if (contents == null)
            {
                contents = (ent as DBText)?.TextString;
            }
            return contents;
        }

        private void SelectHorizontalLines(Point2d segPt1, Point2d segPt2, List/*SortedSet*/<LineSegment2d> horizontalLines)
        {
            Vector2d vector = segPt2 - segPt1;
            if (vector.IsParallelTo(Vector2d.XAxis))
            {
                //Это горизонтальная линия
                List<Point2d> pts = new List<Point2d>() { segPt1, segPt2 };
                pts.Sort((p1, p2) =>
                {
                    return p1.X.CompareTo(p2.X);//Сначала левая, потом правая
                });
                horizontalLines.Add(new LineSegment2d(pts[0], pts[1]));
            }


        }


        private bool AreDivided(TextInfo upper, TextInfo lower, List/*SortedSet*/<LineSegment2d> horizontalLines)
        {
            bool divided = false;

            //SortedSet<LineSegment2d> horizontalsBetween
            //    = horizontalLines.GetViewBetween(new LineSegment2d(lower.CenterPt, Vector2d.XAxis),
            //    new LineSegment2d(upper.CenterPt, Vector2d.XAxis));

            //if (horizontalsBetween.Count > 0)
            //{

            LineSegment2d divider
                        = //horizontalsBetween.ToList()
                        horizontalLines
                        .Find(h => h.StartPoint.X <= upper.CenterPt.X && h.EndPoint.X >= upper.CenterPt.X
                        && h.StartPoint.Y <= upper.CenterPt.Y && h.StartPoint.Y >= lower.CenterPt.Y
                        );
            if (divider != null)
            {
                divided = true;
            }

            //}


            return divided;
        }

        private class TextInfo : IComparable<TextInfo>
        {
            public string Contents { get; private set; }

            public Point2d MaxPt { get; private set; }

            public Point2d MinPt { get; private set; }

            public Point2d CenterPt { get; private set; }

            public TextInfo(string contents, Point2d maxPt, Point2d minPt, SortedSet<SLEvent> eventQueue)
            {
                Contents = contents;
                MaxPt = maxPt;
                MinPt = minPt;
                CenterPt = new Point2d((minPt.X + maxPt.X) / 2, (minPt.Y + maxPt.Y) / 2);

                eventQueue.Add(new SLEvent(minPt.X, true, this));
                eventQueue.Add(new SLEvent(maxPt.X, false, this));

            }

            public int CompareTo(TextInfo other)
            {
                return this.CenterPt.Y.CompareTo(other.CenterPt.Y) * (-1);
            }
        }

        private class SLEvent : IComparable<SLEvent>
        {
            public double Position { get; private set; }

            public bool Start { get; private set; }

            public TextInfo TextInfo { get; private set; }

            public SLEvent(double position, bool start, TextInfo textInfo)
            {
                Position = position;
                Start = start;
                TextInfo = textInfo;
            }

            public int CompareTo(SLEvent other)
            {
                int posComparison = this.Position.CompareTo(other.Position);
                return posComparison != 0 ? posComparison : !this.Start ? -1 : 1;//Конец отрезка всегда ставить перед началом
            }
        }

        private class SweepLine
        {
            public List<SortedSet<TextInfo>> Columns { get; private set; } = new List<SortedSet<TextInfo>>();

            SortedSet<TextInfo> currentColumn = new SortedSet<TextInfo>();
            private int currentTextCount = 0;

            public SweepLine(SortedSet<SLEvent> eventQueue)
            {
                foreach (SLEvent e in eventQueue)
                {
                    if (!e.Start)
                    {
                        currentTextCount--;
                        if (currentColumn.Count > 0 && currentTextCount == 0)
                        {
                            //Столбец закончился
                            Columns.Add(currentColumn);
                            currentColumn = new SortedSet<TextInfo>();
                        }
                    }
                    else
                    {
                        currentColumn.Add(e.TextInfo);
                        currentTextCount++;
                    }


                }
            }
        }

        //private class HorizontalComparer : IComparer<LineSegment2d>
        //{
        //    public int Compare(LineSegment2d l1, LineSegment2d l2)
        //    {
        //        return l1.StartPoint.Y.CompareTo(l2.StartPoint.Y);
        //    }
        //}
    }
}
