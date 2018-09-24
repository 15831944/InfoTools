using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using static NavisWorksInfoTools.Constants;

namespace NavisWorksInfoTools
{
    public static class Utils
    {
        /// <summary>
        /// Создать новое свойство
        /// </summary>
        /// <param name="oState"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ComApi.InwOaProperty CreateNewUserProp(ComApi.InwOpState3 oState,
            string name, object value)
        {
            // create new property
            ComApi.InwOaProperty newP = (ComApi.InwOaProperty)oState
                .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaProperty, null, null);

            // set the name, username and value of the new property
            newP.name = Common.Transliteration.Front(name);
            newP.UserName = name;
            if (value == null || (value is string && String.IsNullOrWhiteSpace(value as string)))
                value = "_";
            newP.value = ObjectToSetAsUserPropertyValue(value);
            return newP;
        }


        /// <summary>
        /// Копировать пользовательское свойство для создания
        /// </summary>
        /// <param name="oState"></param>
        /// <param name="propsColl"></param>
        /// <param name="propToCopy"></param>
        public static ComApi.InwOaProperty CopyProp(ComApi.InwOpState3 oState,
            DataProperty propToCopy)
        {
            object value = GetUserPropValue(propToCopy.Value);
            if (value != null)
            {
                return Utils.CreateNewUserProp(oState, propToCopy.DisplayName, value);
            }
            return null;
        }

        public static ComApi.InwOaProperty CopyProp(ComApi.InwOpState3 oState,
            ComApi.InwOaProperty propToCopy)
        {
            object value = propToCopy.value;
            if (value != null)
            {
                return Utils.CreateNewUserProp(oState, propToCopy.UserName, value);
            }
            return null;
        }

        /// <summary>
        /// Получить значение свойства, которое можно присвоить пользовательскому свойству
        /// </summary>
        /// <param name="variantData"></param>
        /// <returns></returns>
        public static object GetUserPropValue(VariantData variantData)
        {
            if (variantData.IsDisplayString)
            {
                return variantData.ToDisplayString();
            }
            else if (variantData.IsBoolean)
            {
                return variantData.ToBoolean();
            }
            else if (variantData.IsDouble)
            {
                return variantData.ToDouble();
            }
            else if (variantData.IsInt32)
            {
                return variantData.ToInt32();
            }
            else
            {
                //Если свойство имеет какой-то другой тип данных то возвращается строка
                return GetDisplayValue(variantData);
            }

        }

        /// <summary>
        /// Попытаться сконвертировать строку в один из типов, доступных для пользовательских атрибутов
        /// </summary>
        /// <param name="strVal"></param>
        /// <returns></returns>
        public static object ConvertValueByString(string strVal)
        {
            if (String.IsNullOrEmpty(strVal))
            {
                return "_";
            }

            try
            {
                return Convert.ToBoolean(strVal);
            }
            catch (Exception /*FormatException*/)
            {
                try
                {
                    return Convert.ToInt32(strVal);
                }
                catch (Exception /*FormatException*/)
                {
                    try
                    {
                        return Convert.ToDouble(strVal);
                    }
                    catch (Exception /*FormatException*/)
                    {
                        return strVal;
                    }
                }
            }
        }


        /// <summary>
        /// Строковое отображение значения свойства без приставки типа данных
        /// TODO: Если тип данных - double, то 
        /// </summary>
        /// <param name="variantData"></param>
        /// <returns></returns>
        public static string GetDisplayValue(VariantData variantData)
        {
            string[] strs = variantData.ToString().Split(':');
            string dispValue = String.Join("", strs, 1, strs.Length - 1);
            if (dispValue == null)
            {
                dispValue = "";
            }
            return dispValue;
        }


        /// <summary>
        /// Все типы кроме string, int, double и bool возвращают строковое отображение объекта
        /// </summary>
        /// <param name="valueObj"></param>
        /// <returns></returns>
        public static object ObjectToSetAsUserPropertyValue(object valueObj)
        {
            if (valueObj is string || valueObj is int || valueObj is double || valueObj is bool)
            {
                return valueObj;
            }
            else
            {
                return valueObj.ToString();
            }
        }


        /// <summary>
        /// Значение передаваемое в качестве значения для свойства в методе SetS1NF0PropsToItem для создания случа
        /// </summary>
        public enum S1NF0PropSpecialValue
        {
            RandomGUID,
        }

        /// <summary>
        /// Добавить служебные свойства S1NF0 если их еще нет
        /// Не меняет свойства если они уже есть если не передан параметр overwrite = true
        /// Возвращает true если свойства объекта отредактированы
        /// </summary>
        /// <param name="oState"></param>
        /// <param name="item"></param>
        /// <param name="propsToWrite"></param>
        public static bool SetS1NF0PropsToItem
            (ComApi.InwOpState3 oState, ModelItem item, Dictionary<string, object> propsToWrite,
            bool overwrite = false)
        {
            ComApi.InwOaPropertyVec propsToSet
                                    = oState.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec);

            //convert the .NET collection to COM object
            ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(item);
            //Получить текущие свойства элемента
            ComApi.InwGUIPropertyNode2 propertyNode
                = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);
            //Поиск панели Id
            int indexToSet = 0;
            int i = 1;


            foreach (ComApi.InwGUIAttribute2 attr in propertyNode.GUIAttributes())
            {
                if (attr.UserDefined)
                {
                    if (attr.ClassUserName.Equals(S1NF0_DATA_TAB_DISPLAY_NAME))
                    {
                        indexToSet = i;
                        foreach (ComApi.InwOaProperty prop in attr.Properties())
                        {
                            if (propsToWrite.ContainsKey(prop.UserName))
                            {
                                if (!overwrite)
                                    propsToWrite.Remove(prop.UserName);
                                else
                                    continue;//Перейти к следующему свойству
                            }

                            propsToSet.Properties().Add(Utils.CopyProp(oState, prop));
                        }

                    }
                    else
                    {
                        i++;
                    }
                }
            }

            if (propsToWrite.Count > 0)
            {
                foreach (KeyValuePair<string, object> kvp in propsToWrite)
                {
                    string propName = kvp.Key;
                    object value = kvp.Value;
                    if (value is S1NF0PropSpecialValue)
                    {
                        if ((S1NF0PropSpecialValue)value == S1NF0PropSpecialValue.RandomGUID)
                        {
                            value = Guid.NewGuid().ToString();
                        }
                        else
                        {
                            value = "_";
                        }
                    }
                    
                    propsToSet.Properties().Add(CreateNewUserProp(oState, propName, value));
                }

                propertyNode.SetUserDefined(indexToSet, S1NF0_DATA_TAB_DISPLAY_NAME, "S1NF0", propsToSet);
                return true;
            }
            return false;
        }


        public static string GetSafeS1NF0AppPropName(string propName)
        {
            //ДЛИНА НАЗВАНИЯ СВОЙСТВА НЕ БОЛЕЕ 127 СИМВОЛОВ
            if (propName.Length>127)
            {
                propName = propName.Substring(0, 127);
            }

            //УДАЛИТЬ ВСЕ НЕДОПУСТИМЫЕ СИМВОЛЫ В НАЗВАНИЯХ СВОЙСТВ
            propName = string.Join("_", propName.Split(new char[] { '[', ']', '(', ')', '{', '}', '/', '\\'})).Trim();

            return propName;
        }

    }
}
