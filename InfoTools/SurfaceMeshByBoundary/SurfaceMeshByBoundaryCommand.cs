using Autodesk.AutoCAD.ApplicationServices;
using AcadDb = Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.BoundaryRepresentation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Autodesk.AutoCAD.Colors;
using Civil3DInfoTools.RBush;
using RBush;

[assembly: CommandClass(typeof(Civil3DInfoTools.SurfaceMeshByBoundary.SurfaceMeshByBoundaryCommand))]



namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    //ОТМЕНЕНО//TODO: Получить последовательности 3d точек по границам
    //Сделать отдельный объект для обозначения границ (выдваливание по траектории) 
    // - http://www.keanw.com/2010/01/sweeping-an-autocad-solid-using-net.html
    // с другим более темным цветом - https://stackoverflow.com/questions/6615002/given-an-rgb-value-how-do-i-create-a-tint-or-shade
    //С выдавливанием по траектории есть много проблем
    //Попытался создать хотябы 3d полилинию, но при большом количестве таких полилиний модель невозможно открыть в Navis
    //ГОТОВО//TODO: Попробовать с простыми линиями. С ПРОСТЫМИ ЛИНИЯМИ ВСЕ РАБОТАЕТ ГОРАЗДО ЛУЧШЕ
    //ОТМЕНЕНО//TODO: Попробовать для получения точек по границам использовать TinSurface.SampleElevations Method!
    //Этот метод возвращает единую коллекцию точек. Где пересечения с границами непонятно


    //ГОТОВО//TODO: Добавить запрос, нужно ли создавать границы (сделать запрос ключевых слов для настройки параметров)
    //ГОТОВО//TODO: Подсветить поверхность при выборе блоков (сохранять выбор поверхности для следующего раза)

    //ОТМЕНЕНО//TODO: Возможно лучше преобразовывать SubDMesh в Surface?
    //SubDMesh не всегда конвертируется в Surface (проблемы когда есть многократно вложенные островки)

    //ГОТОВО//TODO: Попробовать разобраться с созданием островков внутри треугольников --- https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf

    //TODO: Учесть  eCannotScaleNonUniformly из-за растянутых блоков.
    //Сначала нужно аппроксимировать полилинии с учетом самого большого из коэффициентов масштабирования (по X и Y)
    //Затем точки полилинии пересчитать по трансформации блока

    //ГОТОВО//TODO: Если блок имеет более одного вхождения, то сеть создавать не внутри блока, а в пространстве модели. Но при этом слой использовать такой же как у полилинии
    //ГОТОВО//TODO: Сеть создавать в слое как у первой полилинии, находящейся на самом высоком уровне дерева вложенности полилиний
    //ГОТОВО//TODO: Добавить ввод возвышения над поверхностью (запоминать ввод)
    //TODO: Выдавать предупреждение если обнаружены пересекающиеся прямые
    //TODO: Выдавать предупреждение если возникли ошибки при обходе треугольника
    //TODO: Добавить запрос у пользователя создавать ли все сети в пространстве модели или по возможности создавать внутри блоков
    //ГОТОВО//TODO: Добавить фильтр при выборе блоков

    //TODO: Команду можно вызывать только из пространства модели документа

    //ОТМЕНЕНО//TODO: Подробно изучить CurveCurveIntersector2d Class для расчета пересечений между кривыми!  IsTangential если линии только касаются в точке пересечения?
    //Алгоритм программы не позволит использовать контуры которые касаются друг друга - возможны сбои.



    //ГОТОВО//TODO: Учесть динамические блоки с несколькими состояниями видимости




    public class SurfaceMeshByBoundaryCommand
    {
        public static Database DB { get; private set; }
        public static Editor ED { get; private set; }

        public static double MeshElevation { get; private set; } = 0.05;

        public const double BORDER_DIM_MULTIPLIER = 0.5;
        public static Color ColorForBorder { get; private set; } = Color.FromColorIndex(ColorMethod.ByAci, 256);

        private static ObjectId tinSurfId = ObjectId.Null;

        private static SelectionSet acSSet = null;

        private static double approxParam = 0.02;//Максимальное отклонение от дуги при аппроксимации//TODO: Добавить ввод

        private static bool createBorders = false;

        [CommandMethod("S1NF0_SurfaceMeshByBoundary", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SurfaceMeshByBoundary()
        {

            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;
            DB = db;

            Editor ed = adoc.Editor;
            ED = ed;

            CivilDocument cdok = CivilDocument.GetCivilDocument(adoc.Database);


            try
            {
                Color currColor = db.Cecolor;
                if (!currColor.IsByLayer)//Если текущий слой не по слою, то расчитать более темный цвет для границ участков
                {
                    Color dimmerColor = Utils.GetDimmerColor(currColor, BORDER_DIM_MULTIPLIER);

                    ColorForBorder = dimmerColor;
                }
                else
                {
                    ColorForBorder = Color.FromColorIndex(ColorMethod.ByAci, 256);
                }


                if (!TinSurfIdIsValid())
                {
                    //Выбрать поверхность
                    if (!PickTinSurf(ed))
                    {
                        return;//Если выбор не успешен, прервать выполнение
                    }
                }
                //Подсветить поверхность
                HighlightTinSurf(true);


                //Запрос ключевых слов
                while (true)
                {
                    const string kw1 = "ПОВерхность";
                    const string kw2 = "ВОЗвышение";
                    const string kw3 = "ОТКлонкниеОтДуг";
                    const string kw4 = "СОЗдаватьГраницы";

                    PromptKeywordOptions pko = new PromptKeywordOptions("\nЗадайте параметры или пустой ввод для продолжения");
                    pko.Keywords.Add(kw1);
                    pko.Keywords.Add(kw2);
                    pko.Keywords.Add(kw3);
                    pko.Keywords.Add(kw4);
                    pko.AllowNone = true;
                    PromptResult pr = ed.GetKeywords(pko);
                    if (pr.Status == PromptStatus.Cancel)
                    {
                        return;
                    }

                    if (String.IsNullOrEmpty(pr.StringResult))
                    {
                        break;
                    }

                    switch (pr.StringResult)
                    {
                        case kw1:
                            if (!PickTinSurf(ed))
                            {
                                return;
                            }
                            break;
                        case kw2:
                            if (!GetMeshElevation(ed))
                            {
                                return;
                            }
                            break;
                        case kw3:
                            if (!GetApproxParam(ed))
                            {
                                return;
                            }
                            break;
                        case kw4:
                            if (!GetCreateBorders(ed))
                            {
                                return;
                            }

                            break;
                    }
                }


                //Проверка текущего набора выбора
                acSSet = null;
                PromptSelectionResult acSSPrompt;
                acSSPrompt = ed.SelectImplied();
                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    acSSet = acSSPrompt.Value;
                }
                else
                {
                    //Множественный выбор блоков
                    TypedValue[] tv = new TypedValue[]
                    {
                            new TypedValue(0, "INSERT")
                    };
                    SelectionFilter flt = new SelectionFilter(tv);


                    PromptSelectionOptions pso = new PromptSelectionOptions();
                    pso.MessageForAdding = "\nВыберите блоки участков";

                    acSSPrompt = adoc.Editor.GetSelection(pso, flt);
                    if (acSSPrompt.Status == PromptStatus.OK)
                    {
                        acSSet = acSSPrompt.Value;
                    }
                }

                if (acSSet != null)
                {
                    Common.Timer timerMain = new Common.Timer();
                    timerMain.Start();
                    foreach (SelectedObject acSSObj in acSSet)
                    {

                        string blockName = null;
                        Common.Timer timer = new Common.Timer();
                        timer.Start();
                        try
                        {
                            if (acSSObj != null)
                            {
                                //полилинии внутри блока
                                List<ObjectId> polylines = new List<ObjectId>();
                                BlockReference blockReference = null;
                                ObjectId btrId = ObjectId.Null;
                                using (Transaction tr = db.TransactionManager.StartTransaction())
                                {
                                    //блок внутри набора выбора
                                    blockReference = tr.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as BlockReference;
                                    tr.Commit();
                                }
                                Matrix3d transform = Matrix3d.Identity;
                                if (blockReference != null)
                                {
                                    //трансформация из системы координат блока в мировую систему координат
                                    transform = blockReference.BlockTransform;

                                    //Перебор всех объектов внутри блока
                                    //Найти все правильные полилинии в блоке
                                    using (Transaction tr = db.TransactionManager.StartTransaction())
                                    {
                                        btrId = blockReference.BlockTableRecord;
                                        BlockTableRecord blockTableRecord = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;

                                        if (blockTableRecord.XrefStatus == XrefStatus.NotAnXref)
                                        {
                                            blockName = blockTableRecord.Name;
                                            foreach (ObjectId id in blockTableRecord)
                                            {
                                                using (Polyline poly = tr.GetObject(id, OpenMode.ForRead) as Polyline)
                                                {
                                                    if (poly != null
                                                        && (poly.Closed || poly.GetPoint2dAt(0).Equals(poly.GetPoint2dAt(poly.NumberOfVertices - 1)))//Полилиния замкнута
                                                        && !Utils.PolylineIsSelfIntersecting(poly)//Не имеет самопересечений
                                                        && poly.Visible == true//Учет многовидовых блоков
                                                        && poly.Bounds != null
                                                        )
                                                    {
                                                        polylines.Add(id);
                                                    }
                                                }

                                                AcadDb.Entity ent = tr.GetObject(id, OpenMode.ForRead) as AcadDb.Entity;
                                                if (ent is Polyline)
                                                {
                                                    Polyline poly = ent as Polyline;


                                                }
                                            }


                                            if (polylines.Count > 0)
                                            {
                                                //Проверить все линии на пересечение друг с другом. Удалить из списка те, которые имеют пересечения
                                                HashSet<ObjectId> polylinesWithNoIntersections = new HashSet<ObjectId>(polylines);
                                                //Сделать RBush для всех полилиний
                                                RBush<SpatialEntity> polylinesTree = new RBush<SpatialEntity>();
                                                List<SpatialEntity> spatialEntities = new List<SpatialEntity>();
                                                foreach (ObjectId polyId in polylines)
                                                {
                                                    spatialEntities.Add(new SpatialEntity(polyId));
                                                }
                                                polylinesTree.BulkLoad(spatialEntities);

                                                foreach (SpatialEntity se in spatialEntities)
                                                {
                                                    //Нахождение всех объектов, расположенных в пределах BoundingBox для этой полилинии
                                                    IReadOnlyList<SpatialEntity> nearestNeighbors = polylinesTree.Search(se.Envelope);
                                                    if (nearestNeighbors.Count > 1)
                                                    {
                                                        Polyline thisPoly = tr.GetObject(se.ObjectId, OpenMode.ForRead) as Polyline;

                                                        foreach (SpatialEntity n in nearestNeighbors)
                                                        {
                                                            if (!n.Equals(se))//Всегда будет находиться та же самая полилиния
                                                            {
                                                                Polyline otherPoly = tr.GetObject(n.ObjectId, OpenMode.ForRead) as Polyline;
                                                                Point3dCollection pts = new Point3dCollection();
                                                                thisPoly.IntersectWith(otherPoly, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                                                                if (pts.Count > 0)
                                                                {
                                                                    polylinesWithNoIntersections.Remove(thisPoly.Id);
                                                                    polylinesWithNoIntersections.Remove(otherPoly.Id);
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                    }

                                                }

                                                //Аппроксимация всех полилиний, которые имеют кривизну
                                                List<Polyline> polylinesToProcess = new List<Polyline>();
                                                foreach (ObjectId polyId in polylinesWithNoIntersections)
                                                {
                                                    using (Polyline poly = tr.GetObject(polyId, OpenMode.ForRead) as Polyline)
                                                    {
                                                        polylinesToProcess.Add(ApproximatePolyBulges(poly, approxParam));//Какой допуск оптимален?
                                                    }
                                                }

                                                //Удалить все повторяющиеся подряд точки полилинии
                                                foreach (Polyline poly in polylinesToProcess)
                                                {
                                                    for (int i = 0; i < poly.NumberOfVertices;)
                                                    {
                                                        Point2d curr = poly.GetPoint2dAt(i);
                                                        int nextIndex = (i + 1) % poly.NumberOfVertices;
                                                        Point2d next = poly.GetPoint2dAt(nextIndex);

                                                        if (next.IsEqualTo(curr, new Tolerance(0.001, 0.001)))//Прореживать точки, расположенные слишком близко//TODO: Учесть масштабирование блока
                                                        {
                                                            poly.RemoveVertexAt(nextIndex);
                                                        }
                                                        else
                                                        {
                                                            i++;
                                                        }
                                                    }
                                                }


                                                //Построение дерева вложенности полилиний
                                                using (TinSurface tinSurf = tr.GetObject(tinSurfId, OpenMode.ForRead) as TinSurface)
                                                {
                                                    using (PolylineNesting polylineNesting = new PolylineNesting(tinSurf))
                                                    {
                                                        foreach (Polyline poly in polylinesToProcess)
                                                        {
                                                            poly.TransformBy(transform);
                                                            polylineNesting.Insert(poly);
                                                        }

                                                        //Расчет полигонов
                                                        polylineNesting.CalculatePoligons();

                                                        //Построение сети
                                                        using (SubDMesh sdm = polylineNesting.CreateSubDMesh())
                                                        {
                                                            List<Line> lines = new List<Line>();
                                                            if (createBorders)
                                                            {
                                                                //Создание 3d линий по границе
                                                                lines = polylineNesting.CreateBorderLines();
                                                            }

                                                            //Объекты постоены в координатах пространства модели
                                                            if (sdm != null)
                                                            {
                                                                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);
                                                                if (btr.GetBlockReferenceIds(true, false).Count > 1)
                                                                {
                                                                    //Если у блока несколько вхождений, то создавать объекты в пространстве модели
                                                                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                                                                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                                                                    ms.AppendEntity(sdm);
                                                                    tr.AddNewlyCreatedDBObject(sdm, true);

                                                                    foreach (Line line in lines)
                                                                    {
                                                                        ms.AppendEntity(line);
                                                                        tr.AddNewlyCreatedDBObject(line, true);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    //Если у блока только одно вхождение, то создавать сеть внутри блока
                                                                    sdm.TransformBy(transform.Inverse());
                                                                    btr.AppendEntity(sdm);
                                                                    tr.AddNewlyCreatedDBObject(sdm, true);

                                                                    foreach (Line line in lines)
                                                                    {
                                                                        line.TransformBy(transform.Inverse());
                                                                        btr.AppendEntity(line);
                                                                        tr.AddNewlyCreatedDBObject(line, true);
                                                                    }
                                                                }
                                                            }

                                                            foreach (Line line in lines)
                                                            {
                                                                line.Dispose();
                                                            }
                                                        }

                                                    }

                                                }

                                            }
                                        }

                                        tr.Commit();
                                    }

                                }



                            }
                        }
                        catch (System.Exception ex)
                        {
                            string message = "Возникла ошибка при обработке одного из выбранных объектов";
                            if (!String.IsNullOrEmpty(blockName))
                            {
                                message = "Возникла ошибка при обработке вхождения блока " + blockName;
                            }
                            Utils.ErrorToCommandLine(ed, message, ex);
                        }
                        timer.TimeOutput(blockName);
                    }

                    ed.WriteMessage("\n" +
                        timerMain.TimeOutput("Затрачено времени (параметр аппроксимации - " + approxParam + ")")
                        );

                    ed.Regen();
                }


            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при создании сетей по участкам поверхности");
            }
            finally
            {
                HighlightTinSurf(false);
            }

        }



        private static void HighlightTinSurf(bool on)
        {
            try
            {
                if (TinSurfIdIsValid() && DB != null)
                    using (Transaction tr = DB.TransactionManager.StartTransaction())
                    {
                        TinSurface tinSurface = tr.GetObject(tinSurfId, OpenMode.ForRead) as TinSurface;
                        if (on)
                        {
                            tinSurface.Highlight();
                        }
                        else
                        {
                            tinSurface.Unhighlight();
                        }
                        tr.Commit();
                    }
            }
            catch { }
        }

        private static bool TinSurfIdIsValid()
        {
            try
            {
                bool idIsValid = !tinSurfId.IsNull && !tinSurfId.IsErased && !tinSurfId.IsEffectivelyErased && tinSurfId.IsValid;
                if (idIsValid)
                {
                    //Проверить что поверхность принадлежит текущему документу
                    bool inCurrentDoc = false;
                    using (Transaction tr = DB.TransactionManager.StartTransaction())
                    {
                        TinSurface tinSurf = tr.GetObject(tinSurfId, OpenMode.ForRead) as TinSurface;
                        inCurrentDoc = tinSurf != null;
                        tr.Commit();
                    }
                    return inCurrentDoc;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            

        }


        /// <summary>
        /// Выбор поверхности пользователем
        /// </summary>
        /// <param name="ed"></param>
        /// <returns></returns>
        private static bool PickTinSurf(Editor ed)
        {
            //Сбросить подсветку поверхности если есть
            HighlightTinSurf(false);

            PromptEntityOptions peo = new PromptEntityOptions("\nУкажите поверхность для построения 3d тела по обертывающей");
            peo.SetRejectMessage("\nМожно выбрать только поверхность TIN");
            peo.AddAllowedClass(typeof(TinSurface), true);
            PromptEntityResult per1 = ed.GetEntity(peo);
            if (per1.Status == PromptStatus.OK)
            {
                tinSurfId = per1.ObjectId;
                //Подсветить поверхность
                HighlightTinSurf(true);
                return true;
            }
            return false;
        }


        /// <summary>
        /// Ввод возвышения над поверхностью
        /// </summary>
        /// <param name="ed"></param>
        private static bool GetMeshElevation(Editor ed)
        {
            PromptDoubleOptions pdo = new PromptDoubleOptions("Введите требуемое возвышение над поверхностью TIN");
            pdo.AllowArbitraryInput = false;
            pdo.AllowNegative = true;
            pdo.AllowNone = false;
            pdo.AllowZero = true;
            pdo.DefaultValue = MeshElevation;
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            if (pdr.Status == PromptStatus.OK)
            {
                MeshElevation = pdr.Value;
                return true;
            }
            return false;
        }


        /// <summary>
        /// Ввод максимального отклонения от дуги
        /// </summary>
        /// <param name="ed"></param>
        private static bool GetApproxParam(Editor ed)
        {
            PromptDoubleOptions pdo = new PromptDoubleOptions("Введите максимальное отклонение от дуги полилинии");
            pdo.AllowArbitraryInput = false;
            pdo.AllowNegative = false;
            pdo.AllowNone = false;
            pdo.AllowZero = false;
            pdo.DefaultValue = approxParam;
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            if (pdr.Status == PromptStatus.OK)
            {
                approxParam = pdr.Value;
                return true;
            }
            return false;
        }

        private static bool GetCreateBorders(Editor ed)
        {

            PromptKeywordOptions pko = new PromptKeywordOptions("\nСоздавать границы?");
            pko.Keywords.Add("Да");
            pko.Keywords.Add("Нет");
            pko.Keywords.Default = createBorders ? "Да" : "Нет";
            pko.AllowNone = false;
            PromptResult pr = ed.GetKeywords(pko);
            if (pr.Status == PromptStatus.OK)
            {
                switch (pr.StringResult)
                {
                    case "Да":
                        createBorders = true;
                        break;
                    case "Нет":
                        createBorders = false;
                        break;
                }
                return true;
            }
            return false;


        }




        /// <summary>
        /// Аппроксимация дуг полилинии прямыми вставками
        /// Только для замкнутой полилинии
        /// Все переданные полилинии заменяются на вновь созданные (всегда замкнутые), но не добавленные к базе данных чертежа 
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="delta"></param>
        /// <returns></returns>
        private static Polyline ApproximatePolyBulges(Polyline poly, double delta)
        {
            if (poly.HasBulges)
            {
                Polyline approxPoly = new Polyline();
                approxPoly.LayerId = poly.LayerId;
                int numVert = poly.NumberOfVertices;
                int n = 0;
                for (int i = 0; i < numVert; i++)
                {

                    double bulge = poly.GetBulgeAt(i);
                    if (bulge != 0)
                    {
                        //double r = Math.Abs(1 / bulge);//КРИВИЗНА bulge ПОЛИЛИНИИ НЕ РАВНА 1/R!!!! КАК ПРАВИЛЬНО ПЕРЕЙТИ ОТ bulge К РАДИУСУ???
                        //https://www.afralisp.net/autolisp/tutorials/polyline-bulges-part-1.php
                        double c = poly.GetPoint2dAt(i).GetDistanceTo(poly.GetPoint2dAt((i + 1) % numVert));//длина хорды
                        double s = c / 2 * bulge;
                        double r = Math.Abs((Math.Pow((c / 2), 2) + Math.Pow(s, 2)) / (2 * s));

                        double maxArcLength = Math.Acos((r - delta) / r) * r * 2;
                        double startDist = poly.GetDistanceAtParameter(i);
                        double endDist = poly.GetDistanceAtParameter((i + 1) % numVert);
                        double currDist = startDist;

                        bool exitLoop = false;
                        while (!exitLoop)
                        {
                            currDist += maxArcLength;
                            if (currDist < endDist || (endDist == 0 && currDist < poly.Length))
                            {
                                Point3d ptOnArc = poly.GetPointAtDist(currDist);
                                approxPoly.AddVertexAt(n, new Point2d(ptOnArc.X, ptOnArc.Y), 0, 0, 0);
                                n++;
                            }
                            else
                            {
                                Point3d ptOnArc = poly.GetPointAtDist(endDist);
                                approxPoly.AddVertexAt(n, new Point2d(ptOnArc.X, ptOnArc.Y), 0, 0, 0);
                                n++;
                                exitLoop = true;
                            }
                        }

                    }
                    else
                    {
                        approxPoly.AddVertexAt(n, poly.GetPoint2dAt((i + 1) % numVert), 0, 0, 0);
                        n++;
                    }
                }
                approxPoly.Closed = true;
                return approxPoly;
            }
            else
            {
                Polyline polyClone = (Polyline)poly.Clone();
                polyClone.Closed = true;
                return polyClone;
            }
        }

    }
}
