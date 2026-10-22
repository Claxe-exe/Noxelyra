using System.IO.Pipes;
using System.Text;
namespace ClaxeDesktop.App;
internal sealed class DesktopCommands : IDisposable
{
    private const string Pipe="ClaxeDesktop.Commands.v1";
    private readonly CancellationTokenSource lifetime=new();
    public DesktopCommands(Action<string> command)
    {
        _=Task.Run(async()=>
        {
            while(!lifetime.IsCancellationRequested)
            {
                try
                {
                    using var server=new NamedPipeServerStream(Pipe,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(lifetime.Token);
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(2000);
                    var buffer=new byte[32];var count=0;
                    while(count<buffer.Length){var read=await server.ReadAsync(buffer.AsMemory(count),timeout.Token);if(read==0)break;count+=read;}
                    var text=Encoding.UTF8.GetString(buffer,0,count);
                    if(text is "launcher" or "controls" or "dock" or "exit")_ = System.Windows.Application.Current.Dispatcher.BeginInvoke(()=>command(text));
                }
                catch(OperationCanceledException) { }
                catch(IOException) { }
            }
        });
    }
    public static async Task<bool> SendAsync(string command)
    {
        try{using var client=new NamedPipeClientStream(".",Pipe,PipeDirection.Out,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);using var timeout=new CancellationTokenSource(700);await client.ConnectAsync(timeout.Token);await client.WriteAsync(Encoding.UTF8.GetBytes(command),timeout.Token);return true;}
        catch(Exception ex)when(ex is IOException or OperationCanceledException or UnauthorizedAccessException){return false;}
    }
    public void Dispose()=>lifetime.Cancel();
}
