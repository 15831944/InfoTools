using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavisWorksInfoTools.FBX
{
    /// <summary>
    /// Заменяет в файле FBX имена моделей в соответствии с переданным списком пар замены
    /// Работа основана на том факте, что порядок перечисления узлов в файле FBX соответствует порядку элементов в дереве модели Navis
    /// </summary>
    public abstract class ModelNamesEditor
    {
        protected string fbxFileName = null;
        protected Queue<NameReplacement> replacements = null;
        public string FbxFileNameEdited { get; set; } = null;

        public ModelNamesEditor(string fbxFileName, Queue<NameReplacement> replacements)
        {
            this.fbxFileName = fbxFileName;
            this.replacements = replacements;
            GetEditedFileName();
        }

        /// <summary>
        /// Отредактировать файл FBX
        /// </summary>
        public abstract void EditModelNames();


        private void GetEditedFileName()
        {
            FbxFileNameEdited
                = Path.Combine(Path.GetDirectoryName(fbxFileName),
                Path.GetFileNameWithoutExtension(fbxFileName) + "Edited.fbx");
        } 

    }


    public class NameReplacement
    {
        public bool SkipNode { get; set; } = false;

        /// <summary>
        /// Не всегда можно получить через API Navis точно такое же имя, как оно будет в FBX
        /// Проблемы возникают с блоками AutoCAD
        /// </summary>
        public string OldName { get; private set; } = null;
        /// <summary>
        /// Известно, что для этой замены имя в FBX обязательно должно совпадать с именем в Navis
        /// Это так только если ModelItem.DisplayName возвращает значение
        /// </summary>
        public bool OldNameTrustable { get; set; } = false;

        public string NewName { get; private set; } = null;


        public NameReplacement(string oldName, bool oldNameTrustable, string newName)
        {
            OldName = oldName;
            OldNameTrustable = oldNameTrustable;
            NewName = newName;
        }

        public NameReplacement()
        {
            SkipNode = true;
            OldNameTrustable = false;
        }
    }

}
