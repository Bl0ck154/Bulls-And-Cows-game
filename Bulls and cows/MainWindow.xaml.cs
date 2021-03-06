﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BullsAndCowsAdditions;

namespace Bulls_and_cows
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		const bool TURN_MODE = true; // пошаговый режим true - включен

		bool isStarted = false;
		HiddenNumber answerNumber; 
		DateTime startTime;
		DispatcherTimer dispatcherTimer;
		TcpClient tcpClientOpponent;
		public bool isConnected { get { return (tcpClientOpponent!= null && tcpClientOpponent.Connected); } }
		bool opponentIsReady = false;
		bool isClosed = false;
		int triesCount = 0;
		bool playingOnServer = false;

		public MainWindow()
		{
			InitializeComponent();
			
			btnStart.Focus();

			this.KeyDown += MainWindow_KeyDown;
			this.Closing += MainWindow_Closing;
		}

		private void MainWindow_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && !e.IsRepeat)
				btnStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		}

		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			isClosed = true;
		}

		private void textbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			// numeric textbox
			e.Handled = !IsStringNumeric(e.Text);
			if (!e.Handled)
				(sender as TextBox).Text = "";
		}

		bool findRepeats(string text)
		{
			var result = text.GroupBy(c => c).Where(c => c.Count() > 1);
			return result.Count() > 0;
		}

		// check string has numbers
		bool IsStringNumeric(string str)
		{
			return Regex.IsMatch(str, "^[0-9]+$");
		}

		// PreviewKeyDown event to restrict space key because of PreviewTextInput doesn't catch space
		private void textbox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			TextBox textBox = (sender as TextBox);

			if (e.Key == Key.Space)
			{
				e.Handled = true;
			}
			else if(e.Key == Key.Left || e.Key == Key.Up)
			{
				if (!textBox.Name.Contains('1'))
				{
					myMoveFocus(FocusNavigationDirection.Previous);
				}
			}
			else if(e.Key == Key.Right || e.Key == Key.Down)
			{
				if (!textBox.Name.Contains('4'))
				{
					myMoveFocus(FocusNavigationDirection.Next);
				}
			}
			else if (e.Key == Key.Back && textBox.Text == "" && !e.IsRepeat)
			{
				myMoveFocus(FocusNavigationDirection.Previous);
				e.Handled = true;
			}
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (!isStarted)
				BtnStart();
			else
				Try();
		}

		const byte readyPacket = 234;
		private void BtnStart()
		{
			if (isConnected)
			{
				string number = getTextboxNumberValue();
				if (number.Length < HiddenNumber.LENGTH || findRepeats(number))
				{
					showPopup("Please enter 4 different numbers!", 2500);
					return;
				}

				answerNumber = new HiddenNumber(number);

				if (MenuItem_YourNumber.Items.Count > 0)
				{
					(MenuItem_YourNumber.Items[0] as MenuItem).Header = number;
					MenuItem_YourNumber.Visibility = Visibility.Visible;
				}

				NetworkStream networkStream = tcpClientOpponent.GetStream();
				if (!playingOnServer)
				{
					//  одновременный старт
					networkStream.Write(new byte[] { readyPacket }, 0, 1);
				}
				else
				{
					byte[] data = Encoding.Unicode.GetBytes(number);
					networkStream.Write(data, 0, data.Length);
				}

				if (opponentIsReady)
				{
					StartGame();
					if (TURN_MODE)
					{
						opponentTurn();
					}
				}
				else
				{
					WaitWindow waitWindow = new WaitWindow() { Owner = this,
						Title = "Please wait your opponent", Message = "Please wait your opponent"
					};
					waitWindow.ShowDialog();
				}
			}
			else
			{
				SingleGameStart();
			}

		}

		void clearTextboxes()
		{
			textboxNum1.Text = textboxNum2.Text = textboxNum3.Text = textboxNum4.Text = "";
		}

		void SingleGameStart()
		{
			generateNumber();
			StartGame();
		}

		void StartGame()
		{
			playerDataGrid.Items.Clear();
			opponentDataGrid.Items.Clear();
			triesCount = 0;
			clearTextboxes();

			isStarted = true;
			btnStart.Content = "Try";
			startTime = DateTime.Now;

			dispatcherTimer = new DispatcherTimer();
			dispatcherTimer.Tick += DispatcherTimer_Tick;
			dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
			dispatcherTimer.Start();

			showPopup("New game starts now!\nYour task is to guess the number of the opponent!", 3300);
		}

		private void Try()
		{
			if(TURN_MODE && isConnected && myTurn == false)
			{
				return;
			}

			string textboxNumber = getTextboxNumberValue();
			if (!IsStringNumeric(textboxNumber) || textboxNumber.Length < answerNumber.Length)
			{
				showPopup("Number format error", 2500);
				return;
			}
			if(findRepeats(textboxNumber))
			{
				showPopup("Repetition found in the number", 2500);
				return;
			}
			if(playerDataGrid.Items.Count > 0) // check previous number
			{
				if((playerDataGrid.Items[playerDataGrid.Items.Count-1] as Attempt).Number == textboxNumber)
				{
					showPopup("Your previous number is the same", 2500);
					return;
				}
			}

			if(isConnected)
			{
				try
				{
					NetworkStream networkStream = tcpClientOpponent.GetStream();
					byte[] data = Encoding.Unicode.GetBytes(textboxNumber);
					networkStream.Write(data, 0, data.Length);

					data = new byte[256];
					int bytes = 0;
					do
					{
						bytes = networkStream.Read(data, 0, data.Length);
					} while (networkStream.DataAvailable);

					AddAttemptToDataGrid(playerDataGrid, ++triesCount, textboxNumber,
						data[0], data[1]);

					if (data[0] == HiddenNumber.LENGTH) // TODO
					{
						congratilations();
					}
					else if(TURN_MODE)
					{
						opponentTurn();
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.ToString());
				}
			}
			else
			{
				answerNumber.CheckMatches(textboxNumber);
				AddAttemptToDataGrid(playerDataGrid, answerNumber.Attempts, textboxNumber,
					answerNumber.Bulls, answerNumber.Cows);
				
				if (answerNumber.Bulls == answerNumber.Length)
				{
					congratilations();
				}
			}
			focusOnFirst();
		}

		bool myTurn = false;
		void yourTurn()
		{
			this.Dispatcher.Invoke(() =>
			{
				showPopup("Your turn!", 2000);
				myTurn = btnStart.IsEnabled = playerDataGrid.IsEnabled = true;
				opponentDataGrid.IsEnabled = false;
			});
			// TODO turn timer
		}

		void opponentTurn()
		{
			this.Dispatcher.Invoke(() =>
			{
				showPopup("Opponent turn!\nPlease wait...", 2000);
				myTurn = btnStart.IsEnabled = playerDataGrid.IsEnabled = false;
				opponentDataGrid.IsEnabled = true;
			});
		}

		Attempt AddAttemptToDataGrid(DataGrid dataGrid, int attemptNumber, string number, int bulls, int cows)
		{
			Attempt attempt = new Attempt
			{
				Num = attemptNumber,
				Number = number,
				Bulls = bulls,
				Cows = cows
			};
			dataGrid.Items.Add(attempt);
			dataGrid.ScrollIntoView(attempt);
			return attempt;
		}

		void congratilations()
		{
			if (isConnected) StopGameOnline();
			else StopGame();

			string message = "Congratilations!\nYou win!";
			message += "\nAttempts: " + (triesCount > 0 ? triesCount : answerNumber.Attempts);
			message += "\nTime: " + textTimer.Text;
			MessageBox.Show(this, message, "You win!");
		}

		void StopGame()
		{
			isStarted = false;
			btnStart.Content = "Start";
			dispatcherTimer?.Stop();
		}

		string getTextboxNumberValue()
		{
			return textboxNum1.Text + textboxNum2.Text + textboxNum3.Text + textboxNum4.Text;
		}

		private void DispatcherTimer_Tick(object sender, EventArgs e)
		{
			textTimer.Text = (DateTime.Now - startTime).ToString(@"mm\:ss");
		}

		private void generateNumber()
		{
			Random random = new Random();
			char randomed;
			string generatedNum = "";
			for (int i = 0; i < HiddenNumber.LENGTH; i++)
			{
				randomed = random.Next(0, 9).ToString()[0];
				if (i > 0 && generatedNum.Contains(randomed))
					i--;
				else
					generatedNum += randomed;
			}
			answerNumber = new HiddenNumber(generatedNum);
		}

		private void MenuItemExit_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void textbox_TextChanged(object sender, TextChangedEventArgs e)
		{
			TextBox textBox = (sender as TextBox);
			if (textBox.Text != "" && !textBox.Name.Contains('4'))
			{
				myMoveFocus(FocusNavigationDirection.Next);
			}
		}
		void myMoveFocus(FocusNavigationDirection fnd)
		{
			TraversalRequest tRequest = new TraversalRequest(fnd);
			UIElement keyboardFocus = getFocusedControl();

			if (keyboardFocus != null)
			{
				keyboardFocus.MoveFocus(tRequest);
				setTextSelection(keyboardFocus as TextBox);
			}
		}
		UIElement getFocusedControl()
		{
			return Keyboard.FocusedElement as UIElement;
		}

		void focusOnFirst()
		{
			textboxNum1.Focus();
			setTextSelection(textboxNum1);
		}
		void setTextSelection(TextBox control)
		{
			control.SelectAll();
		}

		private void textbox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
		{
			setTextSelection(sender as TextBox);
		}

		private void MenuItemNewGame_Click(object sender, RoutedEventArgs e)
		{
			StopGameOnline();

			DispatcherTimer delay = new DispatcherTimer();
			delay.Interval = TimeSpan.FromMilliseconds(300);
			delay.Tick += (s,ev) => { SingleGameStart(); delay.Stop(); };
			delay.Start();
		}

		private void MenuItemFind_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				StopGameOnline();

				tcpClientOpponent = new TcpClient();
				tcpClientOpponent.Connect(GameIPConfig.ServerIPAddress, GameIPConfig.Port);
				
				byte[] data = new byte[256];
				NetworkStream stream = tcpClientOpponent.GetStream();
				int bytes;

				MenuItemsToggle(false);

				// open waitwindow to wait opponent
				WaitWindow waitWindow = new WaitWindow() { Owner = this, Message = "Searching for an opponent" };
				bool cancelation = true;
				Task.Run(() =>
				{
					try
					{
						do
						{
							bytes = stream.Read(data, 0, data.Length);
						
							if (bytes == 1 && data[0] == readyPacket)
							{
								this.Dispatcher.Invoke(() =>
								{
									cancelation = false;
									waitWindow.Close();
									playingOnServer = true;
									connectionSuccessful();
								});
							}
						} while (stream.DataAvailable);
					}
					catch(Exception ex) { }
				});
				waitWindow.Closing += (sendr, ev) => { if (cancelation) { tcpClientOpponent.Close(); MenuItemsToggle(true); } };
				waitWindow.ShowDialog();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.ToString());
			}
		}

		void showOpponentsUIElements()
		{
			opponentDataGrid.IsEnabled = true;
			IPEndPoint client = (tcpClientOpponent.Client.RemoteEndPoint as IPEndPoint);
			textOnline.Text = playingOnServer ? $"You playing online" :
				$"You playing online with: {client.Address}:{client.Port}";
		}

		void waitForConnect()
		{
			try
			{
				TcpListener server = new TcpListener(IPAddress.Any, GameIPConfig.Port);

				server.Start();

				WaitWindow waitWindow = new WaitWindow() { Owner = this, Message = "Waiting for connection on port " + GameIPConfig.Port };
				Thread task = new Thread(() => {
					while (tcpClientOpponent == null)
					{
						//	Console.WriteLine("Ожидание подключений... ");

						try { tcpClientOpponent = server.AcceptTcpClient(); }
						catch (Exception ex) { break; }
					//	Console.WriteLine("Подключен клиент. Выполнение запроса...");

						this.Dispatcher.Invoke(() => {
							waitWindow.Close();
							connectionSuccessful();
						});
					}
				});
				task.Start();
				waitWindow.Closing += (sender, e) => { server.Stop(); }; 
				waitWindow.ShowDialog();

			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.ToString());
			}
		}

		private void WaitWindow_Closing(object sender, CancelEventArgs e)
		{
			throw new NotImplementedException();
		}

		public bool connectByIp(string IPPort)
		{
			string[] parts = IPPort.Split(':');
			string Ip = parts[0];
			if (!ValidateIPv4(Ip))
			{
				MessageBox.Show(this, "Incorrect IP address - " + Ip, "Error");
				return false;
			}
			string port = parts.Length < 2 ? GameIPConfig.Port.ToString() : parts[1];
			if(!IsStringNumeric(port))
			{
				MessageBox.Show(this, "Incorrect port - " + port, "Error");
				return false;
			}

			try
			{
				tcpClientOpponent = new TcpClient();
				tcpClientOpponent.Connect(Ip, Int32.Parse(port));
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.ToString());
				return false;
			}

			if (tcpClientOpponent.Connected)
			{
				connectionSuccessful();
			}
			
			return true;
		}

		void connectionSuccessful()
		{
			MessageBox.Show(this, "Connection succeeded!\nPlease set number and press start.", "Opponent connected", MessageBoxButton.OK, MessageBoxImage.Information);
			Task.Run(() => listenOpponent());
			showOpponentsUIElements();
			Task.Run(() => checkConnection());
		}

		void opponentReady()
		{
			opponentIsReady = true;
			if (answerNumber != null)
			{
				closeWaitWindows();
				StartGame();
				if (TURN_MODE)
				{
					yourTurn();
				}
			}
		}

		void closeWaitWindows()
		{
			foreach (Window item in Application.Current.Windows)
			{
				if (item is WaitWindow)
					item.Close();
			}
		}

		private void checkConnection()
		{
			while (tcpClientOpponent?.Connected == true)
			{
				Thread.Sleep(200);
			}

			opponentLeftMessage();

			if (!isClosed && isStarted)
			{
				this.Dispatcher.Invoke(() => StopGameOnline());
			}
		}

		private void listenOpponent()
		{
			try
			{
				while (tcpClientOpponent.Connected)
				{
					byte[] data = new byte[256];
					StringBuilder response = new StringBuilder();
					NetworkStream stream = tcpClientOpponent.GetStream();

					bool receivedReady = false;
					int bytes;
					do
					{
						bytes = stream.Read(data, 0, data.Length);
						if (bytes == 0)
							throw new System.IO.IOException("Lost connect");

						if (!opponentIsReady && bytes == 1 && data[0] == readyPacket)
							receivedReady = true;

						if(opponentIsReady)
							response.Append(Encoding.Unicode.GetString(data, 0, bytes));
					} while (stream.DataAvailable);

					if (receivedReady)
					{
						this.Dispatcher.Invoke(() => opponentReady());
						continue;
					}
					if (!opponentIsReady || answerNumber == null)
					{
						continue;
					}

					answerNumber.CheckMatches(response.ToString());

					if (!playingOnServer)
					{
						data = new byte[] { (byte)answerNumber.Bulls, (byte)answerNumber.Cows };
						stream.Write(data, 0, data.Length);
						//		stream.Close();
					}

					if (bytes == 2) // answer first byte = bulls count, second = cows count
					{
						continue;
					}

					this.Dispatcher.Invoke(() => AddAttemptToDataGrid(opponentDataGrid,
						answerNumber.Attempts, response.ToString(),
						answerNumber.Bulls, answerNumber.Cows));

					if (answerNumber.Bulls == HiddenNumber.LENGTH)
					{
						stream.Write(data, 0, data.Length);
						this.Dispatcher.Invoke(() => YouLoseDisconnect());
						break;
					}

					if(TURN_MODE)
					{
						yourTurn();
					}
				}
			}
			catch (System.IO.IOException ex)
			{
				opponentLeftMessage();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
			finally
			{
				this.Dispatcher.Invoke(() => StopGameOnline());
			}
		}

		private void YouLoseDisconnect()
		{
			string message = $"You lose!\nYour opponent guessed your number {answerNumber}!";
			message += "\nAttempts: " + answerNumber.Attempts;
			message += "\nTime: " + textTimer.Text;
			MessageBox.Show(this, message, "You lose!", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		bool leftMsgShown = false;
		void opponentLeftMessage()
		{
			isStarted = false;

			if (!leftMsgShown && isConnected)
			{
				this.Dispatcher.Invoke(() => MessageBox.Show(this, "It seems your opponent left the game.",
					"Connection lost", MessageBoxButton.OK, MessageBoxImage.Information));
				leftMsgShown = true;
				DispatcherTimer shownDisableTimer = new DispatcherTimer();
				shownDisableTimer.Interval = TimeSpan.FromSeconds(3);
				shownDisableTimer.Tick += (s, e) => { leftMsgShown = false; shownDisableTimer.Stop(); };
			}
		}

		void StopGameOnline()
		{
			closeWaitWindows();
			MenuItemsToggle(true);
			playingOnServer = false;
			opponentIsReady = false;
			disableOpponentsUI();
			tcpClientOpponent?.Close();
			tcpClientOpponent = null;
			StopGame();
			if (TURN_MODE)
			{
				btnStart.IsEnabled = myTurn = playerDataGrid.IsEnabled = true;
			}
		}

		void MenuItemsToggle(bool value)
		{
			FindOnlineMenuItem.IsEnabled = ConnectByIPMenuItem.IsEnabled = HostGameMenuItem.IsEnabled = value;
		}

		void disableOpponentsUI()
		{
			textOnline.Text = "";
			opponentDataGrid.IsEnabled = false;
			MenuItem_YourNumber.Visibility = Visibility.Hidden;
		}

		public bool ValidateIPv4(string ipString)
		{
			string pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)";

			return Regex.IsMatch(ipString, pattern, RegexOptions.Singleline);
		}

		private void MenuItemConnectIP_Click(object sender, RoutedEventArgs e)
		{
			StopGameOnline();

			ConnectWindow connectWindow = new ConnectWindow() { Owner = this };
			bool connectResult = false;

			if (connectWindow.ShowDialog() == true)
			{
				connectResult = connectByIp(connectWindow.EnteredIP);
			}
		}

		private void MenuItemWait_Click(object sender, RoutedEventArgs e)
		{
			StopGameOnline();

			waitForConnect();
		}

		DispatcherTimer popupTimer;
		void showPopup(string message, double duration)
		{
			popupInfo.IsOpen = true;
			popupText.Text = message;

			popupTimer?.Stop();
			popupTimer = new DispatcherTimer();
			popupTimer.Interval = TimeSpan.FromMilliseconds(duration);
			popupTimer.Tick += (s, e) => closePopup();
			popupTimer.Start();
		}

		void closePopup()
		{
			popupInfo.IsOpen = false;
			popupTimer?.Stop();
		}

		private void popupInfo_MouseDown(object sender, MouseButtonEventArgs e)
		{
			closePopup();
		}
	}
}
