using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.TestCommand))]

namespace Civil3DInfoTools.AuxiliaryCommands
{
    public class TestCommand
    {

        [CommandMethod("S1NF0_Test", CommandFlags.Modal)]
        public void Test()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            CivilDocument cdok = CivilDocument.GetCivilDocument(adoc.Database);

            TestWindow testWindow = new TestWindow(adoc, cdok);
            Application.ShowModalWindow(testWindow);

            //PromptEntityOptions peo = new PromptEntityOptions("\nУкажите TEXT,MTEXT");
            //peo.SetRejectMessage("\nМожно выбрать только TEXT или MTEXT");
            //peo.AddAllowedClass(typeof(MText), true);
            //peo.AddAllowedClass(typeof(DBText), true);
            //PromptEntityResult per1 = ed.GetEntity(peo);
            //if (per1.Status == PromptStatus.OK)
            //{
            //    using (Transaction tr = db.TransactionManager.StartTransaction())
            //    {
            //        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
            //        BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);


            //        Entity ent = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as Entity;
            //        Extents3d? ext = ent.Bounds;
            //        if (ext != null)
            //        {
            //            using (Polyline poly = new Polyline())
            //            {
            //                poly.ColorIndex = 1;
            //                poly.AddVertexAt(0, new Point2d(ext.Value.MinPoint.X, ext.Value.MinPoint.Y), 0, 0, 0);
            //                poly.AddVertexAt(1, new Point2d(ext.Value.MinPoint.X, ext.Value.MaxPoint.Y), 0, 0, 0);
            //                poly.AddVertexAt(2, new Point2d(ext.Value.MaxPoint.X, ext.Value.MaxPoint.Y), 0, 0, 0);
            //                poly.AddVertexAt(3, new Point2d(ext.Value.MaxPoint.X, ext.Value.MinPoint.Y), 0, 0, 0);
            //                poly.Closed = true;

            //                ms.AppendEntity(poly);
            //                tr.AddNewlyCreatedDBObject(poly, true);
            //            }



            //        }

            //        tr.Commit();
            //    }
            //}
        }
    }
}
