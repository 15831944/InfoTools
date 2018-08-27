using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NavisWorksInfoTools.FBX
{
    public class ASCIIModelNamesEditor : ModelNamesEditor
    {
        /// <summary>
        /// Пропускать все узлы Model до того как появится узел Environment
        /// Там могут быть камеры и источники света
        /// </summary>
        private bool renamingModelsStarted = false;

        /// <summary>
        /// Строка "Model: 2868598016032, "Model::Environment", "Null" {" - подходит
        /// Строка "Model: 2868598004032, "Model::", "Null" {" - подходит
        /// </summary>
        private static Regex regex = new Regex("^\\s*Model:.*\"Model::.*\".*$");
        private static Regex regex2 = new Regex("\"Model::.*\",");

        public ASCIIModelNamesEditor
            (string fbxFileName, Queue<NameReplacement> replacementPairs)
            : base(fbxFileName, replacementPairs) { }


        public override void EditModelNames()
        {
            renamingModelsStarted = false;
            using (StreamWriter sw = new StreamWriter(FbxFileNameEdited))
            {
                using (StreamReader sr = new StreamReader(fbxFileName))
                {

                    while (ReplaceNextObjFBXLine(sw, sr)!=null) { }

                    //Дописать FBX до конца
                    sw.Write(sr.ReadToEnd());
                }
            }
        }


        /// <summary>
        /// Читает строки из текстового FBX
        /// Если строка содержит не пустое имя модели
        /// </summary>
        /// <param name="sw"></param>
        /// <param name="sr"></param>
        /// <returns></returns>
        private string ReplaceNextObjFBXLine(StreamWriter sw, StreamReader sr)
        {
            //Найти внутри узла Objects следующий подузел Model
            //До тех пор пока не найден, нужно записывать каждую строку в новый файл без изменений
            string fbxLine = null;
            while ((fbxLine = sr.ReadLine()) != null)
            {
                bool changes = false;
                if (regex.IsMatch(fbxLine))
                {

                    //Найдена строка с именем объекта
                    string toReplace = regex2.Match(fbxLine).Value;
                    //string modelName = fbxLine.Split(' ')[2];//Пробелы могут быть в самом имени
                    int len = toReplace.Length > 10 ? toReplace.Length - 10 : 0;
                    string modelName = toReplace.Substring(8, len);

                    //Последовательность узлов моделей, которые подлежат переименованию
                    //начинается только после узла Environment
                    if (!renamingModelsStarted)
                    {
                        if (modelName.Equals("Environment"))
                        {
                            renamingModelsStarted = true;
                        }
                    }
                    if (renamingModelsStarted)
                    {
                        bool editAllowed = true;
                        NameReplacement currReplacement = replacements.Peek();
                        if (currReplacement.OldNameTrustable)
                        {
                            if (!modelName.Equals(currReplacement.OldName))
                            {
                                editAllowed = false;
                            }

                        }

                        if (editAllowed)
                        {
                            currReplacement = replacements.Dequeue();
                            if (!currReplacement.SkipNode)
                            {
                                changes = true;
                                //Строка редактируется
                                string replacement
                                    = "\"Model::" + currReplacement.NewName + "\",";
                                string fbxLineEdited
                                    = fbxLine.Replace(toReplace, replacement);
                                sw.WriteLine(fbxLineEdited);
                            }
                        }
                    }

                    
                }


                if (!changes)
                {
                    sw.WriteLine(fbxLine);
                }

                return fbxLine;
            }
            return null;
        }
    }
}
