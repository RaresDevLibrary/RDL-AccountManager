using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public partial class Manager {
    ComboBox destination=new ComboBox();
    bool resolvingUsername;
    CheckBox lowResource=new AudioCheckBox();
    CheckBox muteAudio=new AudioCheckBox();
    ResourceMode resourceMode;
    ResourceMode audioMode;
    bool settingAudio;
    bool settingMode;
    System.Windows.Forms.Timer usageTimer=new System.Windows.Forms.Timer();
    ResourceUsage usage=new ResourceUsage();
    Label usageLabel;
    Label presetLabel;
    int presetTicks;
    Label accountCount,emptyState,selectionCount;
    static Color Background=Color.FromArgb(15,16,21), Surface=Color.FromArgb(24,26,34), Muted=Color.FromArgb(146,151,170), Accent=Color.FromArgb(148,119,255);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); try { int dark=1; DwmSetWindowAttribute(Handle,20,ref dark,4); } catch {} }
    void BuildUI() {
        Text="R.D.L | Account Manager"; ClientSize=new Size(1080,820); MinimumSize=new Size(1096,859);
        StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); BackColor=Background; ForeColor=Color.White;
        FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false;
        DoubleBuffered=true;
        using (var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RDL.ico")) if(stream!=null) Icon=new Icon(stream);
        Panel sidebar=new Panel { BackColor=Color.FromArgb(8,9,12),Dock=DockStyle.Left,Width=230 }; Controls.Add(sidebar);
        PictureBox logo=new PictureBox { Location=new Point(22,30),Size=new Size(184,78),SizeMode=PictureBoxSizeMode.Zoom };
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RDL.png"))
        using(var original=Image.FromStream(stream)) {
            Bitmap cropped=new Bitmap(760,295);
            using(Graphics g=Graphics.FromImage(cropped)) g.DrawImage(original,new Rectangle(0,0,760,295),new Rectangle(125,309,760,295),GraphicsUnit.Pixel);
            logo.Image=cropped;
        }
        sidebar.Controls.Add(logo);
        sidebar.Controls.Add(new Label { Text="ACCOUNT MANAGER",ForeColor=Muted,Font=new Font("Segoe UI",8,FontStyle.Bold),Location=new Point(27,121),Size=new Size(190,24) });
        Panel nav=new Panel { BackColor=Color.FromArgb(36,30,56),Location=new Point(16,175),Size=new Size(198,48) };
        nav.Controls.Add(new Label { Text="  ●   Your accounts",ForeColor=Color.FromArgb(205,190,255),Location=new Point(12,13),AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold) }); sidebar.Controls.Add(nav);
        sidebar.Controls.Add(new Label { Text="YOUR LIBRARY.\nREADY TO LAUNCH.",Location=new Point(26,285),Size=new Size(195,70),Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(224,226,235) });
        sidebar.Controls.Add(new Label { Text="Sign in once. Choose your\naccounts. Jump into the game.",Location=new Point(26,365),Size=new Size(185,60),ForeColor=Muted });
        sidebar.Controls.Add(new Label { Text="RARE’S DEV LIBRARY\nv13 • Windows",Location=new Point(26,745),Size=new Size(190,50),ForeColor=Muted,Font=new Font("Segoe UI",8),Anchor=AnchorStyles.Bottom|AnchorStyles.Left });
        AddText("Your accounts",270,29,550,42,25,Color.White,true);
        AddText("Manage your sessions. Launch your next game.",273,83,600,25,10,Muted,false);
        var add=ThemeButton("+  Add account",884,43,156,true,delegate { OpenAccount(null,"https://www.roblox.com/login"); }); add.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        accountCount=AddText("0 ACCOUNTS",274,135,180,24,9,Muted,true);
        var all=ThemeButton("Select all",831,125,100,false,delegate { for(int i=0;i<list.Items.Count;i++) list.SetSelected(i,true); });
        var clear=ThemeButton("Clear",941,125,99,false,delegate { list.ClearSelected(); });
        all.Anchor=clear.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        list.SetBounds(270,172,770,274); list.BackColor=Surface; list.ForeColor=Color.White; list.BorderStyle=BorderStyle.None;
        list.DrawMode=DrawMode.OwnerDrawFixed; list.ItemHeight=72; list.IntegralHeight=false;
        list.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        list.DrawItem+=DrawAccount; list.SelectedIndexChanged+=delegate { UpdateCounts(); }; Controls.Add(list);
        emptyState=AddText("Your library starts here.\nClick Add account to sign in to Roblox.",325,252,650,70,13,Muted,false);
        emptyState.BackColor=Surface; emptyState.TextAlign=ContentAlignment.MiddleCenter; emptyState.BringToFront();
        ThemeButton("Open account",270,460,160,false,delegate { if(Selected!=null) OpenAccount(Selected,"https://www.roblox.com/home"); });
        ThemeButton("Remove selected",442,460,170,false,delegate {
            if(Selected==null || !writable || launching) return;
            var chosen=list.SelectedItems.Cast<Account>().ToArray();
            if(MessageBox.Show("Remove "+chosen.Length+" saved account(s)?", "Remove accounts",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
            foreach(var account in chosen) { recovery.Forget(account.Id); accounts.Remove(account); } Save(); RefreshList();
        });
        selectionCount=AddText("0 selected",865,473,175,24,10,Muted,false); selectionCount.TextAlign=ContentAlignment.MiddleRight;
        AddText("LAUNCH A GAME",273,526,350,24,9,Accent,true);
        destination.Items.AddRange(new object[] {"Game place ID","Join Roblox username"});
        destination.DropDownStyle=ComboBoxStyle.DropDownList; destination.SetBounds(273,555,250,30); destination.BackColor=Surface; destination.ForeColor=Color.White;
        destination.DrawMode=DrawMode.OwnerDrawFixed; destination.FlatStyle=FlatStyle.Flat;
        destination.DrawItem+=delegate(object sender,DrawItemEventArgs e) {
            using(var brush=new SolidBrush(Surface)) e.Graphics.FillRectangle(brush,e.Bounds);
            if(e.Index>=0) TextRenderer.DrawText(e.Graphics,Convert.ToString(destination.Items[e.Index]),Font,e.Bounds,Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
        };
        destination.SelectedIndex=0; destination.SelectedIndexChanged+=delegate { place.Clear(); };
        var gameMode=new AudioRadioButton { Text="Game place ID",Location=new Point(273,555),Size=new Size(180,30),ForeColor=Color.White,Checked=true };
        var userMode=new AudioRadioButton { Text="Join Roblox username",Location=new Point(480,555),Size=new Size(250,30),ForeColor=Color.White };
        gameMode.CheckedChanged+=delegate { if(gameMode.Checked) destination.SelectedIndex=0; };
        userMode.CheckedChanged+=delegate { if(userMode.Checked) destination.SelectedIndex=1; };
        destination.SelectedIndexChanged+=delegate { gameMode.Checked=destination.SelectedIndex==0; userMode.Checked=destination.SelectedIndex==1; };
        Controls.Add(gameMode); Controls.Add(userMode);
        var cpuAssignments=ThemeButton("CPU assignments",26,490,178,false,delegate { MessageBox.Show(windowsPolicies.Assignments(),"Balanced processor pairs"); });
        sidebar.Controls.Add(cpuAssignments);
        cpuAssignments.BringToFront();
        place.SetBounds(273,590,483,34); place.BackColor=Surface; place.ForeColor=Color.White; place.BorderStyle=BorderStyle.FixedSingle; place.Font=new Font("Segoe UI",14); Controls.Add(place);
        var launch=ThemeButton("▶   Launch selected",779,584,261,true,async delegate {
            if(launching || resolvingUsername) return;
            if(Selected==null) { status.Text="Select at least one account to launch."; return; }
            if(destination.SelectedIndex==1) {
                Account[] chosen=list.SelectedItems.Cast<Account>().ToArray();
                resolvingUsername=true;
                try {
                    string username=UserLookup.Normalize(place.Text); status.Text="Looking up @"+username+"...";
                    ulong target=await UserLookup.Resolve(username);
                    if(!IsDisposed) await LaunchSelected(target,true,chosen);
                } catch(Exception error) { if(!IsDisposed) status.Text=error is LaunchException ? error.Message : "Username lookup failed. Check your connection."; }
                finally { resolvingUsername=false; }
                return;
            }
            ulong id;
            if(!ulong.TryParse(place.Text.Trim(),out id)||id==0) { status.Text="Enter the numeric place ID from your Roblox game URL."; return; }
            if(Selected==null) { status.Text="Select at least one account to launch."; return; }
            await LaunchSelected(id);
        });
        multi.Text="Multiple clients"; multi.SetBounds(274,639,185,24); multi.ForeColor=Color.FromArgb(210,212,223); Controls.Add(multi);
        lowResource.Text="Low resource mode"; lowResource.SetBounds(480,639,210,24); lowResource.ForeColor=Color.FromArgb(210,212,223); Controls.Add(lowResource);
        lowResource.Checked=resourceMode.Active;
        lowResource.CheckedChanged+=delegate { ChangeResourceMode(); };
        muteAudio.Text="Mute game audio"; muteAudio.SetBounds(720,639,210,24); muteAudio.ForeColor=Color.FromArgb(210,212,223); Controls.Add(muteAudio);
        muteAudio.Checked=audioMode.Active; muteAudio.CheckedChanged+=delegate { ChangeAudioMode(); };
        recoverCrashes.Text="Recover crashed clients"; recoverCrashes.SetBounds(274,675,260,28); recoverCrashes.ForeColor=Color.FromArgb(210,212,223); Controls.Add(recoverCrashes);
        ThemeButton("Stop recovery",831,670,209,false,delegate { recoverCrashes.Checked=false; recovery.Stop(); });
        presetLabel=AddText("Low mode: Efficiency + balanced CPU pairs / 60 FPS / graphics 1",274,718,766,25,9,Muted,false);
        usageLabel=AddText("Usage updates while Roblox is running.",274,749,766,24,9,Muted,false);
        status.SetBounds(271,784,770,30); status.ForeColor=Muted; status.Font=new Font("Segoe UI",9); status.Text=resourceMode.Active ? "Low resource mode is recorded. Uncheck it with Roblox closed to restore your settings." : "Ready. Add an account or select one to launch."; Controls.Add(status);
        usageTimer.Interval=5000; usageTimer.Tick+=delegate {
            if(IsDisposed) return;
            usageLabel.Text=usage.Sample();
            if(!runtimePolicyTesting && lowResource.Checked) {windowsPolicies.Scan(); presetLabel.Text=windowsPolicies.LastStatus;}
            if(++presetTicks%3==0 && resourceMode.Active) {
                try { bool matches=resourceMode.MatchesPreset(); presetLabel.Text=resourceMode.Describe()+(matches?" • Saved preset matches; verify in-game FPS":" • PRESET CHANGED: close Roblox and reapply low mode"); presetLabel.ForeColor=matches?Muted:Color.FromArgb(255,192,105); }
                catch { presetLabel.Text="Saved settings could not be checked. Close Roblox before reapplying."; }
            }
        };
        Shown+=delegate { usage.Sample(); usageTimer.Start(); };
        Resize+=delegate { usageTimer.Interval=WindowState==FormWindowState.Minimized ? 15000 : 5000; };
        FormClosed+=delegate { usageTimer.Stop(); usageTimer.Dispose(); };
    }
    void ChangeAudioMode() {
        if(settingAudio) return;
        settingAudio=true;
        try {
            if(launching) throw new InvalidOperationException("Wait for the launch queue to finish before changing audio.");
            if(muteAudio.Checked) { audioMode.Apply(); status.Text="Roblox in-game volume set to 0 for new clients."; }
            else { audioMode.Restore(); status.Text="Your original Roblox in-game volume was restored and verified."; }
        } catch(Exception error) {
            status.Text=error is InvalidOperationException ? error.Message : "Audio settings could not be changed ("+error.GetType().Name+"). Backup preserved.";
            MessageBox.Show(status.Text,"Game audio");
        } finally { muteAudio.Checked=audioMode.Active; settingAudio=false; }
    }
    void ChangeResourceMode() {
        if(settingMode) return;
        settingMode=true;
        try {
            if(launching) throw new InvalidOperationException("Wait for the launch queue to finish before changing this mode.");
            if(lowResource.Checked) { resourceMode.Apply(); status.Text="Low resource mode applied. New Roblox clients use low graphics and up to 60 FPS."; }
            else { resourceMode.Restore(); windowsPolicies.RestoreAll(); status.Text="Original settings restored and verified. The next launch uses your original values."; }
        } catch(Exception error) {
            status.Text=error is InvalidOperationException ? error.Message : "Settings could not be changed ("+error.GetType().Name+"). The backup has been preserved.";
            MessageBox.Show(status.Text,"Low resource mode");
        } finally { lowResource.Checked=resourceMode.Active; settingMode=false; }
    }
    Label AddText(string text,int x,int y,int width,int height,float size,Color color,bool bold) {
        var label=new Label { Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=color,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular) }; Controls.Add(label); return label;
    }
    Button ThemeButton(string text,int x,int y,int width,bool primary,EventHandler action) {
        var button=new AudioButton { Text=text,Location=new Point(x,y),Size=new Size(width,40),FlatStyle=FlatStyle.Flat,BackColor=primary?Accent:Surface,ForeColor=primary?Color.FromArgb(18,13,35):Color.FromArgb(221,223,234),Cursor=Cursors.Hand,Font=new Font("Segoe UI",10,FontStyle.Bold) };
        button.FlatAppearance.BorderSize=0; button.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(168,145,255):Color.FromArgb(42,45,57); button.Click+=action; Controls.Add(button); return button;
    }
    void UpdateCounts() {
        if(accountCount!=null) accountCount.Text=list.Items.Count+" ACCOUNTS";
        if(selectionCount!=null) selectionCount.Text=list.SelectedItems.Count+" selected";
        if(emptyState!=null) emptyState.Visible=list.Items.Count==0;
    }
    void DrawAccount(object sender,DrawItemEventArgs e) {
        if(e.Index<0) return;
        Account account=(Account)list.Items[e.Index]; bool selected=(e.State&DrawItemState.Selected)!=0;
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var brush=new SolidBrush(selected?Color.FromArgb(43,35,66):Surface)) e.Graphics.FillRectangle(brush,e.Bounds);
        Rectangle circle=new Rectangle(e.Bounds.X+20,e.Bounds.Y+15,42,42);
        using(var brush=new SolidBrush(Color.FromArgb(57,49,83))) e.Graphics.FillEllipse(brush,circle);
        string name=String.IsNullOrEmpty(account.DisplayName)?account.Username:account.DisplayName;
        using(var initialFont=new Font("Segoe UI",14,FontStyle.Bold)) TextRenderer.DrawText(e.Graphics,name.Substring(0,1).ToUpperInvariant(),initialFont,circle,Color.FromArgb(216,201,255),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        using(var titleFont=new Font("Segoe UI",11,FontStyle.Bold)) TextRenderer.DrawText(e.Graphics,name,titleFont,new Point(e.Bounds.X+78,e.Bounds.Y+12),Color.White);
        TextRenderer.DrawText(e.Graphics,"@"+account.Username,Font,new Point(e.Bounds.X+78,e.Bounds.Y+37),Muted);
        Rectangle mark=new Rectangle(e.Bounds.Right-42,e.Bounds.Y+24,22,22);
        using(var brush=new SolidBrush(selected?Accent:Color.FromArgb(48,51,65))) e.Graphics.FillRectangle(brush,mark);
        if(selected) TextRenderer.DrawText(e.Graphics,"✓",Font,mark,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        using(var pen=new Pen(Background,2)) e.Graphics.DrawLine(pen,e.Bounds.X,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
    }
}





