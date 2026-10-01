using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

public class Account {
    public string Id { get; set; }
    public string Username { get; set; }
    public string DisplayName { get; set; }
    public string Profile { get; set; }
    public string Token { get; set; }
    public override string ToString() { return DisplayName + "  (@" + Username + ")"; }
}
public partial class Manager : Form {
    internal static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RobloxAccountManager");
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    List<Account> accounts = new List<Account>();
    ListBox list = new ListBox { SelectionMode=SelectionMode.MultiExtended };
    CheckBox multi = new AudioCheckBox();
    List<System.Threading.Mutex> multiLocks = new List<System.Threading.Mutex>();
    bool launching;
    TextBox place = new TextBox();
    Label status = new Label();
    string vault = Path.Combine(Root, "accounts.bin");
    bool writable = true;
    public Manager() {
        Directory.CreateDirectory(Root);
        resourceMode=new ResourceMode(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Roblox","GlobalBasicSettings_13.xml"),Root,ResourceMode.RobloxRunning);
        audioMode=new ResourceMode(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Roblox","GlobalBasicSettings_13.xml"),Root,ResourceMode.RobloxRunning,true);
        BuildUI();
        SetupRecovery();
        SetupWindowsPolicies();
        FormClosed += delegate { foreach (var item in multiLocks) { try { item.ReleaseMutex(); } catch { } item.Dispose(); } };
        try {
            if (File.Exists(vault)) accounts = Json.Deserialize<List<Account>>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(vault),null,DataProtectionScope.CurrentUser)));
        } catch { writable=false; status.Text="Saved accounts could not be decrypted. The existing file has been preserved."; }
        RefreshList();
    }
    Account Selected { get { return list.SelectedItem as Account; } }
    async Task LaunchSelected(ulong placeId,bool follow=false,Account[] chosen=null) {
        if (launching) return;
        Account[] selected=(chosen??list.SelectedItems.Cast<Account>().ToArray()).Where(account=>accounts.Contains(account)).ToArray();
        if (selected.Length==0) return;
        if(audioMode.Active) {
            try {
                if(!audioMode.MatchesPreset()) {
                    if(ResourceMode.RobloxRunning()) { status.Text="Close Roblox to refresh the zero-volume setting before launching."; return; }
                    audioMode.Apply();
                }
            } catch(Exception error) { status.Text="Audio mode needs attention ("+error.GetType().Name+"). Close Roblox and toggle it off to restore."; return; }
        }
        if(resourceMode.Active) {
            try {
                if(!resourceMode.MatchesPreset()) {
                    if(ResourceMode.RobloxRunning()) { status.Text="Close Roblox to refresh low resource settings before launching."; return; }
                    resourceMode.Apply();
                }
            } catch(Exception error) { status.Text="Low resource mode needs attention ("+error.GetType().Name+"). Close Roblox and toggle it off to restore settings."; return; }
        }
        if (selected.Length>1 && !multi.Checked) { status.Text="Enable multiple Roblox clients before launching several accounts."; return; }
        if (multi.Checked && multiLocks.Count==0) {
            if (Process.GetProcessesByName("RobloxPlayerBeta").Length>0) { status.Text="Close Roblox first, then click Launch Game again."; return; }
            foreach (string name in new[] { "ROBLOX_singletonMutex", "ROBLOX_singletonEvent" }) {
                bool created;
                var item=new System.Threading.Mutex(true,name,out created);
                if (!created) {
                    item.Dispose();
                    foreach (var held in multiLocks) { held.ReleaseMutex(); held.Dispose(); }
                    multiLocks.Clear(); status.Text="Another app owns Roblox's client lock. Close it and try again."; return;
                }
                multiLocks.Add(item);
            }
        }
        using (var registration=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"roblox-player\shell\open\command")) {
            if (registration==null) { status.Text="Install the Roblox desktop launcher, then retry."; return; }
        }
        launching=true;
        int sent=0;
        bool lowMemoryStopped=false;
        var failures=new List<string>();
        try {
            for (int index=0; index<selected.Length; index++) {
                if(lowResource.Checked && index>0) {
                    if(usage.SystemReady && usage.SystemCpu>=75) {
                        status.Text="Stopped additional launches: system CPU is above 75%. Running games stay open.";
                        MessageBox.Show(status.Text,"CPU load"); break;
                    }
                    try { if(!resourceMode.MatchesPreset()) { MessageBox.Show("Roblox's saved settings changed during launch. Close Roblox and reapply low mode; verify 60 FPS and graphics 1 in the game menu.","Preset changed"); break; } }
                    catch { MessageBox.Show("The low-resource preset could not be verified. Additional launches were stopped.","Preset check"); break; }
                }
                if(lowResource.Checked && index>0 && ResourceUsage.MemoryLow()) {
                    lowMemoryStopped=true;
                    status.Text="Launch queue stopped: available RAM is low. Close unused apps before launching more accounts.";
                    MessageBox.Show(status.Text,"Low memory"); break;
                }
                Account account=selected[index];
                status.Text="Launching @"+account.Username+" ("+(index+1)+"/"+selected.Length+")...";
                try {
                    string ticket=await DirectLaunch.GetTicket(account,placeId,follow);
                    Process client=OwnedClient.Launch(DirectLaunch.BuildUri(ticket,placeId,follow));
                    if(lowResource.Checked && !runtimePolicyTesting) windowsPolicies.Scan();
                    if(client!=null) recovery.Track(account,placeId,new ClientLife(client),follow);
                    else if(recoverCrashes.Checked) failures.Add("@"+account.Username+": launch sent, but recovery cannot track this custom launcher.");
                    sent++;
                    if (index<selected.Length-1) await Task.Delay(lowResource.Checked?15000:5000);
                } catch (Exception error) {
                    failures.Add("@"+account.Username+": "+(error is LaunchException ? error.Message : "Launcher failed ("+error.GetType().Name+")."));
                }
            }
            status.Text="Sent "+sent+" of "+selected.Length+" launch requests to Roblox."+(lowMemoryStopped?" Queue stopped: low available RAM.":"");
            if (failures.Count>0) MessageBox.Show(String.Join(Environment.NewLine,failures),"Accounts that could not launch");
        } finally { launching=false; }
    }
    void AddButton(string text,int x,int y,int width, EventHandler action) {
        Button button = new AudioButton { Text=text,Location=new Point(x,y),Size=new Size(width,36) };
        button.Click += action; Controls.Add(button);
    }
    void RefreshList() { list.Items.Clear(); foreach (Account account in accounts) list.Items.Add(account); UpdateCounts(); }
    void Save() {
        if (!writable) throw new InvalidOperationException("The saved account file could not be read and will not be overwritten.");
        byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Serialize(accounts)),null,DataProtectionScope.CurrentUser);
        string temporary=vault+".tmp"; File.WriteAllBytes(temporary,encrypted);
        if (File.Exists(vault)) File.Replace(temporary,vault,null); else File.Move(temporary,vault);
    }
    void OpenAccount(Account account,string url) {
        if (!writable) return;
        using (SessionWindow window = new SessionWindow(account,url)) {
            window.Saved += delegate(Account saved) {
                Account existing=accounts.FirstOrDefault(a=>a.Id==saved.Id);
                if (existing != null) {
                    if (account == null && existing.Profile != saved.Profile) { saved.Profile=existing.Profile; }
                    accounts.Remove(existing);
                }
                accounts.Add(saved); Save(); RefreshList(); status.Text="Saved @"+saved.Username+". Select it and open its account or a game.";
            };
            try {
                window.ShowDialog(this);
            } catch (Exception error) { MessageBox.Show(error.Message,"Browser could not start"); }
        }
    }
    [STAThread] public static int Main(string[] args) {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        if (args.Contains("--launch-check")) { return DirectLaunch.Check().GetAwaiter().GetResult(); }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += delegate(object sender,System.Threading.ThreadExceptionEventArgs e) { MessageBox.Show(e.Exception.Message,"Application error"); };
        if(args.Contains("--startup-check")) {
            AppAudio.Silent=true; AppAudio.Load();
            bool reachedMain=false;
            Application.Run(new BrandedStartup(()=> {
                var manager=new Manager();manager.runtimePolicyTesting=true;reachedMain=true;
                var close=new Timer {Interval=900};
                manager.Shown+=delegate {close.Start();};
                close.Tick+=delegate {close.Stop();close.Dispose();manager.Close();};
                return manager;
            }));
            return reachedMain?0:12;
        }
        if (args.Contains("--check") || args.Contains("--page-check")) {
            byte[] sample=Encoding.UTF8.GetBytes("verification-only");
            if (!sample.SequenceEqual(ProtectedData.Unprotect(ProtectedData.Protect(sample,null,DataProtectionScope.CurrentUser),null,DataProtectionScope.CurrentUser))) return 2;
            string testFile=Path.Combine(Path.GetTempPath(),"ram-check-"+Guid.NewGuid().ToString("N")+".bin");
            try {
                var testAccounts=new List<Account> { new Account { Id="1",Username="TestUser",DisplayName="Test",Profile="test",Token="synthetic-test-only" } };
                File.WriteAllBytes(testFile,ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Serialize(testAccounts)),null,DataProtectionScope.CurrentUser));
                var loaded=Json.Deserialize<List<Account>>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(testFile),null,DataProtectionScope.CurrentUser)));
                if (loaded.Count!=1 || loaded[0].Username!="TestUser" || loaded[0].Token!="synthetic-test-only") return 5;
            } finally { if (File.Exists(testFile)) File.Delete(testFile); }
            using (Manager manager=new Manager()) { }
            bool pageCheck=args.Contains("--page-check");
            using (SessionWindow browser=new SessionWindow(null,pageCheck ? "https://www.roblox.com/login" : "about:blank",true)) {
                int result=1;
                browser.Shown += async delegate { try { await browser.Initialize(); result=pageCheck ? await browser.CheckPage() : 0; } catch { result=3; } finally { browser.Close(); } };
                Application.Run(browser); return result;
            }
        }
        if (args.Contains("--ui-preview")) {
            using(var manager=new Manager()) {
                manager.accounts=new List<Account> {
                    new Account {Username="RareDev",DisplayName="Rare",Id="1"},
                    new Account {Username="RDL_Builder",DisplayName="Builder",Id="2"},
                    new Account {Username="RDL_Explorer",DisplayName="Explorer",Id="3"}
                };
                manager.RefreshList(); manager.list.SetSelected(0,true); manager.list.SetSelected(1,true);
                if(args.Contains("--follow-preview")) { manager.destination.SelectedIndex=1; manager.place.Text="Roblox"; }
                else manager.place.Text="123456789";
                manager.multi.Checked=true;
                manager.runtimePolicyTesting=true; manager.Show(); Application.DoEvents();
                using(var bitmap=new Bitmap(manager.Width,manager.Height)) { manager.DrawToBitmap(bitmap,new Rectangle(0,0,manager.Width,manager.Height)); bitmap.Save(args[1]); }
                manager.Close(); return 0;
            }
        }
        bool created;
        using(var single=new System.Threading.Mutex(true,"Local\\RDLAccountManagerRuntimePolicy",out created)) {
            if(!created) { MessageBox.Show("A copy of this manager is already running. Close it before opening another.","R.D.L Account Manager"); return 1; }
            try { Application.Run(new BrandedStartup(()=>new Manager())); } finally {single.ReleaseMutex();}
        }
        return 0;
    }
}
public class SessionWindow : Form {
    WebView2 web = new WebView2();
    Label status = new Label();
    Timer timer = new Timer();
    Account original;
    string profile,url,lastToken;
    bool busy,finished;
    TaskCompletionSource<string> identityReply;
    string identityNonce;
    public event Action<Account> Saved;
    public SessionWindow(Account account,string target,bool manualStart=false) {
        original=account; url=target; profile=account == null ? Guid.NewGuid().ToString("N") : account.Profile;
        Text=account == null ? "Add Account — sign in to Roblox" : "Roblox — @"+account.Username;
        Size=new Size(1100,800); StartPosition=FormStartPosition.CenterParent;
        status.Text=account == null ? "Sign in on Roblox below. Your account will be saved automatically." : "Your saved account is open. On a game page, click Play to launch Roblox.";
        status.Dock=DockStyle.Top; status.Height=36; status.Padding=new Padding(8);
        web.Dock=DockStyle.Fill; Controls.Add(web); Controls.Add(status);
        Button saveNow=new AudioButton { Text="Save signed-in account",Dock=DockStyle.Bottom,Height=36 };
        saveNow.Click += async delegate { await CaptureSession(); };
        Controls.Add(saveNow);
        if (!manualStart) Shown += async delegate {
            try { status.Text="Loading Roblox login..."; await Initialize(); }
            catch (Exception error) { status.Text="Browser could not start: "+error.Message; }
        };
        timer.Interval=8000; timer.Tick += async delegate { await CaptureSession(); };
        FormClosed += delegate { timer.Stop(); timer.Dispose(); };
    }
    public async Task Initialize() {
        var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Manager.Root,"BrowserProfiles",profile));
        await web.EnsureCoreWebView2Async(environment);
        web.CoreWebView2.WebMessageReceived += delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e) {
            Uri source;
            if (identityReply == null || !Uri.TryCreate(e.Source,UriKind.Absolute,out source) || source.Scheme!="https" || source.Host!="www.roblox.com") return;
            try {
                string message=e.TryGetWebMessageAsString();
                var envelope=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(message);
                if (envelope.ContainsKey("nonce") && Convert.ToString(envelope["nonce"])==identityNonce && envelope.ContainsKey("body")) identityReply.TrySetResult(Convert.ToString(envelope["body"]));
            } catch { }
        };
        web.CoreWebView2.NavigationCompleted += async delegate(object sender,CoreWebView2NavigationCompletedEventArgs e) {
            status.Text=e.IsSuccess ? (original == null ? "Sign in below. Your account will be saved automatically." : "Click Play on a game page to launch Roblox.") : "Page failed to load: "+e.WebErrorStatus+". Close this window and try again.";
            if(e.IsSuccess && url!="about:blank") await CaptureSession();
        };
        web.CoreWebView2.ProcessFailed += delegate { status.Text="The browser process stopped. Close this window and try again."; };
        web.CoreWebView2.Settings.AreDevToolsEnabled=false;
        web.CoreWebView2.Settings.IsPasswordAutosaveEnabled=false;
        web.CoreWebView2.Settings.IsGeneralAutofillEnabled=false;
        web.CoreWebView2.NewWindowRequested += delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e) {
            e.Handled=true; Uri target;
            if (Uri.TryCreate(e.Uri,UriKind.Absolute,out target) && target.Scheme=="https" && (target.Host=="roblox.com" || target.Host.EndsWith(".roblox.com",StringComparison.OrdinalIgnoreCase))) web.CoreWebView2.Navigate(e.Uri);
        };
        web.CoreWebView2.LaunchingExternalUriScheme += delegate(object sender,CoreWebView2LaunchingExternalUriSchemeEventArgs e) {
            Uri target; e.Cancel=!(Uri.TryCreate(e.Uri,UriKind.Absolute,out target) && (target.Scheme=="roblox-player" || target.Scheme=="roblox"));
        };
        if (original != null && !String.IsNullOrEmpty(original.Token)) {
            var cookie=web.CoreWebView2.CookieManager.CreateCookie(".ROBLOSECURITY",original.Token,".roblox.com","/");
            cookie.IsHttpOnly=true; cookie.IsSecure=true;
            web.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie);
        }
        if (url != "about:blank") timer.Start();
        web.CoreWebView2.Navigate(url);
    }
    public async Task<int> CheckPage() {
        for (int attempt=0; attempt<25; attempt++) {
            await Task.Delay(1000);
            string ready=await web.CoreWebView2.ExecuteScriptAsync("document.readyState === 'complete' && document.querySelector('input[type=password]') !== null");
            if (ready=="true") return 0;
        }
        return 4;
    }
    async Task CaptureSession() {
        if (busy || finished || IsDisposed || web.CoreWebView2 == null) return;
        busy=true;
        try {
            var cookies=await web.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
            var token=cookies.FirstOrDefault(c=>c.Name==".ROBLOSECURITY");
            if (token == null) { status.Text="No signed-in session yet. Complete Roblox login, then click Save signed-in account."; return; }
            if (token.Value==lastToken) return;
            status.Text="Checking signed-in account...";
            var container=new CookieContainer();
            container.Add(new Uri("https://users.roblox.com"),new Cookie(".ROBLOSECURITY",token.Value,"/",".roblox.com") { Secure=true,HttpOnly=true });
            using (var handler=new HttpClientHandler { CookieContainer=container,AllowAutoRedirect=false })
            using (var client=new HttpClient(handler)) {
                client.Timeout=TimeSpan.FromSeconds(15);
                string body=null;
                try {
                    using(var response=await client.GetAsync("https://users.roblox.com/v1/users/authenticated")) {
                        if (response.IsSuccessStatusCode) body=await response.Content.ReadAsStringAsync();
                        else status.Text="Account check returned HTTP "+(int)response.StatusCode+". Checking through the login browser...";
                    }
                } catch (HttpRequestException) { status.Text="Checking account through the login browser..."; }
                  catch (TaskCanceledException) { status.Text="Checking account through the login browser..."; }
                if (body==null) body=await GetBrowserIdentity();
                var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(body);
                if (IsDisposed) return;
                if (!data.ContainsKey("id") || !data.ContainsKey("name")) {
                    status.Text="Roblox did not confirm the signed-in account. Open the Roblox home page, then click Save signed-in account.";
                    return;
                }
                string id=Convert.ToString(data["id"]);
                if (original != null && original.Id!=id) { status.Text="This browser is signed into a different account. Use Add Account to save it."; return; }
                Account saved=new Account { Id=id,Username=Convert.ToString(data["name"]),DisplayName=data.ContainsKey("displayName") ? Convert.ToString(data["displayName"]) : Convert.ToString(data["name"]),Profile=profile,Token=token.Value };
                if (Saved != null) Saved(saved);
                lastToken=token.Value;
                timer.Stop();
                status.Text="Session saved for @"+saved.Username+".";
                if (original == null) { finished=true; Close(); }
            }
        } catch (Exception error) { if (!IsDisposed) status.Text="Account not saved: "+SafeError(error)+". Click Save signed-in account to retry."; }
        finally { busy=false; }
    }
    static string SafeError(Exception error) {
        if (error is UnauthorizedAccessException) return "Windows denied access to the saved-account file";
        if (error is IOException) return "the saved-account file could not be written";
        if (error is CryptographicException) return "Windows session encryption failed";
        if (error is TimeoutException) return "the account check timed out";
        return "account verification failed ("+error.GetType().Name+")";
    }
    async Task<string> GetBrowserIdentity() {
        identityNonce=Guid.NewGuid().ToString("N");
        identityReply=new TaskCompletionSource<string>();
        try {
            string script="(async function(){try{let r=await fetch('https://users.roblox.com/v1/users/authenticated',{credentials:'include'});let body=await r.text();chrome.webview.postMessage(JSON.stringify({nonce:'"+identityNonce+"',body:body}));}catch(e){chrome.webview.postMessage(JSON.stringify({nonce:'"+identityNonce+"',body:'{}'}));}})();";
            await web.CoreWebView2.ExecuteScriptAsync(script);
            var reply=identityReply.Task;
            if (await Task.WhenAny(reply,Task.Delay(12000)) != reply) throw new TimeoutException();
            return await reply;
        } finally { identityReply=null; identityNonce=null; }
    }
}



