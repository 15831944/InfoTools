using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinForms = System.Windows.Forms;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

namespace RevitInfoTools
{


    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Draw3DLineCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("ОТМЕНЕНО", "Данная команда предназначена для запуска в документе проекта");
                return Result.Cancelled;
            }

            try
            {
                //Выбрать несколько файлов CSV с координатами
                Transform projectTransform = Utils.GetProjectCoordinatesTransform(doc);

                string[] filenames = null;
                WinForms.OpenFileDialog openFileDialog1 = new WinForms.OpenFileDialog();


                string curDocPath = doc.PathName;
                if (!String.IsNullOrEmpty(curDocPath))
                    openFileDialog1.InitialDirectory = Path.GetDirectoryName(curDocPath);
                openFileDialog1.Filter = "csv files (*.csv)|*.csv";
                openFileDialog1.FilterIndex = 1;
                openFileDialog1.RestoreDirectory = true;
                openFileDialog1.Multiselect = true;
                openFileDialog1.Title = "Выберите таблицы CSV с координатами 3d-полилиний";

                if (openFileDialog1.ShowDialog() == WinForms.DialogResult.OK)
                {
                    filenames = openFileDialog1.FileNames;

                    List<List<XYZ>> lines3d = new List<List<XYZ>>();

                    if (Utils.ReadCoordinatesFromCSV(filenames, projectTransform, lines3d))
                    {
                        //Загрузить семейство 3d линии если нет
                        Family line3dFamily = Utils.GetFamily(doc, "3d line");
                        ElementId symId = line3dFamily.GetFamilySymbolIds().First();
                        FamilySymbol familySymbol = (FamilySymbol)doc.GetElement(symId);

                        //Вывести форму для выбора семейств
                        Categories categories = doc.Settings.Categories;
                        ElementId IdGeneric = categories.get_Item(BuiltInCategory.OST_GenericModel).Id;
                        SelectTypeWindow selectTypeWindow = new SelectTypeWindow(doc, IdGeneric);
                        bool? result = selectTypeWindow.ShowDialog();
                        if (result != null && result.Value
                            && selectTypeWindow.SelectedFamilySymbols.Count > 0)
                        {
                            familySymbol = selectTypeWindow.SelectedFamilySymbols.First();
                        }

                        //Расставить линии по координатам
                        using (Transaction tr = new Transaction(doc))
                        {
                            tr.Start("Draw 3D line");
                            //активировать типоразмер
                            if (!familySymbol.IsActive)
                            {
                                familySymbol.Activate();
                                doc.Regenerate();
                            }


                            foreach (List<XYZ> ptList in lines3d)
                            {
                                for (int i = 0; i < ptList.Count - 1; i++)
                                {
                                    FamilyInstance instance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, familySymbol);
                                    IList<ElementId> placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(instance);
                                    ReferencePoint point1 = (ReferencePoint)doc.GetElement(placePointIds[0]);
                                    point1.Position = ptList[i];
                                    ReferencePoint point2 = (ReferencePoint)doc.GetElement(placePointIds[1]);
                                    point2.Position = ptList[i + 1];

                                }
                            }

                            tr.Commit();
                        }



                    }

                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при вычерчивании 3d линии в Revit");
            }



            return Result.Succeeded;
        }
    }
}
