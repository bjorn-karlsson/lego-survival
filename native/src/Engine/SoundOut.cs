using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace Brickblade;

/* The speakers: raylib's audio device, pulling stereo float frames from the
   Web Audio work-alike on its own thread. With no device (a server, a test)
   the game runs silent and nothing else changes. */
public static unsafe class SoundOut
{
    public const int Rate = 48000;
    static AudioContext ctx;
    static AudioStream stream;
    static bool open, tried, warned;
    public static bool Available
    {
        get
        {
            if (!tried)
            {
                tried = true;
                if (Program.Has("--mute") || Environment.GetEnvironmentVariable("BRICKBLADE_MUTE") == "1") return false;
                try
                {
                    Raylib.InitAudioDevice();
                    open = Raylib.IsAudioDeviceReady();
                }
                catch { open = false; }
                Console.Error.WriteLine(open ? $"audio: device open, {Rate} Hz" : "audio: no sound device; the game runs silent");
            }
            return open;
        }
    }
    public static void Attach(AudioContext c)
    {
        ctx = c;
        if (!open) return;
        Raylib.SetAudioStreamBufferSizeDefault(1024);
        stream = Raylib.LoadAudioStream(Rate, 32, 2);
        Raylib.SetAudioStreamCallback(stream, &Callback);
        Raylib.PlayAudioStream(stream);
    }
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static void Callback(void* buffer, uint frames)
    {
        var span = new Span<float>(buffer, (int)frames * 2);
        try
        {
            if (ctx == null) { span.Clear(); return; }
            ctx.Render(span, (int)frames);
            /* the mix is not clamped (as in Chromium); the speakers are */
            for (int i = 0; i < span.Length; i++) span[i] = Math.Clamp(span[i], -1f, 1f);
        }
        catch (Exception e)
        {
            span.Clear();
            if (!warned) { warned = true; Console.Error.WriteLine("audio: a buffer failed (" + e.GetType().Name + ": " + e.Message + ")\n" + e.StackTrace); }
        }
    }
    public static void Close()
    {
        if (!open) return;
        try { Raylib.StopAudioStream(stream); Raylib.UnloadAudioStream(stream); Raylib.CloseAudioDevice(); } catch { }
        open = false;
    }
}
