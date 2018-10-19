using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NavisWorksInfoTools.Constants;

namespace NavisWorksInfoTools
{
    [Plugin("NavisWorksInfoTools", DEVELOPER_ID, DisplayName = "NavisWorksInfoTools")]
    [RibbonLayout("S1NF0_RibbonPanel.xaml")]
    [RibbonTab("ID_S1NF0_RibbonTab", DisplayName = "S1NF0")]
    [Command("ID_ChangeAllLinks_Button", DisplayName = "Поменять все ссылки", LargeIcon = "ChangeAllLinks.png",
        ToolTip = "Поменять все ссылки")]
    [Command("ID_SetProps_Button", DisplayName = "Заполнить атрибуты", LargeIcon = "SetProps.png",
        ToolTip = "Заполнить атрибуты")]
    [Command("ID_SetPropsByExcel_Button", DisplayName = "Заполнить атрибуты по таблице Excel", LargeIcon = "SetPropsByExcel.png",
        ToolTip = "Заполнить атрибуты по таблице Excel")]

    [Command("ID_SetS1NF0Props_Button", DisplayName = "Добавить служебные свойства", LargeIcon = "SetS1NF0Props.png",
        ToolTip = "Добавить служебные свойства S1NF0 со значениями по умолчанию")]
    [Command("ID_FBXExport_Button", DisplayName = "Экспорт в FBX", LargeIcon = "FBXExport.png",
        ToolTip = "Экспорт в FBX с идентификацией объектов")]
    [Command("ID_AddObjectsToStructure_Button", DisplayName = "Добавление объектов модели в структуру", LargeIcon = "AddObjectsToStructure.png",
        ToolTip = "Указать файлы структуры и классификатора для " + S1NF0_APP_NAME + ". Добавить объекты геометрии в структуру")]
    [Command("ID_CreateStructure_Button", DisplayName = "Создание структуры из наборов выбора", LargeIcon = "CreateStructure.png",
        ToolTip = "Создать структуру для проекта " + S1NF0_APP_NAME + " из сохраненных наборов выбора")]
    public class S1NF0_RibbonPanel : CommandHandlerPlugin
    {
        public override int ExecuteCommand(string name, params string[] parameters)
        {
            switch (name)
            {
                case "ID_ChangeAllLinks_Button":
                    ExecuteAddInPlugin("ChangeAllLinks."+ DEVELOPER_ID);
                    break;
                case "ID_SetProps_Button":
                    ExecuteAddInPlugin("SetProps." + DEVELOPER_ID);
                    break;
                case "ID_SetPropsByExcel_Button":
                    ExecuteAddInPlugin("SetPropsByExcel." + DEVELOPER_ID);
                    break;
                case "ID_SetS1NF0Props_Button":
                    ExecuteAddInPlugin("SetS1NF0Props." + DEVELOPER_ID);
                    break;
                case "ID_FBXExport_Button":
                    ExecuteAddInPlugin("FBXExport." + DEVELOPER_ID);
                    break;
                case "ID_AddObjectsToStructure_Button":
                    ExecuteAddInPlugin("AddObjectsToStructure." + DEVELOPER_ID);
                    break;
                case "ID_CreateStructure_Button":
                    ExecuteAddInPlugin("CreateStructure." + DEVELOPER_ID);
                    break;
            }


            return 0;
        }

        public static void ExecuteAddInPlugin(string name)
        {
            if (!Autodesk.Navisworks.Api.Application.IsAutomated)
            {
                PluginRecord pluginRecord = Autodesk.Navisworks.Api.Application.Plugins.FindPlugin(name);
                if (pluginRecord is AddInPluginRecord && pluginRecord.IsEnabled)
                {
                    AddInPlugin addinPlugin = (AddInPlugin)(pluginRecord.LoadedPlugin ?? pluginRecord.LoadPlugin());
                    addinPlugin.Execute();
                }
            }
        }
    }
}
