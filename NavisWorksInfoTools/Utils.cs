using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;

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
            if (value!=null)
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
        /// Получить значение пользовательского свойства
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
            if(valueObj is string || valueObj is int || valueObj is double || valueObj is bool)
            {
                return valueObj;
            }
            else
            {
                return valueObj.ToString();
            }
        }


       
    }
}
