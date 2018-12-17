using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Civil3DInfoTools.Geology.GeologyHatchLayersWindow;
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
                //открыть окно в котором можно указать путь к файлу Excel,
                //указать номер столбца с названиями слоев и строку с которой они начинаются
                //можно ничего не указывать и тогда команда просто раскидывает штриховки
                //по слоям в зависимости от свойств штриховки
                GeologyHatchLayersView view = new GeologyHatchLayersView();
                GeologyHatchLayersViewModel viewModel = new GeologyHatchLayersViewModel(doc);
                view.DataContext = viewModel;
                Application.ShowModalWindow(view);

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
                        if (viewModel.LayerNamesListSpecified)
                        {
                            SetLayersByList(db, viewModel, acSSet, ed);
                        }
                        else
                        {
                            //разнесение по слоям согласно свойствам штриховки
                            SetLayersByHatchProps(db, acSSet);
                        }
                    }



                }

            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при разнесении штриховок геологии по слоям");
            }
        }

        private static void SetLayersByList(Database db, GeologyHatchLayersViewModel viewModel,
            SelectionSet acSSet, Editor ed)
        {
            Dictionary<string, List<ObjectId>> hatchTypesLookup
                    = new Dictionary<string, List<ObjectId>>();
            List<ObjectId> layerList = new List<ObjectId>();
            PaletteSet ps = null;
            SelectLayerViewModel paletteViewModel = null;
            SelectLayerView paletteView = null;
            List<LayerTableRecord> ltrList = null;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                //Создать слои согласно списку из экселя (если их еще нет)
                List<string> layerNames = viewModel.LayerNames;
                short colorIndex = 1;
                foreach (string ln in layerNames)
                {
                    layerList.Add(Utils.CreateLayerIfNotExists(ln, db, tr,
                        color: Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                        lineWeight: LineWeight.LineWeight030));
                    colorIndex = Convert.ToByte((colorIndex + 1) % 255);
                    if (colorIndex == 0) colorIndex = 1;
                }

                ltrList = layerList.Select(id => (LayerTableRecord)tr
                            .GetObject(id, OpenMode.ForRead)).ToList();

                //Разбить все штриховки по группам в соответствии с их свойствами
                foreach (ObjectId hatchId in acSSet.GetObjectIds())
                {
                    Hatch hatch = (Hatch)tr.GetObject(hatchId, OpenMode.ForWrite);
                    string key = GetHatchTypeKey(hatch);

                    List<ObjectId> currTypeList = null;
                    hatchTypesLookup.TryGetValue(key, out currTypeList);
                    if (currTypeList == null)
                    {
                        currTypeList = new List<ObjectId>();
                        hatchTypesLookup.Add(key, currTypeList);
                    }
                    currTypeList.Add(hatchId);
                }

                tr.Commit();
            }

            if (hatchTypesLookup.Values.Count > 0)
            {
                ps = new PaletteSet("Выбор слоя");
                ps.Style = PaletteSetStyles.ShowPropertiesMenu
                            | PaletteSetStyles.ShowCloseButton;
                paletteViewModel = new SelectLayerViewModel(ltrList, ps);
                paletteView = new SelectLayerView();
                paletteView.DataContext = paletteViewModel;

                ps.AddVisual("SelectLayerPaletteControl", paletteView);
                ps.DockEnabled = DockSides.Left;

                ps.Visible = false;


                //последовательно подсвечивать каждую группу (при этом зумировать камеру, чтобы было их видно)
                //Открывать панель со списком геологических элементов, полученным из экселя
                //и ожидать когда пользователь выберет в этом списке нужный элемент
                //присвоить выбранным штриховкам выбранный слой
                foreach (List<ObjectId> hatchIdsGroup in hatchTypesLookup.Values)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        List<Hatch> hatches = hatchIdsGroup
                            .Select(id => (Hatch)tr.GetObject(id, OpenMode.ForWrite)).ToList();
                        try
                        {
                            Utils.Highlight(hatches, true);


                            //зумирование на первой штриховке из списка
                            Hatch sampleHatch = hatches.First(h => h.Bounds != null);
                            Utils.ZoomWin(ed, sampleHatch.Bounds.Value.MinPoint,
                                sampleHatch.Bounds.Value.MaxPoint);


                            //Открыть панель и дать выбрать один из слоев
                            ps.Visible = true;
                            ps.Size = new System.Drawing.Size(420, 350);
                            ps.Dock = DockSides.Left;
                            bool trueCancel = false;//Dock ВЫЗЫВАЕТ ОТМЕНУ GetKeywords, поэтому первый раз отмена не завершает команду
                            PromptResult pr = null;
                            const string kwAcceptLayer = "ПРИнятьСлой";
                            const string kwSkip = "ПРОпустить";
                            const string kwErase = "УдалитьОбъекты";
                            do
                            {
                                PromptKeywordOptions pko = new PromptKeywordOptions("\nВыберите нужный слой");
                                pko.Keywords.Add(kwAcceptLayer);
                                pko.Keywords.Add(kwSkip);
                                pko.Keywords.Add(kwErase);
                                pko.AllowNone = true;
                                pr = ed.GetKeywords(pko);
                                if (pr.Status == PromptStatus.Cancel)
                                {
                                    if (trueCancel)
                                        return;
                                    trueCancel = true;
                                }

                            } while (paletteViewModel.SelectedLayer == null && 
                            !(pr.StringResult == kwSkip || pr.StringResult == kwErase));
                            ps.Visible = false;


                            switch (pr.StringResult)
                            {
                                case null:
                                case ""://пустой ввод - то же что и принять
                                case kwAcceptLayer:
                                    ObjectId selectedLayerId = (paletteViewModel.SelectedLayer as LayerTableRecord).Id;
                                    paletteViewModel.SelectedLayer = null;

                                    foreach (Hatch hatch in hatches)
                                    {
                                        hatch.LayerId = selectedLayerId;
                                        hatch.ColorIndex = 256;
                                    }
                                    break;
                                case kwErase:
                                    foreach (Hatch hatch in hatches)
                                    {
                                        hatch.Erase();
                                    }
                                    break;
                            }

                            
                        }
                        catch (System.Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            Utils.Highlight(hatches, false);
                            ps.Visible = false;
                        }
                        tr.Commit();
                    }
                }
            }


        }

        private static string GetHatchTypeKey(Hatch hatch)
        {
            return hatch.PatternScale != 1 ?
                                    hatch.PatternName + "_" + hatch.PatternScale.ToString("f2")
                                    : hatch.PatternName;
        }

        private static void SetLayersByHatchProps(Database db, SelectionSet acSSet)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                short colorIndex = 1;
                foreach (ObjectId hatchId in acSSet.GetObjectIds())
                {
                    Hatch hatch = (Hatch)tr.GetObject(hatchId, OpenMode.ForWrite);
                    string layerName = GetHatchTypeKey(hatch);

                    ObjectId hatchLayerId = ObjectId.Null;
                    if (!lt.Has(layerName))
                    {
                        lt.UpgradeOpen();
                        LayerTableRecord ltrNew = null;
                        ltrNew = new LayerTableRecord();
                        ltrNew.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                        colorIndex = Convert.ToByte((colorIndex + 1) % 255);
                        if (colorIndex == 0) colorIndex = 1;
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
                    hatch.ColorIndex = 256;
                }

                tr.Commit();
            }
        }
    }
}
