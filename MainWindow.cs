using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Gtk;
using SimpleChatAppLibrary;
using Application = Gtk.Application;
using UI = Gtk.Builder.ObjectAttribute;
using WrapMode = Pango.WrapMode;

namespace ChatAppClient; 

class MainWindow : Window {

    private const string TitleStart = "Chat App Client";
    
    [UI] private Entry serverIPEntry = null;
    [UI] private Entry channelNameEntry = null;
    [UI] private Entry usernameEntry = null;
    [UI] private Button connectButton = null;
    [UI] private Box messagesBox = null;
    [UI] private Box onlineUsersBox = null;
    [UI] private Entry messageEntry = null;
    [UI] private Button sendButton = null;

    private SimpleChatAppClient client;
    private bool connected = false;
    private bool continueUpdateThread = true;
    private Thread updateThread;
    private List<SimpleChatAppMessage> messages = new List<SimpleChatAppMessage>();
    private Dictionary<string, Widget> greyMessages = new Dictionary<string, Widget>();

    public MainWindow() : this(new Builder("MainWindow.glade")) { }

    private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow")) {
        builder.Autoconnect(this);

        DeleteEvent += Window_DeleteEvent;
        connectButton.Clicked += ConnectPressed;
        messageEntry.Activated += (_,_) => {SendMessagePressed();};
        sendButton.Clicked += (_,_) => {SendMessagePressed();};
        
        serverIPEntry.Text = Prefs.GetString("serverIP", "");
        usernameEntry.Text = Prefs.GetString("username", "");
        channelNameEntry.Text = Prefs.GetString("channel", "");

        Application.Invoke(delegate {
            Window.Title = TitleStart;
            Window.Resize(800, 450);
        });
    }

    private void Window_DeleteEvent(object sender, DeleteEventArgs a) {
        continueUpdateThread = false;
        Application.Quit();
    }

    private void SendMessagePressed() {
        if (!connected) return;

        new Thread(() => {
            string message = Commands.Emoticons(messageEntry.Text);
            client.SendMessage(message);
            messageEntry.Text = "";

            Application.Invoke(delegate {
                CreateMessageLabel(client.Name, message, DateTime.Now, true);

                new Thread(() => {
                    Thread.Sleep(5);
                    Application.Invoke(delegate { ScrollToBottom(); });      
                }).Start();
            });
        }).Start();
    }
    
    private void ConnectPressed(object sender, EventArgs e) {
        connectButton.Label = "Connecting...";
        
        greyMessages.Clear();
        
        ClearBox(messagesBox);
        ClearBox(onlineUsersBox);
        
        // Stop old Thread
        continueUpdateThread = false;
        while (updateThread is { IsAlive: true }) Thread.Sleep(50);

        // Start Thread
        continueUpdateThread = true;
        updateThread = new Thread(MessageUpdateThread);
        updateThread.Start();
    }
    
    private void ClearBox(Box box) {
        foreach (Widget child in box.Children)
            box.Remove(child);
    }

    private void ScrollToBottom() {
        Adjustment adjustment = ((ScrolledWindow)messagesBox.Parent.Parent).Vadjustment;
        adjustment.Value = adjustment.Upper;
    }

    private void CreateMessageLabel(string creator, string message, DateTime time, bool grey = false) {
        Label label = new Label("");
        label.Justify = Justification.Left;
        label.Xpad = 10;
        label.Ypad = 10;
        label.Markup = $"<b>{creator}</b> - <small>{time}</small>\n{message}";
        label.UseMarkup = true;
        label.Xalign = 0;
        label.LineWrap = true;
        label.LineWrapMode = WrapMode.WordChar;
        if (grey) {
            label.Opacity = 0.7;
            greyMessages.Add(message, label);
        }   
        
        messagesBox.Add(label);
        label.Show();
    }
    
    private void MessageUpdateThread() {
        Application.Invoke(delegate {  
            string ip = serverIPEntry.Text;
            if (string.IsNullOrEmpty(ip)) {
                ip = "https://chat.zaneharrison.com";
            }
        
            Prefs.SetString("username", usernameEntry.Text);
            Prefs.SetString("channel", channelNameEntry.Text);
            Prefs.SetString("serverIP", ip);
            Prefs.Save();

            client = new SimpleChatAppClient(ip, usernameEntry.Text, channelNameEntry.Text);
            if (!client.TestConnection()) {
                connectButton.Label = "Connection Failed.";
                return;
            }

            connectButton.Label = "Reconnect";
            messageEntry.Sensitive = true;
            sendButton.Sensitive = true;
            
            connected = true;
            Console.WriteLine("Connected!");

            Window.Title = TitleStart + " - " + client.Channel;
        });
        
        while (!connected) { Thread.Sleep(16); }

        bool firstTime = true;

        while (continueUpdateThread) {
            List<SimpleChatAppMessage> newMessages = new List<SimpleChatAppMessage>();
            {
                IEnumerable<SimpleChatAppMessage> receivedMessages = client.GetMessages(24);

                foreach (SimpleChatAppMessage message in receivedMessages) {
                    if (messages.Any(eMessage => eMessage.messageId == message.messageId)) continue;
                    
                    messages.Add(message);
                    newMessages.Add(message);
                }
            }

            string[] users = client.GetOnlineUsers().ToArray();
            Array.Sort(users);
            
            Application.Invoke(delegate {
                ClearBox(onlineUsersBox);
                
                foreach (SimpleChatAppMessage message in newMessages) {
                    if (message.creatorName == client.Name) {
                        //messages.Find(eMessage => eMessage.messageId == message.messageId);
                        if (greyMessages.TryGetValue(message.text, out Widget label)) {
                            label.Opacity = 1d;
                            greyMessages.Remove(message.text);
                            continue;
                        }
                    }
                    
                    CreateMessageLabel(message.creatorName, message.text, 
                        DateTime.FromBinary(message.createdAt).ToLocalTime());
                }
                
                foreach (string user in users) {
                    Label label = new Label(user);
                    label.Justify = Justification.Left;
                    label.Xpad = 10;
                    label.Ypad = 10;
                    label.Xalign = 0;
                    label.LineWrap = true;
                    label.LineWrapMode = WrapMode.WordChar;
                    
                    onlineUsersBox.Add(label);
                    label.Show();
                }
            });

            if (firstTime) {
                Thread.Sleep(100);
                
                Application.Invoke(delegate {
                    ScrollToBottom();
                });
                
                firstTime = false;
            }

            for (int i = 0; i < 5 && continueUpdateThread; i++) {
                Thread.Sleep(100);
            }
        }
        
        Application.Invoke(delegate {
            connectButton.Label = "Connect";
            messageEntry.Sensitive = false;
            sendButton.Sensitive = false;
            connected = false;
        });
    }
}
