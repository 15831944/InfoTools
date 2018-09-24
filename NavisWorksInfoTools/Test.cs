using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Win = System.Windows;

namespace NavisWorksInfoTools
{
    //[Plugin("Test",
    //    DEVELOPER_ID,
    //    ToolTip = "Test",
    //    DisplayName = "Test")]
    public class Test : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            Document oDoc = Application.ActiveDocument;
            DocumentSelectionSets selectionSets = oDoc.SelectionSets;
            FolderItem rootFolderItem = selectionSets.RootItem;

            string output =
            WriteSelectionSetContent(
            rootFolderItem,
            oDoc.Title,
            "");

            Win.MessageBox.Show(output);

            return 0;
        }


        static public string WriteSelectionSetContent(SavedItem item, string label, string lineText)
        {
            //set the output
            string output = lineText + "+ " + label + "\n";

            //See if this SavedItem is a GroupItem
            if (item.IsGroup)
            {
                //Indent the lines below this item
                lineText += "   ";

                //iterate the children and output
                foreach (SavedItem childItem in ((GroupItem)item).Children)
                {
                    output += WriteSelectionSetContent(childItem, childItem.DisplayName, lineText);
                }
            }

            //return the node information
            return output;
        }

    }
}
