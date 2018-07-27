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

[assembly: CommandClass(typeof(Civil3DInfoTools.LayersByColors.LayersByColorsCommand))]

namespace Civil3DInfoTools.LayersByColors
{
    public class LayersByColorsCommand
    {

        HashSet<ObjectId> traversedBlocks = new HashSet<ObjectId>();

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
                TraverseObjs(ms);

                tr.Commit();
            }
        }



        private void TraverseObjs(BlockTableRecord btr)
        {
            Database db = btr.Database;
            traversedBlocks.Add(btr.Id);//В каждый блок заходим только один раз
            foreach (ObjectId id in btr)
            {
                Entity ent = null;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    tr.Commit();
                }
                if (ent != null)
                {
                    if (!(ent is BlockReference))
                    {

                        //Обработка объекта.
                        //Если у объекта цвет не по слою, то
                        //создать слой, который называется как цвет,
                        //цвет слоя - цвет объекта
                        //перенести объект в этот слой
                        //цвет объекта - по слою
                        Color color = ent.Color;
                        if (!color.IsByLayer)
                        {
                            string colorName = Utils.GetSafeLayername( color.ColorNameForDisplay);
                            using (Transaction tr = btr.Database.TransactionManager.StartTransaction())
                            {
                                ObjectId layerId = Utils.CreateLayerIfNotExists(colorName, db, tr, null, color);
                                ent.LayerId = layerId;
                                ent.ColorIndex = 256;
                                tr.Commit();
                            }

                        }

                    }
                    else
                    {
                        //Рекурсивный вызов если такой блок еще не обойден
                        BlockReference br = (BlockReference)ent;
                        ObjectId btrId = br.BlockTableRecord;

                        if (!traversedBlocks.Contains(btrId))
                        {
                            BlockTableRecord btrIn = null;
                            using (Transaction tr = btr.Database.TransactionManager.StartTransaction())
                            {
                                btrIn = tr.GetObject(btrId, OpenMode.ForWrite) as BlockTableRecord;
                                tr.Commit();
                            }
                            if (btrIn != null)
                            {
                                TraverseObjs(btrIn);
                            }

                        }
                    }
                }
            }


        }
    }



}
