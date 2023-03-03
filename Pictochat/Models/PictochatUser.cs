﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Ionic.Zlib;
using Pictochat.IO;
using Pictochat.Services;

namespace Pictochat.Models;

public class PictochatUser
{
    public int Port;
    public bool InRoom;
    public List<string> Peers = new();
    public event EventHandler<PictochatUser, PictochatReceiveData> Received;

    private UdpClient Broadcaster = new();
    private UdpClient Receiver = new();
    
    private IPEndPoint BroadcastEndpoint;
    private IPEndPoint ReceiverEndpoint;

    public PictochatUser(int port, IPAddress? ipAddress = null)
    {
        Port = port;
        BroadcastEndpoint = new IPEndPoint(ipAddress ?? IPAddress.Broadcast, port);
        ReceiverEndpoint = new IPEndPoint(IPAddress.Any, port);
        
        Broadcaster.Connect(BroadcastEndpoint);
        
        Receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        Receiver.ExclusiveAddressUse = false;
        Receiver.Client.Bind(ReceiverEndpoint);
        
        ThreadService.QueueInfinite(() =>
        {
            var data = Receive();
            Received?.Invoke(this, data);
        });
    }

    public void Join()
    {
        Send(ECommandType.EventJoin);
        InRoom = true;
    }
    
    public void Leave()
    {
        Send(ECommandType.EventLeave);
        InRoom = false;
    }

    public void Send(ECommandType command, object? data = null)
    {
        using var Ar = new BufferWriter();
        Ar.Write((byte) command);
        Ar.WriteArray(GetIP());
        Ar.WriteFString(Environment.UserName);

        switch (command)
        {
            case ECommandType.MessageText:
            {
                Ar.WriteFString((string?) data ?? string.Empty);
                break;
            }
        }
        
        var compressed = GZipStream.CompressBuffer(Ar.GetBuffer());
        Broadcaster.Send(compressed);
    }
    
    public PictochatReceiveData Receive()
    {
        var data = Receiver.Receive(ref ReceiverEndpoint);
        var uncompressed = GZipStream.UncompressBuffer(data)!;

        var Ar = new BufferReader(uncompressed);
        
        var command = (ECommandType) Ar.ReadByte();
        var ip = new IPAddress(Ar.ReadArray<byte>());
        var name = Ar.ReadFString();


        var receiveData = new PictochatReceiveData(command, ip, name);
        switch (command)
        {
            case ECommandType.MessageText:
            {
                receiveData.Data = Ar.ReadFString();
                break;
            }
        }

        return receiveData;
    }
    
    private static byte[] GetIP()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.GetAddressBytes();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    public void Dispose()
    {
        Broadcaster.Dispose();
        Receiver.Dispose();
    }
}