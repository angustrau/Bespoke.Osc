using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Bespoke.Common.Net;

namespace Bespoke.Common.Osc
{
    /// <summary>
    /// Represents a TCP/IP client-side connection.
    /// </summary>
    public class OscClient
    {
        #region Events

        /// <summary>
        /// Raised when an OscPacket is received.
        /// </summary>
        public event EventHandler<OscPacketReceivedEventArgs> PacketReceived;

        /// <summary>
        /// Raised when an OscBundle is received.
        /// </summary>
        public event EventHandler<OscBundleReceivedEventArgs> BundleReceived;

        /// <summary>
        /// Raised when an OscMessage is received.
        /// </summary>
        public event EventHandler<OscMessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Raised when an error occurs during the reception of a packet.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> ReceiveErrored;

        #endregion

        /// <summary>
        /// Gets the IP address of the server-side of the connection.
        /// </summary>
        public IPAddress RemoteIPAddress { get; private set; }        

        /// <summary>
        /// Gets the port of the server-side of the connection.
        /// </summary>
        public int RemotePort { get; private set; }        

        /// <summary>
        /// Gets the underlying <see cref="TcpConnection"/>.
        /// </summary>
        public TcpConnection Connection { get; private set; }

		/// <summary>
		/// Gets ths connected status of the underlying Tcp socket.
		/// </summary>
		public bool IsConnected
		{
			get
			{
				return (Connection != null ? Connection.Client.Connected : false);
			}
		}

        /// <summary>
		/// Gets all registered Osc methods (address patterns).
		/// </summary>
		public string[] RegisteredMethods
        {
            get
            {
                return mRegisteredMethods.ToArray();
            }
        }

        /// <summary>
        /// Specifies if incoming Osc messages should be filtered against the registered methods.
        /// </summary>
        public bool FilterRegisteredMethods { get; set; }

        /// <summary>
        /// Gets or sets the handling of parsing exceptions.
        /// </summary>
        public bool ConsumeParsingExceptions { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OscClient"/> class.
        /// </summary>
        public OscClient()
        {
        }

		/// <summary>
		/// Initializes a new instance of the <see cref="OscClient"/> class.
		/// </summary>
		/// <param name="connection">The <see cref="TcpConnection"/> object associated with this instance.</param>
		public OscClient(TcpConnection connection)
		{
			Connection = connection;
		}

        /// <summary>
        /// Initializes a new instance of the <see cref="OscClient"/> class.
        /// </summary>
        /// <param name="serverEndPoint">The server-side endpoint of the connection.</param>
        public OscClient(IPEndPoint serverEndPoint)
            : this(serverEndPoint.Address, serverEndPoint.Port)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OscClient"/> class.
        /// </summary>
        /// <param name="serverIPAddress">The server-side IP address of the connection.</param>
        /// <param name="serverPort">The server-side port of the connection.</param>
        /// <param name="consumeParsingExceptions">Specifies the behavior of handling parsing exceptions.</param>
        public OscClient(IPAddress serverIPAddress, int serverPort, bool consumeParsingExceptions = true)
            : this()
        {
            RemoteIPAddress = serverIPAddress;
            RemotePort = serverPort;

            // Set up listener
            mRegisteredMethods = new List<string>();
            FilterRegisteredMethods = true;
            ConsumeParsingExceptions = consumeParsingExceptions;
        }

        /// <summary>
        /// Connect to the previously specified server-side endpoint.
        /// </summary>
        public void Connect()
        {
            Connect(RemoteIPAddress, RemotePort);
        }

        /// <summary>
        /// Connect to the previously specified server-side endpoint.
        /// </summary>
        /// <param name="serverEndPoint">The server-side endpoint to connect to.</param>
        public void Connect(IPEndPoint serverEndPoint)
        {
            Connect(serverEndPoint.Address, serverEndPoint.Port);
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="serverIPAddress">The server-side IP address to connect to.</param>
        /// <param name="serverPort">The server-side port to connect to.</param>
        public void Connect(IPAddress serverIPAddress, int serverPort)
        {
            RemoteIPAddress = serverIPAddress;
            RemotePort = serverPort;

			if (Connection == null)
			{
				TcpClient client = new TcpClient();
				client.Connect(RemoteIPAddress, RemotePort);
				Connection = new TcpConnection(client.Client, OscPacket.LittleEndianByteOrder);
			}

            Connection.DataReceived += mTcpServer_DataReceived;
            Connection.ReceiveDataAsync();
            mHandleMessages = true;
        }

        /// <summary>
        /// Close the connection.
        /// </summary>
        public void Close()
        {
			if (Connection != null)
			{
				Connection.Dispose();
				Connection = null;
			}

            mHandleMessages = false;
        }

        /// <summary>
        /// Send an OscPacket over the connection.
        /// </summary>
        /// <param name="packet">The <see cref="OscPacket"/> to send.</param>
        public void Send(OscPacket packet)
        {
            byte[] packetData = packet.ToByteArray();
			Connection.Writer.Write(OscPacket.ValueToByteArray(packetData));
        }

        /// <summary>
		/// Register an Osc method.
		/// </summary>
		/// <param name="method">The Osc address pattern to register.</param>
		public void RegisterMethod(string method)
        {
            if (mRegisteredMethods.Contains(method) == false)
            {
                mRegisteredMethods.Add(method);
            }
        }

        /// <summary>
        /// Unregister an Osc method.
        /// </summary>
        /// <param name="method">The Osc address pattern to unregister.</param>
        public void UnRegisterMethod(string method)
        {
            mRegisteredMethods.Remove(method);
        }

        /// <summary>
        /// Unregister all Osc methods.
        /// </summary>
        public void ClearMethods()
        {
            mRegisteredMethods.Clear();
        }

        #region Private Methods

        /// <summary>
        /// Process data received events.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">An EventArgs object that contains the event data.</param>
        private void mTcpServer_DataReceived(object sender, TcpDataReceivedEventArgs e)
        {
            DataReceived(e.Connection, (IPEndPoint)e.Connection.Client.RemoteEndPoint, e.Data);
        }

        /// <summary>
        /// Process the data received event.
        /// </summary>
		/// <param name="connection">The <see cref="TcpConnection" /> object associated with this data.</param>
        /// <param name="sourceEndPoint">The source endpoint.</param>
        /// <param name="data">The received data.</param>
        private void DataReceived(TcpConnection connection, IPEndPoint sourceEndPoint, byte[] data)
        {
            if (mHandleMessages)
            {
                try
                {
                    OscPacket packet = OscPacket.FromByteArray(sourceEndPoint, data);
                    packet.Client = this;

                    OnPacketReceived(packet);

                    if (packet.IsBundle)
                    {
                        OnBundleReceived(packet as OscBundle);
                    }
                    else
                    {
                        if (FilterRegisteredMethods)
                        {
                            if (mRegisteredMethods.Contains(packet.Address))
                            {
                                OnMessageReceived(packet as OscMessage);
                            }
                        }
                        else
                        {
                            OnMessageReceived(packet as OscMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ConsumeParsingExceptions == false)
                    {
                        OnReceiveErrored(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Raises the <see cref="PacketReceived"/> event.
        /// </summary>
        /// <param name="packet">The packet to include in the event arguments.</param>
        private void OnPacketReceived(OscPacket packet)
        {
            if (PacketReceived != null)
            {
                PacketReceived(this, new OscPacketReceivedEventArgs(packet));
            }
        }

        /// <summary>
        /// Raises the <see cref="BundleReceived"/> event.
        /// </summary>
        /// <param name="bundle">The packet to include in the event arguments.</param>
        private void OnBundleReceived(OscBundle bundle)
        {
            if (BundleReceived != null)
            {
                BundleReceived(this, new OscBundleReceivedEventArgs(bundle));
            }

            foreach (object value in bundle.Data)
            {
                if (value is OscBundle)
                {
                    // Raise events for nested bundles
                    OnBundleReceived((OscBundle)value);
                }
                else if (value is OscMessage)
                {
                    // Raised events for contained messages
                    OscMessage message = (OscMessage)value;
                    if (FilterRegisteredMethods)
                    {
                        if (mRegisteredMethods.Contains(message.Address))
                        {
                            OnMessageReceived(message);
                        }
                    }
                    else
                    {
                        OnMessageReceived(message);
                    }
                }
            }
        }

        /// <summary>
        /// Raises the <see cref="MessageReceived"/> event.
        /// </summary>
        /// <param name="message">The message to include in the event arguments.</param>
        private void OnMessageReceived(OscMessage message)
        {
            if (MessageReceived != null)
            {
                MessageReceived(this, new OscMessageReceivedEventArgs(message));
            }
        }

        /// <summary>
        /// Raises the <see cref="ReceiveErrored"/> event.
        /// </summary>
        /// <param name="ex">The associated exception.</param>
        private void OnReceiveErrored(Exception ex)
        {
            if (ReceiveErrored != null)
            {
                ReceiveErrored(this, new ExceptionEventArgs(ex));
            }
        }

        #endregion Private Methods

        private List<string> mRegisteredMethods;
        private volatile bool mHandleMessages;
    }
}
