using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class Client : Form
    {
        private bool connected = false;
        private Thread client = null;
        private struct MyClient
        {
            public TcpClient client;
            public NetworkStream stream;
            public byte[] buffer;
            public StringBuilder data;
            public EventWaitHandle handle;
        };
        private MyClient obj;
        private Task send = null;
        private bool exit = false;

        public Client()
        {
            InitializeComponent();
        }

        private void Log(string msg = "") // clear the log if message is not supplied or is empty
        {
            if (!exit)
            {
                logTextBox.Invoke((MethodInvoker)delegate
                {
                    if (msg.Length > 0)
                    {
                        logTextBox.AppendText(string.Format("[ {0} ] {1}{2}", DateTime.Now.ToString("HH:mm"), msg, Environment.NewLine));
                    }
                    else
                    {
                        logTextBox.Clear();
                    }
                });
            }
        }

        private void Connected(bool status)
        {
            if (!exit)
            {
                connectButton.Invoke((MethodInvoker)delegate
                {
                    connected = status;
                    if (status)
                    {
                        nameTextBox.Enabled = false;
                        addrMaskedTextBox.Enabled = false;
                        portTextBox.Enabled = false;
                        connectButton.Text = "Disconnect";
                        Log("SYSTEM: You are now connected");
                    }
                    else
                    {
                        nameTextBox.Enabled = true;
                        addrMaskedTextBox.Enabled = true;
                        portTextBox.Enabled = true;
                        connectButton.Text = "Connect";
                        Log("SYSTEM: You are now disconnected");
                    }
                });
            }
        }

        private void Read(IAsyncResult result)
        {
            int bytes = 0;
            if (obj.client.Connected)
            {
                try
                {
                    bytes = obj.stream.EndRead(result);
                }
                catch (Exception ex)
                {
                    Log(string.Format("ERROR: {0}", ex.Message));
                }
            }
            if (bytes > 0)
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));
                try
                {
                    if (obj.stream.DataAvailable)
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), obj);
                    }
                    else
                    {
                        Log(obj.data.ToString());
                        obj.data.Clear();
                        obj.handle.Set();
                    }
                }
                catch (Exception ex)
                {
                    obj.data.Clear();
                    Log(string.Format("ERROR: {0}", ex.Message));
                    obj.handle.Set();
                }
            }
            else
            {
                obj.client.Close();
                obj.handle.Set();
            }
        }

        private void Connection(IPAddress addr, int port)
        {
            try
            {
                obj = new MyClient();
                obj.client = new TcpClient();
                obj.client.Connect(addr, port);
                obj.stream = obj.client.GetStream();
                obj.buffer = new byte[obj.client.ReceiveBufferSize];
                obj.data = new StringBuilder();
                obj.handle = new EventWaitHandle(false, EventResetMode.AutoReset);
                Connected(true);
                while (obj.client.Connected)
                {
                    try
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), null);
                        obj.handle.WaitOne();
                    }
                    catch (Exception ex)
                    {
                        Log(string.Format("ERROR: {0}", ex.Message));
                    }
                }
                obj.client.Close();
                Connected(false);
            }
            catch (Exception ex)
            {
                Log(string.Format("ERROR: {0}", ex.Message));
            }
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                obj.client.Close();
            }
            else if (client == null || !client.IsAlive)
            {
                bool addrResult = IPAddress.TryParse(addrMaskedTextBox.Text, out IPAddress addr);
                if (!addrResult)
                {
                    Log("SYSTEM: IP address is not valid");
                }
                bool portResult = int.TryParse(portTextBox.Text, out int port);
                if (!portResult)
                {
                    Log("SYSTEM: Port number is not valid");
                }
                else if (port < 0 || port > 65535)
                {
                    portResult = false;
                    Log("SYSTEM: Port number is out of range");
                }
                if (addrResult && portResult)
                {
                    client = new Thread(() => Connection(addr, port))
                    {
                        IsBackground = true
                    };
                    client.Start();
                }
            }
        }

        private void Write(IAsyncResult result)
        {
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.EndWrite(result);
                }
                catch (Exception ex)
                {
                    Log(string.Format("ERROR: {0}", ex.Message));
                }
            }
        }

        private void BeginWrite(string msg)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(Write), null);
                }
                catch (Exception ex)
                {
                    Log(string.Format("ERROR: {0}", ex.Message));
                }
            }
        }

        private void Send(string msg)
        {
            if (send == null || send.IsCompleted)
            {
                send = Task.Factory.StartNew(() => BeginWrite(msg));
            }
            else
            {
                send.ContinueWith(antecendent => BeginWrite(msg));
            }
        }

        private void SendTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (sendTextBox.Text.Length > 0)
                {
                    string msg = sendTextBox.Text;
                    sendTextBox.Clear();
                    Log("You: " + msg);
                    if (connected)
                    {
                        Send(msg);
                    }
                }
            }
        }

        private void Client_FormClosing(object sender, FormClosingEventArgs e)
        {
            exit = true;
            if (connected)
            {
                obj.client.Close();
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            Log();
        }
    }
}
