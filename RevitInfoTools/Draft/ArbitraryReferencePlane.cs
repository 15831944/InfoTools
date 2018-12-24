using Autodesk.Revit.Attributes;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitInfoTools
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ArbitraryReferencePlane : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;


            XYZ bubbleEnd = new XYZ(0, 0, 0);
            XYZ freeEnd = new XYZ(5, 0, 1);
            XYZ thirdPnt = new XYZ(0, 15, 5);

            Family testFamily = Utils.GetFamily(doc, "Блок_Тоннеля_Тип2.1");
            ElementId symId = testFamily.GetFamilySymbolIds().First();
            FamilySymbol familySymbol = (FamilySymbol)doc.GetElement(symId);


            using (Transaction tr = new Transaction(doc))
            {
                tr.Start("test");

                ReferencePlane refPlane = doc.Create.NewReferencePlane2(bubbleEnd, freeEnd, thirdPnt, doc.ActiveView);
                refPlane.Name = "Плоскость1";

                Reference reference = refPlane.GetReference();
                doc.Create.NewFamilyInstance(reference, bubbleEnd, freeEnd, familySymbol);




                tr.Commit();
            }

                

            return Result.Succeeded;
        }
    }
}
