using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CommandClass(typeof(Civil3DInfoTools.LayersByColors.LayersByColorsCommand))]

namespace Civil3DInfoTools.LayersByColors
{
    public class LayersByColorsCommand
    {
        /// <summary>
        /// Все объекты в документе переносятся в слои в зависимости от их цвета (если цвет не по слою)
        /// </summary>
        [CommandMethod("S1NF0_LayersByColors", CommandFlags.Modal)]
        public void LayersByColors()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            //
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                
                BlockTableRecord ms
                            = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                tr.Commit();
            }
        }



        private void TraverseObjs(BlockTableRecord btr)
        {

        }
    }


    
}
