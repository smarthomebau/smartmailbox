using System;
using System.Collections.Generic;
using System.Text;
using Tinkerforge;
using System.Net;
using System.Threading;

namespace SmartMailBox
{
	class Program
	{
		private static string HOST = "localhost";
		private static int PORT = 4223;
		private static string UID = "jB1"; // Change to your UID!

		private static readonly byte[] POST = { 0x73, 0x3f, 0x6d, 0x78 };
		private static readonly byte[] OFF = { 0x0, 0x0, 0x0, 0x0, };
		private static readonly byte[] LOCK = { 0x3f, 0x3f, 0x3f, 0x3f, };

		private const string FHEMADDRESS = "http://192.168.0.127:8083";
		private const int lockTimeSeconds = 5;

		private static bool lockable = true;

		private static BrickletDistanceUS dus;

		private enum State { off, on, locked };
		private static State _state;
		private static State CurrentState {
			get 
			{
				return _state; 
			}
			set 
			{
				if (_state != value) 
				{
					_state = value;
					//System.Console.WriteLine(DateTime.Now + ": New State:" + value.ToString());
					System.Threading.Tasks.Task t = System.Threading.Tasks.Task.Factory.StartNew(Notify);
					t.Wait();
					t.Dispose();
				}
			} 
		}


		static private void RunLockCountDown(int lockSeconds)
		{
			if (lockable == true)
			{
				CurrentState = State.locked;
				dus.SetDistanceCallbackThreshold('x', 0, 0);
				Segment4x7.WriteSegments(LOCK);
				var stopTime = System.DateTime.Now.AddSeconds(lockSeconds);
				while (System.DateTime.Now <= stopTime)
				{
					//System.Console.WriteLine(DateTime.Now + ": Lock!");
					System.Threading.Thread.Sleep(1000);
				}
				Segment4x7.WriteSegments(OFF);
				//System.Console.WriteLine(DateTime.Now + ": Unlocked!");
				lockable = false;
				dus.SetDistanceCallbackThreshold('o', 0, 1);
				CurrentState = State.off;
			}
		}


		// Callback for distance changes
		static void ReachedCB(BrickletDistanceUS sender, int distance)
		{
			if (CurrentState != State.locked)
			{
				if (distance < 550 || distance > 630)
				{
					//System.Console.WriteLine(DateTime.Now + ": Distance value out of range: " + distance);
					Segment4x7.WriteSegments(POST);
					CurrentState = State.on;
				}
				else
				{
					Segment4x7.WriteSegments(OFF);
					CurrentState = State.off;
				}
			}
		}

		// notify home server
		static void Notify()
		{
			switch (CurrentState)
			{
				case State.off:
					SendFhemCommand("SetSmartMailBoxOff();;");
					RunLockCountDown(lockTimeSeconds);
					break;
				case State.on:
					SendFhemCommand("SetSmartMailBoxOn();;");
					lockable = true;
					break;
				case State.locked:
					SendFhemCommand("SetSmartMailBoxLocked();;");
					break;
			}
		}

		// send command to FHEM
		static void SendFhemCommand(string fhemCommand)
		{
			//System.Console.WriteLine(DateTime.Now + ": Notifying...");
			var request = (HttpWebRequest)WebRequest.Create(FHEMADDRESS + "/fhem?cmd={" + fhemCommand + "}");
			request.Timeout = 5000;
			WebResponse response;
			try
			{
				response = request.GetResponse();
				response.Close();
				request.Abort();
				//System.Console.WriteLine(DateTime.Now + ": Notified!");
			}
			catch (Exception ex)
			{
				System.Console.WriteLine(DateTime.Now + ": Error - " + fhemCommand + " - " + ex.ToString());
			}
			finally
			{
				request = null;
				response = null;
			}
		}

		static void Main()
		{
			System.Console.WriteLine(DateTime.Now + ": Starting... ");
			IPConnection ipcon = new IPConnection(); // Create IP connection
			dus = new BrickletDistanceUS(UID, ipcon); // Create device object

			Segment4x7.WriteSegments(OFF);

			ipcon.Connect(HOST, PORT); // Connect to brickd
			// Don't use device before ipcon is connected

			// Get threshold callbacks with a debounce time of 1 second (1000ms)
			dus.SetDebouncePeriod(1000);

			// Register threshold reached callback to function ReachedCB
			dus.DistanceReached += ReachedCB;

			// Configure threshold 
			dus.SetDistanceCallbackThreshold('o', 0, 1);

			// Setup and start timer...
			var mre = new ManualResetEvent(false);

			// allow the code to exit from the command line:
			ThreadPool.QueueUserWorkItem((state) =>
			{
				//Console.WriteLine("Press (x) to exit");
				while (true)
				{
					var key = Console.ReadKey();
					if (key.Key == ConsoleKey.X)
					{
						mre.Set(); // This will let the main thread exit
						break;
					}
				}
			});

			System.Console.WriteLine(DateTime.Now + ": Running... (hit 'x' to exit)");

			// The main thread can just wait on the wait handle, which basically puts it into a "sleep" state, and blocks it forever
			mre.WaitOne();

			System.Console.WriteLine();
			System.Console.WriteLine(DateTime.Now + ": Exiting!");
			ipcon.Disconnect();

			Segment4x7.WriteSegments(OFF);
		}
	}
}
