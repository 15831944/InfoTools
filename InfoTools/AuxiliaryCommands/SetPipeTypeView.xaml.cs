﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Civil3DInfoTools.AuxiliaryCommands
{
    /// <summary>
    /// Логика взаимодействия для SetPipeTypeView.xaml
    /// </summary>
    public partial class SetPipeTypeView : Window
    {
        public SetPipeTypeView()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
