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
    private const string CheckMark = "<span foreground=\"green\" style=\"italic\" size=\"larger\">✓</span>";
    
    [UI] private Entry serverIPEntry = null;
    [UI] private Entry channelNameEntry = null;
    [UI] private Entry usernameEntry = null;
    [UI] private Button connectButton = null;
    [UI] private ScrolledWindow messageScrollWindow = null;
    [UI] private Box messagesBox = null;
    [UI] private Box onlineUsersBox = null;
    [UI] private Entry messageEntry = null;
    [UI] private Button sendButton = null;
    [UI] private Menu messageContextMenu = null;

    private SimpleChatAppClient client;
    private bool connected = false;
    private bool continueUpdateThread = true;
    private Thread updateThread;
    private List<SimpleChatAppMessage> messages = new List<SimpleChatAppMessage>();
    private Dictionary<string, Widget> greyMessages = new Dictionary<string, Widget>();
    private string clickedMessageId;

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
        
        // Message Context Menu:
        ((MenuItem)messageContextMenu.Children[0]).Activated += (_, _) => { // Copy Message
            Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", true)).Text
                = GetMessageFromId(clickedMessageId).text;
        };
        
        ((MenuItem)messageContextMenu.Children[1]).Activated += (_, _) => { // Add Trusted
            client.TrustedUsers.AddVerifiedUser(GetMessageFromId(clickedMessageId));
        };

        Application.Invoke(delegate {
            Window.Title = TitleStart;
            Window.Resize(800, 450);
        });
    }

    private void Window_DeleteEvent(object sender, DeleteEventArgs a) {
        continueUpdateThread = false;
        Application.Quit();
    }

    private SimpleChatAppMessage GetMessageFromId(string id) {
        return messages.Find(m => m.messageId == id);
    }

    private void SendMessagePressed() {
        if (!connected) return;

        new Thread(() => {
            string message = Commands.Emoticons(messageEntry.Text);
            string id = client.SendMessage(message).messageId;
            messageEntry.Text = "";
            
            SimpleChatAppMessage lastMessage = messages.Count > 0 ? messages[^1] : null;
            Application.Invoke(delegate {
                CreateMessageLabel(lastMessage, client.Name, message, DateTime.Now, id, false, client.publicKey, true, grey: true);
            });
        }).Start();
    }
    
    private void ConnectPressed(object sender, EventArgs e) {
        connectButton.Label = "Connecting...";
        
        // Stop old Thread
        continueUpdateThread = false;
        while (updateThread is { IsAlive: true }) Thread.Sleep(16);
        
        greyMessages.Clear();
        messages.Clear();
        
        ClearBox(messagesBox);
        ClearBox(onlineUsersBox);

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
        Adjustment adjustment = messageScrollWindow.Vadjustment;
        adjustment.Value = adjustment.Upper;
    }

    private bool IsScrolledToBottom() {
        Adjustment adjustment = messageScrollWindow.Vadjustment;
        return adjustment.Upper - (adjustment.Value + adjustment.PageSize) < 10;
    }

    private void CreateMessageLabel(SimpleChatAppMessage previousMessage, string creator, string message, DateTime time, string id, bool trusted, string publicKey, bool scrollToBottom, bool grey = false) {
        // Combine Messages
        // if creator name is the same, and was sent in the same minute, combine messages
        bool combine = previousMessage != null && 
                       previousMessage.creatorName == creator && previousMessage.publicKey == publicKey &&
                       time - DateTime.FromBinary(previousMessage.createdAt).ToLocalTime() < TimeSpan.FromMinutes(1);

        EventBox box = new EventBox();
        box.ButtonPressEvent += (_, args) => {
            // only allow right clicks
            if (args.Event.Button != 3) return;

            clickedMessageId = id;

            messageContextMenu.Children[1].Sensitive = !client.TrustedUsers.CheckUser(GetMessageFromId(id));
            messageContextMenu.Popup();
        };

        Label label = new Label("");
        label.Justify = Justification.Left;
        label.Xpad = 10;
        label.UseMarkup = true;
        label.Xalign = 0;
        label.LineWrap = true;
        label.LineWrapMode = WrapMode.WordChar;

        if (combine) {
            label.Markup = message;
        }
        else {
            label.Markup = trusted ? 
                $"<b>{creator}</b> {CheckMark} - <small>{time}</small>\n{message}" : 
                $"<b>{creator}</b> - <small>{time}</small>\n{message}";

            label.MarginTop = 10;
        }
        
        if (grey) {
            label.Opacity = 0.7;
            greyMessages.Add(id, label);
        }

        if (scrollToBottom) {
            label.SizeAllocated += (_, _) => {
                ScrollToBottom();
            };
        }

        box.Add(label);
        messagesBox.Add(box);
        
        box.Show();
        label.Show();
    }
    
    private void MessageUpdateThread() {
        string ip = null;
        continueUpdateThread = false;
        
        Application.Invoke(delegate {
            ip = serverIPEntry.Text;
            if (string.IsNullOrEmpty(ip)) {
                ip = "https://chat.zaneharrison.com";
            }

            continueUpdateThread = true;
        });

        while (!continueUpdateThread) { Thread.Sleep(16); }
        
        Prefs.SetString("username", usernameEntry.Text);
        Prefs.SetString("channel", channelNameEntry.Text);
        Prefs.SetString("serverIP", ip);
        Prefs.Save();
        
        client = new SimpleChatAppClient(ip, usernameEntry.Text, channelNameEntry.Text);

        bool connectTest = client.TestConnection();
        
        Application.Invoke(delegate {
            if (!connectTest) {
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

        while (continueUpdateThread) {
            SimpleChatAppMessage lastMessage = messages.Count > 0 ? messages[^1] : null;
            
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
                bool scrollToBottom = IsScrolledToBottom();
                
                foreach (SimpleChatAppMessage message in newMessages) {
                    bool isSameUser = message.creatorName == client.Name && message.publicKey == client.publicKey;

                    if (isSameUser) {
                        // try to find the message in greyMessages and make that have full opacity
                        if (greyMessages.TryGetValue(message.messageId, out Widget label)) {
                            label.Opacity = 1d;
                            Label llabel = (Label)label;

                            if (label.MarginTop == 10)
                                llabel.LabelMarkup = llabel.LabelMarkup.Insert(8 + client.Name.Length, 
                                CheckMark + " ");

                            greyMessages.Remove(message.messageId);
                            continue;
                        }
                    }
                    
                    bool isTrusted = client.TrustedUsers.CheckUser(message) || isSameUser;
                    
                    CreateMessageLabel(lastMessage, message.creatorName, message.text, 
                        DateTime.FromBinary(message.createdAt).ToLocalTime(), message.messageId, isTrusted, 
                        message.publicKey, scrollToBottom);

                    lastMessage = message;
                }

                ClearBox(onlineUsersBox);
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
