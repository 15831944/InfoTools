using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.ChangePipeTypeCommand))]

namespace Civil3DInfoTools.AuxiliaryCommands
{
    public class ChangePipeTypeCommand
    {
        [CommandMethod("S1NF0_ChangePipeType", CommandFlags.Modal)]
        public void ChangePipeType()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            try
            {
                //выбор элементов сети (TODO: или использовать текущий выбор объектов)
                //отобрать только трубы
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\nВыберите трубы:";

                TypedValue[] tv = new TypedValue[] { new TypedValue(0, "AECC_PIPE") };
                SelectionFilter flt = new SelectionFilter(tv);

                PromptSelectionResult acSSPrompt = adoc.Editor.GetSelection(pso, flt);


                if (acSSPrompt.Status != PromptStatus.OK) return;

                //определить каталог
                //трубы должны принадлежать к одному каталогу иначе отменить
                SelectionSet acSSet = acSSPrompt.Value;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectId partsListId = ObjectId.Null;
                    List<Pipe> pipes = new List<Pipe>();

                    ObjectId defPartFam = ObjectId.Null;
                    ObjectId defPartSize = ObjectId.Null;

                    foreach (ObjectId id in acSSet.GetObjectIds())
                    {
                        Pipe pipe = null;
                        //using (Transaction tr = db.TransactionManager.StartTransaction())
                        //{
                        pipe = tr.GetObject(id, OpenMode.ForRead) as Pipe;
                        //    tr.Commit();
                        //}
                        if (pipe == null) continue;


                        pipes.Add(pipe);

                        Network network = null;
                        //using (Transaction tr = db.TransactionManager.StartTransaction())
                        //{
                        network = tr.GetObject(pipe.NetworkId, OpenMode.ForRead) as Network;
                        //    tr.Commit();
                        //}
                        if (partsListId == ObjectId.Null)
                            partsListId = network.PartsListId;
                        else if (partsListId != network.PartsListId)
                        {
                            ed.WriteMessage("\nВыбранные трубы относятся к разным PartsList");
                            return;
                        }

                        //Нельзя узнать текущий PartFamily и PartSize???
                        //pipe.SwapPartFamilyAndSize

                        ed.WriteMessage("\n" + pipe.PartSizeName);
                    }

                    if (partsListId == ObjectId.Null) return;

                    PartsList partsList = null;
                    //using (Transaction tr = db.TransactionManager.StartTransaction())
                    //{
                    partsList = tr.GetObject(partsListId, OpenMode.ForRead) as PartsList;
                    //    tr.Commit();
                    //}

                    //вывести окно с выбором нового типа трубы (если выбранные трубы имели одинаковый тип, то отобразить его)
                    SetPipeTypeViewModel viewModel = new SetPipeTypeViewModel(adoc, partsList);
                    SetPipeTypeView view = new SetPipeTypeView();
                    view.DataContext = viewModel;

                    Application.ShowModalWindow(view);

                    if (!view.DialogResult.HasValue || !view.DialogResult.Value) return;

                    //задать выбранный тип трубы
                    PartFamily partFamily = viewModel.PipeVM.SelectedPartFamily.PartFamily;
                    PartSize partSize = viewModel.PipeVM.SelectedPartSize;


                    foreach (Pipe pipe in pipes)
                    {
                        pipe.SwapPartFamilyAndSize(partFamily.Id, partSize.Id);
                    }

                    tr.Commit();
                }



            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при попытке изменить тип труб");
            }




        }
    }
}
