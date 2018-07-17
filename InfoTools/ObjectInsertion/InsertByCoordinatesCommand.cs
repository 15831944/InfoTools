using Autodesk.AutoCAD.Runtime;
using WinForms = System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.IO;
using System.Xml.Serialization;
using Autodesk.AutoCAD.Geometry;
using Common.XMLClasses;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: CommandClass(typeof(Civil3DInfoTools.ObjectInsertion.InsertByCoordinatesCommand))]

namespace Civil3DInfoTools.ObjectInsertion
{
    class InsertByCoordinatesCommand
    {
        [CommandMethod("S1NF0_InsertByCoordinates", CommandFlags.Modal)]
        public void InsertByCoordinates()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;


            try
            {
                //Указать xml с координатами
                string initialPath = Path.GetDirectoryName(adoc.Name);
                WinForms.OpenFileDialog openFileDialog = new WinForms.OpenFileDialog();
                openFileDialog.InitialDirectory = initialPath;
                openFileDialog.Filter = "xml files (*.xml)|*.xml";
                //openFileDialog1.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.Multiselect = false;
                openFileDialog.Title = "Выберите XML с координатами блоков";

                if (openFileDialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    //парсинг xml
                    PositionData positionData = null;
                    using (StreamReader sr = new StreamReader(openFileDialog.FileName))
                    {
                        string serializedData = sr.ReadToEnd();
                        var xmlSerializer = new XmlSerializer(typeof(PositionData));
                        var stringReader = new StringReader(serializedData);
                        positionData = (PositionData)xmlSerializer.Deserialize(stringReader);

                    }

                    //Поиск в этой же папке файлов dwg с соответствующими названиями.
                    //Вставка каждого блока в соответствующие координаты и задание поворота
                    //После каждой вставки сделать смещение по высоте на разность ZMin и Z точки вставки 
                    //Ненайденные отложить в отдельный список
                    string dir = Path.GetDirectoryName(openFileDialog.FileName);
                    HashSet<string> notFound = new HashSet<string>();
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                        BlockTableRecord ms
                            = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);


                        foreach (ObjectPosition op in positionData.ObjectPositions)
                        {
                            string blockName = Path.GetFileNameWithoutExtension(op.Name);
                            string dwgFilename = blockName + ".dwg";
                            string dwgFullPath = Path.Combine(dir, dwgFilename);

                            using (Transaction trInner = db.TransactionManager.StartTransaction())
                            {

                                if (File.Exists(dwgFullPath))
                                {
                                    //Создать блок если еще нет
                                    ObjectId blockId = ObjectId.Null;
                                    if (!bt.Has(blockName))
                                    {
                                        Database blockDb = new Database(false, true);
                                        blockDb.ReadDwgFile(dwgFullPath, FileShare.Read, true, "");

                                        //Для каждого вложенного блока изменить имя
                                        using (Transaction trBlock = blockDb.TransactionManager.StartTransaction())
                                        {
                                            BlockTable btBlock = (BlockTable)trBlock.GetObject(blockDb.BlockTableId, OpenMode.ForRead);
                                            //BlockTableRecord msBlock = (BlockTableRecord)trBlock.GetObject(btBlock[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                                            foreach (ObjectId id in btBlock)
                                            {
                                                BlockTableRecord nestedBlock
                                                    = (BlockTableRecord)trBlock.GetObject(id, OpenMode.ForWrite);
                                                if (!nestedBlock.Name.StartsWith("*"))
                                                {
                                                    try
                                                    {
                                                        nestedBlock.Name = blockName + "_" + nestedBlock.Name;
                                                    }
                                                    catch (System.Exception)
                                                    {
                                                    }
                                                }
                                                
                                            }
                                        }



                                        blockId = db.Insert(blockName, blockDb, true);

                                        //BlockTableRecord btr1 = trInner.GetObject(blockId, OpenMode.ForRead) as BlockTableRecord;
                                        //btr1.Name = blockName;

                                    }
                                    else
                                    {
                                        blockId = bt[blockName];
                                    }


                                    //Зайти в BlockTableRecord и получить сдвижку по координате Z
                                    double zOffset = 0;
                                    BlockTableRecord btr = trInner.GetObject(blockId, OpenMode.ForRead) as BlockTableRecord;

                                    double minZ = double.MaxValue;
                                    foreach (ObjectId objId in btr)
                                    {
                                        Entity ent = trInner.GetObject(objId, OpenMode.ForRead) as Entity;
                                        Extents3d? ext = ent.Bounds;
                                        if (ext != null)
                                        {
                                            double currMinZ = ext.Value.MinPoint.Z;
                                            if (currMinZ < minZ)
                                            {
                                                minZ = currMinZ;
                                            }

                                        }
                                    }
                                    if (minZ < double.MaxValue)
                                    {
                                        zOffset = -minZ;
                                    }


                                    //Вставить новое вхождение блока
                                    Point3d position = new Point3d(op.X, op.Y, op.Z + zOffset);
                                    BlockReference br = new BlockReference(position, blockId);
                                    br.Rotation = op.Z_Rotation / (180 / Math.PI);
                                    ms.AppendEntity(br);
                                    trInner.AddNewlyCreatedDBObject(br, true);



                                }
                                else
                                {
                                    notFound.Add(dwgFilename);
                                }


                                trInner.Commit();
                            }


                        }

                        tr.Commit();
                    }





                    //Вывод в консоль информации о ненайденных файлах
                    ed.WriteMessage("\nВ папке {0} не обнаружены следующие файлы:", dir);
                    foreach (string str in notFound)
                    {
                        ed.WriteMessage("\n" + str);
                    }
                }

            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при выполнении команды InsertByCoordinates");
                //Utils.ErrorToCommandLine(ed, "Ошибка при выполнении команды InsertByCoordinates", ex);
            }
        }

    }
}
