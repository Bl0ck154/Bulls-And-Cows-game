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
using System.Windows.Threading;

namespace Bulls_and_cows
{
	/// <summary>
	/// Interaction logic for WaitWindow.xaml
	/// </summary>
	public partial class WaitWindow : Window
	{
		public string Message { get; set; }
		public WaitWindow()
		{
			InitializeComponent();
			DispatcherTimer timer = new DispatcherTimer();
			timer.Interval = new TimeSpan(0, 0, 0, 0, 200); // 200 milliseconds
			timer.Tick += Timer_Tick;
			timer.Start();

			Loaded += WaitWindow_Loaded;
		}

		private void WaitWindow_Loaded(object sender, RoutedEventArgs e)
		{
			messageBlock.Text = Message;
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			if (text.Text.Length > 5)
				text.Text = ".";
			else
				text.Text += ".";
		}
	}
}
