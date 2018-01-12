﻿using GPNetworkMessage;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace GPNetworkClient
{
    public class UDPClient : IClient
    {
        private UdpClient client;
        private IPEndPoint ep;
        private Thread thread;

        private int clientAmount = 1;
        private int clientID = 0;
        private bool isConnected = false;
        private Queue<AMessage> messageQueue = new Queue<AMessage>();

        public UDPClient() { }

        public bool Connect(string hostname, int port, string message = "Client has connected")
        {
            try
            {
                ep = new IPEndPoint(IPAddress.Parse(hostname), port);

                client = new UdpClient();
                client.Client.ReceiveTimeout = TimeSpan.FromSeconds(10).Seconds;
                client.Connect(ep);

                SendMessage(MessageType.JOIN, message); //Send Joining Message

                AMessage recieveMessage = ReceiveMessage(); //Recieve response with client number

                if (recieveMessage != null)
                {
                    if (recieveMessage.Type == MessageType.JOIN)
                    {
                        clientID = recieveMessage.ID;
                    }
                }
                else
                {
                    client.Close();
                    return false;
                }

                thread = new Thread(MessageReceiver);
                thread.Start();

                isConnected = true;
            }
            catch (Exception e)
            {
                messageQueue.Enqueue(new AMessage(MessageType.ERROR, clientID, e.Message + " : " + e.TargetSite));
                if (client != null) client.Close();
                if (thread != null) thread.Abort();
                if (thread != null) thread.Join();
                return false;
            }

            return true;
        }

        public void Disconnect(string message = "Client Disconnected")
        {
            SendMessage(MessageType.LEAVE, message);

            isConnected = false;

            if (thread != null)
            {
                thread.Abort();
                thread.Join();
            }

            if (client != null)
            {
                client.Client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }

        public AMessage ReceiveMessage()
        {
            try
            {
                AMessage recievedMessage = new AMessage(MessageType.ANY, 0, "");

                IPEndPoint endpoint = new IPEndPoint(IPAddress.Any,0);
                byte[] recievedData = client.Receive(ref endpoint);

                string message = Encoding.ASCII.GetString(recievedData);
                string[] recievedString = message.Split(';');

                recievedMessage.ID = Convert.ToInt32(recievedString[0]);
                recievedMessage.Type = (MessageType)Enum.Parse(typeof(MessageType), recievedString[1]);
                recievedMessage.Message = message.Substring((recievedString[0] + ";" + recievedString[1] + ";").Length);
                return recievedMessage;
            }
            catch (Exception e)
            {
                messageQueue.Enqueue(new AMessage(MessageType.ERROR, clientID, "Faulty Message Recieved, Message Skipped. " + e.Message + " : " + e.TargetSite));
                return null;
            }
        }

        public void MessageReceiver()
        {
            try
            {
                while (isConnected)
                {
                    if (client.Available != 0)
                    {
                        AMessage recieveMessage = ReceiveMessage();

                        if (recieveMessage != null)
                        {
                            if (recieveMessage.Type == MessageType.CLIENTCOUNT) 
                                clientAmount = Convert.ToInt32(recieveMessage.Message);
                            messageQueue.Enqueue(recieveMessage);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                messageQueue.Enqueue(new AMessage(MessageType.ERROR, clientID, e.Message + ": " + e.TargetSite));
            }
        }

        public void SendMessage(MessageType type, string data)
        {
            if (data.Length > 0 && client != null)
            {
                string messageString = clientID + ";" + type.ToString() + ";" + data;
                Byte[] sendData = Encoding.ASCII.GetBytes(messageString);
                client.Send(sendData, sendData.Length);
            }
        }

        public void SendMessageExceptOne(string data, int ClientID)
        {
            if (data.Length > 0 && client != null)
            {
                string messageString = ClientID + ";" + MessageType.INFOEXCEPT1.ToString() + ";" + ClientID + ";" + data;
                Byte[] sendData = Encoding.ASCII.GetBytes(messageString);
                client.Send(sendData, sendData.Length);
            }
        }

        public void SendMessageToOne(string data, int ClientID)
        {
            if (data.Length > 0 && client != null)
            {
                string messageString = ClientID + ";" + MessageType.INFOTO1.ToString() + ";" + ClientID + ";" + data;
                Byte[] sendData = Encoding.ASCII.GetBytes(messageString);
                client.Send(sendData, sendData.Length);
            }
        }

        public int ClientID
        {
            get { return clientID; }
        }

        public int ClientAmount
        {
            get { return clientAmount; }
        }

        public bool IsConnected
        {
            get { return isConnected; }
        }

        public Queue<AMessage> Messages
        {
            get { return messageQueue; }
        }
    }
}