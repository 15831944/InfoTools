using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.PipeNetworkCreating
{
    public partial class PipeNetworkGraph
    {
        private readonly double ZERO_LENGTH = Tolerance.Global.EqualPoint;

        /// <summary>
        /// Расстояние от колодца, на котором захватываются текстовые метки
        /// номеров колодцев
        /// </summary>
        public const double DISTANCE_TO_GET_WELL_LBL = 4;

        /// <summary>
        /// Расстояние от колодца, на котором захватываются текстовые метки
        /// присоединений к этому колодцу 
        /// </summary>
        private const double DISTANCE_TO_GET_JUNCTION_LBLS = 3;

        /// <summary>
        /// Если текст находится меньшем расстоянии к узлу сети, то он обязательно будет
        /// рассматриваться как один из конкурирующих вариантов подписей этого узла
        /// </summary>
        private const double WELL_LBL_COMPATITORS_DISTANCE = 3;

        /// <summary>
        /// Если подпись присоединения к колодцу находится дальше от присоединения
        /// чем это расстояние, то считать, что данная подпись как правило
        /// не может относиться к этому присоединению
        /// </summary>
        private const double LBL_TOO_FAR_FROM_LINE = 2;

        /// <summary>
        /// Если полилиния длиннее, то положение текста вдоль полилинии будет влиять
        /// на приоритет привязки текста к присоединению
        /// </summary>
        private const double EDGE_LENGTH_LBL_LONGITUDINAL_POSITION_MATTERS = 1.5;

        /// <summary>
        /// Если текст находится меньшем расстоянии к полилинии, то он обязательно будет
        /// рассматриваться как один из конкурирующих вариантов подписей этой полилинии
        /// </summary>
        private const double JUNCTION_LBL_COMPATITORS_DISTANCE = 0.2;

        /// <summary>
        /// Слой маркеров
        /// </summary>
        private const string MARKER_LAYER = "S1NF0_Markers";

        private const int WELL_MARKER_COLOR_INDEX = 230;

        private const int SQUARES_WITH_NO_DATA_COLOR_INDEX = 30;

        private const int NODE_WARNING_COLOR_INDEX = 50;

        private const int LBL_DULICATE_COLOR_INDEX = 20;

        private const string DATA_MATCHING_MESSAGE = "m";

        private const string SQUARE_WITH_NO_DATA_MESSAGE = "Нет файла Excel";

        private const string LBL_DULICATE_MESSAGE = "Одна подпись привязана к нескольким объектам";

        private const double BLOCK_NEAR_POLYLINE_DISTANCE = 0.2;

        private const string BLOCK_NEAR_POLYLINE_NOT_ON_ENDPOINT_MESSAGE = "Блок находится очень близко к линии сети но не в одной из конечных точек";

        private const int BLOCK_NEAR_POLYLINE_NOT_ON_ENDPOINT_COLOR_INDEX = 1;

        private enum NodeWarnings
        {
            Null = 0,
            WellLblNotFound = 1,//подпись колодца не найдена
            JunctionLblsNotFound = 2,//сочетание подписей присоединений не найдено
            AttachmentCountNotMatches = 4,//количество присоединений не совпадает с Excel
            TShapedIntersection = 8,//т-образное пересечение

        }

        private readonly Dictionary<NodeWarnings, string> nodeWarningsMessages = new Dictionary<NodeWarnings, string>()
        {
            { NodeWarnings.WellLblNotFound, "Не найдена подпись колодца" },
            { NodeWarnings.JunctionLblsNotFound, "Не найдены подписи присоединений" },
            { NodeWarnings.AttachmentCountNotMatches, "Количество присоединений не соответствует таблице Excel" },
            { NodeWarnings.TShapedIntersection, "Линии инженерной сети образуют Т-образное пересечение" },
        };
    }
}

