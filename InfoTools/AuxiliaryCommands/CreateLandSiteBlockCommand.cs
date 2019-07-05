using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.Project;
using Autodesk.Gis.Map.ObjectData;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.CreateLandSiteBlockCommand))]

namespace Civil3DInfoTools.AuxiliaryCommands
{
    public class CreateLandSiteBlockCommand
    {
        private static short colorIndex = 82;

        private static bool firstTimeCall = true;

        private static bool selectOnlyPolylines = true;

        private static bool setColor = true;


        /// <summary>
        /// Выбор полилиний, указание текстового примитива, создание блока с этими полилиниями и названием из указанного текста
        /// </summary>
        [CommandMethod("S1NF0_CreateLandSiteBlock", CommandFlags.Modal)]
        public void CreateLandSiteBlock()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            List<Entity> selectedPolylines = null;



            try
            {
                if (firstTimeCall)
                {
                    while (true)
                    {
                        string kw1 = selectOnlyPolylines ? "НЕТолькоПолилинии" : "ТолькоПолилинии";
                        string kw2 = setColor ? "ВсеЦветаПоСлою" : "МенятьЦвет";
                        PromptKeywordOptions pko = new PromptKeywordOptions(
                            "\nВыбор только полилиний - " + selectOnlyPolylines +
                            "\nМенять цвет объектов в блоке - " + setColor +
                            "\nЗадайте параметры или пустой ввод для продолжения");
                        pko.Keywords.Add(kw1);
                        pko.Keywords.Add(kw2);
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
                        if (pr.StringResult.Equals(kw1))
                        {
                            selectOnlyPolylines = !selectOnlyPolylines;
                        }
                        else if (pr.StringResult.Equals(kw2))
                        {
                            setColor = !setColor;
                        }


                    }

                    Application.DocumentManager.DocumentActivated += DocumentActivated_EventHandler;



                    firstTimeCall = false;
                }


                //Выбор полилиний

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = selectOnlyPolylines ? "\nВыберите полилинии границ участка" : "\nВыберите объекты";

                SelectionFilter flt = null;
                if (selectOnlyPolylines)
                {
                    TypedValue[] tv = new TypedValue[]
                    {
                    new TypedValue(0, "LWPOLYLINE")
                    };
                    flt = new SelectionFilter(tv);

                }
                else
                {
                    TypedValue[] tv = new TypedValue[]
                    {
                         new TypedValue(-4, "<NOT"),
                         new TypedValue(0, "INSERT"),
                         new TypedValue(-4, "NOT>")
                    };
                    flt = new SelectionFilter(tv);
                }
                PromptSelectionResult acSSPrompt = adoc.Editor.GetSelection(pso, flt);

                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    SelectionSet acSSet = acSSPrompt.Value;

                    //Подсветить выбранные полилинии
                    selectedPolylines = new List<Entity>();
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId id in acSSet.GetObjectIds())
                        {
                            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                            selectedPolylines.Add(ent);
                        }
                        tr.Commit();
                    }
                    Utils.Highlight(selectedPolylines, true);


                    //Указание текстового примитива
                    PromptEntityOptions peo1 = new PromptEntityOptions("\nУкажите текстовый примитив для создания атрибута");
                    peo1.SetRejectMessage("\nМожно выбрать только поверхность TIN");
                    peo1.AddAllowedClass(typeof(DBText), true);
                    PromptEntityResult per1 = ed.GetEntity(peo1);

                    //Снять подсветку
                    Utils.Highlight(selectedPolylines, false);

                    string blockName = null;

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        string initialName = null;
                        DBText dBText = null;
                        if (per1.Status == PromptStatus.OK)
                        {
                            dBText = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as DBText;
                            initialName = dBText.TextString;
                        }
                        else
                        {
                            initialName = Guid.NewGuid().ToString();
                        }

                        //Создать новый блок
                        blockName = Utils.GetSafeSymbolName(initialName);

                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                        if (bt.Has(blockName))
                        {
                            //Если блок с таким именем уже есть, то найти такое имя, которого еще нет
                            int i = 0;
                            string nameToCheck = null;
                            do
                            {
                                nameToCheck = blockName + "_" + i;
                                i++;
                            }
                            while (bt.Has(nameToCheck));
                            blockName = nameToCheck;
                        }

                        #region
                        //Создать таблицу map3d для записи правильного названия блока (если еще нет) --- ОТМЕНЕНО, ТАК КАК НЕ ВИДНО В NAVIS
                        //MapApplication mapApp = HostMapApplicationServices.Application;
                        //ProjectModel projModel = mapApp.ActiveProject;
                        //Tables odTables = projModel.ODTables;
                        //Autodesk.Gis.Map.ObjectData.Table odTable = null;
                        ////FieldDefinitions fDefs = null;
                        //if (!odTables.IsTableDefined("ReferenceNumber"))
                        //{
                        //    FieldDefinitions fDefs = mapApp.ActiveProject.MapUtility.NewODFieldDefinitions();
                        //    FieldDefinition def = fDefs.Add("ReferenceNumber", "Условный номер участка",
                        //    Autodesk.Gis.Map.Constants.DataType.Character, 0);

                        //    odTables.Add("ReferenceNumber", fDefs, "", true);
                        //}
                        //odTable = odTables["ReferenceNumber"];
                        #endregion

                        //Создание нового блока
                        BlockTableRecord btr = new BlockTableRecord();
                        btr.Name = blockName.Trim();
                        ObjectId btrId = bt.Add(btr);
                        tr.AddNewlyCreatedDBObject(btr, true);

                        ObjectId layerId = ObjectId.Null;
                        ObjectIdCollection objectIdCollection = new ObjectIdCollection();
                        //Копирование всех полилиний в созданный блок
                        foreach (ObjectId id in acSSet.GetObjectIds())
                        {
                            Entity polyline = (Entity)tr.GetObject(id, OpenMode.ForRead);
                            if (layerId == ObjectId.Null)
                                layerId = polyline.LayerId;//Запомнить слой.

                            Entity polylineCopy = (Entity)polyline.Clone();
                            if (polylineCopy is Polyline)
                                (polylineCopy as Polyline).Elevation = 0.0;

                            //Поменять цвет
                            if (setColor)
                                polylineCopy.ColorIndex = colorIndex;
                            else
                                polylineCopy.ColorIndex = 256;//По слою

                            objectIdCollection.Add(btr.AppendEntity(polylineCopy));
                            tr.AddNewlyCreatedDBObject(polylineCopy, true);
                        }


                        //Создать атрибут внутри блока
                        //using (Transaction trAttr = db.TransactionManager.StartTransaction())
                        if (dBText != null)
                            using (AttributeDefinition acAttDef = new AttributeDefinition())
                            {
                                acAttDef.Position = dBText.Position != Point3d.Origin ? dBText.Position : dBText.AlignmentPoint;
                                acAttDef.Verifiable = true;
                                acAttDef.Prompt = "Условный номер";
                                acAttDef.Tag = "ReferenceNumber";
                                acAttDef.TextString = initialName;
                                acAttDef.Height = dBText.Height;
                                acAttDef.Justify = dBText.Justify;
                                acAttDef.TextStyleId = dBText.TextStyleId;
                                if (setColor)
                                    acAttDef.ColorIndex = colorIndex;
                                else
                                    acAttDef.ColorIndex = 256;//По слою
                                acAttDef.WidthFactor = dBText.WidthFactor;
                                acAttDef.LayerId = dBText.LayerId;

                                btr.AppendEntity(acAttDef);
                                tr/*Attr*/.AddNewlyCreatedDBObject(acAttDef, true);
                                //trAttr.Commit();
                            }

                        //Создать штриховку http://adndevblog.typepad.com/autocad/2012/07/hatch-using-the-autocad-net-api.html
                        if (selectOnlyPolylines)
                        {
                            using (Hatch oHatch = new Hatch())
                            {

                                try
                                {
                                    Vector3d normal = new Vector3d(0.0, 0.0, 1.0);
                                    oHatch.Normal = normal;
                                    oHatch.Elevation = 0.0;
                                    oHatch.PatternScale = objectIdCollection.Count == 1 ? 2.0 : 10.0;
                                    oHatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI37");
                                    if (setColor)
                                        oHatch.ColorIndex = colorIndex;
                                    else
                                        oHatch.ColorIndex = 256;//По слою
                                    oHatch.LayerId = layerId;

                                    btr.AppendEntity(oHatch);
                                    tr.AddNewlyCreatedDBObject(oHatch, true);

                                    oHatch.Associative = true;
                                    foreach (ObjectId id in objectIdCollection)
                                    {
                                        oHatch.AppendLoop((int)HatchLoopTypes.Default, new ObjectIdCollection() { id });
                                    }

                                    oHatch.EvaluateHatch(true);
                                }
                                catch (System.Exception)
                                {
                                    try { oHatch.Erase(true); } catch { }
                                }
                            }
                        }



                        //Создание вхождения этого блока
                        BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        BlockReference br = new BlockReference(Point3d.Origin, btrId);
                        br.LayerId = layerId;
                        ObjectId brId = ms.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);


                        //Заполнить значение атрибута
                        AttributeCollection ac = br.AttributeCollection;
                        //AttributeReference ar = (AttributeReference)tr.GetObject(ac[0], OpenMode.ForWrite);
                        //ar.TextString = initialName;

                        //Привязать к вхождению блока запись таблицы map3d --- ОТМЕНЕНО, ТАК КАК НЕ ВИДНО В NAVIS
                        //Records odrecords = odTable.GetObjectTableRecords(Convert.ToUInt32(0), brId,
                        //                                        Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false);
                        //Record odRecord = Autodesk.Gis.Map.ObjectData.Record.Create();
                        //odTable.InitRecord(odRecord);

                        //Autodesk.Gis.Map.Utilities.MapValue mapVal = odRecord[0];
                        //mapVal.Assign(name);
                        //odTable.AddRecord(odRecord, br);


                        tr.Commit();
                    }

                    if (!String.IsNullOrEmpty(blockName)&& per1.Status == PromptStatus.OK)
                    {
                        adoc.SendStringToExecute("_ATTSYNC _N " + blockName + "\n", false, false, false);
                    }



                    adoc.SendStringToExecute("S1NF0_CreateLandSiteBlock\n", false, false, false);





                }
            }
            catch (System.Exception ex)
            {
                //Utils.ErrorToCommandLine(ed, "Ошибка при создании контуров разметки", ex);
                CommonException(ex, "Ошибка при создании контуров разметки");
            }
            finally
            {
                //Снять подсветку
                Utils.Highlight(selectedPolylines, false);
            }
        }



        private void DocumentActivated_EventHandler(object sender, DocumentCollectionEventArgs e)
        {
            firstTimeCall = true;
        }
    }
}
