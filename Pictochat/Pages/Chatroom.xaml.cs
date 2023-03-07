﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Pictochat.Extensions;
using Pictochat.Models;
using Pictochat.PageModels;
using Pictochat.Services;
using Pictochat.Views;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using MessageBox = Pictochat.Controls.MessageBox;

namespace Pictochat.Pages;

public partial class Chatroom
{
    public static Chatroom Instance;
    private PictochatUser User;
    private bool IsFadingOut;

    private static readonly ECommandType[] VisibleCommands =
    {
        ECommandType.MessageText,
        ECommandType.MessageImage,
        ECommandType.EventJoin,
        ECommandType.EventLeave,
        ECommandType.EventRename
    };
    
    public Chatroom(ERoom roomType)
    {
        InitializeComponent();
        Instance = this;
        DataContext = new ChatroomPageModel();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
        BeginAnimation(OpacityProperty, fadeIn);

        User = PictochatService.Get(roomType);
        User.Received += UserOnReceived;
        
        User.Join();
        
        RoomIdentifier.Source = new BitmapImage(new Uri($"pack://application:,,,/Resources/{roomType.ToString()}/Room.png"));
        
        MainView.Instance.KeyDown += OnKeyDown;
    }

    private void UserOnReceived(PictochatUser sender, PictochatReceiveData args)
    {
        if (VisibleCommands.Contains(args.Command))
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Items.Add(new MessageBox(args));
                MessagesScroll.ScrollToBottom();
            });
        }
    }

    private void SendMessage()
    {
        var input = InputText.Text;
        
        if (input.StartsWith("/clear", StringComparison.OrdinalIgnoreCase))
        {
            Messages.Items.Clear();
            Refresh();
            return;
        }

        if (input.StartsWith("/rename", StringComparison.OrdinalIgnoreCase))
        {
            var name = input[8..];
            User.Send(ECommandType.EventRename, name);
            Globals.UserName = name;
            Refresh();
            return;
        }
        
        if (input.StartsWith("/connected", StringComparison.OrdinalIgnoreCase))
        {
            var users = User.Peers.Count == 1 ? "user" : "users";
            var are = User.Peers.Count == 1 ? "is" : "are";
            Messages.Items.Add(new MessageBox($"There {are} {User.Peers.Count} {users} connected.", User.Peers.CommaJoin()));
            Refresh();
            return;
        }
        
        User.Send(ECommandType.MessageText, input);
        Refresh();
    }

    private void Refresh()
    {
        InputText.Text = string.Empty;
        MessagesScroll.ScrollToBottom();
    }
    
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                SendMessage();
                return;
            case Key.Back:
                if (InputText.Text.Length == 0) break;
                InputText.Text = InputText.Text[..^1];
                return;
            default:
                InputText.Text += Keyboard.GetCharFromKey(e.Key).ToString() ?? string.Empty;
                break;
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        DragDropBox.Visibility = Visibility.Visible;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DragDropBox.Visibility = Visibility.Collapsed;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DragDropBox.Visibility = Visibility.Collapsed;
        
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] fileNames) return;

        foreach (var file in fileNames)
        {
            var image = Image.Load(file);
            var widthRatio = image.Width / image.Height;
            var heightRatio = image.Height / image.Width;
            image.Mutate(x => x.Resize(widthRatio * Math.Min(image.Width, 128), heightRatio * Math.Min(image.Height, 128)));
            
            User.Send(ECommandType.MessageImage, image);
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsFadingOut) return;
        IsFadingOut = true;
        
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
        fadeOut.Completed += (o, args) =>
        {
            MainView.Instance.KeyDown -= OnKeyDown;
            User.Received -= UserOnReceived;
            User.Leave();
            AppService.MainVM.ActivePage = new Lobby();
        };
        
        AppService.MainVM.ActivePage.BeginAnimation(OpacityProperty, fadeOut);
    }
}