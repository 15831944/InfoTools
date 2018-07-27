using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: CommandClass(typeof(Civil3DInfoTools.Spillway.Poly3DFromCorridorFeatureLinesCommand))]

namespace Civil3DInfoTools.Spillway
{
    class Poly3DFromCorridorFeatureLinesCommand
    {
        [CommandMethod("S1NF0_Poly3DFromCorridorFeatureLines", CommandFlags.Modal)]
        public void Poly3DFromCorridorFeatureLines()
        {

            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            try
            {
                //Указать объект корридора
                PromptEntityOptions peo1 = new PromptEntityOptions("\nУкажите корридор");
                peo1.SetRejectMessage("\nМожно выбрать только корридор");
                peo1.AddAllowedClass(typeof(Corridor), true);
                PromptEntityResult per1 = ed.GetEntity(peo1);

                if (per1.Status == PromptStatus.OK)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Corridor corridor = tr.GetObject(per1.ObjectId, OpenMode.ForWrite) as Corridor;

                        BaselineCollection baselines = corridor.Baselines;

                        foreach (Baseline bLine in baselines)
                        {
                            BaselineFeatureLines featureLines = bLine.MainBaselineFeatureLines;

                            FeatureLineCollectionMap featureLineCollMap = featureLines.FeatureLineCollectionMap;

                            foreach (FeatureLineCollection featureLineColl in featureLineCollMap)
                            {
                                foreach (CorridorFeatureLine corrFeatureLine in featureLineColl)
                                {

                                    string codeName = corrFeatureLine.CodeName;
                                    //Сразу чертит все полилинии
                                    ObjectIdCollection polyColl = corrFeatureLine.ExportAsPolyline3dCollection();

                                    if (!String.IsNullOrEmpty(codeName))
                                    {
                                        //Все 3d полилинии переносятся в слой, который называется так же как код линии
                                        short colorIndex = 3;
                                        LineWeight lineWeight = LineWeight.ByLayer;
                                        string layerName = codeName;
                                        if (codeName.Equals("КПЧ"))
                                        {
                                            colorIndex = 30;
                                            lineWeight = LineWeight.LineWeight030;
                                        }
                                        else if (codeName.Equals("ОТК") || codeName.Equals("Hinge") || codeName.Equals("Daylight")
                                            || codeName.Equals("Отсчет") || codeName.Equals("Выход на поверхность"))
                                        {
                                            colorIndex = 50;
                                            lineWeight = LineWeight.LineWeight030;
                                            layerName = "ОТК";
                                        }


                                        ObjectId layerId = Utils.CreateLayerIfNotExists(layerName, db, tr, null,
                                            Color.FromColorIndex(ColorMethod.ByAci, colorIndex), lineWeight);
                                        foreach (ObjectId polyId in polyColl)
                                        {
                                            Polyline3d polyline = tr.GetObject(polyId, OpenMode.ForWrite) as Polyline3d;
                                            if (polyline != null)
                                                polyline.LayerId = layerId;
                                        }
                                    }



                                }
                            }


                        }


                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                //Utils.ErrorToCommandLine(ed, "Ошибка при построении полилиний по линиям корридора", ex);
                CommonException(ex, "Ошибка при построении полилиний по линиям корридора");
            }
        }
    }
}
