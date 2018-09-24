using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Collections.Generic;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using System.Linq;
using Excel = Microsoft.Office.Interop.Excel;
using System;

//[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.GeologColumnsToExcelCommand2))]

namespace Civil3DInfoTools.AuxiliaryCommands
{
    public class GeologColumnsToExcelCommand2
    {

        //[CommandMethod("S1NF0_GeologColumnsToExcel2", CommandFlags.Modal)]
        public void GeologColumnsToExcel2()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            List<Entity> selectedEntities = new List<Entity>();
            try
            {
                Point3d? pt1 = null;
                Point3d? pt2 = null;
                //Обвести рамкой область таблицы
                if (Utils.SpecifyWindow(out pt1, out pt2, ed))
                {
                    Point3d _pt1 = pt1.Value;
                    Point3d _pt2 = pt2.Value;

                    double minX = 0;
                    double minY = 0;
                    double maxX = 0;
                    double maxY = 0;
                    if (_pt1.X < _pt2.X)
                    {
                        minX = _pt1.X;
                        maxX = _pt2.X;
                    }
                    else
                    {
                        minX = _pt2.X;
                        maxX = _pt1.X;
                    }
                    if (_pt1.Y < _pt2.Y)
                    {
                        minY = _pt1.Y;
                        maxY = _pt2.Y;
                    }
                    else
                    {
                        minY = _pt2.Y;
                        maxY = _pt1.Y;
                    }

                    Point2d minPt1 = new Point2d(minX, minY);
                    Point2d maxPt2 = new Point2d(maxX, maxY);
                    //Зумирование по рамке
                    Utils.ZoomWin(ed, _pt1, _pt2);
                    //Получить все линии и тексты по рамке
                    TypedValue[] tv = new TypedValue[] { new TypedValue(0, "LWPOLYLINE,LINE,TEXT,MTEXT") };
                    SelectionFilter flt = new SelectionFilter(tv);
                    PromptSelectionResult res = ed.SelectCrossingWindow(_pt1, _pt2, flt);
                    if (res.Status == PromptStatus.OK)
                    {
                        SelectionSet acSSet = res.Value;

                        List<Entity> textEnts = new List<Entity>();
                        List<LineSegment2d> verticalLines = new List<LineSegment2d>();//Точки линии снизу вверх
                        List<LineSegment2d> horizontalLines = new List<LineSegment2d>();//Точки линии слева направо

                        Line2d bottom = new Line2d(minPt1, new Vector2d(1, 0));//по низу
                        Line2d leftBorder = new Line2d(minPt1, new Vector2d(0, 1));//по левой стороне
                        Line2d top = new Line2d(maxPt2, new Vector2d(1, 0));//по верху
                        Line2d rightBorder = new Line2d(maxPt2, new Vector2d(0, 1));//по правой стороне

                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            //Подсветить выбранное. Дать возможность дополнительно выбрать вручную тексты, которые вылезают за рамку
                            HashSet<ObjectId> selectedIds = new HashSet<ObjectId>();
                            foreach (ObjectId id in acSSet.GetObjectIds())
                            {
                                selectedIds.Add(id);
                                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                selectedEntities.Add(ent);
                            }
                            Utils.Highlight(selectedEntities, true);
                            tv = new TypedValue[] { new TypedValue(0, "TEXT,MTEXT") };
                            flt = new SelectionFilter(tv);
                            PromptSelectionOptions pso = new PromptSelectionOptions();
                            pso.MessageForAdding = "\nВыберите дополнительные тексты, вышедшие за рамки таблицы если необходимо (TEXT,MTEXT)";

                            PromptSelectionResult acSSPrompt = adoc.Editor.GetSelection(pso, flt);
                            if (acSSPrompt.Status == PromptStatus.OK)
                            {
                                foreach (SelectedObject acSSObj in acSSet)
                                {
                                    if (acSSObj != null)
                                    {
                                        if (!selectedIds.Contains(acSSObj.ObjectId))
                                        {
                                            selectedIds.Add(acSSObj.ObjectId);
                                            Entity ent = tr.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as Entity;
                                            selectedEntities.Add(ent);
                                        }
                                    }
                                }
                            }

                            //Разобрать все линии на отрезки. Разбить на 2 группы: вертикальные и горизонтальные.
                            //Не брать в расчет линии, проходящие строго по границам указанной рамки
                            //Наклонные линии нигде не учитывать
                            List<Line2d> borderGuidings = new List<Line2d>() { bottom, leftBorder, top, rightBorder };
                            foreach (Entity ent in selectedEntities)
                            {
                                if (ent is MText || ent is DBText)
                                {
                                    textEnts.Add(ent);
                                }
                                else
                                {
                                    if (ent is Polyline)
                                    {
                                        //TODO: Полилиния может иметь дуговые вставки. Учитывать это?
                                        Polyline polyline = ent as Polyline;
                                        for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
                                        {
                                            Point2d segPt1 = polyline.GetPoint2dAt(i);
                                            Point2d segPt2 = polyline.GetPoint2dAt(i + 1);
                                            if (!OverlapBorder(segPt1, segPt2, borderGuidings)
                                                && InsideBorders(segPt1, segPt2, minX, minY, maxX, maxY))
                                                ResolveLineType(segPt1, segPt2, verticalLines, horizontalLines);
                                        }
                                    }
                                    else if (ent is Line)
                                    {
                                        Line line = ent as Line;
                                        Point2d segPt1 = new Point2d(line.StartPoint.X, line.StartPoint.Y);
                                        Point2d segPt2 = new Point2d(line.EndPoint.X, line.EndPoint.Y);
                                        if (!OverlapBorder(segPt1, segPt2, borderGuidings)
                                            && InsideBorders(segPt1, segPt2, minX, minY, maxX, maxY))
                                            ResolveLineType(segPt1, segPt2, verticalLines, horizontalLines);
                                    }
                                }
                            }
                            tr.Commit();
                        }


                        //Определить координаты X по которым проходят сплошные вертикальные линии - это границы столбцов
                        //Разбить вертикальные линии на группы по координате X
                        SortedDictionary<double, SortedSet<LineSegment2d>> columnBorders
                                = new SortedDictionary<double, SortedSet<LineSegment2d>>();
                        foreach (LineSegment2d vertLine in verticalLines)
                        {
                            double x = vertLine.StartPoint.X;
                            SortedSet<LineSegment2d> lines = null;
                            columnBorders.TryGetValue(x, out lines);
                            if (lines == null)
                            {
                                lines = new SortedSet<LineSegment2d>(new VertLineComparer());
                                columnBorders.Add(x, lines);
                            }
                            lines.Add(vertLine);
                        }

                        //Оставить только те вертикальные линии, которые проходят от верха до низа выбранной области
                        List<double> keysToRemove = new List<double>();
                        foreach (KeyValuePair<double, SortedSet<LineSegment2d>> kvp in columnBorders)
                        {
                            //Первый отрезок пересекает нижнюю границу заданной области
                            //Последний отрезок пересекает верхнюю границу заданной области
                            if ((new CurveCurveIntersector2d(kvp.Value.First(), bottom)).NumberOfIntersectionPoints == 0
                                || (new CurveCurveIntersector2d(kvp.Value.Last(), top)).NumberOfIntersectionPoints == 0)
                            {
                                keysToRemove.Add(kvp.Key);
                                break;
                            }

                            //Между отрезками нет зазоров
                            LineSegment2d previousSeg = null;
                            foreach (LineSegment2d seg in kvp.Value)
                            {
                                if (previousSeg != null)
                                {
                                    if (!previousSeg.EndPoint.IsEqualTo(seg.StartPoint) && previousSeg.EndPoint.Y < seg.StartPoint.Y)
                                    {
                                        //Между отрезками есть зазор
                                        keysToRemove.Add(kvp.Key);
                                        break;
                                    }
                                }
                                previousSeg = seg;
                            }
                        }

                        foreach (double key in keysToRemove)
                        {
                            columnBorders.Remove(key);
                        }

                        if (columnBorders.Count == 0)
                        {
                            ed.WriteMessage("Таблица не распознана. В заданной области должны быть сплошные вертикальные линии, обозначающие границы столбцов");
                            return;
                        }

                        //Для каждой колонки образуется список точек пересечений с горизонтальными отрезками, обозначающих низ ячейки
                        List<double> colBorders = new List<double>() { minX };
                        colBorders.AddRange(columnBorders.Keys);
                        colBorders.Add(maxX);
                        List<ColumnInfo> columns = new List<ColumnInfo>();
                        for (int i = 0; i < colBorders.Count - 1; i++)
                        {
                            ColumnInfo ci = new ColumnInfo(colBorders[i], colBorders[i + 1]);
                            ci.CalcRowBottomLevels(horizontalLines, minY);
                            columns.Add(ci);
                        }

                        //Каждый текст относится к столбцу, а затем по списку точек столбца относится к конкретной строке
                        //(ближайшая нижележащая точка указывает на номер строки)

                        foreach (Entity textEnt in textEnts)
                        {
                            string contents = (textEnt as MText)?.Text;
                            if (contents == null)
                            {
                                contents = (textEnt as DBText)?.TextString;
                            }

                            Extents3d? ext = textEnt.Bounds;
                            if (ext != null)
                            {
                                Point2d textLocation =
                                    new Point2d((ext.Value.MinPoint.X + ext.Value.MaxPoint.X) / 2,
                                    (ext.Value.MinPoint.Y + ext.Value.MaxPoint.Y) / 2);
                                double colPosition = textLocation.X;
                                if (colPosition < minX)
                                    columns.First().AddTextToCell(textLocation, contents);
                                else if (colPosition > maxX)
                                    columns.Last().AddTextToCell(textLocation, contents);
                                else
                                {
                                    //Найти колонку, в которую добавляется текст и добавить его
                                    ColumnInfo col = columns.Find(c => c.LeftSide <= colPosition && c.RightSide >= colPosition);
                                    if (col != null)
                                    {
                                        col.AddTextToCell(textLocation, contents);
                                    }
                                }
                            }
                        }


                        //Заполнить таблицу эксель
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

                            int colNum = 1;
                            foreach (ColumnInfo col in columns)
                            {
                                int rowNum = 1;
                                foreach (SortedDictionary<Point2d, string> cellContent in col.RowBottomLevels.Values.Reverse())
                                {
                                    string val = string.Join(" ", cellContent.Values);

                                    wSheet.Cells[rowNum, colNum] = val;
                                    //if (colNum == 2)
                                    //{
                                    //    wSheet.Cells[rowNum, 1] = boreholeName;
                                    //}
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
                CommonException(ex, "Ошибка при получении таблицы Excel по таблице в AutoCAD");
            }
            finally
            {
                Utils.Highlight(selectedEntities, false);
            }


        }


        private bool OverlapBorder(Point2d segPt1, Point2d segPt2, List<Line2d> borderGuidings)
        {
            LineSegment2d line = new LineSegment2d(segPt1, segPt2);
            foreach (Line2d border in borderGuidings)
            {
                if (border.Overlap(line) != null)
                {
                    return true;
                }
            }
            return false;
        }

        private bool InsideBorders(Point2d segPt1, Point2d segPt2, double minX, double minY, double maxX, double maxY)
        {
            return
            (segPt1.X <= maxX && segPt1.X >= minX && segPt1.Y <= maxY && segPt1.Y >= minY)
            || (segPt2.X <= maxX && segPt2.X >= minX && segPt2.Y <= maxY && segPt2.Y >= minY);
        }

        private void ResolveLineType(Point2d segPt1, Point2d segPt2,
            List<LineSegment2d> verticalLines, List<LineSegment2d> horizontalLines)
        {
            if (!segPt1.IsEqualTo(segPt2))
            {
                List<Point2d> pts = new List<Point2d>() { segPt1, segPt2 };

                Vector2d vector = segPt2 - segPt1;
                if (vector.IsParallelTo(Vector2d.YAxis))
                {
                    //Это вертикальная линия
                    pts.Sort((p1, p2) =>
                    {
                        return p1.Y.CompareTo(p2.Y);//Сначала нижняя, потом верхняя
                    });

                    verticalLines.Add(new LineSegment2d(pts[0], pts[1]));
                }
                else if (vector.IsParallelTo(Vector2d.XAxis))
                {
                    //Это горизонтальная линия
                    pts.Sort((p1, p2) =>
                    {
                        return p1.X.CompareTo(p2.X);//Сначала левая, потом правая
                    });

                    horizontalLines.Add(new LineSegment2d(pts[0], pts[1]));
                }
            }

        }

        private class VertLineComparer : IComparer<LineSegment2d>
        {
            public int Compare(LineSegment2d l1, LineSegment2d l2)
            {
                return l1.StartPoint.Y.CompareTo(l2.StartPoint.Y);
            }
        }


        private class ColumnInfo
        {
            public double LeftSide { get; set; }

            public double RightSide { get; set; }

            private double center;

            //Мнимая линия по середине между сплошными вертикальными линиями.
            //Line2d ImaginaryCenterLine { get; set; }

            /// <summary>
            /// Содержимое ячеек, привязанное к низу строк таблицы
            /// </summary>
            public SortedDictionary<double, SortedDictionary<Point2d, string>> RowBottomLevels { get; set; }
                = new SortedDictionary<double, SortedDictionary<Point2d, string>>();

            public ColumnInfo(double leftSide, double rightSide)
            {
                LeftSide = leftSide;
                RightSide = rightSide;
                center = (LeftSide + RightSide) / 2;
                //ImaginaryCenterLine = new Line2d(new Point2d(Center, 0), Vector2d.YAxis);
            }


            //Пересечения с невертикальными отрезками
            public void CalcRowBottomLevels(List<LineSegment2d> horizontalLines, double minY)
            {
                foreach (LineSegment2d horSeg in horizontalLines)
                {
                    double level = horSeg.StartPoint.Y;
                    if (!RowBottomLevels.ContainsKey(level)
                        && horSeg.StartPoint.X <= center && horSeg.EndPoint.X >= center)
                    {
                        RowBottomLevels.Add(level, new SortedDictionary<Point2d, string>(new TextLocationComparer()));
                    }
                }
                //В любом случае добавить границу строк в конце выделенной области
                RowBottomLevels.Add(minY, new SortedDictionary<Point2d, string>(new TextLocationComparer()));
            }

            public void AddTextToCell(Point2d textLocation, string contents)
            {
                SortedDictionary<Point2d, string> cellContent
                    = RowBottomLevels.Reverse().First(kvp => kvp.Key <= textLocation.Y).Value;
                if (cellContent == null)
                    cellContent = RowBottomLevels.First().Value;
                cellContent.Add(textLocation, contents);
            }

            private class TextLocationComparer : IComparer<Point2d>
            {
                public int Compare(Point2d p1, Point2d p2)
                {
                    int comparison = p1.Y.CompareTo(p2.Y);
                    if (comparison != 0)
                    {
                        return comparison;
                    }
                    else
                    {
                        return p1.X.CompareTo(p2.X);
                    }
                }
            }

        }



    }
}
