//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Controls;
//using System.Windows.Input;
//using System.Windows.Interactivity;

//namespace Common.Controls._Behaviors
//{
//    //Интересности про Behavior - https://professorweb.ru/my/WPF/binding_and_styles_WPF/level11/11_10.php
//    //ПОЧЕМУ-ТО ВЫДАЕТ ОШИБКУ FileNotFoundException Could not load file or assembly, or one of its dependencies
//    public class TextBoxEnterKeyUpdateBehavior : Behavior<TextBox>
//    {

//        protected override void OnAttached()
//        {
//            if (this.AssociatedObject != null)
//            {
//                base.OnAttached();
//                this.AssociatedObject.KeyDown += AssociatedObject_KeyDown;
//            }
//        }

//        protected override void OnDetaching()
//        {
//            if (this.AssociatedObject != null)
//            {
//                this.AssociatedObject.KeyDown -= AssociatedObject_KeyDown;
//                base.OnDetaching();
//            }
//        }

//        private void AssociatedObject_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
//        {
//            TextBox textBox = sender as TextBox;
//            if (textBox != null)
//            {
//                if (e.Key == Key.Return)
//                {
//                    if (e.Key == Key.Enter)
//                    {
//                        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
//                    }
//                }
//            }
//        }
//    }
//}
