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
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: CommandClass(typeof(Civil3DInfoTools.RoadMarking.CreateBoundariesCommand))]

namespace Civil3DInfoTools.RoadMarking
{
    class CreateBoundariesCommand
    {

        int n = 0;
        Editor ed = null;

        ObjectId continuousLtype;

        [CommandMethod("S1NF0_CreateBoundaries", CommandFlags.Modal)]
        public void CreateBoundaries()
        {
            n = 0;

            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            ed = adoc.Editor;

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
                    

                    if (ssToProcess!=null && ssToProcess.Count>0)
                    {
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
                    }
                    tr.Commit();
                }





                if (dbTarget!=null)
                {
                    ObjectIdCollection createdBtrs = new ObjectIdCollection();
                    using (Transaction trTarget = dbTarget.TransactionManager.StartTransaction())
                    {
                        continuousLtype = Utils.GetContinuousLinetype(dbTarget);
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
                                Curve curve = ent as Curve;
                                if (ent is Polyline2d)
                                {
                                    Polyline poly = new Polyline();
                                    poly.ConvertFrom(ent, false);
                                    curve = poly;
                                }

                                createdBtrId = ProcessEntity(curve as Polyline, btTarget);
                            }
                            else if (ent is Hatch)
                            {
                                createdBtrId = ProcessEntity(ent as Hatch, btTarget);
                            }

                            //Вставка нового вхождения блока
                            if (createdBtrId != ObjectId.Null)
                            {
                                createdBtrs.Add(createdBtrId);
                                //ЗДЕСЬ НЕ СОЗДАЕТ ВХОЖДЕНИЕ БЛОКА!
                                //BlockReference br = new BlockReference(Point3d.Origin, createdBtrId);
                                //msTarget.AppendEntity(br);
                                //trTarget.AddNewlyCreatedDBObject(br, true);
                            }


                            //Удаление исходного объекта
                            ent.Erase();
                        }




                        trTarget.Commit();

                    }

                    //Создание вхождений блоков
                    using (Transaction trTarget = dbTarget.TransactionManager.StartTransaction())
                    {
                        BlockTable btTarget
                            = trTarget.GetObject(dbTarget.BlockTableId, OpenMode.ForWrite) as BlockTable;
                        BlockTableRecord msTarget
                                = (BlockTableRecord)trTarget
                                .GetObject(btTarget[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        foreach (ObjectId id in createdBtrs)
                        {
                            BlockReference br = new BlockReference(Point3d.Origin, id);
                            msTarget.AppendEntity(br);
                            trTarget.AddNewlyCreatedDBObject(br, true);
                        }
                    }


                    //TODO: Учесть возможные ошибки из-за отсутствия прав
                    //Создание нового чертежа и открытие его
                    //string targetDocFullPath = null;
                    //int n = 0;
                    //do
                    //{
                    //    targetDocFullPath = Path.Combine(Path.GetDirectoryName(adoc.Name), "RoadMarkingBoundaries" + n + ".dwg");
                    //    n++;
                    //} while (File.Exists(targetDocFullPath));

                    string targetDocFullPath
                        = Common.Utils.GetNonExistentFileName(Path.GetDirectoryName(adoc.Name), "RoadMarkingBoundaries", "dwg");
                    dbTarget.SaveAs(targetDocFullPath, DwgVersion.Current);
                    try
                    {
                        //если текущий документ никуда не сохранен, то не сможет открыть
                        adocTarget = Application.DocumentManager.Open(targetDocFullPath, false);
                    }
                    catch { }
                    
                }

                





            }
            catch (System.Exception ex)
            {
                //Utils.ErrorToCommandLine(ed, "Ошибка при создании контуров разметки", ex);
                CommonException(ex, "Ошибка при создании контуров разметки");
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


        private ObjectId ProcessEntity(Polyline curve, BlockTable bt)
        {
            Database db = bt.Database;
            ObjectId createdBtrId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {

                //Масштаб типа линии
                double ltScale = curve.LinetypeScale;
                //Глобальная ширина линии

                double offsetDist = 0;
                offsetDist = (curve as Polyline).GetStartWidthAt(0) / 2;

                if (offsetDist == 0)
                {
                    throw new System.Exception("Не определена глобальная ширина кривой");
                }


                //Анализ типа линии. Получить длину штриха и длину пробела
                LinetypeTableRecord ltype = tr.GetObject(curve.LinetypeId, OpenMode.ForRead) as LinetypeTableRecord;
                //Создать слой, который будет называться как тип линии
                string layerName = curve.Linetype;
                LayerTableRecord layerSample = tr.GetObject(curve.LayerId, OpenMode.ForRead) as LayerTableRecord;
                ObjectId layerId = Utils.CreateLayerIfNotExists(layerName, db, tr, layerSample);

                //string ltypeDef = ltype.Comments;
                //double patternLength = ltype.PatternLength;
                //int numDashes = ltype.NumDashes;
                if (ltype.Name.Equals("ByLayer"))
                {
                    ltype = tr.GetObject(layerSample.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                }
                List<double> pattern = Utils.GetLinePattern(ltype, ltScale);

                bool startFromDash = false;//паттерн начинается со штриха (не пробела)
                if (pattern.Count > 0)
                {
                    startFromDash = pattern[0] > 0;
                }
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
                else //if (startFromDash)//Сплошная линия
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
                    btr.Name = Guid.NewGuid().ToString();
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
                                DBObjectCollection offsetCurves = axisCurve.GetOffsetCurves(offsetDist);
                                Curve c1 = Utils.GetFirstDBObject(offsetCurves) as Curve;
                                offsetCurves = axisCurve.GetOffsetCurves(-offsetDist);
                                Curve c2 = Utils.GetFirstDBObject(offsetCurves) as Curve;
                                if (!curve.Closed)//Если кривая не замкнута, то попытаться построить замкнутый контур из правой и левой кривой отступа 
                                {
                                    try
                                    {
                                        Line concatLine = new Line(c1.EndPoint, c2.EndPoint/*.StartPoint*/);


                                        IntegerCollection joined
                                            = c1.JoinEntities(new Entity[] { concatLine, c2 });
                                        if (joined.Count < 2)
                                        {
                                            throw new System.Exception("Соединение примитивов не выполнено");
                                        }

                                        if (c1 is Polyline)
                                        {
                                            PrepareCurve(c1, layerId);
                                        }
                                        else
                                        {
                                            throw new System.Exception("При соединении примитивов создан невалидный объект");
                                        }

                                        btr.AppendEntity(c1);
                                        tr.AddNewlyCreatedDBObject(c1, true);
                                    }
                                    catch (System.Exception ex)
                                    {
                                        ed.WriteMessage("\nВНИМАНИЕ: Возникла ошибка при попытке создания контура в блоке "+ btr.Name);
                                    }
                                }
                                else//Если кривая замкнута, то просто создать левую и правую кривую
                                {
                                    PrepareCurve(c1, layerId);
                                    PrepareCurve(c2, layerId);
                                    btr.AppendEntity(c1);
                                    tr.AddNewlyCreatedDBObject(c1, true);
                                    btr.AppendEntity(c2);
                                    tr.AddNewlyCreatedDBObject(c2, true);
                                }


                            }
                            currIsDash = !currIsDash;//Считаем, что штрихи и пробелы встречаются строго поочереди
                        }
                    }

                }

                tr.Commit();
            }


            return createdBtrId;
        }


        


        private void PrepareCurve(Curve c1, ObjectId layerId)
        {
            Polyline poly = c1 as Polyline;
            poly.Closed = true;

            //Убрать глобальную ширину
            int numVert = poly.NumberOfVertices;
            for (int i = 0; i < numVert; i++)
            {
                poly.SetStartWidthAt(i, 0);
                poly.SetEndWidthAt(i, 0);
            }

            //Перенести в слой, который называется так же как тип линии
            poly.LayerId = layerId;

            //Сбросить тип линии
            if (continuousLtype != ObjectId.Null)
            {
                poly.LinetypeId = continuousLtype;
            }
        }


        private ObjectId ProcessEntity(Hatch hatch, BlockTable bt)
        {
            //Для создания контура штриховки
            //Hatch.NumberOfLoops
            //Hatch.GetLoopAt
            //HatchLoop.Polyline!!!

            //Получить полилинии
            List<Polyline> polylines = new List<Polyline>();
            for (int i = 0; i < hatch.NumberOfLoops; i++)
            {
                HatchLoop hl = hatch.GetLoopAt(i);
                BulgeVertexCollection bvc = hl.Polyline;
                Curve2dCollection cc = hl.Curves;
                Polyline poly = null;

                if (bvc != null && bvc.Count > 0)
                {
                    //сюда попадет если контур состоит из отрезков и круговых дуг
                    poly = new Polyline();
                    int vertNum = 0;
                    foreach (BulgeVertex bv in bvc)
                    {
                        poly.AddVertexAt(vertNum, bv.Vertex, bv.Bulge, 0, 0);
                        vertNum++;
                    }
                }
                else if (cc != null && cc.Count > 0)
                {
                    //сюда попадет если контур состоит только из отрезков или включает сложные линии (сплайны, эллипсы)
                    poly = new Polyline();
                    //добавить первую вершину
                    //poly.AddVertexAt(0, cc[0].StartPoint, 0, 0, 0);
                    int vertNum = 0;
                    foreach (Curve2d c in cc)
                    {
                        if (c is LinearEntity2d)
                        {
                            //добавить сегмент без кривизны
                            LinearEntity2d l2d = c as LinearEntity2d;
                            poly.AddVertexAt(vertNum, l2d.EndPoint, 0, 0, 0);
                            vertNum++;
                        }
                        else
                        {
                            //просто пропустить эту кривую (может это сплайн или еще чего)
                            //throw new System.Exception("Неверный тип 2d кривой - " + c.GetType().ToString());
                        }
                    }

                }



                if (poly != null && poly.NumberOfVertices > 1)
                {
                    //присвоить слой штриховки
                    poly.LayerId = hatch.LayerId;
                    //замкнуть
                    poly.Closed = true;

                    polylines.Add(poly);
                }

            }

            //Создать новый блок и сохранить в него полилинии
            ObjectId createdBtrId = ObjectId.Null;
            Database db = bt.Database;
            if (polylines.Count > 0)
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {

                    //Создать новый блок для сохранения замкнутых контуров

                    BlockTableRecord btr = new BlockTableRecord();
                    btr.Name = n.ToString();
                    n++;
                    createdBtrId = bt.Add(btr);
                    tr.AddNewlyCreatedDBObject(btr, true);

                    if (createdBtrId != ObjectId.Null)
                    {
                        foreach (Polyline p in polylines)
                        {
                            btr.AppendEntity(p);
                            tr.AddNewlyCreatedDBObject(p, true);
                        }
                    }


                    tr.Commit();
                }
            }






            return createdBtrId;
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
