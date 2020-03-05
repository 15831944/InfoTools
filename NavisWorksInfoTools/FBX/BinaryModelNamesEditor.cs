using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavisWorksInfoTools.FBX
{
    /// <summary>
    /// Редактирует бинарный FBX
    /// https://code.blender.org/2013/08/fbx-binary-file-format-specification/
    /// </summary>
    public class BinaryModelNamesEditor : ModelNamesEditor
    {
        enum BinaryFBXVer
        {
            Small,
            Big
        }
        private BinaryFBXVer binaryFBXVer = BinaryFBXVer.Small;


        private long globalOffsetChange = 0;
        /// <summary>
        /// Пропускать все узлы Model до того как появится узел Environment
        /// Там могут быть камеры и источники света
        /// </summary>
        private bool renamingModelsStarted = false;

        public BinaryModelNamesEditor
            (string fbxFileName, Queue<NameReplacement> replacementPairs)
            : base(fbxFileName, replacementPairs) { }


        public override void EditModelNames()
        {

            using (BinaryReader br = new BinaryReader(File.Open(fbxFileName, FileMode.Open)))
            {
                //Прочитать заголовок файла
                char[] fileMagicArr = br.ReadChars(21);
                string fileMagic = new string(fileMagicArr);
                byte[] unknown = br.ReadBytes(2);
                uint versionNum = br.ReadUInt32();
                if (versionNum <= 7400)
                {
                    binaryFBXVer = BinaryFBXVer.Small;
                }
                else if (versionNum <= 7500)
                {
                    binaryFBXVer = BinaryFBXVer.Big;
                }


                if (versionNum <= 7500)
                {
                    globalOffsetChange = 0;
                    renamingModelsStarted = false;
                    using (BinaryWriter bw = new BinaryWriter(File.Open(base.FbxFileNameEdited, FileMode.Create)))
                    {
                        //Написать Header в новый файл
                        bw.Write(fileMagicArr);
                        bw.Write(unknown);
                        bw.Write(versionNum);
                        //Рекурсивно читать узлы и писать отредактированные
                        if (binaryFBXVer == BinaryFBXVer.Small)
                        {
                            //Последовательность узлов заканчивается  пустым узлом
                            while (ReadWriteFBXNodeSmall(br, bw, 0, null)) { }
                        }
                        else
                        {
                            while (ReadWriteFBXNodeBig(br, bw, 0, null)) { }
                        }


                        //Дальше идет еще какой-то футер. Не менять его
                        bw.Write(br.ReadBytes((int)br.BaseStream.Length));
                    }

                }
                else
                {
                    throw new Exception("Версия FBX не поддреживается");
                }

            }
        }


        /// <summary>
        /// Возвращает false если узел пустой
        /// 13 null байтов - это как раз длина пустого узла FBX (для малого fbx)
        /// </summary>
        /// <param name="br"></param>
        /// <param name="nestingLevel"></param>
        /// <returns></returns>
        private bool ReadWriteFBXNodeSmall(BinaryReader br, BinaryWriter bw, uint nestingLevel, OffsetCounter parentOffsetCounter)
        {
            //Изменение длины узла (за счет изменения имен)
            OffsetCounter offsetCounter = new OffsetCounter();


            long endOffsetPos = bw.BaseStream.Position;//адрес байта, значение в котором возможно нужно будет изменить
            uint endOffset = ReadWriteEndOffsetAccordingToGlobalOffsetChangeSmall(br, bw);//РЕДАКТИРОВАТЬ//ЭТО АДРЕС БАЙТА, С КОТОРОГО НАЧИНАЮТСЯ СЛЕДУЮЩИЕ ДАННЫЕ
            uint numProps = ReadWriteUInt32(br, bw);
            long propListLenPos = bw.BaseStream.Position;//адрес байта, значение в котором возможно нужно будет изменить
            uint propListLen = ReadWriteUInt32(br, bw);//РЕДАКТИРОВАТЬ - длина списка свойств в байтах


            offsetCounter.EndOffsetPos = endOffsetPos;
            offsetCounter.PropListLenPos = propListLenPos;

            byte nameLen = ReadWriteByte(br, bw);//Имя узла - это не имя модели//максимум 255 символов




            string name = null;
            if (nameLen > 0)
            {
                name = new string(ReadWriteChars(br, bw, nameLen));
            }
            else
            {
                name = "---";
            }

            if (name.Equals("Model"))
            {

                //Найти имя модели в свойствах
                for (uint i = 0; i < numProps; i++)
                {
                    ReadFBXProperty(br, bw, offsetCounter);
                }

                if (offsetCounter.OwnOffsetChange != 0)
                {
                    //ВСЕ ПОСЛЕДУЮЩИЕ УЗЛЫ В ФАЙЛЕ ТАК ЖЕ МЕНЯЮТ ОФСЕТ
                    globalOffsetChange += offsetCounter.OwnOffsetChange;
                }

            }
            else
            {
                //просто прочитать и переписать свойства без изменеий
                ReadWriteBytes(br, bw, propListLen);
            }

            //Далее нужно прочитать все свойства

            //Если у узла есть вложенные, то после них будет 13 NULL–record
            //Если вложенных нет, то сразу же идет следующий узел
            if (br.BaseStream.Position < endOffset)
            {
                uint childrenNL = nestingLevel + 1;

                //long offsetChange = 0;
                //13 null байтов - это как раз длина пустого узла FBX
                while (ReadWriteFBXNodeSmall(br, bw, childrenNL, offsetCounter))
                {
                }
            }


            if (offsetCounter.OwnOffsetChange != 0 || offsetCounter.NestedNodesOffsetChanged)
            {
                if (parentOffsetCounter != null)
                    parentOffsetCounter.NestedNodesOffsetChanged = true;//Сообщить родительскому узлу о том что длина вложенного узла изменилась

                //Изменилась длина узла в байтах.
                //Отредактировать в записываемом файле указатель на конец узла и длину свойств для этого узла
                long currBWPos = bw.BaseStream.Position;
                bw.BaseStream.Position = offsetCounter.EndOffsetPos;//Переход на нужную позицию в памяти
                bw.Write(Convert.ToUInt32(currBWPos));//Перезапись endOffset

                if (offsetCounter.OwnOffsetChange != 0)//РЕДАКТИРОВАТЬ propListLen ТОЛЬКО ЕСЛИ СВОЙСТВО ОТРЕДАКТИРОВАНО В ЭТОМ УЗЛЕ
                {
                    bw.BaseStream.Position = offsetCounter.PropListLenPos;//Переход на нужную позицию в памяти
                    bw.Write(Convert.ToUInt32(propListLen + offsetCounter.OwnOffsetChange));//Перезапись propListLen
                }


                bw.BaseStream.Position = currBWPos;//Возврат на текущую позицию

            }


            return endOffset != 0;
        }

        /// <summary>
        /// Возвращает false если узел пустой
        /// 25 null байтов - это как раз длина пустого узла FBX
        /// </summary>
        /// <param name="br"></param>
        /// <param name="bw"></param>
        /// <param name="nestingLevel"></param>
        /// <param name="parentOffsetCounter"></param>
        /// <returns></returns>
        private bool ReadWriteFBXNodeBig(BinaryReader br, BinaryWriter bw,
            uint nestingLevel, OffsetCounter parentOffsetCounter)
        {
            //Изменение длины узла (за счет изменения имен)
            OffsetCounter offsetCounter = new OffsetCounter();


            long endOffsetPos = bw.BaseStream.Position;//адрес байта, значение в котором возможно нужно будет изменить
            ulong endOffset = ReadWriteEndOffsetAccordingToGlobalOffsetChangeBig(br, bw);//РЕДАКТИРОВАТЬ//ЭТО АДРЕС БАЙТА, С КОТОРОГО НАЧИНАЮТСЯ СЛЕДУЮЩИЕ ДАННЫЕ
            ulong numProps = ReadWriteUInt64(br, bw);
            long propListLenPos = bw.BaseStream.Position;//адрес байта, значение в котором возможно нужно будет изменить
            ulong propListLen = ReadWriteUInt64(br, bw);//РЕДАКТИРОВАТЬ - длина списка свойств в байтах


            offsetCounter.EndOffsetPos = endOffsetPos;
            offsetCounter.PropListLenPos = propListLenPos;

            byte nameLen = ReadWriteByte(br, bw);//Имя узла - это не имя модели//максимум 255 символов




            string name = null;
            if (nameLen > 0)
            {
                name = new string(ReadWriteChars(br, bw, nameLen));
            }
            else
            {
                name = "---";
            }

            if (name.Equals("Model"))
            {

                //Найти имя модели в свойствах
                for (uint i = 0; i < numProps; i++)
                {
                    ReadFBXProperty(br, bw, offsetCounter);
                }

                if (offsetCounter.OwnOffsetChange != 0)
                {
                    //ВСЕ ПОСЛЕДУЮЩИЕ УЗЛЫ В ФАЙЛЕ ТАК ЖЕ МЕНЯЮТ ОФСЕТ
                    globalOffsetChange += offsetCounter.OwnOffsetChange;
                }

            }
            else
            {
                //просто прочитать и переписать свойства без изменеий
                ReadWriteBytes(br, bw, propListLen);
            }

            //Далее нужно прочитать все свойства

            //Если у узла есть вложенные, то после них будет 13 NULL–record
            //Если вложенных нет, то сразу же идет следующий узел
            if (Convert.ToUInt64(br.BaseStream.Position) < endOffset)
            {
                uint childrenNL = nestingLevel + 1;

                //long offsetChange = 0;
                //13 null байтов - это как раз длина пустого узла FBX
                while (ReadWriteFBXNodeBig(br, bw, childrenNL, offsetCounter))
                {
                }
            }


            if (offsetCounter.OwnOffsetChange != 0 || offsetCounter.NestedNodesOffsetChanged)
            {
                if (parentOffsetCounter != null)
                    parentOffsetCounter.NestedNodesOffsetChanged = true;//Сообщить родительскому узлу о том что длина вложенного узла изменилась

                //Изменилась длина узла в байтах.
                //Отредактировать в записываемом файле указатель на конец узла и длину свойств для этого узла
                long currBWPos = bw.BaseStream.Position;
                bw.BaseStream.Position = offsetCounter.EndOffsetPos;//Переход на нужную позицию в памяти
                bw.Write(Convert.ToUInt64(currBWPos));//Перезапись endOffset

                if (offsetCounter.OwnOffsetChange != 0)//РЕДАКТИРОВАТЬ propListLen ТОЛЬКО ЕСЛИ СВОЙСТВО ОТРЕДАКТИРОВАНО В ЭТОМ УЗЛЕ
                {
                    bw.BaseStream.Position = offsetCounter.PropListLenPos;//Переход на нужную позицию в памяти
                    bw.Write(Convert.ToUInt64(Convert.ToInt64(propListLen)
                        + offsetCounter.OwnOffsetChange));//Перезапись propListLen
                }


                bw.BaseStream.Position = currBWPos;//Возврат на текущую позицию

            }


            return endOffset != 0;
        }

        /// <summary>
        /// Читает и переписывает любое свойство. Если это название модели, то изменяет его
        /// </summary>
        /// <param name="br"></param>
        private void ReadFBXProperty(BinaryReader br, BinaryWriter bw, OffsetCounter offsetCounter)
        {
            char typeCode = ReadWriteChar(br, bw);
            bool isArray = false;
            bool isSpecial = false;
            byte dataSize = 0;
            switch (typeCode)
            {
                //Primitive Types
                case 'Y':
                    dataSize = 2;
                    break;
                case 'C':
                    dataSize = 1;
                    break;
                case 'I':
                    dataSize = 4;
                    break;
                case 'F':
                    dataSize = 4;
                    break;
                case 'D':
                    dataSize = 8;
                    break;
                case 'L':
                    dataSize = 8;
                    break;
                //Array types
                case 'f':
                    isArray = true;
                    dataSize = 4;
                    break;
                case 'd':
                    isArray = true;
                    dataSize = 8;
                    break;
                case 'l':
                    isArray = true;
                    dataSize = 8;
                    break;
                case 'i':
                    isArray = true;
                    dataSize = 4;
                    break;
                case 'b':
                    isArray = true;
                    dataSize = 1;
                    break;
                //Special types
                case 'S':
                    isSpecial = true;
                    break;
                case 'R':
                    isSpecial = true;
                    break;
                default:
                    throw new Exception("Ошибка при чтении FBX. Неизвестный код свойства - " + typeCode);
                    break;
            }

            if (isArray)
            {
                uint arrLen = ReadWriteUInt32(br, bw);
                uint encoding = ReadWriteUInt32(br, bw);
                uint compressedLength = ReadWriteUInt32(br, bw);
                if (encoding == 0)
                {
                    ReadWriteBytes(br, bw, dataSize * arrLen);
                }
                else if (encoding == 1)
                {
                    ReadWriteBytes(br, bw, compressedLength);
                }
                else
                {
                    throw new Exception("Ошибка при чтении FBX. Неизвестное значение encoding массива");
                }
            }
            else if (isSpecial)
            {
                //здесь может храниться имя модели, которое нудно отредактировать
                ReadFBXPropWithSpecialDataTypeEditModelName(br, bw, typeCode.Equals('S'), offsetCounter);
            }
            else
            {
                //простейший тип данных
                ReadWriteBytes(br, bw, dataSize);
            }
        }




        /// <summary>
        /// Читает байт и пишет то же самое
        /// </summary>
        /// <param name="br"></param>
        /// <returns></returns>
        private byte ReadWriteByte(BinaryReader br, BinaryWriter bw)
        {
            byte value = br.ReadByte();
            bw.Write(value);
            return value;
        }

        /// <summary>
        /// Читает байты и пишет то же самое
        /// </summary>
        /// <param name="br"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private void /*byte[]*/ ReadWriteBytes(BinaryReader br, BinaryWriter bw, uint count)
        {
            int maxValueToRead = int.MaxValue - 10;
            uint maxValueToReadConverted = Convert.ToUInt32(maxValueToRead);
            while (count > maxValueToReadConverted)
            {
                count -= maxValueToReadConverted;
                bw.Write(br.ReadBytes(maxValueToRead));
            }
            bw.Write(br.ReadBytes(Convert.ToInt32(count)));
            //byte[] value = br.ReadBytes(Convert.ToInt32(count));
            //bw.Write(value);
            //return value;
        }

        /// <summary>
        /// Читает байты и пишет то же самое
        /// </summary>
        /// <param name="br"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private void /*byte[]*/ ReadWriteBytes(BinaryReader br, BinaryWriter bw, int count)
        {
            byte[] value = br.ReadBytes(count);
            bw.Write(value);
            //return value;
        }

        /// <summary>
        /// Читает байты и пишет то же самое
        /// </summary>
        /// <param name="br"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private static void ReadWriteBytes(BinaryReader br, BinaryWriter bw, ulong count)
        {
            int maxReadSize = (int.MaxValue - 10);
            ulong convertedReadSize = Convert.ToUInt64(maxReadSize);
            while (count > convertedReadSize)
            {
                count -= convertedReadSize;
                bw.Write(br.ReadBytes(maxReadSize));
            }
            bw.Write(br.ReadBytes(Convert.ToInt32(count)));
        }

        /// <summary>
        /// Читает UInt32 и пишет то же самое
        /// </summary>
        /// <param name="br"></param>
        /// <returns></returns>
        private uint ReadWriteUInt32(BinaryReader br, BinaryWriter bw)
        {
            uint value = br.ReadUInt32();
            bw.Write(value);
            return value;
        }

        /// <summary>
        /// Читает UInt64 и пишет то же самое
        /// </summary>
        /// <param name="br"></param>
        /// <returns></returns>
        private static ulong ReadWriteUInt64(BinaryReader br, BinaryWriter bw)
        {
            ulong value = br.ReadUInt64();
            bw.Write(value);
            return value;
        }

        /// <summary>
        /// Читает символ и пишет то же самое
        /// </summary>
        /// <param name="br"></param>
        /// <returns></returns>
        private char ReadWriteChar(BinaryReader br, BinaryWriter bw)
        {
            char value = br.ReadChar();
            bw.Write(value);
            return value;
        }

        /// <summary>
        /// Читает символ и пишет то же самое
        /// </summary>
        /// <param name="br"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private char[] ReadWriteChars(BinaryReader br, BinaryWriter bw, uint count)
        {
            char[] value = br.ReadChars(Convert.ToInt32(count));
            bw.Write(value);
            return value;
        }


        /// <summary>
        /// Считывает EndOffset узла FBX.
        /// Записывает EndOffset с учетом изменений EndOffset всех злов, которые были записаны выше
        /// </summary>
        /// <param name="br"></param>
        /// <param name="bw"></param>
        /// <returns></returns>
        private uint ReadWriteEndOffsetAccordingToGlobalOffsetChangeSmall
            (BinaryReader br, BinaryWriter bw)
        {
            uint value = br.ReadUInt32();
            uint valueToWrite = value;
            if (globalOffsetChange != 0 && value != 0)
            {
                valueToWrite = Convert.ToUInt32(Convert.ToInt64(valueToWrite) + globalOffsetChange);
            }

            bw.Write(valueToWrite);
            return value;
        }

        private ulong ReadWriteEndOffsetAccordingToGlobalOffsetChangeBig
            (BinaryReader br, BinaryWriter bw)
        {
            ulong value = br.ReadUInt64();
            ulong valueToWrite = value;
            if (globalOffsetChange != 0 && value != 0)
            {
                valueToWrite = Convert.ToUInt64(Convert.ToInt64(valueToWrite) + globalOffsetChange);
            }

            bw.Write(valueToWrite);
            return value;
        }


        /// <summary>
        /// Читает свойство FBX со специальным типом данных после прочтения кода типа данных
        /// 
        /// Если свойство строковое, то
        /// Читает строку в UTF-8 по переданной длине в байтах.
        /// Если строка содержит заголовок Model, то
        /// Если имя модели не пустое
        /// (самая первая модель называется Environment и соответстует корневому узлу в navis),
        /// то редактирует согласно очереди.
        /// 
        /// Записывает изменение длины строки в байтах
        /// Если заголовка Model нет или свойство не строковое или это узел без имени, то переписывает без изменений
        /// </summary>
        /// <param name="br"></param>
        /// <param name="bytesCount"></param>
        /// <param name="addToEnd"></param>
        /// <returns></returns>
        private void ReadFBXPropWithSpecialDataTypeEditModelName
            (BinaryReader br, BinaryWriter bw, bool isString, OffsetCounter offsetCounter)
        {
            int len = br.ReadInt32();
            if (isString)
            {
                byte[] value = br.ReadBytes(len);//Строка в UTF-8
                string str = System.Text.Encoding.UTF8.GetString(value);
                bool changes = false;

                if (replacements.Count > 0 && str.EndsWith("\0\u0001Model") /*&& str.Length > 7*/)
                {
                    //Это свойство с именем модели
                    string modelName = str.Replace("\0\u0001Model", "");

                    //Последовательность узлов моделей, которые подлежат переименованию
                    //начинается только после узла, у которого имя совпадает
                    if (!renamingModelsStarted)
                    {
                        NameReplacement firstElem = replacements.Peek();
                        NameReplacement secondElem = replacements.Count > 1 ?
                                replacements.ElementAt(1)
                                : null;
                        if (
                            //modelName.Equals("Environment")//УЗЕЛ Environment ЕСТЬ НЕ ВСЕГДА!!! В каких случаях его нет пока не понятно.
                            firstElem.OldName.Equals(modelName)
                            )
                        {
                            renamingModelsStarted = true;
                        }
                        else if (secondElem!=null && secondElem.OldNameTrustable && secondElem.OldName.Equals(modelName))
                        {
                            // На всякий случай проверяем второй элемент в очереди (возможно 1-й вообще не появится в FBX?)
                            renamingModelsStarted = true;
                            replacements.Dequeue();//Удаляем узел, который должен был соответствовать Environment
                        }
                    }
                    if (renamingModelsStarted)
                    {
                        bool editAllowed = true;
                        #region hardcode
                        //hardcode//hardcode//hardcode//hardcode//hardcode//hardcode
                        //Замечено, что иногда при экспорте один узел Navis
                        //разбивается на несколько вложенных в FBX (они назывались "curve " и номер)
                        //if (strVal.StartsWith("curve "))
                        //{
                        //    editAllowed = false;
                        //}
                        //else
                        //{
                        //    curveSequenceStarted = false;
                        //}
                        //hardcode//hardcode//hardcode//hardcode//hardcode//hardcode 
                        #endregion
                        //Более гибко - добавить сопоставление имен там, где оно надежно
                        //Это защищает от большинства узлов, которых не было в Navis, но они появились в FBX
                        NameReplacement currReplacement = replacements.Peek();
                        if (currReplacement.OldNameTrustable)
                        {
                            if (!modelName.Equals(currReplacement.OldName))
                            {
                                editAllowed = false;
                            }

                        }
                        if (editAllowed)//Не вытаскивать больше из очереди если идет последовательность несопоставляемых узлов
                        {
                            currReplacement = replacements.Dequeue();

                            if (!currReplacement.SkipNode)
                            {
                                changes = true;
                                //Строка редактируется, длина строки меняется
                                string strEdited = currReplacement.NewName + "\0\u0001Model";
                                byte[] newValue = Encoding.UTF8.GetBytes(strEdited);
                                int newLen = newValue.Length;
                                int lenDiff = newLen - len;
                                offsetCounter.Incr(lenDiff);
                                bw.Write(newLen);
                                bw.Write(newValue);
                            }
                        }
                    }
                }

                if (!changes)
                {
                    //Ничего не меняется
                    bw.Write(len);
                    bw.Write(value);

                }
            }
            else
            {
                //Ничего не меняется
                bw.Write(len);
                ReadWriteBytes(br, bw, len);
            }



        }











        private class OffsetCounter
        {
            public long EndOffsetPos { get; set; } = 0;

            public long PropListLenPos { get; set; } = 0;

            /// <summary>
            /// Изменение длины узла в байтах
            /// </summary>
            public long OwnOffsetChange { get; set; } = 0;

            public bool NestedNodesOffsetChanged { get; set; } = false;

            public void Incr(long incr)
            {
                OwnOffsetChange += incr;
            }
        }
    }
}
