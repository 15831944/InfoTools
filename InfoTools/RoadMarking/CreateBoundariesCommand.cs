using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CommandClass(typeof(Civil3DInfoTools.RoadMarking.CreateBoundariesCommand))]

namespace Civil3DInfoTools.RoadMarking
{
    class CreateBoundariesCommand
    {

        int n = 0;

        [CommandMethod("CreateBoundaries", CommandFlags.Modal)]
        public void CreateBoundaries()
        {
            n = 0;

            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            Database dbTarget = null;

            Document adocTarget = null;

            SelectionSet ssToProcess = null;
            try
            {


                //TODO: Запуск только в пространстве модели

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    //Запрос у пользователя выбора слоев, объекты в которых нужно подвергнуть обработке
                    //Подсветка объектов, подлежащих обработке (линии с глобальной шириной и штриховки)


                    SortedSet<string> selectedLayers = new SortedSet<string>();

                    while (true)
                    {
                        if (selectedLayers.Count > 0)
                        {
                            ed.WriteMessage("\nВыбраны слои " + String.Join(", ", selectedLayers));
                        }

                        PromptEntityOptions peo
                            = new PromptEntityOptions("\nУкажите объект для добавления слоя");
                        PromptEntityResult res = ed.GetEntity(peo);



                        //PromptSelectionOptions options = new PromptSelectionOptions();
                        //options.SingleOnly = true;
                        //options.MessageForAdding = "\nУкажите объекты для выбора слоев разметки";
                        //PromptSelectionResult res = ed.GetSelection(options);
                        if (res.Status == PromptStatus.OK)
                        {
                            ObjectId pickedId = res.ObjectId;
                            if (pickedId != null && pickedId != ObjectId.Null)
                            {
                                //Подсветить все объекты, которые подлежат обработке

                                //Добавить выбранный слой
                                Entity ent = tr.GetObject(pickedId, OpenMode.ForRead) as Entity;
                                selectedLayers.Add(ent.Layer);



                                //Фильтр для набора выбора
                                TypedValue[] tv = new TypedValue[] //{ new TypedValue(0, "POLYLINE,HATCH") };
                                {
                                    //new TypedValue(-4, "<AND"),

                                    new TypedValue(-4, "<OR"),
                                        new TypedValue(0, "HATCH"),//Либо штриховка

                                        new TypedValue(-4, "<AND"),//Либо полилиния, у которой есть глобальная ширина
                                            new TypedValue(0, "LWPOLYLINE,POLYLINE"),
                                            new TypedValue(-4, "!="),
                                            new TypedValue(40, 0),
                                        new TypedValue(-4, "AND>"),

                                    new TypedValue(-4, "OR>"),

                                    new TypedValue(8, String.Join(",", selectedLayers)),//В любом из выбранных слоев!

                                    //new TypedValue(-4, "AND>"),
                                };
                                SelectionFilter flt = new SelectionFilter(tv);

                                PromptSelectionResult res1 = ed.SelectAll(flt);
                                if (res1.Status == PromptStatus.OK)
                                {
                                    ssToProcess = res1.Value;
                                    //подсветить

                                    foreach (SelectedObject acSSObj in ssToProcess)
                                    {
                                        Entity entToHighlight = tr.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as Entity;
                                        entToHighlight.Highlight();
                                    }
                                }

                                continue;
                            }
                        }
                        break;
                    }

                    ObjectIdCollection idToExtract = new ObjectIdCollection();//выбранные объекты
                    foreach (SelectedObject acSSObj in ssToProcess)
                    {
                        idToExtract.Add(acSSObj.ObjectId);
                    }



                    //Создание новой базы данных для записи обработанных объектов и создания нового чертежа
                    dbTarget = new Database(true, false);
                    //Единицы измерения ВСЕГДА метры
                    dbTarget.Insunits = UnitsValue.Meters;
                    //Копирование объектов
                    db.Wblock(dbTarget, idToExtract, Point3d.Origin, DuplicateRecordCloning.Ignore);



                    tr.Commit();
                }







                ObjectIdCollection createdBtrs = new ObjectIdCollection();
                using (Transaction trTarget = dbTarget.TransactionManager.StartTransaction())
                {
                    //Обработка объектов в новой базе данных
                    BlockTable btTarget
                        = trTarget.GetObject(dbTarget.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    BlockTableRecord msTarget
                            = (BlockTableRecord)trTarget
                            .GetObject(btTarget[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    foreach (ObjectId id in msTarget)
                    {
                        Entity ent = trTarget.GetObject(id, OpenMode.ForWrite) as Entity;
                        ObjectId createdBtrId = ObjectId.Null;
                        //Для полилинии имя нового блока должно быть равно имени типа линии + номер
                        //Для штриховки - "Штриховка" + номер
                        if (ent is Curve)
                        {
                            createdBtrId = ProcessEntity(ent as Curve, btTarget);
                        }
                        else if (ent is Hatch)
                        {
                            createdBtrId = ProcessEntity(ent as Hatch, btTarget);
                        }

                        //Вставка нового вхождения блока
                        if (createdBtrId != ObjectId.Null)
                        {
                            createdBtrs.Add(createdBtrId);
                            //НЕ СОЗДАЕТ ВХОЖДЕНИЕ БЛОКА!
                            //BlockReference br = new BlockReference(Point3d.Origin, createdBtrId);
                            //msTarget.AppendEntity(br);
                            //trTarget.AddNewlyCreatedDBObject(br, true);
                        }


                        //Удаление исходного объекта
                        ent.Erase();
                    }



                    trTarget.Commit();

                }




                //Создание нового чертежа и открытие его
                string targetDocFullPath = Path.Combine(Path.GetDirectoryName(adoc.Name), "test.dwg");
                dbTarget.SaveAs(targetDocFullPath, DwgVersion.Current);
                adocTarget = Application.DocumentManager.Open(targetDocFullPath, false);





            }
            catch (System.Exception ex)
            {
                Utils.ErrorToCommandLine(ed, "Ошибка при выполнении команды InsertByCoordinates", ex);
                if (adocTarget != null)
                {
                    //В случае ошибки удалить 
                }

            }
            finally
            {

                //Сброс подсветки!
                Unhighlight(ssToProcess, db);
            }
        }

        private void Unhighlight(SelectionSet ssToProcess, Database db)
        {
            if (ssToProcess != null)
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject acSSObj in ssToProcess)
                    {
                        Entity ent = tr.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as Entity;
                        ent.Unhighlight();
                    }
                    tr.Commit();
                }

            }
        }


        private ObjectId ProcessEntity(Curve curve, BlockTable bt)
        {
            Database db = bt.Database;
            ObjectId createdBtrId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms
                            = (BlockTableRecord)tr
                            .GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);


                //Масштаб типа линии
                double ltScale = curve.LinetypeScale;

                //Анализ типа линии. Получить длину штриха и длину пробела
                LinetypeTableRecord ltype = tr.GetObject(curve.LinetypeId, OpenMode.ForRead) as LinetypeTableRecord;
                //string ltypeDef = ltype.Comments;
                //double patternLength = ltype.PatternLength;
                //int numDashes = ltype.NumDashes;

                List<double> pattern = new List<double>();
                for (int i = 0; i < ltype.NumDashes; i++)
                {
                    //TODO: Учесть возможность вставки подряд двух пробелов или двух штрихов
                    //(если это вообще возможно сделать)
                    double dash = ltype.DashLengthAt(i);
                    if (dash != 0)
                    {
                        pattern.Add(dash * ltScale);//сразу домножить на масштаб типа линии
                    }

                }

                bool startFromDash = pattern[0] > 0;//паттерн начинается со штриха (не пробела)
                //SortedSet<Curve> curves = new SortedSet<Curve>(new CurvePositionComparer(curve));
                List<Curve> curves = new List<Curve>();
                if (pattern.Count > 1)
                {
                    //Расчитать параметры для разбивки полилинии
                    DoubleCollection splittingParams = new DoubleCollection();

                    double currParam = curve.StartParam;
                    double currDist = curve.GetDistanceAtParameter(currParam);
                    double length = curve.GetDistanceAtParameter(curve.EndParam);
                    int dashNum = 0;
                    while (true)
                    {
                        currDist += Math.Abs(pattern[dashNum]);
                        if (currDist < length)
                        {

                            currParam = curve.GetParameterAtDistance(currDist);//Параметр в конце текущего штриха
                            int nextDashNum = (dashNum + 1) % pattern.Count;//следующий штрих согласно паттерну

                            if (Math.Sign(pattern[dashNum]) != Math.Sign(pattern[nextDashNum]))
                            {
                                //Если знаки текущего штриха и следующего разные
                                //(то есть граница между штрихом и пробелом),
                                //то добавить текущий параметр в разделители
                                splittingParams.Add(currParam);
                            }
                            dashNum = nextDashNum;//переход к следующему штриху
                        }
                        else
                        {
                            break;
                        }

                    }




                    //Использовать метод Curve.GetSplitCurves для создания сегментов
                    DBObjectCollection splitted = curve.GetSplitCurves(splittingParams);
                    foreach (DBObject dbo in splitted)
                    {
                        if (dbo is Curve)
                        {
                            curves.Add(dbo as Curve);
                        }
                    }
                }
                else if (startFromDash)//Сплошная линия
                {
                    object o = curve.Clone();
                    if (o != null && o is Curve)
                    {
                        curves.Add(o as Curve);
                    }

                }

                if (curves.Count > 0)
                {
                    //Создать новый блок для сохранения замкнутых контуров

                    BlockTableRecord btr = new BlockTableRecord();
                    btr.Name = n.ToString();
                    n++;
                    createdBtrId = bt.Add(btr);
                    tr.AddNewlyCreatedDBObject(btr, true);

                    if (createdBtrId != ObjectId.Null)
                    {
                        bool currIsDash = startFromDash;
                        foreach (Curve axisCurve in curves)
                        {
                            if (currIsDash)
                            {
                                //Использовать метод Curve.GetOffsetCurves
                                //для создания линий границ справа и слева в соответствии с глобальной шириной
                                //Создание замкнутого контура в новом блоке

                                //test
                                btr.AppendEntity(axisCurve);
                                tr.AddNewlyCreatedDBObject(axisCurve, true);

                            }
                            currIsDash = !currIsDash;//Считаем, что штрихи и пробелы встречаются строго поочереди
                        }
                    }

                }

                tr.Commit();
            }


            return createdBtrId;
        }

        private ObjectId ProcessEntity(Hatch hatch, BlockTable bt)
        {
            //Для создания контура штриховки
            //Hatch.NumberOfLoops
            //Hatch.GetLoopAt
            //HatchLoop.Polyline!!!

            return ObjectId.Null;
        }





        private class CurvePositionComparer : IComparer<Curve>
        {
            private Curve baseCurve;

            public CurvePositionComparer(Curve baseCurve)
            {
                this.baseCurve = baseCurve;
            }

            public int Compare(Curve x, Curve y)
            {
                //Сравнить две кривые по их положению вдоль базовой кривой
                Point3d pt1 = x.StartPoint;
                Point3d pt2 = y.StartPoint;
                double param1 = baseCurve.GetParameterAtPoint(pt1);
                double param2 = baseCurve.GetParameterAtPoint(pt2);

                return param1.CompareTo(param2);

            }
        }
    }
}
