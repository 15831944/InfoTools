using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using WinForms = System.Windows.Forms;
using RCreation = Autodesk.Revit.Creation;
using System.IO;
using Autodesk.Revit.DB.Structure;

namespace RevitInfoTools
{
    //TODO: Нужно обязательно сделать специальную обработку для ошибки, когда выполняются действия за пределами границ редактирования проекта.

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PlaceCrossSectionsCommand : IExternalCommand
    {
        private static string massTemplateName = "Метрическая система, формообразующий элемент";

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

                    if (Utils.ReadCoordinatesFromCSV(filenames, projectTransform, lines3d)
                        && lines3d.Count > 0 && lines3d.First().Count > 0)
                    {
                        //Выбор семейства из числа загруженных семейств формы
                        //Определить id категории Форма
                        Categories categories = doc.Settings.Categories;
                        ElementId IdForm = categories.get_Item(BuiltInCategory.OST_Mass).Id;

                        SelectTypeWindow selectTypeWindow = new SelectTypeWindow(doc, IdForm);
                        bool? result = selectTypeWindow.ShowDialog();
                        if (result != null && result.Value
                            && selectTypeWindow.SelectedFamilySymbols.Count > 0)
                        {
                            FamilySymbol selectedSymbol = selectTypeWindow.SelectedFamilySymbols.First();
                            Document crossSectFamDoc = doc.EditFamily(selectedSymbol.Family);


                            //Создать новое семейство формы
                            string fileName = Path.Combine(Path.GetDirectoryName(App.AssemblyLocation),
                                App.FamilyLibRelativePath + massTemplateName + ".rft");

                            Document massFamDoc = app.NewFamilyDocument(fileName);
                            if (massFamDoc == null)
                            {
                                throw new Exception("Невозможно создать семейство формы");
                            }



                            //Пересчет координат относительно координат первой точки
                            List<List<XYZ>> lines3dInFam = new List<List<XYZ>>();
                            XYZ basePt = lines3d.First().First();
                            foreach (List<XYZ> ptList in lines3d)
                            {
                                List<XYZ> ptListInFam = new List<XYZ>();
                                lines3dInFam.Add(ptListInFam);
                                foreach (XYZ pt in ptList)
                                {
                                    XYZ convertedPt = pt - basePt;
                                    ptListInFam.Add(convertedPt);
                                }
                            }


                            //Полный путь к сохраненному семейству
                            string targetRfaFullPath = null;

                            //Выбрать имя для семейства формы, так чтобы оно не пересекалось с уже загруженными семействами
                            FilteredElementCollector a = new FilteredElementCollector(doc).OfClass(typeof(Family));
                            string famName = null;
                            int n = 0;
                            do
                            {
                                famName = "Massing" + n;
                                n++;
                            } while (a.Any(e => e.Name.Equals(famName)));

                            {
                                //Расставить формы по координатам внутри формы, считая за ноль первую точку первой линии
                                //Вставить созданное семейство в проект
                                //Загрузить семейство поперечного сечения
                                Family family = crossSectFamDoc.LoadFamily(massFamDoc);
                                using (Transaction tr = new Transaction(massFamDoc))
                                {
                                    RCreation.FamilyItemFactory crDoc = massFamDoc.FamilyCreate;
                                    tr.Start("Place cross sections");

                                    //Найти выбранный типоразмер
                                    FamilySymbol familySymbol = null;
                                    foreach (ElementId id in family.GetFamilySymbolIds())
                                    {
                                        FamilySymbol fs = (FamilySymbol)massFamDoc.GetElement(id);
                                        if (fs.Name.Equals(selectedSymbol.Name))
                                        {
                                            familySymbol = fs;
                                        }
                                    }
                                    //активировать типоразмер
                                    Utils.ActivateFamSym(massFamDoc, familySymbol);

                                    //Вставить поперечник в каждую точку и повернуть по биссектриссе
                                    foreach (List<XYZ> ptList in lines3dInFam)
                                    {
                                        for (int i = 0; i < ptList.Count; i++)
                                        {
                                            XYZ pt = ptList[i];

                                            FamilyInstance crossSection = crDoc.NewFamilyInstance(pt, familySymbol, StructuralType.NonStructural);
                                            XYZ prevVector = null;
                                            double prevHorAngle = 0;
                                            XYZ nextVector = null;
                                            double nextHorAngle = 0;
                                            if (i != 0)
                                            {
                                                XYZ prevPt = ptList[i - 1];
                                                prevVector = pt - prevPt;
                                                prevHorAngle = XYZ.BasisY.AngleOnPlaneTo(prevVector, XYZ.BasisZ);
                                            }
                                            if (i != ptList.Count - 1)
                                            {
                                                XYZ nextPt = ptList[i + 1];
                                                nextVector = nextPt - pt;
                                                nextHorAngle = XYZ.BasisY.AngleOnPlaneTo(nextVector, XYZ.BasisZ);
                                            }




                                            //Изначально поперечное сечение ориентировано по оси Y
                                            //Расчет угла поворота
                                            double horRotationAngle = 0;
                                            if (prevVector != null && nextVector != null)
                                            {
                                                horRotationAngle = (prevHorAngle + nextHorAngle) / 2;
                                            }
                                            else if (prevVector != null && nextVector == null)
                                            {
                                                horRotationAngle = prevHorAngle;
                                            }
                                            else if (prevVector == null && nextVector != null)
                                            {
                                                horRotationAngle = nextHorAngle;
                                            }
                                            Line vertAxis = Line.CreateBound(pt, pt + XYZ.BasisZ);
                                            ElementTransformUtils.RotateElement(massFamDoc, crossSection.Id, vertAxis, horRotationAngle);

                                            #region ЭЛЕМЕНТ НЕ МОЖЕТ БЫТЬ ПОВЕРНУТ В ЭТУ ПОЗИЦИЮ
                                            //? Определить угол поворота в плоскости, перпендикулярной сечению
                                            //XYZ normal = Transform.CreateRotation(XYZ.BasisZ, horRotationAngle).OfVector(XYZ.BasisX);//Нормаль плоскости
                                            //double vertRotationAngle = 0;

                                            //double prevVertAngle = 0;
                                            //double nextVertAngle = 0;
                                            //if (prevVector != null)
                                            //{
                                            //    prevVertAngle = XYZ.BasisY.AngleOnPlaneTo(prevVector, normal);
                                            //}
                                            //if (nextVector != null)
                                            //{
                                            //    nextVertAngle = XYZ.BasisY.AngleOnPlaneTo(nextVector, normal);
                                            //}



                                            //if (prevVector != null && nextVector != null)
                                            //{
                                            //    vertRotationAngle = (prevVertAngle + nextVertAngle) / 2;
                                            //}
                                            //else if (prevVector != null && nextVector == null)
                                            //{
                                            //    vertRotationAngle = prevVertAngle;
                                            //}
                                            //else if (prevVector == null && nextVector != null)
                                            //{
                                            //    vertRotationAngle = nextVertAngle;
                                            //}

                                            //Line horAxis = Line.CreateBound(pt, pt + normal);
                                            //ElementTransformUtils.RotateElement(massFamDoc, crossSection.Id, horAxis, vertRotationAngle); 
                                            #endregion


                                        }
                                    }

                                    tr.Commit();
                                }
                                string famSaveDir = doc.PathName;
                                if (!String.IsNullOrEmpty(famSaveDir))
                                {
                                    famSaveDir = Path.GetDirectoryName(famSaveDir);
                                }
                                else
                                {
                                    famSaveDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                                }


                                targetRfaFullPath = Common.Utils.GetNonExistentFileName(famSaveDir, famName, "rfa");
                                massFamDoc.SaveAs(targetRfaFullPath);
                            }


                            {
                                //Загрузить семейство формы.
                                Family family = massFamDoc.LoadFamily(doc);
                                using (Transaction tr = new Transaction(doc))
                                {
                                    RCreation.Document crDoc = doc.Create;
                                    tr.Start("Place mass family");

                                    FamilySymbol familySymbol = (FamilySymbol)doc.GetElement(family.GetFamilySymbolIds().First());
                                    //активировать типоразмер
                                    Utils.ActivateFamSym(doc, familySymbol);
                                    //Вставить экземпляр семейства
                                    crDoc.NewFamilyInstance(basePt, familySymbol, StructuralType.NonStructural);
                                    tr.Commit();
                                }
                            }


                            //Удалить сохраненный файл семейства формы
                            massFamDoc.Close();
                            if (!String.IsNullOrEmpty(targetRfaFullPath))
                            {
                                File.Delete(targetRfaFullPath);
                            }
                            //TODO: Открыть загруженное в проект семейство.
                            //Похоже можно открыть только сохраненное семейство по пути
                            //uiapp.OpenAndActivateDocument()
                        }
                    }

                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при расстановке поперечных сечений в Revit");
            }

            return Result.Succeeded;
        }


    }
}
