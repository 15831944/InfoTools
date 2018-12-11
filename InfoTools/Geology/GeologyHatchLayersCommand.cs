using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: CommandClass(typeof(Civil3DInfoTools.Geology.GeologyHatchLayersCommand))]

namespace Civil3DInfoTools.Geology
{
    public class GeologyHatchLayersCommand
    {
        [CommandMethod("S1NF0_GeologyHatchLayers", CommandFlags.Modal)]
        public void GeologyHatchLayers()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;

            Editor ed = doc.Editor;

            try
            {
                //выбор штриховок
                TypedValue[] tv = new TypedValue[]
                    {
                            new TypedValue(0, "HATCH")
                    };
                SelectionFilter flt = new SelectionFilter(tv);

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\nВыберите штриховки грунтов";

                PromptSelectionResult acSSPrompt = doc.Editor.GetSelection(pso, flt);
                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    SelectionSet acSSet = acSSPrompt.Value;
                    if (acSSet != null)
                    {
                        //разнесение по слоям
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                            short colorIndex = 1;
                            foreach (ObjectId hatchId in acSSet.GetObjectIds())
                            {
                                Hatch hatch = (Hatch)tr.GetObject(hatchId, OpenMode.ForWrite);
                                double patternScale = hatch.PatternScale;
                                string layerName = patternScale != 1 ? hatch.PatternName + "_" + patternScale.ToString("f2") : hatch.PatternName;
                                
                                ObjectId hatchLayerId = ObjectId.Null;
                                if (!lt.Has(layerName))
                                {
                                    lt.UpgradeOpen();
                                    LayerTableRecord ltrNew = null;
                                    ltrNew = new LayerTableRecord();
                                    ltrNew.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                                    colorIndex = Convert.ToByte(((colorIndex + 1) % 255)+1);
                                    ltrNew.LineWeight = LineWeight.LineWeight030;

                                    ltrNew.Name = layerName;
                                    hatchLayerId = lt.Add(ltrNew);
                                    tr.AddNewlyCreatedDBObject(ltrNew, true);
                                }
                                else
                                {
                                    hatchLayerId = lt[layerName];
                                }

                                hatch.LayerId = hatchLayerId;
                            }

                            tr.Commit();
                        }
                    }


                        
                }

            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при разнесении штриховок геологии по слоям");
            }
        }
    }
}
