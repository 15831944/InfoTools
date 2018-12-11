using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools
{
    public class PolygonNestingTree
    {
        //корневой узел фиктивный
        protected PolygonNestingNode root = new PolygonNestingNode(null);

        public void Insert(PolygonNestingNode insertingNode)
        {
            Insert(root, insertingNode);
        }

        protected void Insert(PolygonNestingNode node, PolygonNestingNode insertingNode)
        {
            //вставка контура в дерево вложенности!
            bool isNested = false;
            //Проверить вложена ли добавляемая полилиния в один из дочерних узлов
            foreach (PolygonNestingNode nn in node.NestedNodes)
            {
                if (nn.IsNested(insertingNode))
                {
                    //рекурсия
                    Insert(nn, insertingNode);
                    isNested = true;
                    break;
                }
            }

            if (!isNested)
            {
                //Если полилиния не вложена в дочерние узлы, то проверить не вложены ли дочерние узлы в добавляемую полилинию
                for (int i = 0; i < node.NestedNodes.Count;)
                {
                    PolygonNestingNode nn = (PolygonNestingNode)node.NestedNodes[i];
                    if (insertingNode.IsNested(nn))
                    {
                        //Если вложена, то убрать из node.NestedNodes и добавить в insertingNode.NestedNodes
                        node.NestedNodes.Remove(nn);
                        insertingNode.NestedNodes.Add(nn);
                    }
                    else
                    {
                        i++;
                    }
                }

                //Добавить insertingNode в node.NestedNodes
                node.NestedNodes.Add(insertingNode);
            }

        }
    }


    public class PolygonNestingNode
    {
        public List<PolygonNestingNode> NestedNodes { get; private set; } = new List<PolygonNestingNode>();


        public Point2dCollection Point2dCollection { get; protected set; }

        public PolygonNestingNode(Point2dCollection ptsColl)
        {
            Point2dCollection = ptsColl;
        }

        /// <summary>
        /// Переданный узел вложен в вызывающий узел
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool IsNested(PolygonNestingNode node)
        {
            //Проверка по одной точке, так как предполагается, что полилинии не пересекаются - неподходит
            //return Utils.PointIsInsidePolylineWindingNumber(node.Point2dCollection[0], this.Point2dCollection);


            //предполагается, что полилинии могут касаться
            //=> взять любую внутреннюю точку переданного узла и проверить, попадает ли она в вызывающий узел
            List<Point2d> ptList = node.Point2dCollection.ToArray().ToList();
            Point2d pt = Utils.GetAnyPointInsidePoligon(ptList, Utils.DirectionIsClockwise(ptList));
            return Utils.PointIsInsidePolylineWindingNumber(pt, this.Point2dCollection);
        }
    }
}
