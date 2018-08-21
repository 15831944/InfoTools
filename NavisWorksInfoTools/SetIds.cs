using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavisWorksInfoTools
{
    /// <summary>
    /// Команда для раздачи уникальных идентификаторов всем объектам, которые имеют геометрию
    /// ОБЯЗАТЕЛЬНО НУЖНО УЧЕСТЬ, что свойства, содержащие идентификаторы не должны перезаписываться другими командами
    /// </summary>
    [Plugin("SetIds",
        "S-Info",
        ToolTip = "Раздать идентификаторы",
        DisplayName = "Раздать идентификаторы")]
    public class SetIds
    {
        public static string IdDataTabDisplayName { get; } = "S1NF0";
        public static string IdPropDisplayName { get; } = "Id";
        public static string MaterialIdPropDisplayName { get; } = "MaterialId";



    }
}
