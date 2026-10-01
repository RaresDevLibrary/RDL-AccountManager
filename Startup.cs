using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

public static class AppAudio {
    static SoundPlayer opening,click;
    static Stream openingStream,clickStream;
    public static bool Silent;
    public static double OpeningMilliseconds {get;private set;}
    public static void Load() {
        if(opening!=null) return;
        var assembly=Assembly.GetExecutingAssembly();
        openingStream=assembly.GetManifestResourceStream("Opensound.wav"); clickStream=assembly.GetManifestResourceStream("hoversound.wav");
        if(openingStream==null || clickStream==null) throw new InvalidDataException("Embedded sound resources are missing.");
        OpeningMilliseconds=Duration(openingStream);
        openingStream.Position=0; clickStream.Position=0;
        opening=new SoundPlayer(openingStream); click=new SoundPlayer(clickStream); opening.Load();click.Load();
    }
    static double Duration(Stream stream) {
        using(var reader=new BinaryReader(stream,Encoding.ASCII,true)) {
            stream.Position=12; uint bytesPerSecond=0,data=0;
            while(stream.Position+8<=stream.Length) {
                string kind=Encoding.ASCII.GetString(reader.ReadBytes(4)); uint length=reader.ReadUInt32(); long next=stream.Position+length+(length%2);
                if(next>stream.Length) throw new InvalidDataException("Invalid WAV chunk.");
                if(kind=="fmt ") { if(length<16) throw new InvalidDataException("Invalid WAV format."); reader.ReadUInt16(); reader.ReadUInt16();reader.ReadUInt32();bytesPerSecond=reader.ReadUInt32(); }
                if(kind=="data") data+=length;
                stream.Position=next;
            }
            if(bytesPerSecond==0 || data==0) throw new InvalidDataException("Invalid WAV data.");
            return 1000d*data/bytesPerSecond;
        }
    }
    public static void Opening() {if(Silent)return;try {Load();opening.Play();}catch {} }
    public static void Click() {if(Silent)return;try {Load();click.Play();}catch {} }
    public static void Release() {
        if(opening!=null) {opening.Stop();opening.Dispose();opening=null;}
        if(click!=null) {click.Stop();click.Dispose();click=null;}
        if(openingStream!=null) {openingStream.Dispose();openingStream=null;}
        if(clickStream!=null) {clickStream.Dispose();clickStream=null;}
    }
}
public class AudioButton : Button {protected override void OnClick(EventArgs e) {AppAudio.Click();base.OnClick(e);} }
public class AudioCheckBox : CheckBox {protected override void OnClick(EventArgs e) {AppAudio.Click();base.OnClick(e);} }
public class AudioRadioButton : RadioButton {protected override void OnClick(EventArgs e) {AppAudio.Click();base.OnClick(e);} }

public class LogoSplash : Form {
    IntPtr surface=IntPtr.Zero;
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativePoint {public int X,Y; public NativePoint(int x,int y){X=x;Y=y;}}
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativeSize {public int Width,Height; public NativeSize(int w,int h){Width=w;Height=h;}}
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential,Pack=1)] struct Blend {public byte Operation,Flags,Alpha,Format;}
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref NativePoint position,ref NativeSize size,IntPtr source,ref NativePoint origin,uint key,ref Blend blend,uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr bitmap);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    protected override CreateParams CreateParams {get {var cp=base.CreateParams;cp.ExStyle|=0x80000;return cp;}}
    public LogoSplash() {
        FormBorderStyle=FormBorderStyle.None; ClientSize=new Size(650,420); StartPosition=FormStartPosition.CenterScreen;
        Text="R.D.L Account Manager"; ShowInTaskbar=false;
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RDL.ico")) Icon=new Icon(stream);
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RDL.png"))
        using(var source=Image.FromStream(stream))
        using(var bitmap=new Bitmap(650,420,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {
            using(var g=Graphics.FromImage(bitmap)) {
                g.Clear(Color.Transparent); g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source,new Rectangle(65,110,520,202),new Rectangle(125,309,760,295),GraphicsUnit.Pixel);
            }
            surface=bitmap.GetHbitmap(Color.FromArgb(0));
        }
    }
    protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);SetFade(0);}
    public void SetFade(double opacity) {
        if(surface==IntPtr.Zero || !IsHandleCreated)return;
        IntPtr screen=GetDC(IntPtr.Zero), memory=CreateCompatibleDC(screen), previous=SelectObject(memory,surface);
        try {
            var position=new NativePoint(Left,Top);var size=new NativeSize(650,420);var origin=new NativePoint(0,0);
            var blend=new Blend {Alpha=(byte)Math.Round(Math.Max(0,Math.Min(1,opacity))*255),Format=1};
            if(!UpdateLayeredWindow(Handle,screen,ref position,ref size,memory,ref origin,0,ref blend,2))
                throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        } finally {SelectObject(memory,previous);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
    }
    protected override void Dispose(bool disposing) {if(surface!=IntPtr.Zero){DeleteObject(surface);surface=IntPtr.Zero;}base.Dispose(disposing);}
}
public class BrandedStartup : ApplicationContext {
    readonly LogoSplash splash=new LogoSplash();
    readonly Timer animation=new Timer {Interval=16};
    readonly Stopwatch clock=new Stopwatch();
    readonly Func<Form> factory;
    Form main; bool transitioning,ended;
    double holdUntil,finishedAt;
    public BrandedStartup(Func<Form> makeMain) {
        factory=makeMain;
        try {AppAudio.Load();} catch {}
        holdUntil=Math.Max(1600,AppAudio.OpeningMilliseconds-450); finishedAt=holdUntil+450;
        splash.Shown+=delegate {AppAudio.Opening();clock.Start();animation.Start();};
        splash.FormClosed+=delegate {if(!transitioning) ExitThread();};
        animation.Tick+=Tick; splash.Show();
    }
    static double Smooth(double t) {t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
    void Tick(object sender,EventArgs e) {
        double time=clock.Elapsed.TotalMilliseconds;
        if(main==null) {
            if(time<450) splash.SetFade(Smooth(time/450));
            else if(time<holdUntil) splash.SetFade(1);
            else if(time<finishedAt) splash.SetFade(1-Smooth((time-holdUntil)/450));
            else {
                transitioning=true; splash.Hide();
                try {
                    main=factory(); main.Opacity=0;main.Enabled=false;
                    main.FormClosed+=delegate {ExitThread();}; main.Show();
                    MainForm=main; splash.Close();splash.Dispose();clock.Restart();
                } catch {animation.Stop();ExitThread();throw;}
            }
        } else {
            main.Opacity=Smooth(time/300);
            if(time>=300) {main.Opacity=1;main.Enabled=true;animation.Stop();animation.Dispose();}
        }
    }
    protected override void ExitThreadCore() {
        if(ended)return;ended=true;
        animation.Stop();animation.Dispose();if(!splash.IsDisposed)splash.Dispose();AppAudio.Release();base.ExitThreadCore();
    }
}

